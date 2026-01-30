using System.Diagnostics;

namespace MultithreadDesignPattern.ProducerConsumer;

public class Channel<T>
{
    private readonly object _lockObj = new();
    private readonly int _capacity;
    private readonly Queue<T> _queue;

    public Channel(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        _capacity = capacity;
        _queue = new Queue<T>(capacity);
    }

    public void Add(T item)
    {
        lock (_lockObj)
        {
            while (_queue.Count >= _capacity)
            {
                Debug.WriteLine("Thread#{0} starts waiting to add.", Environment.CurrentManagedThreadId);
                Monitor.Wait(_lockObj);
                Debug.WriteLine("Thread#{0} is notified while waiting to add.", Environment.CurrentManagedThreadId);
            }
            _queue.Enqueue(item);
            Monitor.PulseAll(_lockObj);
        }
    }

    public T Take()
    {
        lock (_lockObj)
        {
            while (_queue.Count == 0)
            {
                Debug.WriteLine("Thread#{0} starts waiting to take.", Environment.CurrentManagedThreadId);
                Monitor.Wait(_lockObj);
                Debug.WriteLine("Thread#{0} is notified while waiting to take.", Environment.CurrentManagedThreadId);
            }
            var item = _queue.Dequeue();
            Monitor.PulseAll(_lockObj);
            return item;
        }
    }
}