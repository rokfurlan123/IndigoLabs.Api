namespace IndigoLabs.Api.Services;

public sealed class MeasurementCacheWarmupService : BackgroundService
{
    private readonly IMeasurementStatisticsService _statisticsService;
    private readonly ILogger<MeasurementCacheWarmupService> _logger;

    public MeasurementCacheWarmupService(
        IMeasurementStatisticsService statisticsService,
        ILogger<MeasurementCacheWarmupService> logger)
    {
        _statisticsService = statisticsService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting measurement cache warmup.");
            var status = await _statisticsService.RecalculateAsync(stoppingToken);
            _logger.LogInformation(
                "Measurement cache warmup completed with {CityCount} cities at {CalculatedAtUtc}.",
                status.CityCount,
                status.CalculatedAtUtc);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Measurement cache warmup was cancelled.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Measurement cache warmup failed.");
        }
    }
}
