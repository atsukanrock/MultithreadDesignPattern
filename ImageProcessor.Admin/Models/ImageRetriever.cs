using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace ImageProcessor.Admin.Models
{
    internal class ImageRetriever : WorkerBase<Uri>
    {
        public ImageRetriever(BlockingCollection<Uri> imageUris) : base(imageUris)
        {
        }

        public event EventHandler<ImageRetrievedEventArgs> ImageRetrieved;

        protected virtual void OnImageRetrieved(ImageRetrievedEventArgs e)
        {
            var handler = ImageRetrieved;
            if (handler != null) handler(this, e);
        }

        protected override async Task ProcessRequestAsync(Uri imageUri)
        {
            Trace.TraceInformation("Retrieving an image: {0}", imageUri);

            HttpResponseMessage response = null;
            try
            {
                response = await new HttpClient().GetAsync(imageUri);
                Trace.TraceInformation("Response status code for retrieving an image: {0}", response.StatusCode);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return;
                }

                var fileName = Path.GetTempFileName();
                using (var stream = File.OpenWrite(fileName))
                {
                    await response.Content.CopyToAsync(stream);
                }
                Trace.TraceInformation("Saved an image to: {0}", fileName);

                OnImageRetrieved(new ImageRetrievedEventArgs(fileName));
            }
            finally
            {
                if (response != null)
                    response.Dispose();
            }
        }
    }
}