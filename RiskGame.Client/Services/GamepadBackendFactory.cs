using System;

namespace RiskGame.Services
{
    public static class GamepadBackendFactory
    {
        public static IGamepadInputBackend CreateDefault()
        {
            return Create(OperatingSystem.IsLinux(), OperatingSystem.IsWindows());
        }

        public static IGamepadInputBackend Create(bool isLinux, bool isWindows)
        {
            if (isLinux)
            {
                return new LinuxGamepadInputBackend();
            }
            if (isWindows)
            {
                return new WindowsGamepadInputBackend();
            }
            return new NullGamepadInputBackend();
        }
    }
}
