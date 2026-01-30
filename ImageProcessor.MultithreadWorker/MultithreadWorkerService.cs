using System.Diagnostics;

namespace ImageProcessor.MultithreadWorker;

/// <summary>
/// .NET 8 BackgroundService that hosts the legacy WorkerRole with Producer-Consumer pattern
/// </summary>
public class MultithreadWorkerService : BackgroundService
{
    private readonly WorkerRole _workerRole;
    private readonly ILogger<MultithreadWorkerService> _logger;

    public MultithreadWorkerService(WorkerRole workerRole, ILogger<MultithreadWorkerService> logger)
    {
        _workerRole = workerRole;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("MultithreadWorkerService starting...");

            // Initialize the worker
            if (!_workerRole.OnStart())
            {
                _logger.LogError("WorkerRole.OnStart() failed");
                return;
            }

            _logger.LogInformation("MultithreadWorkerService started successfully");

            // Run the worker
            await Task.Run(() => _workerRole.RunWorker(), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MultithreadWorkerService is stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MultithreadWorkerService encountered an error");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MultithreadWorkerService is stopping...");
        _workerRole.OnStop();
        await base.StopAsync(cancellationToken);
    }
}
