namespace MartialTactics.Battle
{
    /// <summary>mulberry32. 웹 시뮬레이터와 같은 시드에서 같은 난수열을 낸다.</summary>
    public sealed class Rng
    {
        int state;

        public Rng(int seed) { state = seed; }

        public double Next()
        {
            unchecked
            {
                state += 0x6D2B79F5;
                int t = (state ^ (int)((uint)state >> 15)) * (1 | state);
                t = (t + (t ^ (int)((uint)t >> 7)) * (61 | t)) ^ t;
                return (uint)(t ^ (int)((uint)t >> 14)) / 4294967296.0;
            }
        }
    }
}
