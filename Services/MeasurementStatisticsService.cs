using System.Collections.Frozen;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using IndigoLabs.Api.Models;
using IndigoLabs.Api.Options;
using Microsoft.Extensions.Options;

namespace IndigoLabs.Api.Services;

public sealed class MeasurementStatisticsService : IMeasurementStatisticsService
{
    private const string ExpectedHeader = "datetime;city;temp_celsius";

    private readonly MeasurementDataOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private MeasurementCache? _cache;

    public MeasurementStatisticsService(
        IOptions<MeasurementDataOptions> options,
        IWebHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    public MeasurementCacheStatus? GetCacheStatus()
    {
        return _cache is { } cache ? ToStatus(cache) : null;
    }

    public async Task<IReadOnlyCollection<CityTemperatureStats>> GetAllAsync(CancellationToken cancellationToken)
    {
        var cache = await GetCacheAsync(cancellationToken);
        return cache.Cities;
    }

    public async Task<CityTemperatureStats?> GetByCityAsync(string city, CancellationToken cancellationToken)
    {
        var cache = await GetCacheAsync(cancellationToken);
        return cache.CitiesByName.GetValueOrDefault(city);
    }

    public async Task<IReadOnlyCollection<CityTemperatureStats>> GetByAverageTemperatureGreaterThanAsync(
        double value,
        CancellationToken cancellationToken)
    {
        var cache = await GetCacheAsync(cancellationToken);

        return cache.Cities
            .Where(city => city.AverageTemperatureCelsius > value)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<CityTemperatureStats>> GetByAverageTemperatureLessThanAsync(
        double value,
        CancellationToken cancellationToken)
    {
        var cache = await GetCacheAsync(cancellationToken);

        return cache.Cities
            .Where(city => city.AverageTemperatureCelsius < value)
            .ToArray();
    }

    public async Task<MeasurementCacheStatus> RecalculateAsync(CancellationToken cancellationToken)
    {
        if (!await _cacheLock.WaitAsync(0, cancellationToken))
        {
            throw new MeasurementCalculationInProgressException();
        }

        try
        {
            var cache = await BuildCacheAsync(cancellationToken);
            _cache = cache;

            return ToStatus(cache);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task<MeasurementCache> GetCacheAsync(CancellationToken cancellationToken)
    {
        if (_cache is { } cache)
        {
            return cache;
        }

        if (!await _cacheLock.WaitAsync(0, cancellationToken))
        {
            throw new MeasurementCalculationInProgressException();
        }

        try
        {
            _cache ??= await BuildCacheAsync(cancellationToken);
            return _cache;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task<MeasurementCache> BuildCacheAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var filePath = GetDataFilePath();
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Measurement data file was not found.", filePath);
        }

        var aggregates = new Dictionary<string, TemperatureAggregate>(StringComparer.OrdinalIgnoreCase);

        await using var stream = File.OpenRead(filePath);
        using var reader = new StreamReader(stream);

        var header = await reader.ReadLineAsync(cancellationToken);
        if (!string.Equals(header, ExpectedHeader, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Measurement data header is invalid. Expected '{ExpectedHeader}', got '{header ?? "<empty>"}'.");
        }

        long dataRowCount = 0;
        long skippedMalformedRowCount = 0;

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            dataRowCount++;

            if (!TryParseMeasurement(line, out var city, out var temperature))
            {
                skippedMalformedRowCount++;
                continue;
            }

            ref var aggregate = ref CollectionsMarshal.GetValueRefOrAddDefault(aggregates, city, out var exists);
            if (!exists)
            {
                aggregate = new TemperatureAggregate(temperature);
                continue;
            }

            aggregate.Add(temperature);
        }

        var cities = aggregates
            .Select(entry => entry.Value.ToStats(entry.Key))
            .OrderBy(entry => entry.City, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var fileInfo = new FileInfo(filePath);
        stopwatch.Stop();

        return new MeasurementCache(
            cities,
            cities.ToFrozenDictionary(city => city.City, StringComparer.OrdinalIgnoreCase),
            DateTimeOffset.UtcNow,
            fileInfo.LastWriteTimeUtc,
            fileInfo.Length,
            fileInfo.Name,
            fileInfo.FullName,
            dataRowCount,
            skippedMalformedRowCount,
            stopwatch.Elapsed);
    }

    private static MeasurementCacheStatus ToStatus(MeasurementCache cache)
    {
        return new MeasurementCacheStatus(
            cache.Cities.Count,
            cache.CalculatedAtUtc,
            cache.SourceLastModifiedUtc,
            cache.SourceFileSizeBytes,
            cache.SourceFileName,
            cache.SourceFilePath,
            cache.DataRowCount,
            cache.SkippedMalformedRowCount,
            cache.CalculationDuration.TotalMilliseconds);
    }

    private string GetDataFilePath()
    {
        return Path.IsPathRooted(_options.FilePath)
            ? _options.FilePath
            : Path.Combine(_environment.ContentRootPath, _options.FilePath);
    }

    private static bool TryParseMeasurement(string line, out string city, out double temperature)
    {
        city = string.Empty;
        temperature = default;

        var firstSeparator = line.IndexOf(';');
        if (firstSeparator < 0)
        {
            return false;
        }

        var secondSeparator = line.IndexOf(';', firstSeparator + 1);
        if (secondSeparator < 0)
        {
            return false;
        }

        city = line[(firstSeparator + 1)..secondSeparator];
        var temperatureText = line[(secondSeparator + 1)..];

        return city.Length > 0
            && double.TryParse(
                temperatureText,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out temperature);
    }

    private sealed record MeasurementCache(
        IReadOnlyCollection<CityTemperatureStats> Cities,
        FrozenDictionary<string, CityTemperatureStats> CitiesByName,
        DateTimeOffset CalculatedAtUtc,
        DateTimeOffset SourceLastModifiedUtc,
        long SourceFileSizeBytes,
        string SourceFileName,
        string SourceFilePath,
        long DataRowCount,
        long SkippedMalformedRowCount,
        TimeSpan CalculationDuration);

    private struct TemperatureAggregate
    {
        private double _min;
        private double _max;
        private double _sum;
        private long _count;

        public TemperatureAggregate(double temperature)
        {
            _min = temperature;
            _max = temperature;
            _sum = temperature;
            _count = 1;
        }

        public void Add(double temperature)
        {
            _min = Math.Min(_min, temperature);
            _max = Math.Max(_max, temperature);
            _sum += temperature;
            _count++;
        }

        public CityTemperatureStats ToStats(string city)
        {
            return new CityTemperatureStats(city, _min, _max, _sum / _count);
        }
    }
}
