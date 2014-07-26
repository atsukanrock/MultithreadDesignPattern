using System;
using System.Threading;

namespace ProducerConsumer.ConsoleApp
{
    internal abstract class ThreadPoolJobBase
    {
        private readonly string _name;
        private volatile bool _shutdownRequested;
        private volatile Thread _runningThread;

        protected ThreadPoolJobBase(string name)
        {
            _name = name;
        }

        public string Name
        {
            get { return _name; }
        }

        public bool ShutdownRequested
        {
            get { return _shutdownRequested; }
        }

        public void Run(object state)
        {
            _runningThread = Thread.CurrentThread;
            try
            {
                while (!_shutdownRequested)
                {
                    DoWork();
                }
            }
            catch (ThreadInterruptedException)
            {
                Console.WriteLine("{0} is interrupted.", _name);
            }
            finally
            {
                Console.WriteLine("{0} is terminated.", _name);
                _runningThread = null;
            }
        }

        protected abstract void DoWork();

        public void Shutdown()
        {
            _shutdownRequested = true;
            if (_runningThread != null)
            {
                _runningThread.Interrupt();
            }
        }
    }
}