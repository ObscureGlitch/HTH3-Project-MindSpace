using System;
using System.Diagnostics;
using TheLastWatch.Environment;

public static class PerformanceChecks
{
    private static void Require(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    private static PondWaveField Pond(float size = 13.4f)
    {
        var pond = new PondWaveField(size, size);
        int n = PondWaveField.Resolution;
        for (int z = 1; z < n - 1; z++)
            for (int x = 1; x < n - 1; x++) pond.Wet[z * n + x] = true;
        return pond;
    }

    public static string Run()
    {
        var pond = Pond();
        Require(!pond.IsActive, "An undisturbed pond should sleep.");
        pond.Impulse(0, .5f, -.6f);
        pond.Impulse(.5f, .5f, float.NaN);
        pond.Impulse(.5f, .5f, 0);
        Require(!pond.IsActive, "Invalid and zero impulses should not wake the solver.");
        pond.Impulse(.5f, .5f, -.6f);
        Require(pond.IsActive, "A valid impact should wake the solver.");
        float remote = 0;
        int n = PondWaveField.Resolution, settledAt = 0;
        for (int step = 0; step < 3600; step++)
        {
            pond.Step();
            remote = Math.Max(remote, Math.Abs(pond.Height[n / 2 * n + n / 2 + 8]));
            double mean = 0;
            foreach (float height in pond.Height)
            {
                Require(!float.IsNaN(height) && Math.Abs(height) <= .07001f, "Unstable wave.");
                mean += height;
            }
            Require(Math.Abs(mean / pond.Height.Length) < .0001, "Mean water level changed.");
            Require(pond.Height[0] == 0 && pond.Height[n] == 0, "Wave crossed a dry boundary.");
            if (!pond.IsActive) { settledAt = step + 1; break; }
        }
        Require(remote > .00001f, "Ripples failed to propagate.");
        Require(settledAt > 60 && settledAt < 3600, "Ripples must decay naturally and then sleep.");
        foreach (float height in pond.Height) Require(height == 0, "Sleeping field must be flat.");
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 100000; i++) pond.Step();
        watch.Stop();
        double idleMs = watch.Elapsed.TotalMilliseconds;
        pond.Impulse(.4f, .6f, -.5f);
        pond.Step();
        Require(pond.IsActive, "A settled pond must wake for subsequent rain or footsteps.");
        Require(Array.Exists(pond.Height, h => Math.Abs(h) > 0), "Waking must displace the surface.");

        var storm = Pond(.5f);
        for (int i = 0; i < 600; i++)
        {
            storm.Impulse(.5f, .5f, -.7f); storm.Step();
            foreach (float height in storm.Height)
                Require(!float.IsNaN(height) && Math.Abs(height) <= .07001f, "Small-pond storm instability.");
        }
        return "PASS: idle sleep, input guards, propagation, dry boundary, conservation, settling, wake-up and small-pond storm stability.\n" +
            "Settled after " + settledAt + " simulation steps; 100,000 sleeping steps: " + idleMs.ToString("F3") +
            " ms (CPU microbenchmark, not gameplay FPS).";
    }
}
