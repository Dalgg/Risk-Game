using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using RiskGame.Contracts.Auth;
using RiskGame.Exceptions;
using RiskGame.Models;
using RiskGame.Services;

namespace RiskGame.Server.Services
{
    public sealed class AuthSessionManager
    {
        private const int MinimumCode = 100000;
        private const int ExclusiveMaximumCode = 1000000;
        private const int RegistrationIdentifierBytes = 32;
        private const int VerificationLifetimeMinutes = 5;
        private const int ResendCooldownSeconds = 60;
        private const int MaximumAttempts = 5;
        private const int MaximumMessages = 3;
        private const int MaximumPendingAccounts = 1000;
        private readonly object registrationLock = new object();
        private readonly Dictionary<string, PendingRegistration> pendingRegistrations =
            new Dictionary<string, PendingRegistration>();
        private readonly AuthRepositoryService authRepository;
        private readonly EmailService emailService;

        public AuthSessionManager(AuthRepositoryService authRepository, EmailService emailService)
        {
            this.authRepository = authRepository;
            this.emailService = emailService;
        }

        public User AuthenticateUser(string username, string password)
        {
            return authRepository.AuthenticateUser(username, password);
        }

        public RegisterResponse BeginRegistration(RegisterRequest registerRequest)
        {
            RegisterResult validationResult = authRepository.ValidateRegistration(
                registerRequest.Email, registerRequest.Username, registerRequest.Password);
            RegisterResponse response = new RegisterResponse { ResultCode = (int)validationResult };
            if (validationResult != RegisterResult.Success)
            {
                return response;
            }
            lock (registrationLock)
            {
                RemoveExpiredRegistrations();
                EnsureRegistrationAvailable(registerRequest.Email.Trim());
                PendingRegistration pendingRegistration = new PendingRegistration
                {
                    Account = new User
                    {
                        Email = registerRequest.Email.Trim(), Username = registerRequest.Username.Trim(),
                        Nickname = registerRequest.Username.Trim(),
                        PasswordHash = PasswordHasher.Hash(registerRequest.Password)
                    },
                    ExpiresAt = DateTime.UtcNow.AddMinutes(VerificationLifetimeMinutes)
                };
                DeliverCode(pendingRegistration);
                response.RegistrationId = Convert.ToHexString(
                    RandomNumberGenerator.GetBytes(RegistrationIdentifierBytes));
                pendingRegistrations.Add(response.RegistrationId, pendingRegistration);
            }
            return response;
        }

        public RegisterResponse ConfirmRegistration(RegistrationConfirmation confirmation)
        {
            lock (registrationLock)
            {
                PendingRegistration registration = GetPendingRegistration(confirmation.RegistrationId);
                byte[] receivedHash = SHA256.HashData(Encoding.UTF8.GetBytes(confirmation.Code.Trim()));
                if (!CryptographicOperations.FixedTimeEquals(registration.CodeHash, receivedHash))
                {
                    registration.FailedAttempts++;
                    throw new RegistrationVerificationException("The verification code is invalid.");
                }
                registration.Account.RegistrationDate = DateTime.UtcNow;
                RegisterResult result = authRepository.CreateVerifiedUser(registration.Account);
                pendingRegistrations.Remove(confirmation.RegistrationId);
                return new RegisterResponse { ResultCode = (int)result };
            }
        }

        public void ResendVerification(RegistrationReference registrationReference)
        {
            lock (registrationLock)
            {
                PendingRegistration registration = GetPendingRegistration(registrationReference.RegistrationId);
                bool cooldownActive = registration.LastSentAt.AddSeconds(ResendCooldownSeconds) > DateTime.UtcNow;
                if (cooldownActive || registration.SentMessages >= MaximumMessages)
                {
                    throw new RegistrationVerificationException("The verification email resend limit was reached.");
                }
                DeliverCode(registration);
            }
        }

        private PendingRegistration GetPendingRegistration(string registrationId)
        {
            if (pendingRegistrations.ContainsKey(registrationId))
            {
                PendingRegistration registration = pendingRegistrations[registrationId];
                if (registration.ExpiresAt > DateTime.UtcNow && registration.FailedAttempts < MaximumAttempts)
                {
                    return registration;
                }
            }
            throw new RegistrationVerificationException("The verification expired or its attempts were exhausted.");
        }

        private void EnsureRegistrationAvailable(string email)
        {
            if (pendingRegistrations.Count >= MaximumPendingAccounts)
            {
                throw new RegistrationVerificationException("The pending registration limit was reached.");
            }
            foreach (PendingRegistration registration in pendingRegistrations.Values)
            {
                if (string.Equals(registration.Account.Email, email, StringComparison.OrdinalIgnoreCase))
                {
                    throw new RegistrationVerificationException("This email already has a pending verification.");
                }
            }
        }

        private void DeliverCode(PendingRegistration registration)
        {
            string code = RandomNumberGenerator.GetInt32(MinimumCode, ExclusiveMaximumCode)
                .ToString(CultureInfo.InvariantCulture);
            emailService.SendVerification(registration.Account.Email, code);
            registration.CodeHash = SHA256.HashData(Encoding.UTF8.GetBytes(code));
            registration.LastSentAt = DateTime.UtcNow;
            registration.SentMessages++;
        }

        private void RemoveExpiredRegistrations()
        {
            List<string> expiredIdentifiers = new List<string>();
            foreach (KeyValuePair<string, PendingRegistration> registration in pendingRegistrations)
            {
                if (registration.Value.ExpiresAt <= DateTime.UtcNow)
                {
                    expiredIdentifiers.Add(registration.Key);
                }
            }
            foreach (string registrationId in expiredIdentifiers)
            {
                pendingRegistrations.Remove(registrationId);
            }
        }
    }
}
