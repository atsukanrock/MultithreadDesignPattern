using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ImageProcessor.Imaging.Filters;
using ImageProcessor.Storage.Queue.Messages;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace ImageProcessor.SimpleWorker
{
    public class WorkerRole : RoleEntryPoint
    {
        private CloudQueue _requestQueue;
        private CloudBlobContainer _originalImagesBlobContainer;
        private CloudBlobContainer _resultImagesBlobContainer;

        public override async void Run()
        {
            // This is a sample worker implementation. Replace with your logic.
            Trace.TraceInformation("ImageProcessor.SimpleWorker entry point called");

            while (true)
            {
                CloudQueueMessage reqMsg = null;
                try
                {
                    // Retrieve a new message from the queue.
                    // A production app could be more efficient and scalable and conserve
                    // on transaction costs by using the GetMessages method to get
                    // multiple queue messages at a time. See:
                    // http://azure.microsoft.com/en-us/documentation/articles/cloud-services-dotnet-multi-tier-app-storage-5-worker-role-b/#addcode
                    reqMsg = _requestQueue.GetMessage();
                    if (reqMsg != null)
                    {
                        await ProcessQueueMessageAsync(reqMsg);
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                }
                catch (StorageException e)
                {
                    if (reqMsg != null && reqMsg.DequeueCount > 5)
                    {
                        _requestQueue.DeleteMessage(reqMsg);
                        Trace.TraceError("Deleting poison queue item: '{0}'", reqMsg.AsString);
                    }
                    Trace.TraceError("Exception in SimpleWorker: '{0}'", e.Message);
                    Thread.Sleep(5000);
                }
            }
        }

        private async Task ProcessQueueMessageAsync(CloudQueueMessage requestMessage)
        {
            var reqMsgJson = requestMessage.AsString;
            var reqMsgObj = JsonConvert.DeserializeObject<ProcessingRequestMessage>(reqMsgJson);
            foreach (var orgFileName in reqMsgObj.FileNames)
            {
                var orgBlob = _originalImagesBlobContainer.GetBlockBlobReference(orgFileName);
                var resultFileName = Guid.NewGuid().ToString("N") + Path.GetExtension(orgFileName);
                var resultBlob = _resultImagesBlobContainer.GetBlockBlobReference(resultFileName);

                using (var orgBlobStream = await orgBlob.OpenReadAsync())
                using (var inStream = new MemoryStream())
                {
                    await orgBlobStream.CopyToAsync(inStream);

                    using (var outStream = new MemoryStream())
                    using (var resultBlobStream = await resultBlob.OpenWriteAsync())
                    {
                        using (var imageFactory = new ImageFactory())
                        {
                            imageFactory.Load(inStream)
                                        .Filter(MatrixFilters.Comic)
                                        .Save(outStream);
                        }

                        resultBlob.Properties.ContentType = orgBlob.Properties.ContentType;
                        outStream.Position = 0;
                        await outStream.CopyToAsync(resultBlobStream);
                    }
                }
            }

            await _requestQueue.DeleteMessageAsync(requestMessage);
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

            Trace.TraceInformation("Creating request queue");
            var queueClient = storageAccount.CreateCloudQueueClient();
            _requestQueue = queueClient.GetQueueReference("simple-worker-requests");
            _requestQueue.CreateIfNotExists();

            Trace.TraceInformation("Creating original images blob container");
            var blobClient = storageAccount.CreateCloudBlobClient();
            _originalImagesBlobContainer = blobClient.GetContainerReference("original-images");
            _originalImagesBlobContainer.CreateIfNotExists();

            Trace.TraceInformation("Creating result images blob container");
            _resultImagesBlobContainer = blobClient.GetContainerReference("simple-result-images");
            _resultImagesBlobContainer.CreateIfNotExists();

            return base.OnStart();
        }
    }
}