using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using RiskGame.Exceptions;
using RiskGame.Models;
using RiskGame.Services;

namespace RiskGame.Server.Services
{
    public sealed class PasswordResetManager
    {
        private const int MinimumCode = 100000;
        private const int ExclusiveMaximumCode = 1000000;
        private const int ResetIdentifierBytes = 32;
        private const int ResetLifetimeMinutes = 5;
        private const int ResendCooldownSeconds = 60;
        private const int MaximumAttempts = 5;
        private const int MaximumMessages = 3;
        private const int MaximumPendingResets = 1000;
        private static readonly TimeSpan ResetLifetime = TimeSpan.FromMinutes(ResetLifetimeMinutes);
        private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(ResendCooldownSeconds);
        private readonly object resetLock = new object();
        private readonly Dictionary<string, PendingPasswordReset> pendingResets =
            new Dictionary<string, PendingPasswordReset>();
        private readonly AuthRepositoryService authRepository;
        private readonly IEmailDeliveryService emailService;
        private readonly TimeProvider timeProvider;

        public PasswordResetManager(
            AuthRepositoryService authRepository,
            IEmailDeliveryService emailService,
            TimeProvider timeProvider)
        {
            this.authRepository = authRepository;
            this.emailService = emailService;
            this.timeProvider = timeProvider;
        }

        public string BeginReset(string email)
        {
            string normalizedEmail = email.Trim();
            if (!authRepository.IsValidEmail(normalizedEmail))
            {
                throw new ArgumentException("The email format is invalid.");
            }
            User user = authRepository.FindUserByEmail(normalizedEmail);
            lock (resetLock)
            {
                RemoveExpiredResets();
                EnsureResetAvailable(user.Email);
                PendingPasswordReset pendingReset = new PendingPasswordReset
                {
                    UserId = user.IdUser,
                    Email = user.Email,
                    CreatedTimestamp = timeProvider.GetTimestamp()
                };
                DeliverCode(pendingReset);
                string resetId = Convert.ToHexString(RandomNumberGenerator.GetBytes(ResetIdentifierBytes));
                pendingResets.Add(resetId, pendingReset);
                return resetId;
            }
        }

        public void ConfirmReset(string resetId, string code, string newPassword)
        {
            if (!PasswordPolicy.PasswordIsValid(newPassword))
            {
                throw new ArgumentException("The password does not meet security requirements.");
            }
            lock (resetLock)
            {
                PendingPasswordReset pendingReset = GetPendingReset(resetId);
                byte[] receivedHash = SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim()));
                if (!CryptographicOperations.FixedTimeEquals(pendingReset.CodeHash, receivedHash))
                {
                    pendingReset.FailedAttempts++;
                    throw new RegistrationVerificationException("The password reset code is invalid.");
                }
                authRepository.UpdatePasswordHash(pendingReset.UserId, PasswordHasher.Hash(newPassword));
                pendingResets.Remove(resetId);
            }
        }

        public void ResendReset(string resetId)
        {
            lock (resetLock)
            {
                PendingPasswordReset pendingReset = GetPendingReset(resetId);
                bool cooldownActive = timeProvider.GetElapsedTime(pendingReset.LastSentTimestamp) < ResendCooldown;
                if (cooldownActive || pendingReset.SentMessages >= MaximumMessages)
                {
                    throw new RegistrationVerificationException("The password reset resend limit was reached.");
                }
                DeliverCode(pendingReset);
            }
        }

        private PendingPasswordReset GetPendingReset(string resetId)
        {
            if (pendingResets.ContainsKey(resetId))
            {
                PendingPasswordReset pendingReset = pendingResets[resetId];
                bool isActive = timeProvider.GetElapsedTime(pendingReset.CreatedTimestamp) < ResetLifetime;
                if (isActive && pendingReset.FailedAttempts < MaximumAttempts)
                {
                    return pendingReset;
                }
            }
            throw new RegistrationVerificationException("The password reset expired or its attempts were exhausted.");
        }

        private void EnsureResetAvailable(string email)
        {
            if (pendingResets.Count >= MaximumPendingResets)
            {
                throw new RegistrationVerificationException("The pending password reset limit was reached.");
            }
            foreach (PendingPasswordReset pendingReset in pendingResets.Values)
            {
                if (string.Equals(pendingReset.Email, email, StringComparison.OrdinalIgnoreCase))
                {
                    throw new RegistrationVerificationException("This email already has a pending password reset.");
                }
            }
        }

        private void DeliverCode(PendingPasswordReset pendingReset)
        {
            string code = RandomNumberGenerator.GetInt32(MinimumCode, ExclusiveMaximumCode)
                .ToString(CultureInfo.InvariantCulture);
            emailService.SendPasswordReset(pendingReset.Email, code);
            pendingReset.CodeHash = SHA256.HashData(Encoding.UTF8.GetBytes(code));
            pendingReset.LastSentTimestamp = timeProvider.GetTimestamp();
            pendingReset.SentMessages++;
        }

        private void RemoveExpiredResets()
        {
            List<string> expiredIdentifiers = new List<string>();
            foreach (KeyValuePair<string, PendingPasswordReset> entry in pendingResets)
            {
                if (timeProvider.GetElapsedTime(entry.Value.CreatedTimestamp) >= ResetLifetime)
                {
                    expiredIdentifiers.Add(entry.Key);
                }
            }
            foreach (string resetId in expiredIdentifiers)
            {
                pendingResets.Remove(resetId);
            }
        }
    }
}
