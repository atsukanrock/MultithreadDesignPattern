using System.Diagnostics;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using ImageProcessor.ServiceRuntime;
using Microsoft.Extensions.Configuration;

namespace ImageProcessor.SearchWorker;

public class WorkerRole : TasksRoleEntryPoint
{
    private QueueClient? _keywordsQueue;
    private BlobContainerClient? _originalImagesBlobContainer;
    private QueueClient? _simpleWorkerRequestQueue;
    private QueueClient? _multithreadWorkerRequestQueue;
    private readonly IConfiguration _configuration;

    public WorkerRole(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void RunWorker()
    {
        // This is a sample worker implementation. Replace with your logic.
        Trace.TraceInformation("ImageProcessor.SearchWorker entry point called");

        if (_keywordsQueue == null || _originalImagesBlobContainer == null ||
            _simpleWorkerRequestQueue == null || _multithreadWorkerRequestQueue == null)
        {
            throw new InvalidOperationException("Worker not initialized. Call OnStart first.");
        }

        var workers = new List<WorkerEntryPoint>();

        var searcher = new Searcher(_keywordsQueue, _originalImagesBlobContainer, _simpleWorkerRequestQueue,
                                    _multithreadWorkerRequestQueue, _configuration);
        workers.Add(searcher);

        Run([.. workers]);
    }

    public bool OnStart()
    {
        Trace.TraceInformation("ImageProcessor.SearchWorker OnStart called");

        // Get storage connection string from configuration
        var storageConnectionString = _configuration.GetConnectionString("StorageAccount")
            ?? throw new InvalidOperationException("StorageAccount connection string not found in configuration");

        // Create queue clients
        _keywordsQueue = new QueueClient(storageConnectionString, "keywords");
        _keywordsQueue.CreateIfNotExists();

        _simpleWorkerRequestQueue = new QueueClient(storageConnectionString, "simple-worker-requests");
        _simpleWorkerRequestQueue.CreateIfNotExists();

        _multithreadWorkerRequestQueue = new QueueClient(storageConnectionString, "multithread-worker-requests");
        _multithreadWorkerRequestQueue.CreateIfNotExists();

        // Create blob service client
        var blobServiceClient = new BlobServiceClient(storageConnectionString);

        // Create blob containers
        _originalImagesBlobContainer = blobServiceClient.GetBlobContainerClient("original-images");
        _originalImagesBlobContainer.CreateIfNotExists();

        Trace.TraceInformation("ImageProcessor.SearchWorker initialized successfully");

        return true;
    }
}