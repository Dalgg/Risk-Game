using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RiskGame.Exceptions;
using RiskGame.Server.Services;
using Xunit;

namespace RiskGame.Server.Tests.Builders
{
    public sealed class SingleSessionBuilder
    {
        public const int UserId = 41;
        public const int OtherUserId = 43;
        public const int GuestUserId = -41;
        public const int OneAcceptedLogin = 1;
        public const int GraceSeconds = 30;
        public const int BeforeGraceSeconds = 29;
        public const int BeyondGraceSeconds = 31;
        public const int LifetimeHours = 12;
        public const string FirstConnectionId = "first-connection";
        public const string ReplacementConnectionId = "replacement-connection";
        private const int ConcurrentRegistrations = 2;
        private const int Coordinator = 1;
        private const int WaitSeconds = 5;

        public SingleSessionBuilder()
        {
            Time = new SessionTimeProvider();
            Registry = new ProfileSessionRegistry(Time);
            Token = Registry.Register(UserId);
        }

        public SessionTimeProvider Time { get; }

        public ProfileSessionRegistry Registry { get; }

        public string Token { get; }

        public void AttachFirstConnection()
        {
            Registry.AttachSession(UserId, Token, FirstConnectionId);
        }

        public void DisconnectFirstConnection()
        {
            AttachFirstConnection();
            Registry.DetachSession(Token, FirstConnectionId);
        }

        public void ReconnectBeforeGraceExpires()
        {
            DisconnectFirstConnection();
            Time.Advance(TimeSpan.FromSeconds(BeforeGraceSeconds));
            Registry.AttachSession(UserId, Token, ReplacementConnectionId);
        }

        public void AttemptSecondLogin()
        {
            Record.Exception(() => Registry.Register(UserId));
        }

        public int RegisterConcurrently()
        {
            Registry.RemoveSession(Token);
            using Barrier startingGate = new Barrier(ConcurrentRegistrations + Coordinator);
            Task<bool> firstLogin = Task.Run(() => RegisterTogether(startingGate));
            Task<bool> secondLogin = Task.Run(() => RegisterTogether(startingGate));
            WaitTogether(startingGate);
            Task<bool>[] loginAttempts = new[] { firstLogin, secondLogin };
            if (!Task.WaitAll(loginAttempts, TimeSpan.FromSeconds(WaitSeconds)))
            {
                throw new TimeoutException("The simultaneous login attempts did not complete.");
            }
            return loginAttempts.Count(loginAttempt => loginAttempt.Result);
        }

        private bool RegisterTogether(Barrier startingGate)
        {
            WaitTogether(startingGate);
            Exception? registrationFailure = Record.Exception(() => Registry.Register(UserId));
            if (registrationFailure != null && registrationFailure is not SessionAlreadyActiveException)
            {
                throw registrationFailure;
            }
            return registrationFailure == null;
        }

        private static void WaitTogether(Barrier startingGate)
        {
            if (!startingGate.SignalAndWait(TimeSpan.FromSeconds(WaitSeconds)))
            {
                throw new TimeoutException("The simultaneous login attempts did not reach the starting gate.");
            }
        }

        public sealed class SessionTimeProvider : TimeProvider
        {
            private const int InitialYear = 2026;
            private const int InitialMonth = 11;
            private const int InitialDay = 26;
            private const int InitialHour = 12;
            private const int InitialMinute = 0;
            private const int InitialSecond = 0;
            private static readonly DateTimeOffset initialUtc =
                new DateTimeOffset(InitialYear, InitialMonth, InitialDay, InitialHour, InitialMinute, InitialSecond,
                    TimeSpan.Zero);
            private long timestamp;

            public override long TimestampFrequency => TimeSpan.TicksPerSecond;

            public override long GetTimestamp()
            {
                return Interlocked.Read(ref timestamp);
            }

            public override DateTimeOffset GetUtcNow()
            {
                return initialUtc.AddTicks(GetTimestamp());
            }

            public void Advance(TimeSpan elapsed)
            {
                Interlocked.Add(ref timestamp, elapsed.Ticks);
            }
        }
    }
}
