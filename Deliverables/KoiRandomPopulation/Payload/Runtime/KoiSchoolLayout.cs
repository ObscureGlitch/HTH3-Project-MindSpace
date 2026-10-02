using System;

namespace TheLastWatch.Environment
{
    // Pure setup logic, shared by gameplay and offline population tests.
    public static class KoiSchoolLayout
    {
        public const int MinimumFish = 15;
        public const int MaximumFish = 30;
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
        public static int RollCount(Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            return random.Next(MinimumFish, MaximumFish + 1);
        }
        public static Entry[] Create(Random random, int count, int varietyCount)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (varietyCount < 1) throw new ArgumentOutOfRangeException(nameof(varietyCount));
            count = Math.Max(1, Math.Min(MaximumFish, count));
            var varieties = new int[varietyCount];
            var sizeBands = new int[count];
            var routeSeeds = new int[RouteCount];
            var routes = new int[RouteCount];
            for (int i = 0; i < routeSeeds.Length; i++) routeSeeds[i] = random.Next();
            for (int i = 0; i < routes.Length; i++) routes[i] = i;
            Shuffle(random, routes);
            for (int i = 0; i < varieties.Length; i++) varieties[i] = i;
            for (int i = 0; i < count; i++) sizeBands[i] = i;
            Shuffle(random, sizeBands);
            var entries = new Entry[count];
            for (int i = 0; i < count; i++)
            {
                // A shuffled bag allows more fish than models, with no variety dominating.
                if (i % varietyCount == 0) Shuffle(random, varieties);
                float size = MinimumScale + (MaximumScale - MinimumScale) * (sizeBands[i] + .05f + .9f * (float)random.NextDouble()) / count;
                int slot = i % RouteCount, route = routes[slot];
                int occupants = count / RouteCount + (slot < count % RouteCount ? 1 : 0);
                // Evenly space up to four fish on a shared route clock. Shuffle which
                // routes receive the extra occupants so large schools remain varied.
                entries[i] = new Entry(varieties[i % varietyCount], routeSeeds[route], size,
                    FirstLane + route * LaneSpacing, (i / RouteCount) * Math.PI * 2 / occupants);
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
