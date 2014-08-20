using System;
using System.Collections.Generic;
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

namespace ImageProcessor.SearchWorker
{
    public class WorkerRole : TasksRoleEntryPoint
    {
        private CloudQueue _keywordsQueue;
        private CloudBlobContainer _originalImagesBlobContainer;
        private CloudQueue _simpleWorkerRequestQueue;
        private CloudQueue _multithreadWorkerRequestQueue;

        public override void Run()
        {
            // This is a sample worker implementation. Replace with your logic.
            Trace.TraceInformation("ImageProcessor.SearchWorker entry point called");

            var workers = new List<WorkerEntryPoint>();

            var searcher = new Searcher(_keywordsQueue, _originalImagesBlobContainer, _simpleWorkerRequestQueue,
                                        _multithreadWorkerRequestQueue);
            workers.Add(searcher);

            Run(workers.ToArray());
        }

        public override bool OnStart()
        {
            Trace.TraceInformation("ImageProcessor.SearchWorker OnStart called");

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

            //Trace.TraceInformation("Creating keywords queue");
            var queueClient = storageAccount.CreateCloudQueueClient();
            _keywordsQueue = queueClient.GetQueueReference("keywords");
            _keywordsQueue.CreateIfNotExists();

            //Trace.TraceInformation("Creating original images blob container");
            var blobClient = storageAccount.CreateCloudBlobClient();
            _originalImagesBlobContainer = blobClient.GetContainerReference("original-images");
            _originalImagesBlobContainer.CreateIfNotExists();

            //Trace.TraceInformation("Creating simple worker request queue");
            _simpleWorkerRequestQueue = queueClient.GetQueueReference("simple-worker-requests");
            _simpleWorkerRequestQueue.CreateIfNotExists();

            //Trace.TraceInformation("Creating multi-thread worker request queue");
            _multithreadWorkerRequestQueue = queueClient.GetQueueReference("multithread-worker-requests");
            _multithreadWorkerRequestQueue.CreateIfNotExists();

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