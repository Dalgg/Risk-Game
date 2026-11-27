using System;
using RiskGame.Server.Tests.Builders;
using Xunit;

namespace RiskGame.Server.Tests
{
    public sealed class SuspendedPasswordResetTests
    {
        private const int SuspensionHours = 24;
        private const int OneResetEmail = 1;

        [Fact]
        public void BeginReset_SuspendedAccount_StillDeliversRecoveryCode()
        {
            using PasswordManagementBuilder setup = new PasswordManagementBuilder();
            DateTime bannedUntilUtc = setup.Time.GetUtcNow().AddHours(SuspensionHours).UtcDateTime;
            setup.SetBannedUntil(bannedUntilUtc);

            setup.ResetManager.BeginReset(PasswordManagementBuilder.Email);

            Assert.Equal(OneResetEmail, setup.EmailDelivery.SentResetCount);
        }

        [Fact]
        public void ConfirmReset_SuspendedAccount_PreservesPersistedSuspension()
        {
            using PasswordManagementBuilder setup = new PasswordManagementBuilder();
            DateTime bannedUntilUtc = setup.Time.GetUtcNow().AddHours(SuspensionHours).UtcDateTime;
            setup.SetBannedUntil(bannedUntilUtc);
            string resetId = setup.ResetManager.BeginReset(PasswordManagementBuilder.Email);

            setup.ResetManager.ConfirmReset(resetId, setup.EmailDelivery.LatestResetCode,
                PasswordManagementBuilder.ReplacementPassword);

            Assert.Equal<DateTime?>(bannedUntilUtc, setup.ReadBannedUntil());
        }
    }
}
