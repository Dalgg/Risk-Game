using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Markup.Xaml.Styling;
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
        public static IReadOnlyList<ProfileAvatarOption> CreateOptions()
        {
            return new List<ProfileAvatarOption>
            {
                new ProfileAvatarOption("default", Strings.ProfileAvatarDefault),
                new ProfileAvatarOption("explorer", Strings.ProfileAvatarExplorer),
                new ProfileAvatarOption("tower", Strings.ProfileAvatarTower),
                new ProfileAvatarOption("crown", Strings.ProfileAvatarCrown),
                new ProfileAvatarOption("dice", Strings.ProfileAvatarDice)
            };
        }

        public static IImage GetImage(string? reference)
        {
            string resourceKey = "ProfileDefaultImage";
            switch (reference)
            {
                case "explorer":
                    resourceKey = "ProfileExplorerImage";
                    break;
                case "tower":
                    resourceKey = "ProfileTowerImage";
                    break;
                case "crown":
                    resourceKey = "ProfileCrownImage";
                    break;
                case "dice":
                    resourceKey = "ProfileDiceImage";
                    break;
                default:
                    break;
            }
            if (Application.Current != null)
            {
                foreach (IResourceProvider provider in Application.Current.Resources.MergedDictionaries)
                {
                    IResourceDictionary? dictionary = provider as IResourceDictionary;
                    if (provider is ResourceInclude include)
                    {
                        dictionary = include.Loaded;
                    }
                    bool hasImage = dictionary != null && dictionary.ContainsKey(resourceKey);
                    if (hasImage && dictionary != null && dictionary[resourceKey] is IImage image)
                    {
                        return image;
                    }
                }
            }
            throw new InvalidOperationException("The profile avatar resources are not available.");
        }
    }
}
