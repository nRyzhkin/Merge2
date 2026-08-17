using System;

namespace SanIsland.Merge
{
    public sealed class GameTimeProvider : IGameTimeProvider
    {
        double _debugOffsetSeconds;

        public double UnixTimeNow => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0 + _debugOffsetSeconds;

        public void AdvanceDebugOffset(double seconds)
        {
            if (seconds <= 0d)
            {
                return;
            }

            _debugOffsetSeconds += seconds;
        }
    }
}
