using System.Diagnostics;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using ImageProcessor.ServiceRuntime;
using ImageProcessor.Storage.Queue.Messages;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ImageProcessor.SimpleWorker;

internal class Worker : WorkerEntryPoint
{
    private readonly QueueClient _requestQueue;
    private readonly BlobContainerClient _originalImagesBlobContainer;
    private readonly BlobContainerClient _resultImagesBlobContainer;

    private CancellationToken _cancellationToken;

    public Worker(QueueClient requestQueue, BlobContainerClient originalImagesBlobContainer,
                  BlobContainerClient resultImagesBlobContainer)
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
                // on transaction costs by using the ReceiveMessages method to get
                // multiple queue messages at a time.
                var response = await _requestQueue.ReceiveMessageAsync(cancellationToken: _cancellationToken);
                _cancellationToken.ThrowIfCancellationRequested();

                if (response.Value == null)
                {
                    // There is no message in the _requestQueue.
                    await Task.Delay(1000, _cancellationToken);
                    _cancellationToken.ThrowIfCancellationRequested();
                    continue;
                }

                var reqMsg = response.Value;

                // Delete a poison message.
                if (reqMsg.DequeueCount > 5)
                {
                    Trace.TraceError("Deleting a poison queue item: '{0}'", reqMsg.MessageText);
                    await _requestQueue.DeleteMessageAsync(reqMsg.MessageId, reqMsg.PopReceipt, _cancellationToken);
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

    private async Task ProcessQueueMessageAsync(QueueMessage requestMessage)
    {
        var reqMsgJson = requestMessage.MessageText;
        var reqMsgObj = JsonConvert.DeserializeObject<ProcessingRequestMessage>(reqMsgJson);

        if (reqMsgObj == null)
        {
            Trace.TraceWarning("[Worker] Failed to deserialize message");
            return;
        }

        foreach (var orgFileName in reqMsgObj.FileNames)
        {
            var orgBlobClient = _originalImagesBlobContainer.GetBlobClient(orgFileName);
            var resultFileName = Guid.NewGuid().ToString("N") + Path.GetExtension(orgFileName);
            var resultBlobClient = _resultImagesBlobContainer.GetBlobClient(resultFileName);

            // Download original image
            var downloadResponse = await orgBlobClient.DownloadAsync(_cancellationToken);
            var contentType = downloadResponse.Value.Details.ContentType;

            using var inStream = new MemoryStream();
            await downloadResponse.Value.Content.CopyToAsync(inStream, _cancellationToken);
            inStream.Position = 0;

            // Apply comic filter using ImageSharp
            using var outStream = new MemoryStream();
            using (var image = await Image.LoadAsync(inStream, _cancellationToken))
            {
                // Apply comic-like effect (simple example - adjust as needed)
                image.Mutate(x => x
                    .Grayscale()
                    .DetectEdges()
                );

                await image.SaveAsync(outStream, image.Metadata.DecodedImageFormat!, _cancellationToken);
            }

            // Upload result image
            outStream.Position = 0;
            await resultBlobClient.UploadAsync(outStream, overwrite: true, cancellationToken: _cancellationToken);

            // Set content type
            await resultBlobClient.SetHttpHeadersAsync(new Azure.Storage.Blobs.Models.BlobHttpHeaders
            {
                ContentType = contentType
            }, cancellationToken: _cancellationToken);
        }

        await _requestQueue.DeleteMessageAsync(requestMessage.MessageId, requestMessage.PopReceipt, _cancellationToken);
    }
}