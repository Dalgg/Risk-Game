using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using RiskGame.Contracts.Auth;
using RiskGame.Exceptions;
using RiskGame.Models;
using RiskGame.Services;

namespace RiskGame.Server.Services
{
    public class AuthGrpcService : AuthGrpc.AuthGrpcBase
    {
        private readonly AuthSessionManager authSessionManager;
        private readonly ProfileSessionRegistry profileSessions;
        private readonly LobbySessionManager lobbySessionManager;

        public AuthGrpcService(
            AuthSessionManager authSessionManager,
            ProfileSessionRegistry sessions,
            LobbySessionManager lobbySessionManager)
        {
            this.authSessionManager = authSessionManager;
            profileSessions = sessions;
            this.lobbySessionManager = lobbySessionManager;
        }

        public override Task<RegisterResponse> Register(RegisterRequest registerRequest, ServerCallContext callContext)
        {
            try
            {
                return Task.FromResult(authSessionManager.BeginRegistration(registerRequest));
            }
            catch (DataOperationException dataOperationException)
            {
                throw CreateRegistrationException(dataOperationException, "Registration failed during data access.");
            }
        }

        public override Task<LoginResponse> Login(LoginRequest loginRequest, ServerCallContext callContext)
        {
            try
            {
                return Task.FromResult(SignIn(loginRequest));
            }
            catch (DataOperationException dataOperationException)
            {
                return HandleLoginFailure(dataOperationException);
            }
        }

        private LoginResponse SignIn(LoginRequest request)
        {
            User user = authSessionManager.AuthenticateUser(request.Username, request.Password);
            LoginResponse response = new LoginResponse
            {
                IsAuthenticated = true,
                SessionToken = profileSessions.Register(user.IdUser),
                Player = CreatePlayer(user)
            };
            lobbySessionManager.BroadcastPresenceUpdate();
            return response;
        }

        private static PlayerDto CreatePlayer(User user)
        {
            string avatar = string.Empty;
            if (user.AvatarReference != null)
            {
                avatar = user.AvatarReference;
            }
            return new PlayerDto
            {
                UserId = user.IdUser, Username = user.Username, Nickname = user.Nickname, AvatarReference = avatar
            };
        }

        private Task<LoginResponse> HandleLoginFailure(DataOperationException exception)
        {
            if (exception is InvalidCredentialsException)
            {
                return Task.FromResult(new LoginResponse { IsAuthenticated = false });
            }
            throw CreateLoginFailure(exception);
        }

        private static RpcException CreateLoginFailure(DataOperationException exception)
        {
            StatusCode statusCode = StatusCode.FailedPrecondition;
            if (exception is not SessionAlreadyActiveException)
            {
                statusCode = StatusCode.Unavailable;
                JsonLogger.Error(exception, "Login failed during data access.");
            }
            return new RpcException(new Status(statusCode, exception.Message));
        }

        public override Task<Empty> Logout(LogoutRequest logoutRequest, ServerCallContext callContext)
        {
            profileSessions.RemoveSession(logoutRequest.SessionToken);
            lobbySessionManager.BroadcastPresenceUpdate();
            return Task.FromResult(new Empty());
        }

        public override Task SubscribeSession(SessionRequest request,
            IServerStreamWriter<SessionState> responseStream, ServerCallContext callContext)
        {
            try
            {
                SessionConnectionManager connections = new SessionConnectionManager(profileSessions);
                return connections.Subscribe(request, responseStream, callContext.CancellationToken);
            }
            catch (InvalidSessionException invalidSessionException)
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, invalidSessionException.Message));
            }
        }

        public override Task<RegisterResponse> ConfirmRegistration(
            RegistrationConfirmation confirmation, ServerCallContext callContext)
        {
            try
            {
                return Task.FromResult(authSessionManager.ConfirmRegistration(confirmation));
            }
            catch (DataOperationException dataOperationException)
            {
                throw CreateRegistrationException(dataOperationException, "Could not confirm the registration.");
            }
        }

        public override Task<Empty> ResendVerification(RegistrationReference reference, ServerCallContext callContext)
        {
            try
            {
                authSessionManager.ResendVerification(reference);
                return Task.FromResult(new Empty());
            }
            catch (DataOperationException dataOperationException)
            {
                throw CreateRegistrationException(dataOperationException, "Could not resend the verification email.");
            }
        }

        private RpcException CreateRegistrationException(DataOperationException dataOperationException,
            string logMessage)
        {
            StatusCode statusCode = StatusCode.Unavailable;
            if (dataOperationException is RegistrationVerificationException)
            {
                statusCode = StatusCode.FailedPrecondition;
            }
            else
            {
                JsonLogger.Error(dataOperationException, logMessage);
            }
            return new RpcException(new Status(statusCode, "Registration verification could not be completed."));
        }
    }
}
