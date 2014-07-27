using System;
using System.Net;
using System.Net.Http;
using ImageSearchTest.Bing.ResultObjects;
using Newtonsoft.Json;

namespace ImageSearchTest
{
    internal class Program
    {
        private static void Main()
        {
            var searchWord = Uri.EscapeDataString("スラムダンク");
            const string market = "ja-JP";
            const string adult = "Off"; // Adult filter: Off / Moderate / Strict
            const int top = 5; // How many numbers of images do I want? default: 50
            const string format = "json"; // xml (ATOM) / json
            const string accountKey = "<My Account Key>";
            var httpClientHandler = new HttpClientHandler {Credentials = new NetworkCredential(accountKey, accountKey)};
            var httpClient = new HttpClient(httpClientHandler);
            var response = httpClient.GetStringAsync("https://api.datamarket.azure.com/Bing/Search/Image" +
                                                     "?Query='" + searchWord + "'" +
                                                     "&Market='" + market + "'" +
                                                     "&Adult='" + adult + "'" +
                                                     "&$top=" + top +
                                                     "&$format=" + format /* +
                                                     "&ImageFilters='" + imageFilter + "'"*/).Result;
            var unescapedResponse = Uri.UnescapeDataString(response);
            var contents = JsonConvert.DeserializeObject<ImageSearchObject>(unescapedResponse);
        }
    }
}