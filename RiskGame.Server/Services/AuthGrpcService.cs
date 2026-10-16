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
        private readonly AuthRepositoryService _authRepository;

        public AuthGrpcService(AuthRepositoryService authRepository)
        {
            _authRepository = authRepository;
        }

        public override Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
        {
            try
            {
                RegisterResult result = _authRepository.RegisterUser(
                    request.Email,
                    request.Username,
                    request.Password);

                RegisterResponse response = new RegisterResponse
                {
                    ResultCode = (int)result,
                    Message = result.ToString()
                };

                return Task.FromResult(response);
            }
            catch (DataOperationException ex)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, ex.Message));
            }
        }

        public override Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
        {
            try
            {
                User? user = _authRepository.AuthenticateUser(request.Username, request.Password);
                if (user == null)
                {
                    LoginResponse failedResponse = new LoginResponse
                    {
                        IsAuthenticated = false
                    };
                    return Task.FromResult(failedResponse);
                }

                LoginResponse successResponse = new LoginResponse
                {
                    IsAuthenticated = true,
                    SessionToken = Guid.NewGuid().ToString("N"),
                    Player = new PlayerDto
                    {
                        UserId = user.IdUser,
                        Username = user.Username,
                        Nickname = user.Nickname,
                        AvatarReference = user.AvatarReference ?? string.Empty
                    }
                };

                return Task.FromResult(successResponse);
            }
            catch (DataOperationException ex)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, ex.Message));
            }
        }
    }
}