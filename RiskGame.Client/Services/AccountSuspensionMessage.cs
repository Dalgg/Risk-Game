using System;
using RiskGame.Resources;

namespace RiskGame.Services
{
    public static class AccountSuspensionMessage
    {
        private const long NoSuspensionExpiration = 0;
        private static readonly long MaximumUnixMilliseconds = DateTimeOffset.MaxValue.ToUnixTimeMilliseconds();

        public static string Format(long bannedUntilUnixMilliseconds)
        {
            string message = Strings.AccountSuspendedMessage;
            if (bannedUntilUnixMilliseconds > NoSuspensionExpiration
                && bannedUntilUnixMilliseconds <= MaximumUnixMilliseconds)
            {
                DateTime localExpiration = DateTimeOffset.FromUnixTimeMilliseconds(bannedUntilUnixMilliseconds)
                    .UtcDateTime.ToLocalTime();
                message = string.Format(Strings.Culture, Strings.AccountSuspendedUntilFormat, localExpiration);
            }
            return message;
        }
    }
}
