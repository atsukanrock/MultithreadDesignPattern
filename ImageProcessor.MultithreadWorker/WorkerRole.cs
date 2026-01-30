using System.Collections.Concurrent;
using System.Diagnostics;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using ImageProcessor.ServiceRuntime;
using Microsoft.Extensions.Configuration;

namespace ImageProcessor.MultithreadWorker;

public class WorkerRole : TasksRoleEntryPoint
{
    private QueueClient? _requestQueue;
    private BlobContainerClient? _originalImagesBlobContainer;
    private BlobContainerClient? _resultImagesBlobContainer;

    private BlockingCollection<OriginalImageInfo>? _channel;
    private readonly IConfiguration _configuration;

    public WorkerRole(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void RunWorker()
    {
        Trace.TraceInformation("ImageProcessor.MultithreadWorker entry point called.");

        if (_channel == null || _requestQueue == null || _originalImagesBlobContainer == null || _resultImagesBlobContainer == null)
        {
            throw new InvalidOperationException("Worker not initialized. Call OnStart first.");
        }

        var workers = new List<WorkerEntryPoint>();

        workers.Add(new Producer(_channel, _requestQueue, _originalImagesBlobContainer));

        var consumerThreadCount = _configuration.GetValue<int>("Worker:ConsumerThreadCount", 4);
        Trace.TraceInformation("The # of consumer threads: {0}", consumerThreadCount);
        for (int i = 0; i < consumerThreadCount; i++)
        {
            var consumer = new Consumer(_channel, _resultImagesBlobContainer);
            workers.Add(consumer);
        }

        Run([.. workers]);
    }

    public bool OnStart()
    {
        Trace.TraceInformation("ImageProcessor.MultithreadWorker OnStart called");

        // Get storage connection string from configuration
        var storageConnectionString = _configuration.GetConnectionString("StorageAccount")
            ?? throw new InvalidOperationException("StorageAccount connection string not found in configuration");

        // Create queue client
        _requestQueue = new QueueClient(storageConnectionString, "multithread-worker-requests");
        _requestQueue.CreateIfNotExists();

        // Create blob service client
        var blobServiceClient = new BlobServiceClient(storageConnectionString);

        // Create blob containers
        _originalImagesBlobContainer = blobServiceClient.GetBlobContainerClient("original-images");
        _originalImagesBlobContainer.CreateIfNotExists();

        _resultImagesBlobContainer = blobServiceClient.GetBlobContainerClient("multithread-result-images");
        _resultImagesBlobContainer.CreateIfNotExists();

        // Create channel with configured capacity
        var channelCapacity = _configuration.GetValue<int>("Worker:ChannelCapacity", 100);
        _channel = new BlockingCollection<OriginalImageInfo>(channelCapacity);

        Trace.TraceInformation("ImageProcessor.MultithreadWorker initialized successfully");

        return true;
    }

    public new void OnStop()
    {
        _channel?.Dispose();
        base.OnStop();
    }
}