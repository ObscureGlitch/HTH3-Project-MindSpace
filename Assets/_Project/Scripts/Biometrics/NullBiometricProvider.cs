namespace TheLastWatch.Biometrics
{
    public sealed class NullBiometricProvider : IBiometricProvider
    {
        public bool IsAvailable => false;

        public bool TryGetLatestSample(out BiometricSample sample)
        {
            sample = default;
            return false;
        }
    }
}
