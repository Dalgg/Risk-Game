using System.Security.Cryptography;
using Grpc.Core;
using Grpc.Net.Client;
using RiskGame.Contracts.Auth;
using RiskGame.Contracts.Profile;

namespace RiskGame.Services
{
    public static class SessionService
    {
        private const int BottomUserIdRange = 1000;
        private const int TopUserIdRange = 9999;
        private const string GuestPrefix = "Invitado_";
        private const int LogoutTimeoutSeconds = 3;

        public static SessionUser? CurrentUser { get; private set; }

        public static bool IsLoggedIn => CurrentUser != null;

        public static void Login(SessionUser user)
        {
            CurrentUser = user;
        }

        public static bool UpdateProfile(SessionUser expectedSession, UserProfile profile)
        {
            bool updated = false;
            SessionUser? session = CurrentUser;
            if (session != null && session.IdUser == profile.UserId)
            {
                bool sameSession = session.IdUser == expectedSession.IdUser
                    && session.SessionToken == expectedSession.SessionToken;
                if (sameSession)
                {
                    CurrentUser = new SessionUser(session.IdUser, session.Username, profile.Nickname)
                    {
                        SessionToken = session.SessionToken,
                        AvatarReference = profile.AvatarReference
                    };
                    updated = true;
                }
            }
            return updated;
        }

        public static SessionUser GetOrCreateCurrentUser()
        {
            if (CurrentUser == null)
            {
                int guestNumber = RandomNumberGenerator.GetInt32(BottomUserIdRange, TopUserIdRange);
                string guestAlias = $"{GuestPrefix}{guestNumber}";
                CurrentUser = new SessionUser(-guestNumber, guestAlias, guestAlias);
            }

            return CurrentUser;
        }

        public static void Logout()
        {
            SessionUser? currentSessionUser = CurrentUser;
            CurrentUser = null;
            if (currentSessionUser != null && !string.IsNullOrWhiteSpace(currentSessionUser.SessionToken))
            {
                try
                {
                    GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                    AuthGrpc.AuthGrpcClient authClient = new AuthGrpc.AuthGrpcClient(grpcChannel);
                    LogoutRequest logoutRequest = new LogoutRequest
                    {
                        SessionToken = currentSessionUser.SessionToken
                    };
                    authClient.Logout(logoutRequest,
                        deadline: System.DateTime.UtcNow.AddSeconds(LogoutTimeoutSeconds));
                }
                catch (RpcException rpcException)
                {
                    JsonLogger.Error(rpcException, "Could not notify the server during logout.");
                }
            }
        }
    }
}
