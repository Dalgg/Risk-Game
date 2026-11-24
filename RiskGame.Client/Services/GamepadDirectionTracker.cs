using System;
using System.Threading;

namespace RiskGame.Services
{
    internal struct GamepadDirectionState
    {
        public int StickHorizontal;
        public int StickVertical;
        public int DpadHorizontal;
        public int DpadVertical;
        public int ControllerIndex;
        public uint PreviousWinmmButtons;
        public ushort PreviousXInputButtons;
    }

    internal static class GamepadDirectionTracker
    {
        public const int MaximumControllers = 4;
        public const int FirstControllerIndex = 0;
        public const short AxisThreshold = 16000;
        public const int ReconnectDelayMilliseconds = 1000;
        public const int DirectionNegative = -1;
        public const int DirectionNeutral = 0;
        public const int DirectionPositive = 1;

        public static void UpdateHorizontalState(int currentDirection, ref int previousDirection,
            int controllerIndex, Action<GamepadAction, int> onAction)
        {
            if (currentDirection != previousDirection)
            {
                previousDirection = currentDirection;
                DispatchHorizontalAction(currentDirection, controllerIndex, onAction);
            }
        }

        public static void UpdateVerticalState(int currentDirection, ref int previousDirection,
            int controllerIndex, Action<GamepadAction, int> onAction)
        {
            if (currentDirection != previousDirection)
            {
                previousDirection = currentDirection;
                DispatchVerticalAction(currentDirection, controllerIndex, onAction);
            }
        }

        public static int GetAxisDirection(short axisValue, short threshold)
        {
            if (axisValue < -threshold)
            {
                return DirectionNegative;
            }
            if (axisValue > threshold)
            {
                return DirectionPositive;
            }
            return DirectionNeutral;
        }

        public static int GetInvertedAxisDirection(short axisValue, short threshold)
        {
            if (axisValue > threshold)
            {
                return DirectionNegative;
            }
            if (axisValue < -threshold)
            {
                return DirectionPositive;
            }
            return DirectionNeutral;
        }

        public static void WaitBeforeRetry(int delayMilliseconds, CancellationToken cancellationToken)
        {
            cancellationToken.WaitHandle.WaitOne(delayMilliseconds);
        }

        private static void DispatchHorizontalAction(int direction, int controllerIndex,
            Action<GamepadAction, int> onAction)
        {
            if (direction == DirectionNegative)
            {
                onAction(GamepadAction.NavigateLeft, controllerIndex);
            }
            else if (direction == DirectionPositive)
            {
                onAction(GamepadAction.NavigateRight, controllerIndex);
            }
        }

        private static void DispatchVerticalAction(int direction, int controllerIndex,
            Action<GamepadAction, int> onAction)
        {
            if (direction == DirectionNegative)
            {
                onAction(GamepadAction.NavigateUp, controllerIndex);
            }
            else if (direction == DirectionPositive)
            {
                onAction(GamepadAction.NavigateDown, controllerIndex);
            }
        }
    }
}
