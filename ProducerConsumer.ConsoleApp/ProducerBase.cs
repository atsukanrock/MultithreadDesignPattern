using System;
using System.Threading;

namespace ProducerConsumer.ConsoleApp
{
    internal abstract class ProducerBase : ThreadPoolJobBase
    {
        private readonly Random _sleepTimeoutRandom = new Random();
        private readonly int _workload;
        private readonly SequentialIdGenerator _idGenerator;

        protected ProducerBase(string name, int workload, SequentialIdGenerator idGenerator)
            : base(name)
        {
            _workload = workload;
            _idGenerator = idGenerator;
        }

        protected override void DoWork()
        {
            while (!ShutdownRequested)
            {
                Thread.Sleep(_sleepTimeoutRandom.Next(_workload / 2, _workload));
                var product = string.Format("Product#{0}", _idGenerator.NextId());

                Console.WriteLine("{0} is putting {1}.", Name, product);
                AddProductToChannel(product);
                Console.WriteLine("{0} has put {1}.", Name, product);
            }
        }

        protected abstract void AddProductToChannel(string product);
    }
}