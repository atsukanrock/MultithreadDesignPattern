namespace ProducerConsumer.ConsoleApp;

internal abstract class ThreadPoolJobBase
{
    private readonly string _name;
    private volatile bool _shutdownRequested;
    private volatile Thread? _runningThread;

    protected ThreadPoolJobBase(string name)
    {
        _name = name;
    }

    public string Name => _name;

    public bool ShutdownRequested => _shutdownRequested;

    public void Run()
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
        _runningThread?.Interrupt();
    }
}