namespace TheLastWatch.Biometrics
{
    public readonly struct BiometricSample
    {
        public BiometricSample(float normalizedArousal, double timestampSeconds)
        {
            NormalizedArousal = normalizedArousal;
            TimestampSeconds = timestampSeconds;
        }

        public float NormalizedArousal { get; }
        public double TimestampSeconds { get; }
    }
}
