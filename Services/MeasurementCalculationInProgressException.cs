namespace IndigoLabs.Api.Services;

public sealed class MeasurementCalculationInProgressException : Exception
{
    public MeasurementCalculationInProgressException()
        : base("Temperature calculations are currently happening. Try again shortly.")
    {
    }
}
