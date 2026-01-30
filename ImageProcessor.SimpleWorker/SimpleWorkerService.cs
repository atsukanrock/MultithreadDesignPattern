using System.Diagnostics;

namespace ImageProcessor.SimpleWorker;

/// <summary>
/// .NET 8 BackgroundService that hosts the legacy WorkerRole
/// </summary>
public class SimpleWorkerService : BackgroundService
{
    private readonly WorkerRole _workerRole;
    private readonly ILogger<SimpleWorkerService> _logger;

    public SimpleWorkerService(WorkerRole workerRole, ILogger<SimpleWorkerService> logger)
    {
        _workerRole = workerRole;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("SimpleWorkerService starting...");

            // Initialize the worker
            if (!_workerRole.OnStart())
            {
                _logger.LogError("WorkerRole.OnStart() failed");
                return;
            }

            _logger.LogInformation("SimpleWorkerService started successfully");

            // Run the worker
            await Task.Run(() => _workerRole.RunWorker(), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SimpleWorkerService is stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SimpleWorkerService encountered an error");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SimpleWorkerService is stopping...");
        _workerRole.OnStop();
        await base.StopAsync(cancellationToken);
    }
}
