using IndigoLabs.Api.Models;

namespace IndigoLabs.Api.Services;

public interface IMeasurementStatisticsService
{
    MeasurementCacheStatus? GetCacheStatus();

    Task<IReadOnlyCollection<CityTemperatureStats>> GetAllAsync(CancellationToken cancellationToken);

    Task<CityTemperatureStats?> GetByCityAsync(string city, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CityTemperatureStats>> GetByAverageTemperatureGreaterThanAsync(
        double value,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CityTemperatureStats>> GetByAverageTemperatureLessThanAsync(
        double value,
        CancellationToken cancellationToken);

    Task<MeasurementCacheStatus> RecalculateAsync(CancellationToken cancellationToken);
}
