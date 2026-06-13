namespace IndigoLabs.Api.Options;

public sealed class MeasurementDataOptions
{
    public const string SectionName = "MeasurementData";

    public string FilePath { get; init; } = "Data/measurements.csv";
}
