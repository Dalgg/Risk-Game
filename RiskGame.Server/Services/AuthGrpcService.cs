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
        private readonly AuthRepositoryService authRepository;
        private readonly ProfileSessionRegistry profileSessions;
        private readonly LobbySessionManager lobbySessionManager;

        public AuthGrpcService(
            AuthRepositoryService authRepository,
            ProfileSessionRegistry sessions,
            LobbySessionManager lobbySessionManager)
        {
            this.authRepository = authRepository;
            profileSessions = sessions;
            this.lobbySessionManager = lobbySessionManager;
        }

        public override Task<RegisterResponse> Register(RegisterRequest registerRequest, ServerCallContext callContext)
        {
            try
            {
                RegisterResult registrationResult = authRepository.RegisterUser(
                    registerRequest.Email,
                    registerRequest.Username,
                    registerRequest.Password);

                RegisterResponse registerResponse = new RegisterResponse
                {
                    ResultCode = (int)registrationResult,
                    Message = registrationResult.ToString()
                };

                return Task.FromResult(registerResponse);
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Registration failed during data access.");
                throw new RpcException(new Status(StatusCode.Unavailable, dataOperationException.Message));
            }
        }

        public override Task<LoginResponse> Login(LoginRequest loginRequest, ServerCallContext callContext)
        {
            try
            {
                User authenticatedUser = authRepository.AuthenticateUser(loginRequest.Username, loginRequest.Password);

                string avatarReference = string.Empty;
                if (authenticatedUser.AvatarReference != null)
                {
                    avatarReference = authenticatedUser.AvatarReference;
                }

                LoginResponse successLoginResponse = new LoginResponse
                {
                    IsAuthenticated = true,
                    SessionToken = profileSessions.Register(authenticatedUser.IdUser),
                    Player = new PlayerDto
                    {
                        UserId = authenticatedUser.IdUser,
                        Username = authenticatedUser.Username,
                        Nickname = authenticatedUser.Nickname,
                        AvatarReference = avatarReference
                    }
                };

                lobbySessionManager.BroadcastPresenceUpdate();
                return Task.FromResult(successLoginResponse);
            }
            catch (DataOperationException dataOperationException)
            {
                JsonLogger.Error(dataOperationException, "Login failed during data access.");
                if (dataOperationException is InvalidCredentialsException)
                {
                    return Task.FromResult(new LoginResponse { IsAuthenticated = false });
                }
                throw new RpcException(new Status(StatusCode.Unavailable, dataOperationException.Message));
            }
        }

        public override Task<Empty> Logout(LogoutRequest logoutRequest, ServerCallContext callContext)
        {
            profileSessions.RemoveSession(logoutRequest.SessionToken);
            lobbySessionManager.BroadcastPresenceUpdate();
            return Task.FromResult(new Empty());
        }
    }
}
