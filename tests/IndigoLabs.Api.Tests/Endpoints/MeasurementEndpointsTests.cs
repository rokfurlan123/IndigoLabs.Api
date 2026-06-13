using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using IndigoLabs.Api.Models;
using IndigoLabs.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace IndigoLabs.Api.Tests.Endpoints;

public sealed class MeasurementEndpointsTests
{
    [Fact]
    public async Task GetCity_WithoutCredentials_ReturnsUnauthorized()
    {
        using var application = new TestApplication(new FakeMeasurementStatisticsService());
        using var client = application.CreateClient();

        var response = await client.GetAsync("/api/cities/Paris");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Basic", response.Headers.WwwAuthenticate.Single().Scheme);
    }

    [Fact]
    public async Task GetCity_WhenCityDoesNotExist_ReturnsNotFound()
    {
        using var application = new TestApplication(new FakeMeasurementStatisticsService());
        using var client = application.CreateClient();

        var response = await client.GetAsyncWithBasicAuth("/api/cities/Atlantis");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCity_WhenCalculationIsRunning_ReturnsServiceUnavailableProblem()
    {
        using var application = new TestApplication(new FakeMeasurementStatisticsService
        {
            ExceptionToThrow = new MeasurementCalculationInProgressException()
        });
        using var client = application.CreateClient();

        var response = await client.GetAsyncWithBasicAuth("/api/cities/Paris");
        var body = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ErrorResponse>(body, JsonOptions);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(error);
        Assert.Equal("Temperature calculations are currently happening. Try again shortly.", error.Message);
        Assert.DoesNotContain("title", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("detail", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetCity_WhenMeasurementFileIsMissing_ReturnsInternalServerErrorProblem()
    {
        using var application = new TestApplication(new FakeMeasurementStatisticsService
        {
            ExceptionToThrow = new FileNotFoundException("Measurement data file was not found.", "Data/measurements.csv")
        });
        using var client = application.CreateClient();

        var response = await client.GetAsyncWithBasicAuth("/api/cities/Paris");
        var body = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ErrorResponse>(body, JsonOptions);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.NotNull(error);
        Assert.Equal("Measurement data file was not found.", error.Message);
        Assert.DoesNotContain("title", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("detail", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetCity_WhenMeasurementFileIsInvalid_ReturnsInternalServerErrorProblem()
    {
        using var application = new TestApplication(new FakeMeasurementStatisticsService
        {
            ExceptionToThrow = new InvalidDataException("Measurement data header is invalid.")
        });
        using var client = application.CreateClient();

        var response = await client.GetAsyncWithBasicAuth("/api/cities/Paris");
        var body = await response.Content.ReadAsStringAsync();
        var error = JsonSerializer.Deserialize<ErrorResponse>(body, JsonOptions);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.NotNull(error);
        Assert.Equal("Measurement data header is invalid.", error.Message);
        Assert.DoesNotContain("title", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("detail", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetCity_WithCredentials_ReturnsCityStats()
    {
        using var application = new TestApplication(new FakeMeasurementStatisticsService
        {
            CityResult = new CityTemperatureStats("Paris", -5.0, 30.0, 12.5)
        });
        using var client = application.CreateClient();

        var response = await client.GetAsyncWithBasicAuth("/api/cities/Paris");
        var payload = await response.Content.ReadAsStringAsync();
        var city = JsonSerializer.Deserialize<CityTemperatureStats>(payload, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(city);
        Assert.Equal("Paris", city.City);
        Assert.Equal(-5.0, city.MinTemperatureCelsius);
        Assert.Equal(30.0, city.MaxTemperatureCelsius);
        Assert.Equal(12.5, city.AverageTemperatureCelsius);
    }

    [Fact]
    public async Task GetAllCities_WithCredentials_ReturnsAllCities()
    {
        using var application = new TestApplication(new FakeMeasurementStatisticsService
        {
            AllCities =
            [
                new CityTemperatureStats("Berlin", 0.0, 10.0, 5.0),
                new CityTemperatureStats("Paris", -5.0, 30.0, 12.5)
            ]
        });
        using var client = application.CreateClient();

        var response = await client.GetAsyncWithBasicAuth("/api/cities");
        var payload = await response.Content.ReadAsStringAsync();
        var cities = JsonSerializer.Deserialize<CityTemperatureStats[]>(payload, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(cities);
        Assert.Equal(["Berlin", "Paris"], cities.Select(city => city.City));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class TestApplication : WebApplicationFactory<Program>
    {
        private readonly IMeasurementStatisticsService _statisticsService;

        public TestApplication(IMeasurementStatisticsService statisticsService)
        {
            _statisticsService = statisticsService;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IMeasurementStatisticsService>();
                services.AddSingleton(_statisticsService);
            });
        }
    }

    private sealed class FakeMeasurementStatisticsService : IMeasurementStatisticsService
    {
        public Exception? ExceptionToThrow { get; init; }

        public CityTemperatureStats? CityResult { get; init; }

        public IReadOnlyCollection<CityTemperatureStats> AllCities { get; init; } = [];

        public MeasurementCacheStatus? GetCacheStatus()
        {
            return CreateCacheStatus();
        }

        public Task<IReadOnlyCollection<CityTemperatureStats>> GetAllAsync(CancellationToken cancellationToken)
        {
            ThrowIfCalculating();
            return Task.FromResult(AllCities);
        }

        public Task<CityTemperatureStats?> GetByCityAsync(string city, CancellationToken cancellationToken)
        {
            ThrowIfCalculating();
            return Task.FromResult(CityResult);
        }

        public Task<IReadOnlyCollection<CityTemperatureStats>> GetByAverageTemperatureGreaterThanAsync(
            double value,
            CancellationToken cancellationToken)
        {
            ThrowIfCalculating();
            IReadOnlyCollection<CityTemperatureStats> cities = AllCities
                .Where(city => city.AverageTemperatureCelsius > value)
                .ToArray();

            return Task.FromResult(cities);
        }

        public Task<IReadOnlyCollection<CityTemperatureStats>> GetByAverageTemperatureLessThanAsync(
            double value,
            CancellationToken cancellationToken)
        {
            ThrowIfCalculating();
            IReadOnlyCollection<CityTemperatureStats> cities = AllCities
                .Where(city => city.AverageTemperatureCelsius < value)
                .ToArray();

            return Task.FromResult(cities);
        }

        public Task<MeasurementCacheStatus> RecalculateAsync(CancellationToken cancellationToken)
        {
            ThrowIfCalculating();
            return Task.FromResult(CreateCacheStatus());
        }

        private static MeasurementCacheStatus CreateCacheStatus()
        {
            return new MeasurementCacheStatus(
                99,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                1_087_787_485,
                "measurements.csv",
                "Data/measurements.csv",
                34_713_360,
                0,
                1234.5);
        }

        private void ThrowIfCalculating()
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }
        }
    }
}

file static class HttpClientExtensions
{
    public static Task<HttpResponseMessage> GetAsyncWithBasicAuth(this HttpClient client, string requestUri)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = CreateBasicAuthHeader();
        return client.SendAsync(request);
    }

    private static AuthenticationHeaderValue CreateBasicAuthHeader()
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("indigo:labs"));
        return new AuthenticationHeaderValue("Basic", credentials);
    }
}
