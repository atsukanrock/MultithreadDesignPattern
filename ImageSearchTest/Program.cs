using System.Net.Http;
using ImageSearchTest.Unsplash.ResultObjects;
using Newtonsoft.Json;

namespace ImageSearchTest
{
    internal class Program
    {
        private static async Task Main()
        {
            const string accessKey = "<Your Unsplash Access Key>";
            var query = Uri.EscapeDataString("スラムダンク");
            var searchUri = $"https://api.unsplash.com/search/photos?query={query}&per_page=5&client_id={accessKey}";

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Accept-Version", "v1");
            var json = await httpClient.GetStringAsync(searchUri);
            var result = JsonConvert.DeserializeObject<UnsplashSearchResult>(json);

            foreach (var photo in result?.results ?? [])
            {
                Console.WriteLine($"id: {photo.id}, url: {photo.urls?.regular}");
            }
        }
    }
}
