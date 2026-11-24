using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace RiskGame.Services
{
    public sealed partial class WindowsGamepadInputBackend : IGamepadInputBackend
    {
        private const int PollingDelayMilliseconds = 25;
        private const byte NoFlags = 0;
        private const uint WindowsReturnAllFlags = 0x00FF;
        private const uint WindowsJoystickNoError = 0;
        private const uint NoButtonsPressedMask = 0;
        private const uint WindowsButtonOneMask = 0x0001;
        private const uint WindowsButtonTwoMask = 0x0002;
        private const uint WindowsButtonThreeMask = 0x0004;
        private const uint WindowsButtonFourMask = 0x0008;
        private const uint WindowsButtonFiveMask = 0x0010;
        private const uint WindowsButtonSixMask = 0x0020;
        private const uint WindowsButtonSevenMask = 0x0040;
        private const uint WindowsButtonEightMask = 0x0080;
        private const uint WindowsAxisCenter = 32767;
        private const int WindowsAxisDeadzone = 16000;
        private const uint WindowsPovCentered = 65535;
        private const uint WindowsPovUp = 0;
        private const uint WindowsPovRight = 9000;
        private const uint WindowsPovDown = 18000;
        private const uint WindowsPovLeft = 27000;
        private const uint WindowsXInputSuccess = 0;
        private const ushort XInputDpadUpMask = 0x0001;
        private const ushort XInputDpadDownMask = 0x0002;
        private const ushort XInputDpadLeftMask = 0x0004;
        private const ushort XInputDpadRightMask = 0x0008;
        private const ushort XInputStartMask = 0x0010;
        private const ushort XInputBackMask = 0x0020;
        private const ushort XInputLeftShoulderMask = 0x0100;
        private const ushort XInputRightShoulderMask = 0x0200;
        private const ushort XInputButtonAMask = 0x1000;
        private const ushort XInputButtonBMask = 0x2000;
        private const ushort XInputButtonXMask = 0x4000;
        private const ushort XInputButtonYMask = 0x8000;

        private bool isXInputAvailable = true;

        public void Run(Action<GamepadAction, int> onAction, CancellationToken cancellationToken)
        {
            GamepadDirectionState[] slotStates = new GamepadDirectionState[GamepadDirectionTracker.MaximumControllers];

            while (!cancellationToken.IsCancellationRequested)
            {
                bool anyControllerConnected = PollAllSlots(slotStates, onAction);
                int delayMilliseconds = GamepadDirectionTracker.ReconnectDelayMilliseconds;
                if (anyControllerConnected)
                {
                    delayMilliseconds = PollingDelayMilliseconds;
                }
                GamepadDirectionTracker.WaitBeforeRetry(delayMilliseconds, cancellationToken);
            }
        }

        private bool PollAllSlots(GamepadDirectionState[] slotStates, Action<GamepadAction, int> onAction)
        {
            bool anyControllerConnected = false;

            for (uint controllerId = GamepadDirectionTracker.FirstControllerIndex;
                controllerId < GamepadDirectionTracker.MaximumControllers; controllerId++)
            {
                int controllerIndex = (int)controllerId;
                slotStates[controllerIndex].ControllerIndex = controllerIndex;

                if (TryPollXInputSlot(controllerId, ref slotStates[controllerIndex], onAction))
                {
                    anyControllerConnected = true;
                    slotStates[controllerIndex].PreviousWinmmButtons = NoButtonsPressedMask;
                }
                else if (TryPollWinmmSlot(controllerId, ref slotStates[controllerIndex], onAction))
                {
                    anyControllerConnected = true;
                    slotStates[controllerIndex].PreviousXInputButtons = NoFlags;
                }
                else
                {
                    ResetSlotState(ref slotStates[controllerIndex]);
                }
            }

            return anyControllerConnected;
        }

        private bool TryPollXInputSlot(uint controllerId, ref GamepadDirectionState slotState,
            Action<GamepadAction, int> onAction)
        {
            if (!isXInputAvailable)
            {
                return false;
            }

            try
            {
                WindowsXInputState xinputState = new WindowsXInputState();
                uint status = QueryWindowsXInputState(controllerId, ref xinputState);
                if (status != WindowsXInputSuccess)
                {
                    return false;
                }

                ushort currentButtons = xinputState.Gamepad.Buttons;
                ushort newlyPressed = (ushort)(currentButtons & ~slotState.PreviousXInputButtons);
                slotState.PreviousXInputButtons = currentButtons;

                DispatchXInputButtons(newlyPressed, slotState.ControllerIndex, onAction);
                DispatchXInputDirections(xinputState.Gamepad, ref slotState, onAction);
                return true;
            }
            catch (DllNotFoundException)
            {
                isXInputAvailable = false;
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                isXInputAvailable = false;
                return false;
            }
        }

        private static bool TryPollWinmmSlot(uint controllerId, ref GamepadDirectionState slotState,
            Action<GamepadAction, int> onAction)
        {
            WindowsJoyInfoEx joyInfo = new WindowsJoyInfoEx
            {
                Size = (uint)Marshal.SizeOf<WindowsJoyInfoEx>(),
                Flags = WindowsReturnAllFlags
            };

            uint result = joyGetPosEx(controllerId, ref joyInfo);
            if (result != WindowsJoystickNoError)
            {
                return false;
            }

            uint newlyPressed = joyInfo.Buttons & ~slotState.PreviousWinmmButtons;
            slotState.PreviousWinmmButtons = joyInfo.Buttons;

            DispatchWinmmButtons(newlyPressed, slotState.ControllerIndex, onAction);
            DispatchWinmmDirections(joyInfo, ref slotState, onAction);
            return true;
        }

        private static void ResetSlotState(ref GamepadDirectionState slotState)
        {
            slotState.PreviousWinmmButtons = NoButtonsPressedMask;
            slotState.PreviousXInputButtons = NoFlags;
            slotState.StickHorizontal = GamepadDirectionTracker.DirectionNeutral;
            slotState.StickVertical = GamepadDirectionTracker.DirectionNeutral;
            slotState.DpadHorizontal = GamepadDirectionTracker.DirectionNeutral;
            slotState.DpadVertical = GamepadDirectionTracker.DirectionNeutral;
        }

        private static void DispatchXInputButtons(ushort newlyPressedMask, int controllerIndex,
            Action<GamepadAction, int> onAction)
        {
            if ((newlyPressedMask & (XInputButtonAMask | XInputStartMask)) != NoFlags)
            {
                onAction(GamepadAction.Confirm, controllerIndex);
            }
            if ((newlyPressedMask & (XInputButtonBMask | XInputBackMask)) != NoFlags)
            {
                onAction(GamepadAction.Cancel, controllerIndex);
            }
            if ((newlyPressedMask & XInputButtonXMask) != NoFlags)
            {
                onAction(GamepadAction.RollDice, controllerIndex);
            }
            if ((newlyPressedMask & XInputButtonYMask) != NoFlags)
            {
                onAction(GamepadAction.SelectOption2, controllerIndex);
            }
            if ((newlyPressedMask & XInputLeftShoulderMask) != NoFlags)
            {
                onAction(GamepadAction.SelectOption1, controllerIndex);
            }
            if ((newlyPressedMask & XInputRightShoulderMask) != NoFlags)
            {
                onAction(GamepadAction.SelectOption3, controllerIndex);
            }
        }

        private static void DispatchXInputDirections(WindowsXInputGamepad gamepad,
            ref GamepadDirectionState directionState, Action<GamepadAction, int> onAction)
        {
            int stickHorizontal = GamepadDirectionTracker.GetAxisDirection(
                gamepad.ThumbLeftX, GamepadDirectionTracker.AxisThreshold);
            int stickVertical = GamepadDirectionTracker.GetInvertedAxisDirection(
                gamepad.ThumbLeftY, GamepadDirectionTracker.AxisThreshold);
            int dpadHorizontal = ResolveXInputDpadHorizontal(gamepad.Buttons);
            int dpadVertical = ResolveXInputDpadVertical(gamepad.Buttons);

            GamepadDirectionTracker.UpdateHorizontalState(
                stickHorizontal, ref directionState.StickHorizontal, directionState.ControllerIndex, onAction);
            GamepadDirectionTracker.UpdateVerticalState(
                stickVertical, ref directionState.StickVertical, directionState.ControllerIndex, onAction);
            GamepadDirectionTracker.UpdateHorizontalState(
                dpadHorizontal, ref directionState.DpadHorizontal, directionState.ControllerIndex, onAction);
            GamepadDirectionTracker.UpdateVerticalState(
                dpadVertical, ref directionState.DpadVertical, directionState.ControllerIndex, onAction);
        }

        private static int ResolveXInputDpadHorizontal(ushort buttonsMask)
        {
            if ((buttonsMask & XInputDpadLeftMask) != NoFlags)
            {
                return GamepadDirectionTracker.DirectionNegative;
            }
            if ((buttonsMask & XInputDpadRightMask) != NoFlags)
            {
                return GamepadDirectionTracker.DirectionPositive;
            }
            return GamepadDirectionTracker.DirectionNeutral;
        }

        private static int ResolveXInputDpadVertical(ushort buttonsMask)
        {
            if ((buttonsMask & XInputDpadUpMask) != NoFlags)
            {
                return GamepadDirectionTracker.DirectionNegative;
            }
            if ((buttonsMask & XInputDpadDownMask) != NoFlags)
            {
                return GamepadDirectionTracker.DirectionPositive;
            }
            return GamepadDirectionTracker.DirectionNeutral;
        }

        private static void DispatchWinmmButtons(uint newlyPressedMask, int controllerIndex,
            Action<GamepadAction, int> onAction)
        {
            if ((newlyPressedMask & (WindowsButtonOneMask | WindowsButtonEightMask)) != NoButtonsPressedMask)
            {
                onAction(GamepadAction.Confirm, controllerIndex);
            }
            if ((newlyPressedMask & (WindowsButtonTwoMask | WindowsButtonSevenMask)) != NoButtonsPressedMask)
            {
                onAction(GamepadAction.Cancel, controllerIndex);
            }
            if ((newlyPressedMask & WindowsButtonThreeMask) != NoButtonsPressedMask)
            {
                onAction(GamepadAction.RollDice, controllerIndex);
            }
            if ((newlyPressedMask & WindowsButtonFourMask) != NoButtonsPressedMask)
            {
                onAction(GamepadAction.SelectOption2, controllerIndex);
            }
            if ((newlyPressedMask & WindowsButtonFiveMask) != NoButtonsPressedMask)
            {
                onAction(GamepadAction.SelectOption1, controllerIndex);
            }
            if ((newlyPressedMask & WindowsButtonSixMask) != NoButtonsPressedMask)
            {
                onAction(GamepadAction.SelectOption3, controllerIndex);
            }
        }

        private static void DispatchWinmmDirections(WindowsJoyInfoEx joyInfo,
            ref GamepadDirectionState directionState, Action<GamepadAction, int> onAction)
        {
            int currentHorizontal = ResolveWinmmHorizontal(joyInfo);
            int currentVertical = ResolveWinmmVertical(joyInfo);

            GamepadDirectionTracker.UpdateHorizontalState(
                currentHorizontal, ref directionState.StickHorizontal, directionState.ControllerIndex, onAction);
            GamepadDirectionTracker.UpdateVerticalState(
                currentVertical, ref directionState.StickVertical, directionState.ControllerIndex, onAction);
        }

        private static int ResolveWinmmHorizontal(WindowsJoyInfoEx joyInfo)
        {
            if (joyInfo.Pov != WindowsPovCentered)
            {
                if (joyInfo.Pov == WindowsPovLeft)
                {
                    return GamepadDirectionTracker.DirectionNegative;
                }
                if (joyInfo.Pov == WindowsPovRight)
                {
                    return GamepadDirectionTracker.DirectionPositive;
                }
                return GamepadDirectionTracker.DirectionNeutral;
            }

            int horizontalOffset = (int)joyInfo.HorizontalPosition - (int)WindowsAxisCenter;
            if (horizontalOffset < -WindowsAxisDeadzone)
            {
                return GamepadDirectionTracker.DirectionNegative;
            }
            if (horizontalOffset > WindowsAxisDeadzone)
            {
                return GamepadDirectionTracker.DirectionPositive;
            }
            return GamepadDirectionTracker.DirectionNeutral;
        }

        private static int ResolveWinmmVertical(WindowsJoyInfoEx joyInfo)
        {
            if (joyInfo.Pov != WindowsPovCentered)
            {
                if (joyInfo.Pov == WindowsPovUp)
                {
                    return GamepadDirectionTracker.DirectionNegative;
                }
                if (joyInfo.Pov == WindowsPovDown)
                {
                    return GamepadDirectionTracker.DirectionPositive;
                }
                return GamepadDirectionTracker.DirectionNeutral;
            }

            int verticalOffset = (int)joyInfo.VerticalPosition - (int)WindowsAxisCenter;
            if (verticalOffset < -WindowsAxisDeadzone)
            {
                return GamepadDirectionTracker.DirectionNegative;
            }
            if (verticalOffset > WindowsAxisDeadzone)
            {
                return GamepadDirectionTracker.DirectionPositive;
            }
            return GamepadDirectionTracker.DirectionNeutral;
        }

        [LibraryImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static partial uint QueryWindowsXInputState(uint userIndex, ref WindowsXInputState state);

        [LibraryImport("winmm.dll")]
        private static partial uint joyGetPosEx(uint joyId, ref WindowsJoyInfoEx joyInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowsXInputGamepad
        {
            public ushort Buttons;
            public byte LeftTrigger;
            public byte RightTrigger;
            public short ThumbLeftX;
            public short ThumbLeftY;
            public short ThumbRightX;
            public short ThumbRightY;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowsXInputState
        {
            public uint PacketNumber;
            public WindowsXInputGamepad Gamepad;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowsJoyInfoEx
        {
            public uint Size;
            public uint Flags;
            public uint HorizontalPosition;
            public uint VerticalPosition;
            public uint ZAxisPosition;
            public uint RudderPosition;
            public uint UAxisPosition;
            public uint VAxisPosition;
            public uint Buttons;
            public uint ButtonNumber;
            public uint Pov;
            public uint Reserved1;
            public uint Reserved2;
        }
    }
}
