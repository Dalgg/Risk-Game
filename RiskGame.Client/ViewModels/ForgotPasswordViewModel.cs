using System;
using System.Text.RegularExpressions;
using Grpc.Core;
using Grpc.Net.Client;
using RiskGame.Contracts.Auth;
using RiskGame.Exceptions;
using RiskGame.Resources;
using RiskGame.Services;

namespace RiskGame.ViewModels
{
    public class ForgotPasswordViewModel : ViewModelBase
    {
        private const int RequestTimeoutSeconds = 20;
        private const int MaxEmailLength = 100;
        private const int RegexTimeoutMilliseconds = 250;
        private static readonly Regex EmailRegex = new Regex(
            @"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$",
            RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(RegexTimeoutMilliseconds));

        public string ResetId { get; private set; } = string.Empty;

        public static bool IsValidEmail(string email)
        {
            string normalizedEmail = email.Trim();
            bool isEmpty = normalizedEmail.Length == 0;
            bool isTooLong = normalizedEmail.Length > MaxEmailLength;
            if (isEmpty || isTooLong)
            {
                return false;
            }
            return EmailRegex.IsMatch(normalizedEmail);
        }

        public static string ValidateResetInputs(string code, string newPassword, string confirmPassword)
        {
            bool hasEmptyField = string.IsNullOrWhiteSpace(code)
                || string.IsNullOrWhiteSpace(newPassword);
            if (hasEmptyField || string.IsNullOrWhiteSpace(confirmPassword))
            {
                return Strings.ProfilePasswordEmptyError;
            }
            if (newPassword != confirmPassword)
            {
                return Strings.ProfilePasswordMismatchError;
            }
            if (!PasswordPolicy.PasswordIsValid(newPassword))
            {
                return Strings.ProfilePasswordPolicyError;
            }
            return string.Empty;
        }

        public void RequestPasswordReset(string email)
        {
            string normalizedEmail = email.Trim();
            if (!IsValidEmail(normalizedEmail))
            {
                throw new DataOperationException(Strings.ForgotPasswordEmailInvalid);
            }

            try
            {
                GrpcChannel channel = GrpcChannelProvider.GetChannel();
                AuthGrpc.AuthGrpcClient client = new AuthGrpc.AuthGrpcClient(channel);
                PasswordResetResponse response = client.RequestPasswordReset(
                    new PasswordResetRequest { Email = normalizedEmail },
                    deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds));

                if (string.IsNullOrWhiteSpace(response.ResetId))
                {
                    throw new DataOperationException(Strings.VerificationServerOutdated);
                }
                ResetId = response.ResetId;
            }
            catch (RpcException rpcException)
            {
                throw new DataOperationException(MapRequestResetError(rpcException.StatusCode), rpcException);
            }
        }

        public void ConfirmPasswordReset(string code, string newPassword, string confirmPassword)
        {
            string validationError = ValidateResetInputs(code, newPassword, confirmPassword);
            if (!string.IsNullOrEmpty(validationError))
            {
                throw new DataOperationException(validationError);
            }

            PasswordResetConfirmation confirmation = new PasswordResetConfirmation
            {
                ResetId = ResetId,
                Code = code.Trim(),
                NewPassword = newPassword
            };

            try
            {
                GrpcChannel channel = GrpcChannelProvider.GetChannel();
                AuthGrpc.AuthGrpcClient client = new AuthGrpc.AuthGrpcClient(channel);
                client.ConfirmPasswordReset(
                    confirmation,
                    deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds));
            }
            catch (RpcException rpcException)
            {
                throw new DataOperationException(MapConfirmResetError(rpcException.StatusCode), rpcException);
            }
        }

        public void ResendPasswordReset()
        {
            PasswordResetReference reference = new PasswordResetReference { ResetId = ResetId };
            try
            {
                GrpcChannel channel = GrpcChannelProvider.GetChannel();
                AuthGrpc.AuthGrpcClient client = new AuthGrpc.AuthGrpcClient(channel);
                client.ResendPasswordReset(
                    reference,
                    deadline: DateTime.UtcNow.AddSeconds(RequestTimeoutSeconds));
            }
            catch (RpcException rpcException)
            {
                throw new DataOperationException(Strings.VerificationResendError, rpcException);
            }
        }

        private static string MapRequestResetError(StatusCode statusCode)
        {
            if (statusCode == StatusCode.InvalidArgument)
            {
                return Strings.ForgotPasswordEmailInvalid;
            }
            if (statusCode == StatusCode.NotFound)
            {
                return Strings.ForgotPasswordEmailNotFound;
            }
            if (statusCode == StatusCode.FailedPrecondition)
            {
                return Strings.VerificationPendingError;
            }
            return Strings.VerificationEmailError;
        }

        private static string MapConfirmResetError(StatusCode statusCode)
        {
            if (statusCode == StatusCode.InvalidArgument)
            {
                return Strings.ProfilePasswordPolicyError;
            }
            if (statusCode == StatusCode.FailedPrecondition)
            {
                return Strings.VerificationCodeError;
            }
            return Strings.ForgotPasswordResetError;
        }
    }
}
