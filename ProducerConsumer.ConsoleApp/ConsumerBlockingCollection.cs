using System.Collections.Concurrent;

namespace ProducerConsumer.ConsoleApp
{
    internal class ConsumerBlockingCollection : ConsumerBase
    {
        private readonly BlockingCollection<string> _channel;

        public ConsumerBlockingCollection(string name, int workload, BlockingCollection<string> channel)
            : base(name, workload)
        {
            _channel = channel;
        }

        protected override string TakeProductFromChannel()
        {
            return _channel.Take();
        }
    }
}