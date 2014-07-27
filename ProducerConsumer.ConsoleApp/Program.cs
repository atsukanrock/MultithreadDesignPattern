//#define UseBlockingCollection

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
#if !UseBlockingCollection
using ChannelType = MultithreadDesignPattern.ProducerConsumer.Channel<string>;
using ProducerType = ProducerConsumer.ConsoleApp.Producer;
using ConsumerType = ProducerConsumer.ConsoleApp.Consumer;

#else
using ChannelType = System.Collections.Concurrent.BlockingCollection<string>;
using ProducerType = ProducerConsumer.ConsoleApp.ProducerBlockingCollection;
using ConsumerType = ProducerConsumer.ConsoleApp.ConsumerBlockingCollection;

#endif

namespace ProducerConsumer.ConsoleApp
{
    internal class Program
    {
        private static void Main()
        {
            const int channelCapacity = 3;

            const int producerCount = 3;
            const int producerWorkload = 1500;

            const int consumerCount = 3;
            const int consumerWorkload = 3000;

            // By calling other constructor overloads of BlockingCollection<T>,
            // it becomes possible to switch algorithm of the collection among
            // FIFO (ConcurrentQueue<T>), LIFO (ConcurrentStack<T>) and so on.
            var channel = new ChannelType(channelCapacity);
            var producers = new List<ProducerType>();
            var consumers = new List<ConsumerType>();

            for (int i = 0; i < producerCount; i++)
            {
                var producer = new ProducerType("Producer#" + i, producerWorkload, channel);
                Task.Run(() => producer.Run());
                producers.Add(producer);
            }

            for (int i = 0; i < consumerCount; i++)
            {
                var consumer = new ConsumerType("Consumer#" + i, consumerWorkload, channel);
                Task.Run(() => consumer.Run());
                consumers.Add(consumer);
            }

            Console.ReadLine();
            producers.ForEach(p => p.Shutdown());
            consumers.ForEach(c => c.Shutdown());
            Console.ReadLine();
#if UseBlockingCollection
            // BlockingCollection<T> implements IDisposable.
            channel.Dispose();
#endif
        }
    }
}