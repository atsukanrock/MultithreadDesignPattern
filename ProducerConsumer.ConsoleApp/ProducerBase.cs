using System;
using System.Threading;

namespace ProducerConsumer.ConsoleApp
{
    internal abstract class ProducerBase : ThreadPoolJobBase
    {
        private readonly Random _sleepTimeoutRandom = new Random();
        private readonly int _workload;

        private static int __id;

        protected ProducerBase(string name, int workload)
            : base(name)
        {
            _workload = workload;
        }

        protected override void DoWork()
        {
            while (!ShutdownRequested)
            {
                Thread.Sleep(_sleepTimeoutRandom.Next(_workload / 2, _workload));
                var product = string.Format("Product#{0}", Interlocked.Increment(ref __id));

                Console.WriteLine("{0} is putting {1}.", Name, product);
                AddProductToChannel(product);
                Console.WriteLine("{0} has put {1}.", Name, product);
            }
        }

        protected abstract void AddProductToChannel(string product);
    }
}