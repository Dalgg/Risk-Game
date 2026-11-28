using System;

namespace RiskGame.Services
{
    public enum GamepadPlatform
    {
        Unsupported = 0,
        Linux,
        Windows,
        MacOs
    }

    public static class GamepadBackendFactory
    {
        public static IGamepadInputBackend CreateDefault()
        {
            return Create(DetectCurrentPlatform());
        }

        public static GamepadPlatform DetectCurrentPlatform()
        {
            GamepadPlatform platform = GamepadPlatform.Unsupported;
            if (OperatingSystem.IsLinux())
            {
                platform = GamepadPlatform.Linux;
            }
            else if (OperatingSystem.IsWindows())
            {
                platform = GamepadPlatform.Windows;
            }
            else if (OperatingSystem.IsMacOS())
            {
                platform = GamepadPlatform.MacOs;
            }
            return platform;
        }

        public static IGamepadInputBackend Create(GamepadPlatform platform)
        {
            IGamepadInputBackend backend;
            switch (platform)
            {
                case GamepadPlatform.Linux:
                    backend = new LinuxGamepadInputBackend();
                    break;
                case GamepadPlatform.Windows:
                    backend = new WindowsGamepadInputBackend();
                    break;
                case GamepadPlatform.MacOs:
                case GamepadPlatform.Unsupported:
                default:
                    backend = new NullGamepadInputBackend();
                    break;
            }
            return backend;
        }
    }
}
