// http://mark.mymonster.nl/2013/01/29/running-multiple-workers-inside-one-windows-azure-worker-role

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace ImageProcessor.ServiceRuntime
{
    /// <summary>
    ///     Middle class that sits between WorkerRole and RoleEntryPoint
    /// </summary>
    public abstract class TasksRoleEntryPoint : RoleEntryPoint
    {
        /// <summary>
        ///     Tasks for workers
        /// </summary>
        private readonly List<Task> _tasks = new List<Task>();

        private readonly CancellationTokenSource _tokenSource;

        /// <summary>
        ///     Worker array passed in from WebRole
        /// </summary>
        private WorkerEntryPoint[] _workers;

        /// <summary>
        ///     Initializes a new instance of the TasksRoleEntryPoint class
        /// </summary>
        protected TasksRoleEntryPoint()
        {
            _tokenSource = new CancellationTokenSource();
        }

        /// <summary>
        ///     Called from WorkerRole, bringing in workers to add to threads
        /// </summary>
        /// <param name="arrayWorkers">WorkerEntryPoint[] arrayWorkers</param>
        public async void Run(WorkerEntryPoint[] arrayWorkers)
        {
            try
            {
                _workers = arrayWorkers;

                foreach (var worker in _workers)
                {
                    await worker.OnStart(_tokenSource.Token);
                }

                foreach (var worker in _workers)
                {
                    _tasks.Add(worker.ProtectedRun());
                }

                int completedTaskIndex;
                while ((completedTaskIndex = Task.WaitAny(_tasks.ToArray())) != -1 && _tasks.Count > 0)
                {
                    _tasks.RemoveAt(completedTaskIndex);
                    //Not cancelled so rerun the worker
                    if (!_tokenSource.Token.IsCancellationRequested)
                    {
                        _tasks.Insert(completedTaskIndex, _workers[completedTaskIndex].ProtectedRun());
                        await Task.Delay(1000);
                    }
                }
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);
            }
        }

        /// <summary>
        ///     OnStop override
        /// </summary>
        public override void OnStop()
        {
            try
            {
                _tokenSource.Cancel();
                Task.WaitAll(_tasks.ToArray());
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);
            }
            base.OnStop();
        }
    }
}