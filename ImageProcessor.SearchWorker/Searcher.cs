using System.Diagnostics;
using System.Net;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using ImageProcessor.ServiceRuntime;
using ImageProcessor.Storage.Queue.Messages;
using ImageSearchTest.Bing.ResultObjects;
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
        // This is a sample worker implementation. Replace with your logic.
        Trace.TraceInformation("ImageProcessor.SearchWorker entry point called");

        try
        {
            while (true)
            {
                // Retrieve a new message from the queue.
                var response = await _keywordsQueue.ReceiveMessageAsync(cancellationToken: _cancellationToken);
                _cancellationToken.ThrowIfCancellationRequested();

                if (response.Value == null)
                {
                    // There is no message in the queue.
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

    private async Task<ImageSearchObject?> SearchAsync(string searchWord, int top = 5)
    {
        var escapedSearchWord = Uri.EscapeDataString(searchWord);
        const string market = "ja-JP";
        const string adult = "Off"; // Adult filter: Off / Moderate / Strict
        const string format = "json"; // xml (ATOM) / json

        var accountKey = _configuration["Bing:ApiKey"];
        if (string.IsNullOrEmpty(accountKey))
        {
            Trace.TraceError("Bing API key not found in configuration");
            return null;
        }

        var httpClientHandler = new HttpClientHandler { Credentials = new NetworkCredential(accountKey, accountKey) };
        var httpClient = new HttpClient(httpClientHandler);
        var searchUri = "https://api.datamarket.azure.com/Bing/Search/Image" +
                        "?Query='" + escapedSearchWord + "'" +
                        "&Market='" + market + "'" +
                        "&Adult='" + adult + "'" +
                        "&$top=" + top +
                        "&$format=" + format;

        HttpResponseMessage response;
        try
        {
            Trace.TraceInformation("Searching: {0}", searchUri);
            response = await httpClient.GetAsync(searchUri);
            Trace.TraceInformation("Response status code for search: {0}", response.StatusCode);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("An error occurred while searching images for {0}: {1}", searchWord, ex);
            return null;
        }

        var unescapedResponse = Uri.UnescapeDataString(await response.Content.ReadAsStringAsync());
        var contents = JsonConvert.DeserializeObject<ImageSearchObject>(unescapedResponse);
        return contents;
    }

    private async Task<IEnumerable<string>> AddOriginalImagesToBlob(ImageSearchObject contents)
    {
        var fileNames = new List<string>();

        if (contents.d?.results == null)
        {
            Trace.TraceWarning("Search results are null");
            return fileNames;
        }

        foreach (var result in contents.d.results)
        {
            if (string.IsNullOrEmpty(result.MediaUrl))
            {
                continue;
            }

            var imageUri = new Uri(result.MediaUrl);
            HttpResponseMessage response;
            try
            {
                var httpClient = new HttpClient();
                Trace.TraceInformation("Retrieving an image: {0}", imageUri);
                response = await httpClient.GetAsync(imageUri, _cancellationToken);
                Trace.TraceInformation("Response status code for retrieving an image: {0}", response.StatusCode);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    continue;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("An error occurred while retrieving an image ({0}): {1}", imageUri, ex);
                continue;
            }

            var originalExtension = Path.GetExtension(imageUri.AbsolutePath);
            var fileName = Guid.NewGuid().ToString("N") + originalExtension;

            try
            {
                var blobClient = _originalImagesBlobContainer.GetBlobClient(fileName);
                var imageStream = await response.Content.ReadAsStreamAsync(_cancellationToken);

                await blobClient.UploadAsync(imageStream, overwrite: true, cancellationToken: _cancellationToken);

                // Set content type
                if (!string.IsNullOrEmpty(result.ContentType))
                {
                    await blobClient.SetHttpHeadersAsync(new Azure.Storage.Blobs.Models.BlobHttpHeaders
                    {
                        ContentType = result.ContentType
                    }, cancellationToken: _cancellationToken);
                }

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