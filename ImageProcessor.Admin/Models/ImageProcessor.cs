using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageProcessor.Imaging.Filters;

namespace ImageProcessor.Admin.Models
{
    internal class ImageProcessor : WorkerBase<string>
    {
        public ImageProcessor(BlockingCollection<string> channel) : base(channel)
        {
        }

        public event EventHandler<ImageProcessedEventArgs> ImageProcessed;

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

            using (var inStream = new MemoryStream(File.ReadAllBytes(originalFilePath)))
            {
                using (var outStream = new MemoryStream())
                using (var resultFileStream = new FileStream(resultFilePath, FileMode.Open, FileAccess.Write))
                {
                    Trace.TraceInformation("Consumer thread #{0} starts processing an image.",
                                           Thread.CurrentThread.ManagedThreadId);

                    using (var imageFactory = new ImageFactory())
                    {
                        imageFactory.Load(inStream)
                                    .Filter(MatrixFilters.Comic)
                                    .Save(outStream);
                    }

                    outStream.Position = 0;
                    await outStream.CopyToAsync(resultFileStream);

                    Trace.TraceInformation("Consumer thread #{0} saved an result image.",
                                           Thread.CurrentThread.ManagedThreadId);
                }
            }

            return resultFilePath;
        }
    }
}