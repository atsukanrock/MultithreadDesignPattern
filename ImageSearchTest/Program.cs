using System.Net.Http;
using ImageSearchTest.Unsplash.ResultObjects;
using Newtonsoft.Json;

namespace ImageSearchTest
{
    internal class Program
    {
        private static async Task Main()
        {
            try
            {
                var accessKey = Environment.GetEnvironmentVariable("UNSPLASH_ACCESS_KEY")
                    ?? throw new InvalidOperationException("環境変数 UNSPLASH_ACCESS_KEY を設定してください");

                var query = Uri.EscapeDataString("Ninja");
                var searchUri = $"https://api.unsplash.com/search/photos?query={query}&per_page=5&client_id={accessKey}";

                Console.WriteLine($"Requesting: {searchUri}");

                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Accept-Version", "v1");

                var response = await httpClient.GetAsync(searchUri);
                Console.WriteLine($"Status: {response.StatusCode}");

                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Response: {json[..Math.Min(500, json.Length)]}");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Request failed.");
                    return;
                }

                var result = JsonConvert.DeserializeObject<UnsplashSearchResult>(json);
                Console.WriteLine($"Total results: {result?.total}");

                foreach (var photo in result?.results ?? [])
                {
                    Console.WriteLine($"id: {photo.id}, url: {photo.urls?.regular}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex}");
            }
        }
    }
}
