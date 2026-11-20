using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using RiskGame.Resources;

namespace RiskGame.Services
{
    public sealed class ProfileAvatarOption
    {
        public ProfileAvatarOption(string reference, string label)
        {
            Reference = reference;
            Label = label;
        }

        public string Reference { get; }

        public string Label { get; }

        public IImage Image => ProfileAvatarCatalog.GetImage(Reference);
    }

    public static class ProfileAvatarCatalog
    {
        public const string DefaultAvatarReference = "default";
        public const string ExplorerAvatarReference = "explorer";
        public const string TowerAvatarReference = "tower";
        public const string CrownAvatarReference = "crown";
        public const string DiceAvatarReference = "dice";
        private const string DefaultImageResourceKey = "ProfileDefaultImage";
        private const string ExplorerImageResourceKey = "ProfileExplorerImage";
        private const string TowerImageResourceKey = "ProfileTowerImage";
        private const string CrownImageResourceKey = "ProfileCrownImage";
        private const string DiceImageResourceKey = "ProfileDiceImage";

        public static IReadOnlyList<ProfileAvatarOption> CreateOptions()
        {
            return new List<ProfileAvatarOption>
            {
                new ProfileAvatarOption(DefaultAvatarReference, Strings.ProfileAvatarDefault),
                new ProfileAvatarOption(ExplorerAvatarReference, Strings.ProfileAvatarExplorer),
                new ProfileAvatarOption(TowerAvatarReference, Strings.ProfileAvatarTower),
                new ProfileAvatarOption(CrownAvatarReference, Strings.ProfileAvatarCrown),
                new ProfileAvatarOption(DiceAvatarReference, Strings.ProfileAvatarDice)
            };
        }

        public static IImage GetImage(string? reference)
        {
            string resourceKey = ResolveResourceKey(reference);
            if (Application.Current != null)
            {
                foreach (IResourceProvider provider in Application.Current.Resources.MergedDictionaries)
                {
                    IResourceDictionary? dictionary = ResolveDictionary(provider);
                    IImage? foundImage = FindImage(dictionary, resourceKey);
                    if (foundImage != null)
                    {
                        return foundImage;
                    }
                }
            }
            throw new InvalidOperationException("The profile avatar resources are not available.");
        }

        private static string ResolveResourceKey(string? reference)
        {
            switch (reference)
            {
                case ExplorerAvatarReference:
                    return ExplorerImageResourceKey;
                case TowerAvatarReference:
                    return TowerImageResourceKey;
                case CrownAvatarReference:
                    return CrownImageResourceKey;
                case DiceAvatarReference:
                    return DiceImageResourceKey;
                default:
                    return DefaultImageResourceKey;
            }
        }

        private static IResourceDictionary? ResolveDictionary(IResourceProvider provider)
        {
            if (provider is ResourceInclude include)
            {
                return include.Loaded;
            }
            return provider as IResourceDictionary;
        }

        private static IImage? FindImage(IResourceDictionary? dictionary, string resourceKey)
        {
            IImage? matchedImage = null;
            if (dictionary != null)
            {
                matchedImage = ReadMaterializedImage(dictionary, resourceKey);
            }
            return matchedImage;
        }

        private static IImage? ReadMaterializedImage(IResourceDictionary dictionary, string resourceKey)
        {
            IImage? matchedImage = null;
            if (dictionary.Keys.Contains(resourceKey) && dictionary[resourceKey] is IImage image)
            {
                matchedImage = image;
            }
            return matchedImage;
        }
    }
}
