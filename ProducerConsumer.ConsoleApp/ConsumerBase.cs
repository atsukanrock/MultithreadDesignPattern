using System;
using System.Threading;

namespace ProducerConsumer.ConsoleApp
{
    internal abstract class ConsumerBase : ThreadPoolJobBase
    {
        private readonly Random _sleepTimeoutRandom = new Random();
        private readonly int _workload;

        protected ConsumerBase(string name, int workload)
            : base(name)
        {
            _workload = workload;
        }

        protected override void DoWork()
        {
            Console.WriteLine("{0} is taking a product.", Name);
            var product = TakeProductFromChannel();
            Console.WriteLine("{0} has taken {1}.", Name, product);

            Thread.Sleep(_sleepTimeoutRandom.Next(_workload / 2, _workload));
        }

        protected abstract string TakeProductFromChannel();
    }
}