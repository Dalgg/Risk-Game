using System;
using System.Threading;
using System.Threading.Tasks;

namespace RiskGame.Services
{
    public enum GamepadAction
    {
        None = 0,
        NavigateUp,
        NavigateDown,
        NavigateLeft,
        NavigateRight,
        Confirm,
        Cancel,
        RollDice,
        SelectOption1,
        SelectOption2,
        SelectOption3
    }

    public sealed class GamepadActionEventArgs : EventArgs
    {
        public GamepadAction Action { get; }
        public int ControllerIndex { get; }

        public GamepadActionEventArgs(GamepadAction action, int controllerIndex = 0)
        {
            Action = action;
            ControllerIndex = controllerIndex;
        }
    }

    public static class GamepadService
    {
        private const int FirstControllerIndex = 0;

        private static readonly object SyncRoot = new object();
        private static CancellationTokenSource? pollingCancellation;

        public static event EventHandler<GamepadActionEventArgs>? ActionTriggered;

        public static void Initialize()
        {
            Initialize(GamepadBackendFactory.CreateDefault());
        }

        public static void Initialize(IGamepadInputBackend inputBackend)
        {
            if (inputBackend == null)
            {
                throw new ArgumentNullException(nameof(inputBackend));
            }

            lock (SyncRoot)
            {
                if (pollingCancellation == null)
                {
                    pollingCancellation = new CancellationTokenSource();
                    CancellationToken cancellationToken = pollingCancellation.Token;
                    Task.Run(() => inputBackend.Run(TriggerAction, cancellationToken), cancellationToken);
                }
            }
        }

        public static void TriggerAction(GamepadAction action, int controllerIndex = FirstControllerIndex)
        {
            EventHandler<GamepadActionEventArgs>? actionHandler = ActionTriggered;
            if (action != GamepadAction.None && actionHandler != null)
            {
                actionHandler(null, new GamepadActionEventArgs(action, controllerIndex));
            }
        }

        public static void Shutdown()
        {
            lock (SyncRoot)
            {
                if (pollingCancellation != null)
                {
                    pollingCancellation.Cancel();
                    pollingCancellation.Dispose();
                    pollingCancellation = null;
                }
            }
        }
    }
}
