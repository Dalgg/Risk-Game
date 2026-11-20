using System;
using System.Collections.Generic;
using System.Linq;

namespace RiskGame.Server.Services
{
    public static class ProfileCustomizationRules
    {
        public const string DefaultAvatarReference = "default";
        public const string ExplorerAvatarReference = "explorer";
        public const string TowerAvatarReference = "tower";
        public const string CrownAvatarReference = "crown";
        public const string DiceAvatarReference = "dice";
        private const int MinimumNicknameLength = 1;
        private const int MaximumNicknameLength = 50;
        private static readonly HashSet<string> AllowedAvatarReferences = new HashSet<string>(StringComparer.Ordinal)
        {
            DefaultAvatarReference,
            ExplorerAvatarReference,
            TowerAvatarReference,
            CrownAvatarReference,
            DiceAvatarReference
        };

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
            if (!AllowedAvatarReferences.Contains(avatarReference))
            {
                throw new ArgumentException("The avatar must be selected from the profile catalog.");
            }
            return avatarReference;
        }
    }
}
