using System.Diagnostics;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using ImageProcessor.ServiceRuntime;
using Microsoft.Extensions.Configuration;

namespace ImageProcessor.SimpleWorker;

public class WorkerRole : TasksRoleEntryPoint
{
    private QueueClient? _requestQueue;
    private BlobContainerClient? _originalImagesBlobContainer;
    private BlobContainerClient? _resultImagesBlobContainer;
    private readonly IConfiguration _configuration;

    public WorkerRole(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void RunWorker()
    {
        // This is a sample worker implementation. Replace with your logic.
        Trace.TraceInformation("ImageProcessor.SimpleWorker entry point called");

        if (_requestQueue == null || _originalImagesBlobContainer == null || _resultImagesBlobContainer == null)
        {
            throw new InvalidOperationException("Worker not initialized. Call OnStart first.");
        }

        var worker = new Worker(_requestQueue, _originalImagesBlobContainer, _resultImagesBlobContainer);

        Run([worker]);
    }

    public bool OnStart()
    {
        Trace.TraceInformation("ImageProcessor.SimpleWorker OnStart called");

        // Get storage connection string from configuration
        var storageConnectionString = _configuration.GetConnectionString("StorageAccount")
            ?? throw new InvalidOperationException("StorageAccount connection string not found in configuration");

        // Create queue client
        _requestQueue = new QueueClient(storageConnectionString, "simple-worker-requests");
        _requestQueue.CreateIfNotExists();

        // Create blob service client
        var blobServiceClient = new BlobServiceClient(storageConnectionString);

        // Create blob containers
        _originalImagesBlobContainer = blobServiceClient.GetBlobContainerClient("original-images");
        _originalImagesBlobContainer.CreateIfNotExists();

        _resultImagesBlobContainer = blobServiceClient.GetBlobContainerClient("simple-result-images");
        _resultImagesBlobContainer.CreateIfNotExists();

        Trace.TraceInformation("ImageProcessor.SimpleWorker initialized successfully");

        return true;
    }
}