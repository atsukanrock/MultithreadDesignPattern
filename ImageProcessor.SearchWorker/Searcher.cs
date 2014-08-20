using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ImageProcessor.ServiceRuntime;
using ImageProcessor.Storage.Queue.Messages;
using ImageSearchTest.Bing.ResultObjects;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace ImageProcessor.SearchWorker
{
    internal class Searcher : WorkerEntryPoint
    {
        private readonly CloudQueue _keywordsQueue;
        private readonly CloudBlobContainer _originalImagesBlobContainer;
        private readonly CloudQueue _simpleWorkerRequestQueue;
        private readonly CloudQueue _multithreadWorkerRequestQueue;

        private CancellationToken _cancellationToken;

        public Searcher(CloudQueue keywordsQueue, CloudBlobContainer originalImagesBlobContainer,
                        CloudQueue simpleWorkerRequestQueue, CloudQueue multithreadWorkerRequestQueue)
        {
            _keywordsQueue = keywordsQueue;
            _originalImagesBlobContainer = originalImagesBlobContainer;
            _simpleWorkerRequestQueue = simpleWorkerRequestQueue;
            _multithreadWorkerRequestQueue = multithreadWorkerRequestQueue;
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
                    // A production app could be more efficient and scalable and conserve
                    // on transaction costs by using the GetMessages method to get
                    // multiple queue messages at a time. See:
                    // http://azure.microsoft.com/en-us/documentation/articles/cloud-services-dotnet-multi-tier-app-storage-5-worker-role-b/#addcode
                    var msg = _keywordsQueue.GetMessage();
                    if (msg == null)
                    {
                        // There is no message in the _requestQueue.
                        await Task.Delay(1000, _cancellationToken);
                        _cancellationToken.ThrowIfCancellationRequested();
                        continue;
                    }

                    if (msg.DequeueCount > 5)
                    {
                        Trace.TraceError("Deleting poison queue item: '{0}'", msg.AsString);
                        await _keywordsQueue.DeleteMessageAsync(msg, _cancellationToken);
                        _cancellationToken.ThrowIfCancellationRequested();
                        continue;
                    }

                    await ProcessQueueMessageAsync(msg);
                }
            }
            catch (OperationCanceledException)
            {
                Trace.TraceInformation("[Producer] Cancelled by cancellation token.");
            }
            catch (SystemException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Trace.TraceError("[Producer] Exception: '{0}'", ex);
            }
        }

        private async Task ProcessQueueMessageAsync(CloudQueueMessage message)
        {
            Trace.TraceInformation("Processing queue message {0}", message);

            var kwd = message.AsString;
            var contents = await SearchAsync(kwd);
            if (contents == null)
            {
                return;
            }
            var fileNames = await AddOriginalImagesToBlob(contents);
            await NotifySearchResultToProcessingWorkersAsync(kwd, fileNames);

            await _keywordsQueue.DeleteMessageAsync(message, _cancellationToken);
        }

        private static async Task<ImageSearchObject> SearchAsync(string searchWord, int top = 5)
        {
            var escapedSearchWord = Uri.EscapeDataString(searchWord);
            const string market = "ja-JP";
            const string adult = "Off"; // Adult filter: Off / Moderate / Strict
            //const int top = 5; // How many numbers of images do I want? default: 50
            const string format = "json"; // xml (ATOM) / json

            var accountKey = RoleEnvironment.GetConfigurationSettingValue("AzureMarketplaceAccountKey");
            var httpClientHandler = new HttpClientHandler {Credentials = new NetworkCredential(accountKey, accountKey)};
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

            foreach (var result in contents.d.results)
            {
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
                    var blob = _originalImagesBlobContainer.GetBlockBlobReference(fileName);
                    blob.Properties.ContentType = result.ContentType;
                    using (var blobStream = await blob.OpenWriteAsync(_cancellationToken))
                    {
                        await response.Content.CopyToAsync(blobStream);
                    }
                    Trace.TraceInformation("An image has been saved: {0}", blob.Uri.AbsoluteUri);
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
            var queueMsg = new CloudQueueMessage(msgJson);

            try
            {
                await _simpleWorkerRequestQueue.AddMessageAsync(queueMsg, _cancellationToken);
                await _multithreadWorkerRequestQueue.AddMessageAsync(queueMsg, _cancellationToken);
            }
            catch (Exception ex)
            {
                Trace.TraceError("An error occurred while adding a message to a queue: {0}", ex);
            }
        }
    }
}