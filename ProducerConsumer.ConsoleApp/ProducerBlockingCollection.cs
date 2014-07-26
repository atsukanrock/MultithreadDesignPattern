using System.Collections.Concurrent;

namespace ProducerConsumer.ConsoleApp
{
    internal class ProducerBlockingCollection : ProducerBase
    {
        private readonly BlockingCollection<string> _channel;

        public ProducerBlockingCollection(string name,
                                          int workload,
                                          BlockingCollection<string> channel)
            : base(name, workload)
        {
            _channel = channel;
        }

        protected override void AddProductToChannel(string product)
        {
            _channel.Add(product);
        }
    }
}