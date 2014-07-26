using System.Collections.Concurrent;

namespace ProducerConsumer.ConsoleApp
{
    internal class ProducerBlockingCollection : ProducerBase
    {
        private readonly BlockingCollection<string> _channel;

        public ProducerBlockingCollection(string name,
                                          int workload,
                                          SequentialIdGenerator idGenerator,
                                          BlockingCollection<string> channel)
            : base(name, workload, idGenerator)
        {
            _channel = channel;
        }

        protected override void AddProductToChannel(string product)
        {
            _channel.Add(product);
        }
    }
}