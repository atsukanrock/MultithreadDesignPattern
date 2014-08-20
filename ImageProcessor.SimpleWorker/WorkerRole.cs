using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using ImageProcessor.ServiceRuntime;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using LogLevel = Microsoft.WindowsAzure.Diagnostics.LogLevel;

namespace ImageProcessor.SimpleWorker
{
    public class WorkerRole : TasksRoleEntryPoint
    {
        private CloudQueue _requestQueue;
        private CloudBlobContainer _originalImagesBlobContainer;
        private CloudBlobContainer _resultImagesBlobContainer;

        public override void Run()
        {
            // This is a sample worker implementation. Replace with your logic.
            Trace.TraceInformation("ImageProcessor.SimpleWorker entry point called");

            var worker = new Worker(_requestQueue, _originalImagesBlobContainer, _resultImagesBlobContainer);

            Run(new WorkerEntryPoint[] {worker});
        }

        public override bool OnStart()
        {
            Trace.TraceInformation("ImageProcessor.SimpleWorker OnStart called");

            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            var config = DiagnosticMonitor.GetDefaultInitialConfiguration();
            config.Logs.ScheduledTransferPeriod = TimeSpan.FromMinutes(1.0);
            config.Logs.ScheduledTransferLogLevelFilter = LogLevel.Information;
            config.Logs.BufferQuotaInMB = 500;
            DiagnosticMonitor.Start("Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString", config);

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.
            RoleEnvironment.Changing += RoleEnvironmentOnChanging;

            // Open storage account using credentials from .cscfg file.
            var storageAccount = CloudStorageAccount.Parse(
                RoleEnvironment.GetConfigurationSettingValue("StorageConnectionString"));

            //Trace.TraceInformation("Creating request queue");
            var queueClient = storageAccount.CreateCloudQueueClient();
            _requestQueue = queueClient.GetQueueReference("simple-worker-requests");
            _requestQueue.CreateIfNotExists();

            //Trace.TraceInformation("Creating original images blob container");
            var blobClient = storageAccount.CreateCloudBlobClient();
            _originalImagesBlobContainer = blobClient.GetContainerReference("original-images");
            _originalImagesBlobContainer.CreateIfNotExists();

            //Trace.TraceInformation("Creating result images blob container");
            _resultImagesBlobContainer = blobClient.GetContainerReference("simple-result-images");
            _resultImagesBlobContainer.CreateIfNotExists();

            return base.OnStart();
        }

        private static void RoleEnvironmentOnChanging(object sender, RoleEnvironmentChangingEventArgs e)
        {
            // If the configuration setting(s) is changed, restart this role instance.
            if (e.Changes.Any(change => change is RoleEnvironmentConfigurationSettingChange))
            {
                e.Cancel = true;
            }
        }
    }
}