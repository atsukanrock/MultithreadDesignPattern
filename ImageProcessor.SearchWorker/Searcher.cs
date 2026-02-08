using System.Diagnostics;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using ImageProcessor.ServiceRuntime;
using ImageProcessor.Storage.Queue.Messages;
using ImageSearchTest.Unsplash.ResultObjects;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace ImageProcessor.SearchWorker;

internal class Searcher : WorkerEntryPoint
{
    private readonly QueueClient _keywordsQueue;
    private readonly BlobContainerClient _originalImagesBlobContainer;
    private readonly QueueClient _simpleWorkerRequestQueue;
    private readonly QueueClient _multithreadWorkerRequestQueue;
    private readonly IConfiguration _configuration;

    private CancellationToken _cancellationToken;

    public Searcher(QueueClient keywordsQueue, BlobContainerClient originalImagesBlobContainer,
                    QueueClient simpleWorkerRequestQueue, QueueClient multithreadWorkerRequestQueue,
                    IConfiguration configuration)
    {
        _keywordsQueue = keywordsQueue;
        _originalImagesBlobContainer = originalImagesBlobContainer;
        _simpleWorkerRequestQueue = simpleWorkerRequestQueue;
        _multithreadWorkerRequestQueue = multithreadWorkerRequestQueue;
        _configuration = configuration;
    }

    public override Task<bool> OnStart(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        return base.OnStart(cancellationToken);
    }

    public override async Task Run()
    {
        Trace.TraceInformation("ImageProcessor.SearchWorker entry point called");

        try
        {
            while (true)
            {
                var response = await _keywordsQueue.ReceiveMessageAsync(cancellationToken: _cancellationToken);
                _cancellationToken.ThrowIfCancellationRequested();

                if (response.Value == null)
                {
                    await Task.Delay(1000, _cancellationToken);
                    _cancellationToken.ThrowIfCancellationRequested();
                    continue;
                }

                var msg = response.Value;

                if (msg.DequeueCount > 5)
                {
                    Trace.TraceError("Deleting poison queue item: '{0}'", msg.MessageText);
                    await _keywordsQueue.DeleteMessageAsync(msg.MessageId, msg.PopReceipt, _cancellationToken);
                    _cancellationToken.ThrowIfCancellationRequested();
                    continue;
                }

                await ProcessQueueMessageAsync(msg);
            }
        }
        catch (OperationCanceledException)
        {
            Trace.TraceInformation("[Searcher] Cancelled by cancellation token.");
        }
        catch (SystemException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Trace.TraceError("[Searcher] Exception: '{0}'", ex);
        }
    }

    private async Task ProcessQueueMessageAsync(QueueMessage message)
    {
        Trace.TraceInformation("Processing queue message {0}", message.MessageText);

        var kwd = message.MessageText;
        var contents = await SearchAsync(kwd);
        if (contents == null)
        {
            return;
        }
        var fileNames = await AddOriginalImagesToBlob(contents);
        await NotifySearchResultToProcessingWorkersAsync(kwd, fileNames);

        await _keywordsQueue.DeleteMessageAsync(message.MessageId, message.PopReceipt, _cancellationToken);
    }

    private async Task<UnsplashSearchResult?> SearchAsync(string searchWord, int perPage = 5)
    {
        var accessKey = _configuration["Unsplash:AccessKey"];
        if (string.IsNullOrEmpty(accessKey))
        {
            Trace.TraceError("Unsplash access key not found in configuration");
            return null;
        }

        var escapedQuery = Uri.EscapeDataString(searchWord);
        var searchUri = $"https://api.unsplash.com/search/photos?query={escapedQuery}&per_page={perPage}&client_id={accessKey}";

        HttpResponseMessage response;
        try
        {
            Trace.TraceInformation("Searching: {0}", searchUri);
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Accept-Version", "v1");
            response = await httpClient.GetAsync(searchUri);
            Trace.TraceInformation("Response status code for search: {0}", response.StatusCode);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("An error occurred while searching images for {0}: {1}", searchWord, ex);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<UnsplashSearchResult>(json);
    }

    private async Task<IEnumerable<string>> AddOriginalImagesToBlob(UnsplashSearchResult contents)
    {
        var fileNames = new List<string>();

        if (contents.results == null)
        {
            Trace.TraceWarning("Search results are null");
            return fileNames;
        }

        foreach (var photo in contents.results)
        {
            var imageUrl = photo.urls?.regular;
            if (string.IsNullOrEmpty(imageUrl))
            {
                continue;
            }

            var imageUri = new Uri(imageUrl);
            HttpResponseMessage response;
            try
            {
                var httpClient = new HttpClient();
                Trace.TraceInformation("Retrieving an image: {0}", imageUri);
                response = await httpClient.GetAsync(imageUri, _cancellationToken);
                Trace.TraceInformation("Response status code for retrieving an image: {0}", response.StatusCode);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("An error occurred while retrieving an image ({0}): {1}", imageUri, ex);
                continue;
            }

            var fileName = Guid.NewGuid().ToString("N") + ".jpg";

            try
            {
                var blobClient = _originalImagesBlobContainer.GetBlobClient(fileName);
                var imageStream = await response.Content.ReadAsStreamAsync(_cancellationToken);

                await blobClient.UploadAsync(imageStream, overwrite: true, cancellationToken: _cancellationToken);
                await blobClient.SetHttpHeadersAsync(new Azure.Storage.Blobs.Models.BlobHttpHeaders
                {
                    ContentType = "image/jpeg"
                }, cancellationToken: _cancellationToken);

                Trace.TraceInformation("An image has been saved: {0}", blobClient.Uri.AbsoluteUri);
            }
            catch (Exception ex)
            {
                Trace.TraceError("An error occurred while saving an image: {0}", ex);
                continue;
            }

            fileNames.Add(fileName);
        }

        return fileNames;
    }

    private async Task NotifySearchResultToProcessingWorkersAsync(string keyword, IEnumerable<string> fileNames)
    {
        var msgObj = new ProcessingRequestMessage(keyword, fileNames);
        var msgJson = JsonConvert.SerializeObject(msgObj);

        try
        {
            await _simpleWorkerRequestQueue.SendMessageAsync(msgJson, _cancellationToken);
            await _multithreadWorkerRequestQueue.SendMessageAsync(msgJson, _cancellationToken);
        }
        catch (Exception ex)
        {
            Trace.TraceError("An error occurred while adding a message to a queue: {0}", ex);
        }
    }
}
