using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using ImageProcessor.Imaging.Filters;
using Microsoft.WindowsAzure.Storage.Blob;

namespace ImageProcessor.MultithreadWorker
{
    internal class Consumer
    {
        private readonly BlockingCollection<OriginalImageInfo> _channel;
        private readonly CloudBlobContainer _resultImagesBlobContainer;

        public Consumer(BlockingCollection<OriginalImageInfo> channel, CloudBlobContainer resultImagesBlobContainer)
        {
            _channel = channel;
            _resultImagesBlobContainer = resultImagesBlobContainer;
        }

        public async Task Run()
        {
            while (!_channel.IsCompleted)
            {
                OriginalImageInfo originalImageInfo;
                while (!_channel.TryTake(out originalImageInfo, TimeSpan.FromSeconds(1.0)))
                {
                }

                var resultFileName = Guid.NewGuid().ToString("N") + Path.GetExtension(originalImageInfo.FileName);
                var resultBlob = _resultImagesBlobContainer.GetBlockBlobReference(resultFileName);

                using (var inStream = new MemoryStream(originalImageInfo.FileBytes))
                {
                    using (var outStream = new MemoryStream())
                    using (var resultBlobStream = await resultBlob.OpenWriteAsync())
                    {
                        using (var imageFactory = new ImageFactory())
                        {
                            imageFactory.Load(inStream)
                                        .Filter(MatrixFilters.Comic)
                                        .Save(outStream);
                        }

                        resultBlob.Properties.ContentType = originalImageInfo.ContentType;
                        outStream.Position = 0;
                        await outStream.CopyToAsync(resultBlobStream);
                    }
                }
            }
        }
    }
}