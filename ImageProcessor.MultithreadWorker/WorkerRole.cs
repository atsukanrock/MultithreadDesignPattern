using System;
using System.Collections.Concurrent;
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

namespace ImageProcessor.MultithreadWorker
{
    public class WorkerRole : TasksRoleEntryPoint
    {
        private CloudQueue _requestQueue;
        private CloudBlobContainer _originalImagesBlobContainer;
        private CloudBlobContainer _resultImagesBlobContainer;

        private BlockingCollection<OriginalImageInfo> _channel;

        public override void Run()
        {
            Trace.TraceInformation("ImageProcessor.MultithreadWorker entry point called.");

            var workers = new List<WorkerEntryPoint>();

            workers.Add(new Producer(_channel, _requestQueue, _originalImagesBlobContainer));

            var consumerThreadCount = int.Parse(RoleEnvironment.GetConfigurationSettingValue("ConsumerThreadCount"));
            Trace.TraceInformation("The # of consumer threads: {0}", consumerThreadCount);
            for (int i = 0; i < consumerThreadCount; i++)
            {
                var consumer = new Consumer(_channel, _resultImagesBlobContainer);
                workers.Add(consumer);
                //Task.Run((Func<Task>)consumer.Run).ConfigureAwait(false);
            }
            //Trace.TraceInformation("Consumer {0} threads has been started.", consumerThreadCount);

            Run(workers.ToArray());
        }

        public override bool OnStart()
        {
            Trace.TraceInformation("ImageProcessor.MultithreadWorker OnStart called");

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
            _requestQueue = queueClient.GetQueueReference("multithread-worker-requests");
            _requestQueue.CreateIfNotExists();

            //Trace.TraceInformation("Creating original images blob container");
            var blobClient = storageAccount.CreateCloudBlobClient();
            _originalImagesBlobContainer = blobClient.GetContainerReference("original-images");
            _originalImagesBlobContainer.CreateIfNotExists();

            //Trace.TraceInformation("Creating result images blob container");
            _resultImagesBlobContainer = blobClient.GetContainerReference("multithread-result-images");
            _resultImagesBlobContainer.CreateIfNotExists();

            _channel =
                new BlockingCollection<OriginalImageInfo>(
                    int.Parse(RoleEnvironment.GetConfigurationSettingValue("ChannelCapacity")));

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

        public override void OnStop()
        {
            if (_channel != null)
                _channel.Dispose();

            base.OnStop();
        }
    }
}