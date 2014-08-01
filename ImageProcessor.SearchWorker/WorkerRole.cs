using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ImageProcessor.Storage.Queue.Messages;
using ImageSearchTest.Bing.ResultObjects;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using LogLevel = Microsoft.WindowsAzure.Diagnostics.LogLevel;

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
                        await Task.Delay(1000);
                    }
                }
                catch (Exception ex)
                {
                    if (msg != null && msg.DequeueCount > 5)
                    {
                        _keywordsQueue.DeleteMessage(msg);
                        Trace.TraceError("Deleting poison queue item: '{0}'", msg.AsString);
                    }
                    Trace.TraceError("Exception in SearchWorker: '{0}'", ex);
                    Thread.Sleep(5000);
                }
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

            await _keywordsQueue.DeleteMessageAsync(message);
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
                    response = await httpClient.GetAsync(imageUri);
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
                    using (var blobStream = await blob.OpenWriteAsync())
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
                await _simpleWorkerRequestQueue.AddMessageAsync(queueMsg);
                await _multithreadWorkerRequestQueue.AddMessageAsync(queueMsg);
            }
            catch (Exception ex)
            {
                Trace.TraceError("An error occurred while adding a message to a queue: {0}", ex);
            }
        }

        public override bool OnStart()
        {
            Trace.TraceInformation("ImageProcessor.SearchWorker OnStart called");

            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            var config = DiagnosticMonitor.GetDefaultInitialConfiguration();
            config.Logs.ScheduledTransferPeriod = TimeSpan.FromMinutes(1.0);
            config.Logs.ScheduledTransferLogLevelFilter = LogLevel.Information;
            config.Logs.BufferQuotaInMB = 500;
            DiagnosticMonitor.Start("Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString", config);

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.
            RoleEnvironment.Changing += RoleEnvironmentOnChanging;

            // Open storage account using credentials from .cscfg file.
            var storageAccount = CloudStorageAccount.Parse(
                RoleEnvironment.GetConfigurationSettingValue("StorageConnectionString"));

            //Trace.TraceInformation("Creating keywords queue");
            var queueClient = storageAccount.CreateCloudQueueClient();
            _keywordsQueue = queueClient.GetQueueReference("keywords");
            _keywordsQueue.CreateIfNotExists();

            //Trace.TraceInformation("Creating original images blob container");
            var blobClient = storageAccount.CreateCloudBlobClient();
            _originalImagesBlobContainer = blobClient.GetContainerReference("original-images");
            _originalImagesBlobContainer.CreateIfNotExists();

            //Trace.TraceInformation("Creating simple worker request queue");
            _simpleWorkerRequestQueue = queueClient.GetQueueReference("simple-worker-requests");
            _simpleWorkerRequestQueue.CreateIfNotExists();

            //Trace.TraceInformation("Creating multi-thread worker request queue");
            _multithreadWorkerRequestQueue = queueClient.GetQueueReference("multithread-worker-requests");
            _multithreadWorkerRequestQueue.CreateIfNotExists();

            return base.OnStart();
        }

        private void RoleEnvironmentOnChanging(object sender, RoleEnvironmentChangingEventArgs e)
        {
            // If the configuration setting(s) is changed, restart this role instance.
            if (e.Changes.Any(change => change is RoleEnvironmentConfigurationSettingChange))
            {
                e.Cancel = true;
            }
        }
    }
}