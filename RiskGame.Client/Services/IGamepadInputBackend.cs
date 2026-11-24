using System;
using System.Threading;

namespace RiskGame.Services
{
    public interface IGamepadInputBackend
    {
        void Run(Action<GamepadAction, int> onAction, CancellationToken cancellationToken);
    }
}
