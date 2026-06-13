using System.Threading.Channels;
using IndigoLabs.Api.Options;
using Microsoft.Extensions.Options;

namespace IndigoLabs.Api.Services;

public sealed class MeasurementFileWatcherService : BackgroundService
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromSeconds(5);

    private readonly IMeasurementStatisticsService _statisticsService;
    private readonly MeasurementDataOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<MeasurementFileWatcherService> _logger;
    private readonly Channel<bool> _changeNotifications = Channel.CreateUnbounded<bool>(
        new UnboundedChannelOptions { SingleReader = true });

    public MeasurementFileWatcherService(
        IMeasurementStatisticsService statisticsService,
        IOptions<MeasurementDataOptions> options,
        IWebHostEnvironment environment,
        ILogger<MeasurementFileWatcherService> logger)
    {
        _statisticsService = statisticsService;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var filePath = GetDataFilePath();
        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);

        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
        {
            _logger.LogWarning("Measurement file watcher was not started because the file path is invalid: {FilePath}", filePath);
            return;
        }

        Directory.CreateDirectory(directory);

        using var watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.FileName
                | NotifyFilters.LastWrite
                | NotifyFilters.Size
                | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        watcher.Changed += OnMeasurementFileChanged;
        watcher.Created += OnMeasurementFileChanged;
        watcher.Renamed += OnMeasurementFileChanged;

        _logger.LogInformation("Watching measurement file for changes: {FilePath}", filePath);

        try
        {
            await ProcessChangeNotificationsAsync(stoppingToken);
        }
        finally
        {
            watcher.Changed -= OnMeasurementFileChanged;
            watcher.Created -= OnMeasurementFileChanged;
            watcher.Renamed -= OnMeasurementFileChanged;
        }
    }

    private async Task ProcessChangeNotificationsAsync(CancellationToken stoppingToken)
    {
        await foreach (var _ in _changeNotifications.Reader.ReadAllAsync(stoppingToken))
        {
            await DebounceAsync(stoppingToken);

            try
            {
                var currentSource = GetCurrentSourceStatus();
                var cachedSource = _statisticsService.GetCacheStatus();

                if (cachedSource is not null
                    && currentSource is not null
                    && cachedSource.SourceFileSizeBytes == currentSource.Value.FileSizeBytes
                    && cachedSource.SourceLastModifiedUtc == currentSource.Value.LastModifiedUtc)
                {
                    _logger.LogInformation("Measurement file event ignored because source metadata did not change.");
                    continue;
                }

                _logger.LogInformation("Measurement file changed. Starting cache recalculation.");
                var status = await _statisticsService.RecalculateAsync(stoppingToken);
                _logger.LogInformation(
                    "Measurement file change recalculation completed with {CityCount} cities at {CalculatedAtUtc}. Source size: {SourceFileSizeBytes} bytes.",
                    status.CityCount,
                    status.CalculatedAtUtc,
                    status.SourceFileSizeBytes);
            }
            catch (MeasurementCalculationInProgressException)
            {
                _logger.LogInformation("Measurement file change was detected, but a calculation is already running.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Measurement file watcher was cancelled.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Measurement file change recalculation failed.");
            }
        }
    }

    private async Task DebounceAsync(CancellationToken stoppingToken)
    {
        while (true)
        {
            await Task.Delay(DebounceDelay, stoppingToken);

            if (!_changeNotifications.Reader.TryRead(out _))
            {
                return;
            }
        }
    }

    private void OnMeasurementFileChanged(object sender, FileSystemEventArgs args)
    {
        _logger.LogInformation("Detected measurement file change: {ChangeType} {FullPath}", args.ChangeType, args.FullPath);
        _changeNotifications.Writer.TryWrite(true);
    }

    private string GetDataFilePath()
    {
        return Path.IsPathRooted(_options.FilePath)
            ? _options.FilePath
            : Path.Combine(_environment.ContentRootPath, _options.FilePath);
    }

    private SourceFileStatus? GetCurrentSourceStatus()
    {
        var filePath = GetDataFilePath();
        if (!File.Exists(filePath))
        {
            return null;
        }

        var fileInfo = new FileInfo(filePath);
        return new SourceFileStatus(fileInfo.LastWriteTimeUtc, fileInfo.Length);
    }

    private readonly record struct SourceFileStatus(
        DateTimeOffset LastModifiedUtc,
        long FileSizeBytes);
}
