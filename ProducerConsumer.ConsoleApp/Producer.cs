using MultithreadDesignPattern.ProducerConsumer;

namespace ProducerConsumer.ConsoleApp
{
    internal class Producer : ProducerBase
    {
        private readonly Channel<string> _channel;

        public Producer(string name, int workload, SequentialIdGenerator idGenerator, Channel<string> channel)
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