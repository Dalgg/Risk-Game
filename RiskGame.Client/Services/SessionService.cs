using System;
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
        private static readonly object sessionLock = new object();
        private static SessionUser? currentUser;
        private static AccountSessionConnection? accountConnection;

        public static event EventHandler? SessionExpired;

        public static SessionUser? CurrentUser
        {
            get
            {
                lock (sessionLock)
                {
                    return currentUser;
                }
            }
        }

        public static bool IsLoggedIn => CurrentUser != null;

        public static void Login(SessionUser user)
        {
            lock (sessionLock)
            {
                StopConnection();
                currentUser = user;
            }
        }

        public static void StartSessionConnection()
        {
            lock (sessionLock)
            {
                SessionUser? user = currentUser;
                bool isRegisteredUser = user != null && !user.IsGuest;
                bool hasSessionToken = isRegisteredUser && !string.IsNullOrWhiteSpace(user!.SessionToken);
                if (accountConnection == null && hasSessionToken)
                {
                    accountConnection = new AccountSessionConnection(user!, ExpireSession);
                    accountConnection.Start();
                }
            }
        }

        public static bool UpdateProfile(SessionUser expectedSession, UserProfile profile)
        {
            bool updated = false;
            lock (sessionLock)
            {
                SessionUser? session = currentUser;
                if (session != null && session.IdUser == profile.UserId && IsCurrentSession(expectedSession))
                {
                    currentUser = new SessionUser(session.IdUser, session.Username, profile.Nickname)
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
            lock (sessionLock)
            {
                if (currentUser == null)
                {
                    int guestNumber = RandomNumberGenerator.GetInt32(BottomUserIdRange, TopUserIdRange);
                    string guestAlias = $"{GuestPrefix}{guestNumber}";
                    currentUser = new SessionUser(-guestNumber, guestAlias, guestAlias);
                }
                return currentUser;
            }
        }

        public static void Logout()
        {
            SessionUser? currentSessionUser;
            lock (sessionLock)
            {
                currentSessionUser = currentUser;
                currentUser = null;
                StopConnection();
            }
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

        private static void StopConnection()
        {
            if (accountConnection != null)
            {
                accountConnection.Stop();
                accountConnection = null;
            }
        }

        private static bool IsCurrentSession(SessionUser expectedSession)
        {
            bool sameSession = false;
            if (currentUser != null)
            {
                sameSession = currentUser.IdUser == expectedSession.IdUser
                    && currentUser.SessionToken == expectedSession.SessionToken;
            }
            return sameSession;
        }

        private static void ExpireSession(AccountSessionConnection expectedConnection)
        {
            bool expired = false;
            lock (sessionLock)
            {
                if (ReferenceEquals(accountConnection, expectedConnection)
                    && IsCurrentSession(expectedConnection.Session))
                {
                    currentUser = null;
                    StopConnection();
                    expired = true;
                }
            }
            EventHandler? handler = SessionExpired;
            if (expired && handler != null)
            {
                handler(null, EventArgs.Empty);
            }
        }
    }
}
