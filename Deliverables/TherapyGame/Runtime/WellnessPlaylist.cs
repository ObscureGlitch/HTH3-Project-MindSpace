using System;

namespace TheLastWatch.Audio
{
    // Stable shuffled cycle: Previous and Next are exact opposites, including at either end.
    public sealed class WellnessPlaylist
    {
        private readonly int[] order;
        private int position = -1;
        public WellnessPlaylist(int count, int seed)
        {
            order = new int[Math.Max(0, count)]; var random = new Random(seed);
            for (int i = 0; i < order.Length; i++) order[i] = i;
            for (int i = order.Length - 1; i > 0; i--)
            { int j = random.Next(i + 1); int value = order[i]; order[i] = order[j]; order[j] = value; }
        }
        public int Move(int direction)
        {
            if (order.Length == 0) return -1;
            position = position < 0 ? 0 : (position + (direction < 0 ? -1 : 1) + order.Length) % order.Length;
            return order[position];
        }
    }
}
