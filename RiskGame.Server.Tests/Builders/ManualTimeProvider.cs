using System;
using System.Threading;

namespace RiskGame.Server.Tests.Builders
{
    public sealed class ManualTimeProvider : TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            return Interlocked.Read(ref timestamp);
        }

        public override DateTimeOffset GetUtcNow()
        {
            return DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());
        }

        public void Advance(TimeSpan elapsed)
        {
            Interlocked.Add(ref timestamp, elapsed.Ticks);
        }

    }
}
