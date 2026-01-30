// http://mark.mymonster.nl/2013/01/29/running-multiple-workers-inside-one-windows-azure-worker-role

namespace ImageProcessor.ServiceRuntime;

/// <summary>
/// Model for Workers
/// </summary>
public class WorkerEntryPoint
{
    /// <summary>
    /// OnStart method for workers
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>bool for success</returns>
    public virtual Task<bool> OnStart(CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    /// <summary>
    /// Run method
    /// </summary>
    public virtual Task Run()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// This method prevents unhandled exceptions from being thrown
    /// from the worker thread.
    /// </summary>
    internal async Task ProtectedRun()
    {
        try
        {
            // Call the Workers Run() method
            await Run();
        }
        catch (SystemException)
        {
            // Exit Quickly on a System Exception
            throw;
        }
        catch (Exception)
        {
            // Swallow non-system exceptions
        }
    }
}