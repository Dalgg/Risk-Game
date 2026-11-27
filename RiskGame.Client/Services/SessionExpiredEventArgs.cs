using System;

namespace RiskGame.Services
{
    public sealed class SessionExpiredEventArgs : EventArgs
    {
        public SessionExpiredEventArgs()
        {
        }

        public SessionExpiredEventArgs(bool isSuspended, long bannedUntilUnixMilliseconds)
        {
            IsSuspended = isSuspended;
            BannedUntilUnixMilliseconds = bannedUntilUnixMilliseconds;
        }

        public bool IsSuspended { get; }

        public long BannedUntilUnixMilliseconds { get; }
    }
}
