using System.Text.RegularExpressions;
using System;
using Grpc.Core;
using Grpc.Net.Client;
using RiskGame.Contracts.Auth;
using RiskGame.Exceptions;
using RiskGame.Models;
using RiskGame.Resources;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public class RegisterViewModel : ViewModelBase
    {
        private const int RegistrationTimeoutSeconds = 20;
        public string RegistrationId { get; private set; } = string.Empty;
        private const int MaxEmailLength = 100;
        private static readonly Regex EmailRegex = new Regex(@"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$",
            RegexOptions.Compiled);
        private static readonly Regex UsernameRegex = new Regex(@"^[A-Za-z0-9._\-]{3,50}$",
            RegexOptions.Compiled);

        public RegisterResult RegisterUser(string email, string username, string password)
        {
            string normalizedEmail = email.Trim();
            string normalizedUsername = username.Trim();

            if (normalizedEmail.Length == 0 || normalizedUsername.Length == 0 || password.Length == 0)
            {
                return RegisterResult.EmptyFields;
            }

            if (normalizedEmail.Length > MaxEmailLength || !EmailRegex.IsMatch(normalizedEmail))
            {
                return RegisterResult.InvalidEmail;
            }

            if (!UsernameRegex.IsMatch(normalizedUsername))
            {
                return RegisterResult.InvalidUsername;
            }

            if (!PasswordPolicy.PasswordIsValid(password))
            {
                return RegisterResult.WeakPassword;
            }

            try
            {
                GrpcChannel channel = GrpcChannelProvider.GetChannel();
                AuthGrpc.AuthGrpcClient client = new AuthGrpc.AuthGrpcClient(channel);
                RegisterResponse response = client.Register(new RegisterRequest
                {
                    Email = normalizedEmail,
                    Username = normalizedUsername,
                    Password = password
                }, deadline: DateTime.UtcNow.AddSeconds(RegistrationTimeoutSeconds));

                if (!Enum.IsDefined(typeof(RegisterResult), response.ResultCode))
                {
                    throw new DataOperationException(Strings.RegisterErrorConnectionFailed);
                }

                RegistrationId = response.RegistrationId;
                if ((RegisterResult)response.ResultCode == RegisterResult.Success
                    && string.IsNullOrWhiteSpace(RegistrationId))
                {
                    throw new DataOperationException(Strings.VerificationServerOutdated);
                }
                return (RegisterResult)response.ResultCode;
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "gRPC communication error (RpcException) while registering user {0}.", normalizedUsername);
                string errorMessage = Strings.VerificationEmailError;
                if (rpcException.StatusCode == StatusCode.FailedPrecondition)
                {
                    errorMessage = Strings.VerificationPendingError;
                }
                throw new DataOperationException(errorMessage, rpcException);
            }
        }

        public RegisterResult ConfirmRegistration(string code)
        {
            RegistrationConfirmation confirmation = new RegistrationConfirmation
            {
                RegistrationId = RegistrationId, Code = code.Trim()
            };
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                AuthGrpc.AuthGrpcClient client = new AuthGrpc.AuthGrpcClient(grpcChannel);
                RegisterResponse response = client.ConfirmRegistration(confirmation,
                    deadline: DateTime.UtcNow.AddSeconds(RegistrationTimeoutSeconds));
                if (!Enum.IsDefined(typeof(RegisterResult), response.ResultCode))
                {
                    throw new DataOperationException(Strings.RegisterErrorConnectionFailed);
                }
                return (RegisterResult)response.ResultCode;
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "Could not confirm the registration code.");
                throw new DataOperationException(Strings.VerificationCodeError, rpcException);
            }
        }

        public void ResendVerification()
        {
            RegistrationReference registrationReference = new RegistrationReference { RegistrationId = RegistrationId };
            try
            {
                GrpcChannel grpcChannel = GrpcChannelProvider.GetChannel();
                AuthGrpc.AuthGrpcClient client = new AuthGrpc.AuthGrpcClient(grpcChannel);
                client.ResendVerification(registrationReference,
                    deadline: DateTime.UtcNow.AddSeconds(RegistrationTimeoutSeconds));
            }
            catch (RpcException rpcException)
            {
                JsonLogger.Error(rpcException, "Could not resend the registration verification code.");
                throw new DataOperationException(Strings.VerificationResendError, rpcException);
            }
        }
    }
}
