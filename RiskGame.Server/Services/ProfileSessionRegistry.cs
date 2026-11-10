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
        private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(SessionLifetimeHours);
        private readonly object sessionLock = new object();
        private readonly Dictionary<string, RegisteredSession> sessionsByToken =
            new Dictionary<string, RegisteredSession>();

        public string Register(int userId)
        {
            string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(TokenSizeBytes));
            lock (sessionLock)
            {
                List<string> expiredTokens = new List<string>();
                foreach (KeyValuePair<string, RegisteredSession> session in sessionsByToken)
                {
                    if (session.Value.ExpiresAt <= DateTime.UtcNow)
                    {
                        expiredTokens.Add(session.Key);
                    }
                }

                foreach (string expiredToken in expiredTokens)
                {
                    sessionsByToken.Remove(expiredToken);
                }

                RegisteredSession registeredSession = new RegisteredSession
                {
                    UserId = userId,
                    ExpiresAt = DateTime.UtcNow.Add(SessionLifetime)
                };
                sessionsByToken.Add(token, registeredSession);
            }

            return token;
        }

        public int GetUserId(string token)
        {
            lock (sessionLock)
            {
                if (sessionsByToken.ContainsKey(token))
                {
                    RegisteredSession registeredSession = sessionsByToken[token];
                    if (registeredSession.ExpiresAt > DateTime.UtcNow)
                    {
                        return registeredSession.UserId;
                    }

                    sessionsByToken.Remove(token);
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
                    if (session.Value.UserId == userId && session.Value.ExpiresAt > DateTime.UtcNow)
                    {
                        isOnline = true;
                    }
                }
            }

            return isOnline;
        }
    }
}
