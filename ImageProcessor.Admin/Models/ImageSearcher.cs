using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading.Tasks;
using ImageProcessor.Admin.Properties;
using ImageSearchTest.Unsplash.ResultObjects;
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
            var accessKey = Settings.Default.UnsplashAccessKey;
            var escapedQuery = Uri.EscapeDataString(keyword);
            var searchUri = $"https://api.unsplash.com/search/photos?query={escapedQuery}&per_page={_imagesPerKeyword}&client_id={accessKey}";

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Accept-Version", "v1");
            var response = await httpClient.GetAsync(searchUri);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"HTTP status code is {response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var searchResult = JsonConvert.DeserializeObject<UnsplashSearchResult>(json);

            OnImageSearched(new ImageSearchedEventArgs(keyword, searchResult));
        }
    }
}
