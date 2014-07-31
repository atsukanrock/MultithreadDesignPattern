using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ImageProcessor.Imaging.Filters;
using ImageSearchTest.Bing.ResultObjects;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;

namespace ImageProcessor.SimpleWorker
{
    public class WorkerRole : RoleEntryPoint
    {
        private CloudQueue _imagesQueue;
        private CloudBlobContainer _imagesBlobContainer;

        public override async void Run()
        {
            // This is a sample worker implementation. Replace with your logic.
            Trace.TraceInformation("ImageProcessor.SimpleWorker entry point called");
            Thread.Sleep(600000);
            return;

            var httpClient = new HttpClient();
            while (true)
            {
                const string query = "ƒWƒ‡ƒWƒ‡‚ÌŠï–­‚È–`Œ¯";
                var contents = await SearchAsync(query);

                foreach (var result in contents.d.results)
                {
                    var imageUri = new Uri(result.MediaUrl);

                    var response = await httpClient.GetAsync(imageUri);
                    if (response.StatusCode != HttpStatusCode.OK) continue;

                    var fileName = Path.GetFileName(imageUri.LocalPath);
                    //var extension = GetExtension(result);
                    //var fileName = Guid.NewGuid().ToString("N") + extension;

                    try
                    {
                        var blob = _imagesBlobContainer.GetBlockBlobReference(fileName);

                        using (var inStream = new MemoryStream())
                        {
                            await response.Content.CopyToAsync(inStream);
                            using (var outStream = new MemoryStream())
                            using (var blobStream = await blob.OpenWriteAsync())
                            {
                                using (var imageFactory = new ImageFactory())
                                {
                                    imageFactory.Load(inStream)
                                                .Filter(MatrixFilters.Comic)
                                                .Save(outStream);
                                }

                                blob.Properties.ContentType = result.ContentType;
                                outStream.Position = 0;
                                await outStream.CopyToAsync(blobStream);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError(ex.ToString());
                    }
                }

                Thread.Sleep(600000);
            }
        }

        private string GetExtension(Result result)
        {
            if (result.ContentType.ToLowerInvariant().Contains("jpeg"))
            {
                return ".jpg";
            }
            if (result.ContentType.ToLowerInvariant().Contains("png"))
            {
                return ".png";
            }
            if (result.ContentType.ToLowerInvariant().Contains("png"))
            {
                return ".png";
            }
            if (result.ContentType.ToLowerInvariant().Contains("bmp"))
            {
                return ".bmp";
            }
            return ".jpg";
        }

        private static async Task<ImageSearchObject> SearchAsync(string searchWord, int top = 5)
        {
            var escapedSearchWord = Uri.EscapeDataString(searchWord);
            const string market = "ja-JP";
            const string adult = "Off"; // Adult filter: Off / Moderate / Strict
            //const int top = 5; // How many numbers of images do I want? default: 50
            const string format = "json"; // xml (ATOM) / json
            const string accountKey = "<My Account Key>";
            var httpClientHandler = new HttpClientHandler {Credentials = new NetworkCredential(accountKey, accountKey)};
            var httpClient = new HttpClient(httpClientHandler);
            var response = await httpClient.GetStringAsync("https://api.datamarket.azure.com/Bing/Search/Image" +
                                                           "?Query='" + escapedSearchWord + "'" +
                                                           "&Market='" + market + "'" +
                                                           "&Adult='" + adult + "'" +
                                                           "&$top=" + top +
                                                           "&$format=" + format);
            var unescapedResponse = Uri.UnescapeDataString(response);
            var contents = JsonConvert.DeserializeObject<ImageSearchObject>(unescapedResponse);
            return contents;
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            // Open storage account using credentials from .cscfg file.
            var storageAccount = CloudStorageAccount.Parse
                (RoleEnvironment.GetConfigurationSettingValue("StorageConnectionString"));

            Trace.TraceInformation("Creating images blob container");
            var blobClient = storageAccount.CreateCloudBlobClient();
            _imagesBlobContainer = blobClient.GetContainerReference("simple-images");
            if (_imagesBlobContainer.CreateIfNotExists())
            {
                // Enable public access on the newly created "simple-images" container.
                _imagesBlobContainer.SetPermissions(
                    new BlobContainerPermissions
                    {
                        PublicAccess = BlobContainerPublicAccessType.Blob
                    });
            }

            Trace.TraceInformation("Creating images queue");
            var queueClient = storageAccount.CreateCloudQueueClient();
            _imagesQueue = queueClient.GetQueueReference("simple-images");
            _imagesQueue.CreateIfNotExists();

            return base.OnStart();
        }
    }
}