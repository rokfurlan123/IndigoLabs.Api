namespace IndigoLabs.Api.Models;

public sealed record CityTemperatureStats(
    string City,
    double MinTemperatureCelsius,
    double MaxTemperatureCelsius,
    double AverageTemperatureCelsius);
