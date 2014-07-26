//#define UseBlockingCollection

#if UseBlockingCollection
using System.Collections.Concurrent;
#endif
using System;
using System.Collections.Generic;
using System.Threading;
#if !UseBlockingCollection
using MultithreadDesignPattern.ProducerConsumer;

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

#if !UseBlockingCollection
            var channel = new Channel<string>(channelCapacity);
            var producers = new List<Producer>();
            var consumers = new List<Consumer>();
#else
            var channel = new BlockingCollection<string>(channelCapacity);
            var producers = new List<ProducerBlockingCollection>();
            var consumers = new List<ConsumerBlockingCollection>();
#endif
            var productIdGenerator = new SequentialIdGenerator();

            for (int i = 0; i < producerCount; i++)
            {
                var producer =
#if !UseBlockingCollection
                    new Producer(
#else
                    new ProducerBlockingCollection(
#endif
                        "Producer-" + i, producerWorkload, productIdGenerator, channel);
                ThreadPool.QueueUserWorkItem(producer.Run);
                producers.Add(producer);
            }

            for (int i = 0; i < consumerCount; i++)
            {
                var consumer =
#if !UseBlockingCollection
                    new Consumer(
#else
                    new ConsumerBlockingCollection(
#endif
                        "Consumer-" + i, consumerWorkload, channel);
                ThreadPool.QueueUserWorkItem(consumer.Run);
                consumers.Add(consumer);
            }

            Console.ReadLine();
            producers.ForEach(p => p.Shutdown());
            consumers.ForEach(c => c.Shutdown());
            Console.ReadLine();
        }
    }
}