using System.Collections.Concurrent;
using System.Diagnostics;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using ImageProcessor.ServiceRuntime;
using ImageProcessor.Storage.Queue.Messages;
using Newtonsoft.Json;

namespace ImageProcessor.MultithreadWorker;

internal class Producer : WorkerEntryPoint
{
    private readonly BlockingCollection<OriginalImageInfo> _channel;
    private readonly QueueClient _requestQueue;
    private readonly BlobContainerClient _originalImagesBlobContainer;

    public Producer(BlockingCollection<OriginalImageInfo> channel, QueueClient requestQueue,
                    BlobContainerClient originalImagesBlobContainer)
    {
        _channel = channel;
        _requestQueue = requestQueue;
        _originalImagesBlobContainer = originalImagesBlobContainer;
    }

    private CancellationToken CancellationToken { get; set; }

    public override Task<bool> OnStart(CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;
        return base.OnStart(cancellationToken);
    }

    public override async Task Run()
    {
        try
        {
            while (true)
            {
                // Retrieve a new message from the queue.
                var response = await _requestQueue.ReceiveMessageAsync(cancellationToken: CancellationToken);
                CancellationToken.ThrowIfCancellationRequested();

                if (response.Value == null)
                {
                    // There is no message in the _requestQueue.
                    await Task.Delay(1000, CancellationToken);
                    CancellationToken.ThrowIfCancellationRequested();
                    continue;
                }

                var reqMsg = response.Value;

                // Delete a poison message.
                if (reqMsg.DequeueCount > 5)
                {
                    Trace.TraceError("Deleting a poison queue item: '{0}'", reqMsg.MessageText);
                    await _requestQueue.DeleteMessageAsync(reqMsg.MessageId, reqMsg.PopReceipt, CancellationToken);
                    CancellationToken.ThrowIfCancellationRequested();
                    continue;
                }

                await ProcessQueueMessageAsync(reqMsg);
                CancellationToken.ThrowIfCancellationRequested();
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

    private async Task ProcessQueueMessageAsync(QueueMessage requestMessage)
    {
        var reqMsgJson = requestMessage.MessageText;
        var reqMsgObj = JsonConvert.DeserializeObject<ProcessingRequestMessage>(reqMsgJson);

        if (reqMsgObj == null)
        {
            Trace.TraceWarning("[Producer] Failed to deserialize message");
            return;
        }

        // Download all images in parallel and add to channel
        var tasks = reqMsgObj.FileNames.Select(async orgFileName =>
        {
            var orgBlobClient = _originalImagesBlobContainer.GetBlobClient(orgFileName);
            var downloadResponse = await orgBlobClient.DownloadAsync(CancellationToken);
            var contentType = downloadResponse.Value.Details.ContentType;

            using var memoryStream = new MemoryStream();
            await downloadResponse.Value.Content.CopyToAsync(memoryStream, CancellationToken);

            _channel.Add(
                new OriginalImageInfo(orgFileName, contentType, memoryStream.ToArray()),
                CancellationToken);
        });

        await Task.WhenAll(tasks);

        await _requestQueue.DeleteMessageAsync(requestMessage.MessageId, requestMessage.PopReceipt, CancellationToken);
    }
}