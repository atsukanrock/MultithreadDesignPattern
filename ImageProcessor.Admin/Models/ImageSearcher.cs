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
using Newtonsoft.Json;

namespace ImageProcessor.Admin.Models
{
    internal class ImageSearcher
    {
        private readonly BlockingCollection<string> _keywords;
        private readonly int _imagesPerKeyword;

        public ImageSearcher(BlockingCollection<string> keywords, int imagesPerKeyword = 3)
        {
            _keywords = keywords;
            _imagesPerKeyword = imagesPerKeyword;
        }

        public event EventHandler<ImageSearchedEventArgs> OriginalImageGathered;

        protected virtual void OnOriginalImageGathered(ImageSearchedEventArgs e)
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
                    searchResult = await SearchAsync(keyword, _imagesPerKeyword);
                }
                catch (Exception ex)
                {
                    // TODO: Log exception.
                    Trace.TraceError("An error occurred while searching images: {0}", ex);
                    continue;
                }

                var fileNames = await Task.WhenAll(searchResult.d.results.Select(SaveImageAsync));

                var args = new ImageSearchedEventArgs(
                    new ProcessingRequestMessage(keyword, fileNames.Where(fileName => fileName != null)));
                OnOriginalImageGathered(args);
            }
        }

        private static async Task<ImageSearchObject> SearchAsync(string searchWord, int top)
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

        private async Task<string> SaveImageAsync(Result result)
        {
            var imageUri = new Uri(result.MediaUrl);
            Trace.TraceInformation("Retrieving an image: {0}", imageUri);

            HttpResponseMessage response = null;
            try
            {
                response = await new HttpClient().GetAsync(imageUri);
                Trace.TraceInformation("Response status code for retrieving an image: {0}", response.StatusCode);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    response.Dispose();
                    return null;
                }
            }
            catch (Exception ex)
            {
                // TODO: Log exception.
                Trace.TraceError("An error occurred while retrieving an image ({0}): {1}", imageUri, ex);
                if (response != null)
                {
                    response.Dispose();
                }
                return null;
            }

            try
            {
                var fileName = Path.GetTempFileName();
                using (var stream = File.OpenWrite(fileName))
                {
                    await response.Content.CopyToAsync(stream);
                }
                return fileName;
            }
            catch (Exception ex)
            {
                // TODO: Log exception.
                Trace.TraceError("An error occurred while saving an image: {0}", ex);
                return null;
            }
            finally
            {
                response.Dispose();
            }
        }
    }
}