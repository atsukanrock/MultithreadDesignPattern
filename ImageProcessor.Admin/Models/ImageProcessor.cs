using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ImageProcessor.Admin.Models
{
    internal class ImageProcessor : WorkerBase<string>
    {
        public ImageProcessor(BlockingCollection<string> channel) : base(channel)
        {
        }

        public event EventHandler<ImageProcessedEventArgs>? ImageProcessed;

        protected virtual void OnImageProcessed(ImageProcessedEventArgs e)
        {
            var handler = ImageProcessed;
            if (handler != null) handler(this, e);
        }

        protected override async Task ProcessRequestAsync(string originalFilePath)
        {
            var resultFilePath = await ProcessAsync(originalFilePath);
            OnImageProcessed(new ImageProcessedEventArgs(resultFilePath));
        }

        public static async Task<string> ProcessAsync(string originalFilePath)
        {
            var resultFilePath = Path.GetTempFileName();

            Trace.TraceInformation("Consumer thread #{0} starts processing an image.",
                                   Thread.CurrentThread.ManagedThreadId);

            using (var image = await Image.LoadAsync(originalFilePath))
            {
                // Apply grayscale filter (as a replacement for Comic filter)
                image.Mutate(x => x.Grayscale());

                await image.SaveAsync(resultFilePath);
            }

            Trace.TraceInformation("Consumer thread #{0} saved a result image.",
                                   Thread.CurrentThread.ManagedThreadId);

            return resultFilePath;
        }
    }
}
