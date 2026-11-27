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
    public sealed class AuthGrpcService : AuthGrpc.AuthGrpcBase
    {
        private readonly AuthSessionManager authSessionManager;
        private readonly SessionConnectionManager sessionConnectionManager;
        private readonly PasswordResetManager passwordResetManager;

        public AuthGrpcService(
            AuthSessionManager authSessionManager,
            SessionConnectionManager sessionConnectionManager,
            PasswordResetManager passwordResetManager)
        {
            this.authSessionManager = authSessionManager;
            this.sessionConnectionManager = sessionConnectionManager;
            this.passwordResetManager = passwordResetManager;
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
                SessionToken = sessionConnectionManager.Register(user.IdUser),
                Player = CreatePlayer(user)
            };
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
            LoginResponse response = new LoginResponse { IsAuthenticated = false };
            if (exception is AccountSuspendedException suspension)
            {
                response = CreateSuspendedLogin(suspension);
            }
            else if (exception is not InvalidCredentialsException)
            {
                throw CreateLoginFailure(exception);
            }
            return Task.FromResult(response);
        }

        private static LoginResponse CreateSuspendedLogin(AccountSuspendedException suspension)
        {
            return new LoginResponse
            {
                IsSuspended = true,
                BannedUntilUnixMilliseconds = new DateTimeOffset(suspension.BannedUntilUtc).ToUnixTimeMilliseconds()
            };
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
            sessionConnectionManager.Logout(logoutRequest.SessionToken);
            return Task.FromResult(new Empty());
        }

        public override Task SubscribeSession(SessionRequest request,
            IServerStreamWriter<SessionState> responseStream, ServerCallContext callContext)
        {
            try
            {
                AccountSessionIdentity identity = new AccountSessionIdentity(request.UserId, request.SessionToken);
                return sessionConnectionManager.Subscribe(identity, responseStream,
                    callContext.CancellationToken);
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

        public override Task<PasswordResetResponse> RequestPasswordReset(
            PasswordResetRequest request, ServerCallContext callContext)
        {
            try
            {
                string resetId = passwordResetManager.BeginReset(request.Email);
                return Task.FromResult(new PasswordResetResponse { ResetId = resetId });
            }
            catch (ArgumentException argumentException)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, argumentException.Message));
            }
            catch (ProfileNotFoundException profileNotFoundException)
            {
                throw new RpcException(new Status(StatusCode.NotFound, profileNotFoundException.Message));
            }
            catch (DataOperationException dataOperationException)
            {
                throw CreatePasswordResetException(dataOperationException, "Could not start the password reset.");
            }
        }

        public override Task<Empty> ConfirmPasswordReset(
            PasswordResetConfirmation confirmation, ServerCallContext callContext)
        {
            try
            {
                passwordResetManager.ConfirmReset(confirmation.ResetId, confirmation.Code, confirmation.NewPassword);
                return Task.FromResult(new Empty());
            }
            catch (ArgumentException argumentException)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, argumentException.Message));
            }
            catch (ProfileNotFoundException profileNotFoundException)
            {
                throw new RpcException(new Status(StatusCode.NotFound, profileNotFoundException.Message));
            }
            catch (DataOperationException dataOperationException)
            {
                throw CreatePasswordResetException(dataOperationException, "Could not confirm the password reset.");
            }
        }

        public override Task<Empty> ResendPasswordReset(
            PasswordResetReference reference, ServerCallContext callContext)
        {
            try
            {
                passwordResetManager.ResendReset(reference.ResetId);
                return Task.FromResult(new Empty());
            }
            catch (DataOperationException dataOperationException)
            {
                throw CreatePasswordResetException(dataOperationException, "Could not resend the password reset email.");
            }
        }

        private static RpcException CreatePasswordResetException(
            DataOperationException dataOperationException, string logMessage)
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
            return new RpcException(new Status(statusCode, "Password reset could not be completed."));
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
