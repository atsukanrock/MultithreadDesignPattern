using MultithreadDesignPattern.ProducerConsumer;

namespace ProducerConsumer.ConsoleApp
{
    internal class Consumer : ConsumerBase
    {
        private readonly Channel<string> _channel;

        public Consumer(string name, int workload, Channel<string> channel)
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