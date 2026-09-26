namespace TheLastWatch.Biometrics
{
    public interface IBiometricProvider
    {
        bool IsAvailable { get; }
        bool TryGetLatestSample(out BiometricSample sample);
    }
}
