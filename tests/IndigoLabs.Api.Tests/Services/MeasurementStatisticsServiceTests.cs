using IndigoLabs.Api.Options;
using IndigoLabs.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace IndigoLabs.Api.Tests.Services;

public sealed class MeasurementStatisticsServiceTests
{
    [Fact]
    public async Task GetAllAsync_CalculatesMinMaxAndAveragePerCity()
    {
        using var testData = TestMeasurementData.Create(
            "datetime;city;temp_celsius",
            "2026-01-01T00:00;Paris;10.0",
            "2026-01-02T00:00;Paris;20.0",
            "2026-01-03T00:00;Paris;-5.0",
            "2026-01-01T00:00;Berlin;0.0",
            "2026-01-02T00:00;Berlin;8.0");

        var service = CreateService(testData);

        var cities = await service.GetAllAsync(CancellationToken.None);

        var paris = Assert.Single(cities, city => city.City == "Paris");
        Assert.Equal(-5.0, paris.MinTemperatureCelsius);
        Assert.Equal(20.0, paris.MaxTemperatureCelsius);
        Assert.Equal(8.333333333333334, paris.AverageTemperatureCelsius, precision: 12);

        var berlin = Assert.Single(cities, city => city.City == "Berlin");
        Assert.Equal(0.0, berlin.MinTemperatureCelsius);
        Assert.Equal(8.0, berlin.MaxTemperatureCelsius);
        Assert.Equal(4.0, berlin.AverageTemperatureCelsius);
    }

    [Fact]
    public async Task GetByCityAsync_FindsCityCaseInsensitively()
    {
        using var testData = TestMeasurementData.Create(
            "datetime;city;temp_celsius",
            "2026-01-01T00:00;Paris;10.0");

        var service = CreateService(testData);

        var city = await service.GetByCityAsync("paris", CancellationToken.None);

        Assert.NotNull(city);
        Assert.Equal("Paris", city.City);
    }

    [Fact]
    public async Task AverageTemperatureFilters_ReturnMatchingCities()
    {
        using var testData = TestMeasurementData.Create(
            "datetime;city;temp_celsius",
            "2026-01-01T00:00;Paris;10.0",
            "2026-01-02T00:00;Paris;20.0",
            "2026-01-01T00:00;Berlin;0.0",
            "2026-01-02T00:00;Berlin;4.0",
            "2026-01-01T00:00;Madrid;25.0",
            "2026-01-02T00:00;Madrid;35.0");

        var service = CreateService(testData);

        var greaterThan = await service.GetByAverageTemperatureGreaterThanAsync(20.0, CancellationToken.None);
        var lessThan = await service.GetByAverageTemperatureLessThanAsync(10.0, CancellationToken.None);

        Assert.Equal(["Madrid"], greaterThan.Select(city => city.City));
        Assert.Equal(["Berlin"], lessThan.Select(city => city.City));
    }

    [Fact]
    public async Task RecalculateAsync_RefreshesCacheAfterCsvChanges()
    {
        using var testData = TestMeasurementData.Create(
            "datetime;city;temp_celsius",
            "2026-01-01T00:00;Paris;10.0");

        var service = CreateService(testData);
        _ = await service.GetByCityAsync("Paris", CancellationToken.None);

        testData.Write(
            "datetime;city;temp_celsius",
            "2026-01-01T00:00;Paris;10.0",
            "2026-01-02T00:00;Paris;30.0");

        var status = await service.RecalculateAsync(CancellationToken.None);
        var paris = await service.GetByCityAsync("Paris", CancellationToken.None);

        Assert.NotNull(paris);
        Assert.Equal(20.0, paris.AverageTemperatureCelsius);
        Assert.Equal(1, status.CityCount);
        Assert.Equal(new FileInfo(testData.FilePath).Length, status.SourceFileSizeBytes);
    }

    [Fact]
    public async Task GetAllAsync_WhenCsvIsMissing_ThrowsFileNotFoundException()
    {
        using var testData = TestMeasurementData.Create("datetime;city;temp_celsius");
        File.Delete(testData.FilePath);

        var service = CreateService(testData);

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            service.GetAllAsync(CancellationToken.None));
    }

    private static MeasurementStatisticsService CreateService(TestMeasurementData testData)
    {
        return new MeasurementStatisticsService(
            Microsoft.Extensions.Options.Options.Create(new MeasurementDataOptions { FilePath = testData.RelativeFilePath }),
            new TestWebHostEnvironment(testData.ContentRootPath));
    }

    private sealed class TestMeasurementData : IDisposable
    {
        private TestMeasurementData(string contentRootPath, string relativeFilePath)
        {
            ContentRootPath = contentRootPath;
            RelativeFilePath = relativeFilePath;
            FilePath = Path.Combine(contentRootPath, relativeFilePath);
        }

        public string ContentRootPath { get; }

        public string RelativeFilePath { get; }

        public string FilePath { get; }

        public static TestMeasurementData Create(params string[] lines)
        {
            var contentRootPath = Path.Combine(Path.GetTempPath(), $"indigolabs-api-tests-{Guid.NewGuid():N}");
            var testData = new TestMeasurementData(contentRootPath, Path.Combine("Data", "measurements.csv"));
            testData.Write(lines);
            return testData;
        }

        public void Write(params string[] lines)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllLines(FilePath, lines);
        }

        public void Dispose()
        {
            if (Directory.Exists(ContentRootPath))
            {
                Directory.Delete(ContentRootPath, recursive: true);
            }
        }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
            WebRootPath = Path.Combine(contentRootPath, "wwwroot");
            WebRootFileProvider = new NullFileProvider();
        }

        public string ApplicationName { get; set; } = "IndigoLabs.Api.Tests";

        public IFileProvider ContentRootFileProvider { get; set; }

        public string ContentRootPath { get; set; }

        public string EnvironmentName { get; set; } = Environments.Development;

        public IFileProvider WebRootFileProvider { get; set; }

        public string WebRootPath { get; set; }
    }
}
