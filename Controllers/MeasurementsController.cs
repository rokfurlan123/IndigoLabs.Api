using IndigoLabs.Api.Models;
using IndigoLabs.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IndigoLabs.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
[Produces("application/json")]
public sealed class MeasurementsController : ControllerBase
{
    private readonly IMeasurementStatisticsService _statisticsService;

    public MeasurementsController(IMeasurementStatisticsService statisticsService)
    {
        _statisticsService = statisticsService;
    }

    [HttpGet("measurements/cache", Name = "GetMeasurementCacheStatus")]
    [ProducesResponseType<MeasurementCacheStatus>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult GetCacheStatus()
    {
        var status = _statisticsService.GetCacheStatus();
        return status is null
            ? Error(
                "The cache is still warming up. Try again shortly.",
                StatusCodes.Status503ServiceUnavailable)
            : Ok(status);
    }

    [HttpGet("cities", Name = "GetCityTemperatureStats")]
    [ProducesResponseType<IReadOnlyCollection<CityTemperatureStats>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetCities(CancellationToken cancellationToken)
    {
        return ExecuteMeasurementRequestAsync(async () =>
        {
            var cities = await _statisticsService.GetAllAsync(cancellationToken);
            return Ok(cities);
        });
    }

    [HttpGet("cities/{city}", Name = "GetCityTemperatureStatsByCity")]
    [ProducesResponseType<CityTemperatureStats>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetCity(string city, CancellationToken cancellationToken)
    {
        return ExecuteMeasurementRequestAsync(async () =>
        {
            var stats = await _statisticsService.GetByCityAsync(city, cancellationToken);
            return stats is null ? NotFound() : Ok(stats);
        });
    }

    [HttpGet("cities/average-temperature/greater-than/{value:double}", Name = "GetCitiesWithAverageTemperatureGreaterThan")]
    [ProducesResponseType<IReadOnlyCollection<CityTemperatureStats>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetCitiesWithAverageTemperatureGreaterThan(
        double value,
        CancellationToken cancellationToken)
    {
        return ExecuteMeasurementRequestAsync(async () =>
        {
            var cities = await _statisticsService.GetByAverageTemperatureGreaterThanAsync(
                value,
                cancellationToken);

            return Ok(cities);
        });
    }

    [HttpGet("cities/average-temperature/less-than/{value:double}", Name = "GetCitiesWithAverageTemperatureLessThan")]
    [ProducesResponseType<IReadOnlyCollection<CityTemperatureStats>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetCitiesWithAverageTemperatureLessThan(
        double value,
        CancellationToken cancellationToken)
    {
        return ExecuteMeasurementRequestAsync(async () =>
        {
            var cities = await _statisticsService.GetByAverageTemperatureLessThanAsync(
                value,
                cancellationToken);

            return Ok(cities);
        });
    }

    [HttpPost("measurements/recalculate", Name = "RecalculateMeasurements")]
    [ProducesResponseType<MeasurementCacheStatus>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> Recalculate(CancellationToken cancellationToken)
    {
        return ExecuteMeasurementRequestAsync(async () =>
        {
            var status = await _statisticsService.RecalculateAsync(cancellationToken);
            return Ok(status);
        });
    }

    private async Task<IActionResult> ExecuteMeasurementRequestAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (MeasurementCalculationInProgressException exception)
        {
            return Error(exception.Message, StatusCodes.Status503ServiceUnavailable);
        }
        catch (FileNotFoundException exception)
        {
            return Error(exception.Message, StatusCodes.Status500InternalServerError);
        }
        catch (InvalidDataException exception)
        {
            return Error(exception.Message, StatusCodes.Status500InternalServerError);
        }
    }

    private ObjectResult Error(string message, int statusCode)
    {
        return StatusCode(statusCode, new ErrorResponse(message));
    }
}
