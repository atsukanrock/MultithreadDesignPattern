using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageProcessor.Imaging.Filters;

namespace ImageProcessor.Admin.Models
{
    internal class ImageProcessor
    {
        private readonly BlockingCollection<string> _channel;

        public ImageProcessor(BlockingCollection<string> channel)
        {
            _channel = channel;
        }

        public event EventHandler<ImageProcessedEventArgs> ImageProcessed;

        protected virtual void OnImageProcessed(ImageProcessedEventArgs e)
        {
            var handler = ImageProcessed;
            if (handler != null) handler(this, e);
        }

        public async Task Run()
        {
            try
            {
                while (!_channel.IsCompleted)
                {
                    string request;
                    if (!_channel.TryTake(out request, TimeSpan.FromSeconds(1.0)))
                    {
                        continue;
                    }
                    Trace.TraceInformation("Consumer thread #{0} tooked a request from the the channel.",
                                           Thread.CurrentThread.ManagedThreadId);

                    try
                    {
                        var resultFileName = await ProcessAsync(request);
                        OnImageProcessed(new ImageProcessedEventArgs(resultFileName));
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("An error occurred when processing {0}: {1}", request, ex);
                    }
                }
            }
            finally
            {
                Trace.TraceInformation("Consumer thread #{0} ends running.", Thread.CurrentThread.ManagedThreadId);
            }
        }

        internal static async Task<string> ProcessAsync(string sourceFileName)
        {
            var resultFileName = Path.GetTempFileName();

            using (var inStream = new MemoryStream(File.ReadAllBytes(sourceFileName)))
            {
                using (var outStream = new MemoryStream())
                using (var resultFileStream = new FileStream(resultFileName, FileMode.Open, FileAccess.Write))
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

                    Trace.TraceInformation("Consumer thread #{0} saved an result image to the blob.",
                                           Thread.CurrentThread.ManagedThreadId);
                }
            }

            return resultFileName;
        }
    }
}