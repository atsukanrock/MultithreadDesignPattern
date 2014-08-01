using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ImageProcessor.Admin.Models
{
    internal abstract class WorkerBase<T>
    {
        private readonly BlockingCollection<T> _channel;

        protected WorkerBase(BlockingCollection<T> channel)
        {
            _channel = channel;
        }

        public event EventHandler<ExceptionThrownEventArgs<T>> ExceptionThrown;

        protected virtual void OnExceptionThrown(ExceptionThrownEventArgs<T> e)
        {
            var handler = ExceptionThrown;
            if (handler != null) handler(this, e);
        }

        public async Task Run()
        {
            Trace.TraceInformation("Consumer thread #{0} started running.", Thread.CurrentThread.ManagedThreadId);

            while (!_channel.IsCompleted)
            {
                T request;
                if (!_channel.TryTake(out request, TimeSpan.FromSeconds(1.0)))
                {
                    continue;
                }
                Trace.TraceInformation("Consumer thread #{0} tooked a request from the the channel.",
                                       Thread.CurrentThread.ManagedThreadId);

                try
                {
                    await ProcessRequestAsync(request);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("An error occurred while processing a request: {0}", ex);

                    var e = new ExceptionThrownEventArgs<T>(request, ex);
                    OnExceptionThrown(e);
                    if (e.Rethrow)
                    {
                        throw;
                    }
                }
            }

            Trace.TraceInformation("Consumer thread #{0} ends running.", Thread.CurrentThread.ManagedThreadId);
        }

        protected abstract Task ProcessRequestAsync(T request);
    }
}