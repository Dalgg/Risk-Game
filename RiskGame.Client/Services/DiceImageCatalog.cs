using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace RiskGame.Services
{
    public static class DiceImageCatalog
    {
        public const int MinimumFace = 1;
        public const int MaximumFace = 6;
        private const int FaceTwo = 2;
        private const int FaceThree = 3;
        private const int FaceFour = 4;
        private const int FaceFive = 5;
        private const string Face1Key = "DiceFace1Image";
        private const string Face2Key = "DiceFace2Image";
        private const string Face3Key = "DiceFace3Image";
        private const string Face4Key = "DiceFace4Image";
        private const string Face5Key = "DiceFace5Image";
        private const string Face6Key = "DiceFace6Image";

        public static bool IsValidFace(int value)
        {
            return value >= MinimumFace && value <= MaximumFace;
        }

        public static IImage GetFaceImage(int value)
        {
            string resourceKey = ResolveResourceKey(value);
            if (Application.Current != null && Application.Current.FindResource(resourceKey) is IImage image)
            {
                return image;
            }
            throw new InvalidOperationException("The dice face resources are not available.");
        }

        private static string ResolveResourceKey(int value)
        {
            switch (value)
            {
                case MinimumFace:
                    return Face1Key;
                case FaceTwo:
                    return Face2Key;
                case FaceThree:
                    return Face3Key;
                case FaceFour:
                    return Face4Key;
                case FaceFive:
                    return Face5Key;
                case MaximumFace:
                    return Face6Key;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value));
            }
        }
    }
}
