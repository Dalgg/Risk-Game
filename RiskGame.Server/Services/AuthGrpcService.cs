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
                User user = authSessionManager.AuthenticateUser(loginRequest.Username, loginRequest.Password);

                string avatarReference = string.Empty;
                if (user.AvatarReference != null)
                {
                    avatarReference = user.AvatarReference;
                }

                LoginResponse successLoginResponse = new LoginResponse
                {
                    IsAuthenticated = true,
                    SessionToken = profileSessions.Register(user.IdUser),
                    Player = new PlayerDto
                    {
                        UserId = user.IdUser,
                        Username = user.Username,
                        Nickname = user.Nickname,
                        AvatarReference = avatarReference
                    }
                };

                lobbySessionManager.BroadcastPresenceUpdate();
                return Task.FromResult(successLoginResponse);
            }
            catch (DataOperationException dataOperationException)
            {
                if (dataOperationException is InvalidCredentialsException)
                {
                    return Task.FromResult(new LoginResponse { IsAuthenticated = false });
                }
                JsonLogger.Error(dataOperationException, "Login failed during data access.");
                throw new RpcException(new Status(StatusCode.Unavailable, dataOperationException.Message));
            }
        }

        public override Task<Empty> Logout(LogoutRequest logoutRequest, ServerCallContext callContext)
        {
            profileSessions.RemoveSession(logoutRequest.SessionToken);
            lobbySessionManager.BroadcastPresenceUpdate();
            return Task.FromResult(new Empty());
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

        private RpcException CreateRegistrationException(DataOperationException dataOperationException, string logMessage)
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
