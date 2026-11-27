using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using RiskGame.Exceptions;

namespace RiskGame.Server.Services
{
    public sealed class ProfileSessionRegistry
    {
        private const int TokenSizeBytes = 32;
        private const int SessionLifetimeHours = 12;
        private const int ReconnectionGraceSeconds = 30;
        private const int MinimumRegisteredIdentifier = 1;
        private const int NoExpiredSessions = 0;
        private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(SessionLifetimeHours);
        private readonly object sessionLock = new object();
        private readonly TimeProvider timeProvider;
        private readonly Dictionary<string, RegisteredSession> sessionsByToken =
            new Dictionary<string, RegisteredSession>();
        private readonly Dictionary<int, DateTime> suspensionsByUserId = new Dictionary<int, DateTime>();

        public ProfileSessionRegistry() : this(TimeProvider.System)
        {
        }

        public ProfileSessionRegistry(TimeProvider timeProvider)
        {
            this.timeProvider = timeProvider;
        }

        public string Register(int userId)
        {
            lock (sessionLock)
            {
                ExpireSessions();
                if (suspensionsByUserId.ContainsKey(userId))
                {
                    throw new AccountSuspendedException(suspensionsByUserId[userId]);
                }
                foreach (KeyValuePair<string, RegisteredSession> session in sessionsByToken)
                {
                    if (session.Value.UserId == userId)
                    {
                        throw new SessionAlreadyActiveException("The account already has an active session.");
                    }
                }
                string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(TokenSizeBytes));
                RegisteredSession registeredSession = new RegisteredSession
                {
                    UserId = userId,
                    ExpiresAt = timeProvider.GetUtcNow().UtcDateTime.Add(SessionLifetime),
                    DisconnectedTimestamp = timeProvider.GetTimestamp()
                };
                sessionsByToken.Add(token, registeredSession);
                return token;
            }
        }

        public int GetUserId(string token)
        {
            lock (sessionLock)
            {
                if (sessionsByToken.ContainsKey(token))
                {
                    RegisteredSession registeredSession = sessionsByToken[token];
                    if (registeredSession.BannedUntil.HasValue
                        && registeredSession.BannedUntil.Value > timeProvider.GetUtcNow().UtcDateTime)
                    {
                        throw new AccountSuspendedException(registeredSession.BannedUntil.Value);
                    }
                    if (IsActive(registeredSession))
                    {
                        return registeredSession.UserId;
                    }
                }
            }

            throw new InvalidSessionException("A valid session is required.");
        }
    
        public bool IsUserOnline(int userId)
        {
            bool isOnline = false;
            lock (sessionLock)
            {
                foreach (KeyValuePair<string, RegisteredSession> session in sessionsByToken)
                {
                    if (session.Value.UserId == userId && IsActive(session.Value))
                    {
                        isOnline = true;
                    }
                }
            }

            return isOnline;
        }

        public void RemoveSession(string token)
        {
            if (!string.IsNullOrWhiteSpace(token))
            {
                lock (sessionLock)
                {
                    sessionsByToken.Remove(token);
                }
            }
        }

        public void ValidateUser(int userId, string token)
        {
            lock (sessionLock)
            {
                bool ownsSession = false;
                if (sessionsByToken.ContainsKey(token))
                {
                    ownsSession = sessionsByToken[token].UserId == userId;
                }
                if (userId < MinimumRegisteredIdentifier || !ownsSession)
                {
                    throw new InvalidSessionException("The session does not belong to the requested account.");
                }
                GetUserId(token);
            }
        }

        public void SuspendUser(int userId, DateTime bannedUntilUtc)
        {
            lock (sessionLock)
            {
                DateTime expiration = DateTime.SpecifyKind(bannedUntilUtc, DateTimeKind.Utc);
                suspensionsByUserId[userId] = expiration;
                foreach (RegisteredSession session in sessionsByToken.Values)
                {
                    if (session.UserId == userId)
                    {
                        session.BannedUntil = expiration;
                        session.DisconnectedTimestamp = timeProvider.GetTimestamp();
                    }
                }
            }
        }

        public bool IsSessionValid(int userId, string token)
        {
            lock (sessionLock)
            {
                return sessionsByToken.ContainsKey(token) && sessionsByToken[token].UserId == userId
                    && IsActive(sessionsByToken[token]);
            }
        }

        public void AttachSession(int userId, string token, string connectionId)
        {
            lock (sessionLock)
            {
                ValidateUser(userId, token);
                sessionsByToken[token].ConnectionId = connectionId;
            }
        }

        public void DetachSession(string token, string connectionId)
        {
            lock (sessionLock)
            {
                if (sessionsByToken.ContainsKey(token) && sessionsByToken[token].ConnectionId == connectionId)
                {
                    RegisteredSession session = sessionsByToken[token];
                    session.ConnectionId = string.Empty;
                    session.DisconnectedTimestamp = timeProvider.GetTimestamp();
                }
            }
        }

        public bool IsConnectionCurrent(string token, string connectionId)
        {
            lock (sessionLock)
            {
                return sessionsByToken.ContainsKey(token) && IsActive(sessionsByToken[token])
                    && sessionsByToken[token].ConnectionId == connectionId;
            }
        }

        public bool ExpireSessions()
        {
            lock (sessionLock)
            {
                List<string> expiredTokens = new List<string>();
                foreach (KeyValuePair<string, RegisteredSession> session in sessionsByToken)
                {
                    bool pendingSuspensionNotice = HasPendingSuspensionNotice(session.Value);
                    bool shouldRetainSession = IsActive(session.Value) || pendingSuspensionNotice;
                    if (!shouldRetainSession)
                    {
                        expiredTokens.Add(session.Key);
                    }
                }
                foreach (string token in expiredTokens)
                {
                    sessionsByToken.Remove(token);
                }
                RemoveExpiredSuspensions();
                return expiredTokens.Count > NoExpiredSessions;
            }
        }

        private bool HasPendingSuspensionNotice(RegisteredSession session)
        {
            bool hasConnection = !string.IsNullOrEmpty(session.ConnectionId);
            bool isWithinNoticeGrace = timeProvider.GetElapsedTime(session.DisconnectedTimestamp)
                < TimeSpan.FromSeconds(ReconnectionGraceSeconds);
            bool canDeliverNotice = hasConnection || isWithinNoticeGrace;
            return session.BannedUntil.HasValue && canDeliverNotice;
        }

        private bool IsActive(RegisteredSession session)
        {
            bool withinGrace = !string.IsNullOrEmpty(session.ConnectionId)
                || timeProvider.GetElapsedTime(session.DisconnectedTimestamp)
                    < TimeSpan.FromSeconds(ReconnectionGraceSeconds);
            bool isUnrestrictedSession = !session.BannedUntil.HasValue
                && session.ExpiresAt > timeProvider.GetUtcNow().UtcDateTime;
            return isUnrestrictedSession && withinGrace;
        }

        private void RemoveExpiredSuspensions()
        {
            List<int> expiredUsers = new List<int>();
            foreach (KeyValuePair<int, DateTime> suspension in suspensionsByUserId)
            {
                if (suspension.Value <= timeProvider.GetUtcNow().UtcDateTime)
                {
                    expiredUsers.Add(suspension.Key);
                }
            }
            foreach (int userId in expiredUsers)
            {
                suspensionsByUserId.Remove(userId);
            }
        }
    }
}
