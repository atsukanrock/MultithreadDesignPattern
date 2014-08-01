using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using ImageProcessor.Admin.Properties;
using ImageProcessor.Storage.Queue.Messages;
using ImageSearchTest.Bing.ResultObjects;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace ImageProcessor.Admin.Models
{
    internal class OriginalImageGatherer
    {
        private readonly BlockingCollection<string> _keywords;
        private readonly CloudBlobContainer _originalImagesBlobContainer;

        public OriginalImageGatherer(BlockingCollection<string> keywords, CloudBlobContainer originalImagesBlobContainer)
        {
            _keywords = keywords;
            _originalImagesBlobContainer = originalImagesBlobContainer;
        }

        public event EventHandler<OriginalImageGatheredEventArgs> OriginalImageGathered;

        protected virtual void OnOriginalImageGathered(OriginalImageGatheredEventArgs e)
        {
            var handler = OriginalImageGathered;
            if (handler != null) handler(this, e);
        }

        public async Task Run()
        {
            while (!_keywords.IsCompleted)
            {
                string keyword;
                if (!_keywords.TryTake(out keyword, TimeSpan.FromSeconds(1.0)))
                {
                    continue;
                }

                ImageSearchObject searchResult;
                try
                {
                    searchResult = await SearchAsync(keyword);
                }
                catch (Exception ex)
                {
                    // TODO: Log exception.
                    Trace.TraceError("An error occurred while retrieving an image: {0}", ex);
                    continue;
                }

                var fileNames = await Task.WhenAll(searchResult.d.results.Select(AddOriginalImageToBlobAsync));

                var args = new OriginalImageGatheredEventArgs(
                    new ProcessingRequestMessage(keyword, fileNames.Where(fileName => fileName != null)));
                OnOriginalImageGathered(args);
            }
        }

        private static async Task<ImageSearchObject> SearchAsync(string searchWord, int top = 3)
        {
            var escapedSearchWord = Uri.EscapeDataString(searchWord);
            const string market = "ja-JP";
            const string adult = "Off"; // Adult filter: Off / Moderate / Strict
            //const int top = 5; // How many numbers of images do I want? default: 50
            const string format = "json"; // xml (ATOM) / json

            var accountKey = Settings.Default.AzureMarketplaceAccountKey;
            var httpClientHandler = new HttpClientHandler {Credentials = new NetworkCredential(accountKey, accountKey)};
            var httpClient = new HttpClient(httpClientHandler);
            var searchUri = "https://api.datamarket.azure.com/Bing/Search/Image" +
                            "?Query='" + escapedSearchWord + "'" +
                            "&Market='" + market + "'" +
                            "&Adult='" + adult + "'" +
                            "&$top=" + top +
                            "&$format=" + format;

            var response = await httpClient.GetAsync(searchUri);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                // TODO: Use appropriate exception type.
                throw new Exception(string.Format("HTTP status code is {0}", response.StatusCode));
            }

            var unescapedResponse = Uri.UnescapeDataString(await response.Content.ReadAsStringAsync());
            var contents = JsonConvert.DeserializeObject<ImageSearchObject>(unescapedResponse);
            return contents;
        }

        private async Task<string> AddOriginalImageToBlobAsync(Result result)
        {
            var imageUri = new Uri(result.MediaUrl);
            HttpResponseMessage response;
            try
            {
                var httpClient = new HttpClient();
                Trace.TraceInformation("Retrieving an image: {0}", imageUri);
                response = await httpClient.GetAsync(imageUri);
                Trace.TraceInformation("Response status code for retrieving an image: {0}", response.StatusCode);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                // TODO: Log exception.
                Trace.TraceError("An error occurred while retrieving an image ({0}): {1}", imageUri, ex);
                return null;
            }

            var originalExtension = Path.GetExtension(imageUri.AbsolutePath);
            var fileName = Guid.NewGuid().ToString("N") + originalExtension;
            try
            {
                var blob = _originalImagesBlobContainer.GetBlockBlobReference(fileName);
                blob.Properties.ContentType = result.ContentType;
                using (var blobStream = await blob.OpenWriteAsync())
                {
                    await response.Content.CopyToAsync(blobStream);
                }
                Trace.TraceInformation("An image has been saved: {0}", blob.Uri.AbsoluteUri);

                return fileName;
            }
            catch (Exception ex)
            {
                // TODO: Log exception.
                Trace.TraceError("An error occurred while saving an image: {0}", ex);
                return null;
            }
        }
    }
}