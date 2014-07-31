using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ImageProcessor.Storage.Queue.Messages;
using ImageSearchTest.Bing.ResultObjects;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace ImageProcessor.SearchWorker
{
    public class WorkerRole : RoleEntryPoint
    {
        private CloudQueue _keywordsQueue;
        private CloudBlobContainer _originalImagesBlobContainer;
        private CloudQueue _simpleWorkerRequestQueue;
        private CloudQueue _multithreadWorkerRequestQueue;

        public override async void Run()
        {
            // This is a sample worker implementation. Replace with your logic.
            Trace.TraceInformation("ImageProcessor.SearchWorker entry point called");

            while (true)
            {
                CloudQueueMessage msg = null;
                try
                {
                    // Retrieve a new message from the queue.
                    // A production app could be more efficient and scalable and conserve
                    // on transaction costs by using the GetMessages method to get
                    // multiple queue messages at a time. See:
                    // http://azure.microsoft.com/en-us/documentation/articles/cloud-services-dotnet-multi-tier-app-storage-5-worker-role-b/#addcode
                    msg = _keywordsQueue.GetMessage();
                    if (msg != null)
                    {
                        await ProcessQueueMessageAsync(msg);
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                }
                catch (StorageException e)
                {
                    if (msg != null && msg.DequeueCount > 5)
                    {
                        _keywordsQueue.DeleteMessage(msg);
                        Trace.TraceError("Deleting poison queue item: '{0}'", msg.AsString);
                    }
                    Trace.TraceError("Exception in SearchWorker: '{0}'", e.Message);
                    Thread.Sleep(5000);
                }
            }
        }

        private async Task ProcessQueueMessageAsync(CloudQueueMessage message)
        {
            Trace.TraceInformation("Processing queue message {0}", message);

            var kwd = message.AsString;
            var contents = await SearchAsync(kwd);
            var fileNames = await AddOriginalImagesToBlob(contents);
            await NotifySearchResultToProcessingWorkersAsync(kwd, fileNames);

            await _keywordsQueue.DeleteMessageAsync(message);
        }

        private static async Task<ImageSearchObject> SearchAsync(string searchWord, int top = 5)
        {
            var escapedSearchWord = Uri.EscapeDataString(searchWord);
            const string market = "ja-JP";
            const string adult = "Off"; // Adult filter: Off / Moderate / Strict
            //const int top = 5; // How many numbers of images do I want? default: 50
            const string format = "json"; // xml (ATOM) / json
            const string accountKey = "<My Account Key>";

            var httpClientHandler = new HttpClientHandler {Credentials = new NetworkCredential(accountKey, accountKey)};
            var httpClient = new HttpClient(httpClientHandler);
            var response = await httpClient.GetStringAsync("https://api.datamarket.azure.com/Bing/Search/Image" +
                                                           "?Query='" + escapedSearchWord + "'" +
                                                           "&Market='" + market + "'" +
                                                           "&Adult='" + adult + "'" +
                                                           "&$top=" + top +
                                                           "&$format=" + format);

            var unescapedResponse = Uri.UnescapeDataString(response);
            var contents = JsonConvert.DeserializeObject<ImageSearchObject>(unescapedResponse);
            return contents;
        }

        private async Task<IEnumerable<string>> AddOriginalImagesToBlob(ImageSearchObject contents)
        {
            var fileNames = new List<string>();

            var httpClient = new HttpClient();
            foreach (var result in contents.d.results)
            {
                var imageUri = new Uri(result.MediaUrl);
                var response = await httpClient.GetAsync(imageUri);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    // TODO: Handle error status code
                    continue;
                }

                var originalExtension = Path.GetExtension(imageUri.AbsolutePath);
                var fileName = Guid.NewGuid().ToString("N") + originalExtension;

                var blob = _originalImagesBlobContainer.GetBlockBlobReference(fileName);
                blob.Properties.ContentType = result.ContentType;
                using (var blobStream = await blob.OpenWriteAsync())
                {
                    await response.Content.CopyToAsync(blobStream);
                }
                Trace.TraceInformation("An image has been saved: {0}", blob.Uri.AbsoluteUri);

                fileNames.Add(fileName);
            }

            return fileNames;
        }

        private async Task NotifySearchResultToProcessingWorkersAsync(string keyword, IEnumerable<string> fileNames)
        {
            var msgObj = new ProcessingRequestMessage(keyword, fileNames);
            var msgJson = JsonConvert.SerializeObject(msgObj);
            var queueMsg = new CloudQueueMessage(msgJson);

            await _simpleWorkerRequestQueue.AddMessageAsync(queueMsg);
            await _multithreadWorkerRequestQueue.AddMessageAsync(queueMsg);
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            // Open storage account using credentials from .cscfg file.
            var storageAccount = CloudStorageAccount.Parse
                (RoleEnvironment.GetConfigurationSettingValue("StorageConnectionString"));

            Trace.TraceInformation("Creating keywords queue");
            var queueClient = storageAccount.CreateCloudQueueClient();
            _keywordsQueue = queueClient.GetQueueReference("keywords");
            _keywordsQueue.CreateIfNotExists();

            Trace.TraceInformation("Creating original images blob container");
            var blobClient = storageAccount.CreateCloudBlobClient();
            _originalImagesBlobContainer = blobClient.GetContainerReference("original-images");
            _originalImagesBlobContainer.CreateIfNotExists();

            Trace.TraceInformation("Creating simple worker request queue");
            _simpleWorkerRequestQueue = queueClient.GetQueueReference("simple-worker-requests");
            _simpleWorkerRequestQueue.CreateIfNotExists();

            Trace.TraceInformation("Creating multi-thread worker request queue");
            _multithreadWorkerRequestQueue = queueClient.GetQueueReference("multithread-worker-requests");
            _multithreadWorkerRequestQueue.CreateIfNotExists();

            return base.OnStart();
        }
    }
}