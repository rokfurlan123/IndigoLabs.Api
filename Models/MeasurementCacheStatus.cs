namespace IndigoLabs.Api.Models;

public sealed record MeasurementCacheStatus(
    int CityCount,
    DateTimeOffset CalculatedAtUtc,
    DateTimeOffset SourceLastModifiedUtc,
    long SourceFileSizeBytes,
    string SourceFileName,
    string SourceFilePath,
    long DataRowCount,
    long SkippedMalformedRowCount,
    double CalculationDurationMilliseconds);
