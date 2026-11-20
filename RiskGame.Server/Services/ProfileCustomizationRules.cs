using System;
using System.Linq;

namespace RiskGame.Server.Services
{
    public static class ProfileCustomizationRules
    {
        private const int MinimumNicknameLength = 1;
        private const int MaximumNicknameLength = 50;

        public static string NormalizeNickname(string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname))
            {
                throw new ArgumentException("A nickname is required.");
            }
            string normalized = nickname.Trim();
            bool invalidLength = normalized.Length < MinimumNicknameLength || normalized.Length > MaximumNicknameLength;
            if (invalidLength || normalized.Any(char.IsControl))
            {
                throw new ArgumentException("The nickname must have 1 to 50 characters without control characters.");
            }
            return normalized;
        }

        public static string ValidateAvatar(string avatarReference)
        {
            bool isKnownAvatar = avatarReference == "default" || avatarReference == "explorer";
            bool isOtherAvatar = avatarReference == "tower" || avatarReference == "crown";
            if (!isKnownAvatar && !isOtherAvatar && avatarReference != "dice")
            {
                throw new ArgumentException("The avatar must be selected from the profile catalog.");
            }
            return avatarReference;
        }
    }
}
