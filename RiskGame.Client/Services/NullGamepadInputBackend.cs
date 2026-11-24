using System;
using System.Threading;

namespace RiskGame.Services
{
    public sealed class NullGamepadInputBackend : IGamepadInputBackend
    {
        public void Run(Action<GamepadAction, int> onAction, CancellationToken cancellationToken)
        {
            cancellationToken.WaitHandle.WaitOne();
        }
    }
}
