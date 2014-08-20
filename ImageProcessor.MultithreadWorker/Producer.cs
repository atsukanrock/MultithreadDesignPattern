using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageProcessor.ServiceRuntime;
using ImageProcessor.Storage.Queue.Messages;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace ImageProcessor.MultithreadWorker
{
    internal class Producer : WorkerEntryPoint
    {
        private readonly BlockingCollection<OriginalImageInfo> _channel;
        private readonly CloudQueue _requestQueue;
        private readonly CloudBlobContainer _originalImagesBlobContainer;

        public Producer(BlockingCollection<OriginalImageInfo> channel, CloudQueue requestQueue,
                        CloudBlobContainer originalImagesBlobContainer)
        {
            _channel = channel;
            _requestQueue = requestQueue;
            _originalImagesBlobContainer = originalImagesBlobContainer;
        }

        private CancellationToken CancellationToken { get; set; }

        public override Task<bool> OnStart(CancellationToken cancellationToken)
        {
            this.CancellationToken = cancellationToken;
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
                    var reqMsg = await _requestQueue.GetMessageAsync(this.CancellationToken);
                    this.CancellationToken.ThrowIfCancellationRequested();
                    if (reqMsg == null)
                    {
                        // There is no message in the _requestQueue.
                        await Task.Delay(1000, this.CancellationToken);
                        this.CancellationToken.ThrowIfCancellationRequested();
                        continue;
                    }

                    // Delete a poison message.
                    if (reqMsg.DequeueCount > 5)
                    {
                        Trace.TraceError("Deleting a poison queue item: '{0}'", reqMsg.AsString);
                        await _requestQueue.DeleteMessageAsync(reqMsg, this.CancellationToken);
                        this.CancellationToken.ThrowIfCancellationRequested();
                        continue;
                    }

                    await ProcessQueueMessageAsync(reqMsg);
                    this.CancellationToken.ThrowIfCancellationRequested();
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

        private async Task ProcessQueueMessageAsync(CloudQueueMessage requestMessage)
        {
            var reqMsgJson = requestMessage.AsString;
            var reqMsgObj = JsonConvert.DeserializeObject<ProcessingRequestMessage>(reqMsgJson);

            Parallel.ForEach(
                reqMsgObj.FileNames,
                async orgFileName =>
                {
                    var orgBlob = _originalImagesBlobContainer.GetBlockBlobReference(orgFileName);
                    using (var orgBlobStream = await orgBlob.OpenReadAsync(this.CancellationToken))
                    using (var memoryStream = new MemoryStream())
                    {
                        await orgBlobStream.CopyToAsync(memoryStream);
                        _channel.Add(
                            new OriginalImageInfo(orgFileName, orgBlob.Properties.ContentType, memoryStream.ToArray()),
                            this.CancellationToken);
                    }
                });

            await _requestQueue.DeleteMessageAsync(requestMessage, this.CancellationToken);
        }
    }
}