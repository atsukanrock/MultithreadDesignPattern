using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace MultithreadDesignPattern.ProducerConsumer
{
    public class Channel<T>
    {
        private readonly object _lockObj = new object();
        private readonly int _capacity;
        private readonly Queue<T> _queue;

        public Channel(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException("capacity");
            }
            _capacity = capacity;
            _queue = new Queue<T>(capacity);
        }

        public void Add(T item)
        {
            lock (_lockObj)
            {
                while (_queue.Count >= _capacity)
                {
                    Debug.WriteLine("Thread#{0} starts waiting to add.", Thread.CurrentThread.ManagedThreadId);
                    Monitor.Wait(_lockObj);
                    Debug.WriteLine("Thread#{0} is notified while waiting to add.", Thread.CurrentThread.ManagedThreadId);
                }
                _queue.Enqueue(item);
                Monitor.PulseAll(_lockObj);
            }
        }

        public T Take()
        {
            lock (_lockObj)
            {
                while (!_queue.Any())
                {
                    Debug.WriteLine("Thread#{0} starts waiting to take.", Thread.CurrentThread.ManagedThreadId);
                    Monitor.Wait(_lockObj);
                    Debug.WriteLine("Thread#{0} is notified while waiting to take.",
                                    Thread.CurrentThread.ManagedThreadId);
                }
                var item = _queue.Dequeue();
                Monitor.PulseAll(_lockObj);
                return item;
            }
        }
    }
}