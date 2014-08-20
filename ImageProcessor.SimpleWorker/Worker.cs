using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageProcessor.Imaging.Filters;
using ImageProcessor.ServiceRuntime;
using ImageProcessor.Storage.Queue.Messages;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace ImageProcessor.SimpleWorker
{
    internal class Worker : WorkerEntryPoint
    {
        private readonly CloudQueue _requestQueue;
        private readonly CloudBlobContainer _originalImagesBlobContainer;
        private readonly CloudBlobContainer _resultImagesBlobContainer;

        private CancellationToken _cancellationToken;

        public Worker(CloudQueue requestQueue, CloudBlobContainer originalImagesBlobContainer,
                      CloudBlobContainer resultImagesBlobContainer)
        {
            _requestQueue = requestQueue;
            _originalImagesBlobContainer = originalImagesBlobContainer;
            _resultImagesBlobContainer = resultImagesBlobContainer;
        }

        public override Task<bool> OnStart(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            return base.OnStart(cancellationToken);
        }

        public override async Task Run()
        {
            try
            {
                while (true)
                {
                    // Retrieve a new message from the queue.
                    // A production app could be more efficient and scalable and conserve
                    // on transaction costs by using the GetMessages method to get
                    // multiple queue messages at a time. See:
                    // http://azure.microsoft.com/en-us/documentation/articles/cloud-services-dotnet-multi-tier-app-storage-5-worker-role-b/#addcode
                    var reqMsg = await _requestQueue.GetMessageAsync(_cancellationToken);
                    _cancellationToken.ThrowIfCancellationRequested();
                    if (reqMsg == null)
                    {
                        // There is no message in the _requestQueue.
                        await Task.Delay(1000, _cancellationToken);
                        _cancellationToken.ThrowIfCancellationRequested();
                        continue;
                    }

                    // Delete a poison message.
                    if (reqMsg.DequeueCount > 5)
                    {
                        Trace.TraceError("Deleting a poison queue item: '{0}'", reqMsg.AsString);
                        await _requestQueue.DeleteMessageAsync(reqMsg, _cancellationToken);
                        _cancellationToken.ThrowIfCancellationRequested();
                        continue;
                    }

                    await ProcessQueueMessageAsync(reqMsg);
                }
            }
            catch (OperationCanceledException)
            {
                Trace.TraceInformation("[Worker] Cancelled by cancellation token.");
            }
            catch (SystemException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Trace.TraceError("[Worker] Exception: '{0}'", ex);
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

                using (var orgBlobStream = await orgBlob.OpenReadAsync(_cancellationToken))
                using (var inStream = new MemoryStream())
                {
                    await orgBlobStream.CopyToAsync(inStream);

                    using (var outStream = new MemoryStream())
                    using (var resultBlobStream = await resultBlob.OpenWriteAsync(_cancellationToken))
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

            await _requestQueue.DeleteMessageAsync(requestMessage, _cancellationToken);
        }
    }
}