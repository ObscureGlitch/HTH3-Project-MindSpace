using System;

namespace TheLastWatch.Environment
{
    // Pure setup logic, shared by gameplay and offline population tests.
    public static class KoiSchoolLayout
    {
        public const int MaximumFish = 18;
        public const int RouteCount = 9;
        public const float MinimumScale = .90f;
        public const float MaximumScale = 1.08f;
        public const float FirstLane = .16f;
        public const float LaneSpacing = .055f;

        public readonly struct Entry
        {
            public readonly int Variety, MotionSeed;
            public readonly float Scale, Radius;
            public readonly double PhaseOffset;
            public Entry(int variety, int seed, float scale, float radius, double phaseOffset)
            { Variety = variety; MotionSeed = seed; Scale = scale; Radius = radius; PhaseOffset = phaseOffset; }
        }
        public static Entry[] Create(Random random, int count, int varietyCount)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (varietyCount < 1) throw new ArgumentOutOfRangeException(nameof(varietyCount));
            count = Math.Max(1, Math.Min(MaximumFish, count));
            var varieties = new int[varietyCount];
            var sizeBands = new int[count];
            var routeSeeds = new int[RouteCount];
            for (int i = 0; i < routeSeeds.Length; i++) routeSeeds[i] = random.Next();
            for (int i = 0; i < varieties.Length; i++) varieties[i] = i;
            for (int i = 0; i < count; i++) sizeBands[i] = i;
            Shuffle(random, sizeBands);
            var entries = new Entry[count];
            for (int i = 0; i < count; i++)
            {
                // A shuffled bag allows more fish than models, with no variety dominating.
                if (i % varietyCount == 0) Shuffle(random, varieties);
                float size = MinimumScale + (MaximumScale - MinimumScale) * (sizeBands[i] + .05f + .9f * (float)random.NextDouble()) / count;
                int route = i % RouteCount;
                // Partners share the speed/rest schedule and start half a lap apart.
                // This keeps both fish separated indefinitely without collision searches.
                entries[i] = new Entry(varieties[i % varietyCount], routeSeeds[route], size,
                    FirstLane + route * LaneSpacing, (i / RouteCount) * Math.PI);
            }
            return entries;
        }
        static void Shuffle(Random random, int[] items)
        {
            for (int i = items.Length - 1; i > 0; i--)
            { int j = random.Next(i + 1), old = items[i]; items[i] = items[j]; items[j] = old; }
        }
    }
}
