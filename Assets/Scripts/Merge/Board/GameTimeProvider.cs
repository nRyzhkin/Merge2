using System;

namespace SanIsland.Merge
{
    public sealed class GameTimeProvider : IGameTimeProvider
    {
        public double UnixTimeNow => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
    }
}
