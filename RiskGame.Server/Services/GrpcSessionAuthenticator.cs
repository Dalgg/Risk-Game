using System;
using Grpc.Core;
using RiskGame.Exceptions;

namespace RiskGame.Server.Services
{
    public sealed class GrpcSessionAuthenticator
    {
        private const string AuthorizationHeader = "authorization";
        private const string BearerPrefix = "Bearer ";
        private const int MinimumRegisteredIdentifier = 1;
        private readonly ProfileSessionRegistry sessionRegistry;

        public GrpcSessionAuthenticator(ProfileSessionRegistry sessionRegistry)
        {
            this.sessionRegistry = sessionRegistry;
        }

        public int GetRegisteredUserId(ServerCallContext callContext)
        {
            string token = ReadToken(callContext.RequestHeaders);
            int userId = sessionRegistry.GetUserId(token);
            if (userId < MinimumRegisteredIdentifier)
            {
                throw new GuestNotAllowedException("A registered account is required to access the scoreboard.");
            }
            return userId;
        }

        private string ReadToken(Metadata headers)
        {
            string token = string.Empty;
            foreach (Metadata.Entry entry in headers)
            {
                bool isAuthorization = entry.Key == AuthorizationHeader && !entry.IsBinary;
                if (isAuthorization)
                {
                    if (entry.Value.StartsWith(BearerPrefix, StringComparison.Ordinal))
                    {
                        token = entry.Value.Substring(BearerPrefix.Length);
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidSessionException("A bearer session token is required.");
            }
            return token;
        }
    }
}
