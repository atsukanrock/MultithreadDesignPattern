using System.Diagnostics;

namespace ImageProcessor.SearchWorker;

/// <summary>
/// .NET 8 BackgroundService that hosts the legacy WorkerRole for image search
/// </summary>
public class SearchWorkerService : BackgroundService
{
    private readonly WorkerRole _workerRole;
    private readonly ILogger<SearchWorkerService> _logger;

    public SearchWorkerService(WorkerRole workerRole, ILogger<SearchWorkerService> logger)
    {
        _workerRole = workerRole;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("SearchWorkerService starting...");

            // Initialize the worker
            if (!_workerRole.OnStart())
            {
                _logger.LogError("WorkerRole.OnStart() failed");
                return;
            }

            _logger.LogInformation("SearchWorkerService started successfully");

            // Run the worker
            await Task.Run(() => _workerRole.RunWorker(), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SearchWorkerService is stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SearchWorkerService encountered an error");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SearchWorkerService is stopping...");
        _workerRole.OnStop();
        await base.StopAsync(cancellationToken);
    }
}
