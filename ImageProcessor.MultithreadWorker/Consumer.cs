using System.Collections.Concurrent;
using System.Diagnostics;
using Azure.Storage.Blobs;
using ImageProcessor.ServiceRuntime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ImageProcessor.MultithreadWorker;

internal class Consumer : WorkerEntryPoint
{
    private readonly BlockingCollection<OriginalImageInfo> _channel;
    private readonly BlobContainerClient _resultImagesBlobContainer;

    public Consumer(BlockingCollection<OriginalImageInfo> channel, BlobContainerClient resultImagesBlobContainer)
    {
        _channel = channel;
        _resultImagesBlobContainer = resultImagesBlobContainer;
    }

    private CancellationToken CancellationToken { get; set; }

    public override Task<bool> OnStart(CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;
        return base.OnStart(cancellationToken);
    }

    public override Task Run()
    {
        return Task.Run(async () =>
        {
            try
            {
                while (!_channel.IsCompleted)
                {
                    if (!_channel.TryTake(out var originalImageInfo, TimeSpan.FromSeconds(1.0)))
                    {
                        CancellationToken.ThrowIfCancellationRequested();
                        continue;
                    }
                    CancellationToken.ThrowIfCancellationRequested();

                    Trace.TraceInformation("Consumer thread #{0} took a request from the channel.",
                                           Environment.CurrentManagedThreadId);

                    var resultFileName = Guid.NewGuid().ToString("N") +
                                         Path.GetExtension(originalImageInfo.FileName);
                    var resultBlobClient = _resultImagesBlobContainer.GetBlobClient(resultFileName);

                    using var inStream = new MemoryStream(originalImageInfo.FileBytes);
                    inStream.Position = 0;

                    using var outStream = new MemoryStream();

                    Trace.TraceInformation("Consumer thread #{0} starts processing an image.",
                                           Environment.CurrentManagedThreadId);

                    // Apply comic-like effect using ImageSharp
                    using (var image = await Image.LoadAsync(inStream, CancellationToken))
                    {
                        image.Mutate(x => x
                            .Grayscale()
                            .DetectEdges()
                        );

                        await image.SaveAsync(outStream, image.Metadata.DecodedImageFormat!, CancellationToken);
                    }

                    // Upload result image
                    outStream.Position = 0;
                    await resultBlobClient.UploadAsync(outStream, overwrite: true, cancellationToken: CancellationToken);

                    // Set content type
                    await resultBlobClient.SetHttpHeadersAsync(new Azure.Storage.Blobs.Models.BlobHttpHeaders
                    {
                        ContentType = originalImageInfo.ContentType
                    }, cancellationToken: CancellationToken);

                    Trace.TraceInformation("Consumer thread #{0} saved a result image to the blob.",
                                           Environment.CurrentManagedThreadId);

                    CancellationToken.ThrowIfCancellationRequested();
                }
            }
            finally
            {
                Trace.TraceInformation("Consumer thread #{0} ends running.", Environment.CurrentManagedThreadId);
            }
        }, CancellationToken);
    }
}