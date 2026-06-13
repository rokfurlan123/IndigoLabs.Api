using IndigoLabs.Api.Models;
using IndigoLabs.Api.Services;

namespace IndigoLabs.Api.Endpoints;

public static class MeasurementEndpoints
{
    public static IEndpointRouteBuilder MapMeasurementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api")
            .WithTags("Measurements")
            .RequireAuthorization();

        group.MapGet("/measurements/cache", (
                IMeasurementStatisticsService statisticsService) =>
            {
                var status = statisticsService.GetCacheStatus();
                return status is null
                    ? Results.Problem(
                        title: "Measurement cache is not ready.",
                        detail: "The cache is still warming up. Try again shortly.",
                        statusCode: StatusCodes.Status503ServiceUnavailable)
                    : Results.Ok(status);
            })
            .WithName("GetMeasurementCacheStatus")
            .WithSummary("Gets the current measurement cache status.")
            .Produces<MeasurementCacheStatus>()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/cities", async (
                IMeasurementStatisticsService statisticsService,
                CancellationToken cancellationToken) =>
            {
                return await ExecuteMeasurementRequestAsync(async () =>
                {
                    var cities = await statisticsService.GetAllAsync(cancellationToken);
                    return Results.Ok(cities);
                });
            })
            .WithName("GetCityTemperatureStats")
            .WithSummary("Gets min, max, and average temperatures for every city.")
            .Produces<IReadOnlyCollection<CityTemperatureStats>>()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/cities/{city}", async (
                string city,
                IMeasurementStatisticsService statisticsService,
                CancellationToken cancellationToken) =>
            {
                return await ExecuteMeasurementRequestAsync(async () =>
                {
                    var stats = await statisticsService.GetByCityAsync(city, cancellationToken);
                    return stats is null ? Results.NotFound() : Results.Ok(stats);
                });
            })
            .WithName("GetCityTemperatureStatsByCity")
            .WithSummary("Gets min, max, and average temperatures for one city.")
            .Produces<CityTemperatureStats>()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/cities/average-temperature/greater-than/{value:double}", async (
                double value,
                IMeasurementStatisticsService statisticsService,
                CancellationToken cancellationToken) =>
            {
                return await ExecuteMeasurementRequestAsync(async () =>
                {
                    var cities = await statisticsService.GetByAverageTemperatureGreaterThanAsync(
                        value,
                        cancellationToken);

                    return Results.Ok(cities);
                });
            })
            .WithName("GetCitiesWithAverageTemperatureGreaterThan")
            .WithSummary("Gets cities whose average temperature is greater than the supplied value.")
            .Produces<IReadOnlyCollection<CityTemperatureStats>>()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/cities/average-temperature/less-than/{value:double}", async (
                double value,
                IMeasurementStatisticsService statisticsService,
                CancellationToken cancellationToken) =>
            {
                return await ExecuteMeasurementRequestAsync(async () =>
                {
                    var cities = await statisticsService.GetByAverageTemperatureLessThanAsync(
                        value,
                        cancellationToken);

                    return Results.Ok(cities);
                });
            })
            .WithName("GetCitiesWithAverageTemperatureLessThan")
            .WithSummary("Gets cities whose average temperature is less than the supplied value.")
            .Produces<IReadOnlyCollection<CityTemperatureStats>>()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("/measurements/recalculate", async (
                IMeasurementStatisticsService statisticsService,
                CancellationToken cancellationToken) =>
            {
                return await ExecuteMeasurementRequestAsync(async () =>
                {
                    var status = await statisticsService.RecalculateAsync(cancellationToken);
                    return Results.Ok(status);
                });
            })
            .WithName("RecalculateMeasurements")
            .WithSummary("Recalculates and replaces the cached temperature statistics from the CSV file.")
            .Produces<MeasurementCacheStatus>()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> ExecuteMeasurementRequestAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (MeasurementCalculationInProgressException exception)
        {
            return Results.Problem(
                title: "Temperature calculations are currently happening.",
                detail: exception.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (FileNotFoundException exception)
        {
            return Results.Problem(
                title: "Measurement data file was not found.",
                detail: exception.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (InvalidDataException exception)
        {
            return Results.Problem(
                title: "Measurement data file is invalid.",
                detail: exception.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
