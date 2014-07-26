namespace ProducerConsumer.ConsoleApp
{
    internal class SequentialIdGenerator
    {
        private int _currentId;
        private readonly object _lockObj = new object();

        public SequentialIdGenerator() : this(0)
        {
        }

        public SequentialIdGenerator(int initialId)
        {
            _currentId = initialId;
        }

        public int CurrentId
        {
            get { return _currentId; }
        }

        public int NextId()
        {
            lock (_lockObj)
            {
                return _currentId++;
            }
        }
    }
}