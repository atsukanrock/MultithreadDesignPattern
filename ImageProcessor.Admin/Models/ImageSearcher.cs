using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using ImageProcessor.Admin.Properties;
using ImageSearchTest.Bing.ResultObjects;
using Newtonsoft.Json;

namespace ImageProcessor.Admin.Models
{
    internal class ImageSearcher : WorkerBase<string>
    {
        private readonly int _imagesPerKeyword;

        public ImageSearcher(BlockingCollection<string> keywords, int imagesPerKeyword = 3) : base(keywords)
        {
            _imagesPerKeyword = imagesPerKeyword;
        }

        public event EventHandler<ImageSearchedEventArgs> ImageSearched;

        protected virtual void OnImageSearched(ImageSearchedEventArgs e)
        {
            var handler = ImageSearched;
            if (handler != null) handler(this, e);
        }

        protected override async Task ProcessRequestAsync(string keyword)
        {
            var escapedKeyword = Uri.EscapeDataString(keyword);
            const string market = "ja-JP";
            const string adult = "Off"; // Adult filter: Off / Moderate / Strict
            //const int top = 5; // How many numbers of images do I want? default: 50
            const string format = "json"; // xml (ATOM) / json

            var accountKey = Settings.Default.AzureMarketplaceAccountKey;
            var httpClientHandler = new HttpClientHandler {Credentials = new NetworkCredential(accountKey, accountKey)};
            var httpClient = new HttpClient(httpClientHandler);
            var searchUri = "https://api.datamarket.azure.com/Bing/Search/Image" +
                            "?Query='" + escapedKeyword + "'" +
                            "&Market='" + market + "'" +
                            "&Adult='" + adult + "'" +
                            "&$top=" + _imagesPerKeyword +
                            "&$format=" + format;

            var response = await httpClient.GetAsync(searchUri);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                // TODO: Use appropriate exception type.
                throw new Exception(string.Format("HTTP status code is {0}", response.StatusCode));
            }

            var unescapedResponse = Uri.UnescapeDataString(await response.Content.ReadAsStringAsync());
            var imageSearchObject = JsonConvert.DeserializeObject<ImageSearchObject>(unescapedResponse);

            OnImageSearched(new ImageSearchedEventArgs(keyword, imageSearchObject));
        }
    }
}