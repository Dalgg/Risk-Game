using System;
using System.Threading.Tasks;
using Grpc.Core;
using RiskGame.Contracts.Auth;
using RiskGame.Exceptions;
using RiskGame.Models;

namespace RiskGame.Server.Services
{
    public class AuthGrpcService : AuthGrpc.AuthGrpcBase
    {
        private readonly AuthRepositoryService authRepository;

        public AuthGrpcService(AuthRepositoryService authRepository)
        {
            this.authRepository = authRepository;
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
                throw new RpcException(new Status(StatusCode.Unavailable, dataOperationException.Message));
            }
        }

        public override Task<LoginResponse> Login(LoginRequest loginRequest, ServerCallContext callContext)
        {
            try
            {
                User? authenticatedUser = authRepository.AuthenticateUser(loginRequest.Username, loginRequest.Password);
                if (authenticatedUser == null)
                {
                    LoginResponse failedLoginResponse = new LoginResponse
                    {
                        IsAuthenticated = false
                    };
                    return Task.FromResult(failedLoginResponse);
                }

                string avatarReference = string.Empty;
                if (authenticatedUser.AvatarReference != null)
                {
                    avatarReference = authenticatedUser.AvatarReference;
                }

                LoginResponse successLoginResponse = new LoginResponse
                {
                    IsAuthenticated = true,
                    SessionToken = Guid.NewGuid().ToString("N"),
                    Player = new PlayerDto
                    {
                        UserId = authenticatedUser.IdUser,
                        Username = authenticatedUser.Username,
                        Nickname = authenticatedUser.Nickname,
                        AvatarReference = avatarReference
                    }
                };

                return Task.FromResult(successLoginResponse);
            }
            catch (DataOperationException dataOperationException)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, dataOperationException.Message));
            }
        }
    }
}