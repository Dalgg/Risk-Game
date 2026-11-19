using System;
using System.IO;
using System.Runtime.InteropServices;
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
        private const int MaximumControllers = 4;
        private const int FirstControllerIndex = 0;
        private const int NoControllerIndex = -1;
        private const int LinuxPacketSize = 8;
        private const int PacketStartOffset = 0;
        private const int EventValueIndex = 4;
        private const int EventTypeIndex = 6;
        private const int EventNumberIndex = 7;
        private const byte LinuxButtonEvent = 0x01;
        private const byte LinuxAxisEvent = 0x02;
        private const byte LinuxInitFlag = 0x80;
        private const byte NoFlags = 0;
        private const short ButtonPressedValue = 1;
        private const short AxisThreshold = 16000;
        private const byte ButtonSouth = 0;
        private const byte ButtonEast = 1;
        private const byte ButtonWest = 2;
        private const byte ButtonNorth = 3;
        private const byte ButtonLeftShoulder = 4;
        private const byte ButtonRightShoulder = 5;
        private const byte ButtonSelect = 6;
        private const byte ButtonStart = 7;
        private const byte AxisLeftStickHorizontal = 0;
        private const byte AxisLeftStickVertical = 1;
        private const byte AxisDpadHorizontal = 6;
        private const byte AxisDpadVertical = 7;
        private const int PollingDelayMilliseconds = 25;
        private const int ReconnectDelayMilliseconds = 1000;
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
        private const int DirectionNegative = -1;
        private const int DirectionNeutral = 0;
        private const int DirectionPositive = 1;

        private static readonly object SyncRoot = new object();
        private static CancellationTokenSource? pollingCancellation;

        public static event EventHandler<GamepadActionEventArgs>? ActionTriggered;

        public static void Initialize()
        {
            lock (SyncRoot)
            {
                if (pollingCancellation == null)
                {
                    pollingCancellation = new CancellationTokenSource();
                    CancellationToken cancellationToken = pollingCancellation.Token;
                    Task.Run(() => RunHardwareLoop(cancellationToken));
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

        private static void RunHardwareLoop(CancellationToken cancellationToken)
        {
            if (OperatingSystem.IsLinux())
            {
                PollLinuxControllers(cancellationToken);
            }
            else if (OperatingSystem.IsWindows())
            {
                PollWindowsControllers(cancellationToken);
            }
        }

        private static void PollLinuxControllers(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int controllerIndex = FindLinuxControllerIndex();
                if (controllerIndex == NoControllerIndex)
                {
                    WaitBeforeRetry(cancellationToken, ReconnectDelayMilliseconds);
                }
                else
                {
                    string devicePath = $"/dev/input/js{controllerIndex}";
                    ReadLinuxJoystickStream(devicePath, controllerIndex, cancellationToken);
                }
            }
        }

        private static int FindLinuxControllerIndex()
        {
            int detectedIndex = NoControllerIndex;
            for (int candidateIndex = FirstControllerIndex; candidateIndex < MaximumControllers; candidateIndex++)
            {
                string candidatePath = $"/dev/input/js{candidateIndex}";
                if (detectedIndex == NoControllerIndex && File.Exists(candidatePath))
                {
                    detectedIndex = candidateIndex;
                }
            }

            return detectedIndex;
        }

        private static void ReadLinuxJoystickStream(string devicePath, int controllerIndex,
            CancellationToken cancellationToken)
        {
            byte[] packetBuffer = new byte[LinuxPacketSize];
            ControllerDirectionState directionState = new ControllerDirectionState
            {
                ControllerIndex = controllerIndex
            };

            try
            {
                using FileStream deviceStream = new FileStream(
                    devicePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                bool isStreamOpen = true;

                while (!cancellationToken.IsCancellationRequested && isStreamOpen)
                {
                    int bytesRead = deviceStream.Read(packetBuffer, PacketStartOffset, LinuxPacketSize);
                    if (bytesRead < LinuxPacketSize)
                    {
                        isStreamOpen = false;
                    }
                    else
                    {
                        ProcessLinuxPacket(packetBuffer, ref directionState);
                    }
                }
            }
            catch (IOException)
            {
                WaitBeforeRetry(cancellationToken, ReconnectDelayMilliseconds);
            }
            catch (UnauthorizedAccessException unauthorizedException)
            {
                JsonLogger.Error(unauthorizedException, "Could not access joystick device {0}.", devicePath);
                WaitBeforeRetry(cancellationToken, ReconnectDelayMilliseconds);
            }
        }

        private static void ProcessLinuxPacket(byte[] packetBuffer, ref ControllerDirectionState directionState)
        {
            byte rawEventType = packetBuffer[EventTypeIndex];
            if ((rawEventType & LinuxInitFlag) == NoFlags)
            {
                short eventValue = BitConverter.ToInt16(packetBuffer, EventValueIndex);
                byte eventNumber = packetBuffer[EventNumberIndex];
                byte cleanEventType = (byte)(rawEventType & ~LinuxInitFlag);

                if (cleanEventType == LinuxButtonEvent && eventValue == ButtonPressedValue)
                {
                    GamepadAction buttonAction = MapLinuxButton(eventNumber);
                    TriggerAction(buttonAction, directionState.ControllerIndex);
                }
                else if (cleanEventType == LinuxAxisEvent)
                {
                    ProcessLinuxAxis(eventNumber, eventValue, ref directionState);
                }
            }
        }

        private static GamepadAction MapLinuxButton(byte buttonNumber)
        {
            switch (buttonNumber)
            {
                case ButtonSouth:
                case ButtonStart:
                    return GamepadAction.Confirm;
                case ButtonEast:
                case ButtonSelect:
                    return GamepadAction.Cancel;
                case ButtonWest:
                    return GamepadAction.RollDice;
                case ButtonNorth:
                    return GamepadAction.SelectOption2;
                case ButtonLeftShoulder:
                    return GamepadAction.SelectOption1;
                case ButtonRightShoulder:
                    return GamepadAction.SelectOption3;
                default:
                    return GamepadAction.None;
            }
        }

        private static void ProcessLinuxAxis(byte axisNumber, short axisValue,
            ref ControllerDirectionState directionState)
        {
            if (axisNumber == AxisLeftStickHorizontal || axisNumber == AxisDpadHorizontal)
            {
                int currentHorizontal = GetAxisDirection(axisValue);
                if (currentHorizontal != directionState.Horizontal)
                {
                    directionState.Horizontal = currentHorizontal;
                    DispatchHorizontalAction(currentHorizontal, directionState.ControllerIndex);
                }
            }
            else if (axisNumber == AxisLeftStickVertical || axisNumber == AxisDpadVertical)
            {
                int currentVertical = GetAxisDirection(axisValue);
                if (currentVertical != directionState.Vertical)
                {
                    directionState.Vertical = currentVertical;
                    DispatchVerticalAction(currentVertical, directionState.ControllerIndex);
                }
            }
        }

        private static int GetAxisDirection(short axisValue)
        {
            if (axisValue < -AxisThreshold)
            {
                return DirectionNegative;
            }

            if (axisValue > AxisThreshold)
            {
                return DirectionPositive;
            }

            return DirectionNeutral;
        }

        private static void PollWindowsControllers(CancellationToken cancellationToken)
        {
            uint[] previousButtons = new uint[MaximumControllers];
            ControllerDirectionState[] directionStates = new ControllerDirectionState[MaximumControllers];

            while (!cancellationToken.IsCancellationRequested)
            {
                bool anyControllerConnected = false;

                for (uint controllerId = FirstControllerIndex; controllerId < MaximumControllers; controllerId++)
                {
                    WindowsJoyInfoEx joyInfo = new WindowsJoyInfoEx
                    {
                        Size = (uint)Marshal.SizeOf<WindowsJoyInfoEx>(),
                        Flags = WindowsReturnAllFlags
                    };

                    int controllerIndex = (int)controllerId;
                    directionStates[controllerIndex].ControllerIndex = controllerIndex;
                    uint result = joyGetPosEx(controllerId, ref joyInfo);
                    if (result != WindowsJoystickNoError)
                    {
                        previousButtons[controllerIndex] = NoButtonsPressedMask;
                        directionStates[controllerIndex].Horizontal = DirectionNeutral;
                        directionStates[controllerIndex].Vertical = DirectionNeutral;
                    }
                    else
                    {
                        anyControllerConnected = true;
                        uint newlyPressed = joyInfo.Buttons & ~previousButtons[controllerIndex];
                        previousButtons[controllerIndex] = joyInfo.Buttons;

                        DispatchWindowsButtons(newlyPressed, controllerIndex);
                        DispatchWindowsDirections(joyInfo, ref directionStates[controllerIndex]);
                    }
                }

                int delayMilliseconds = ReconnectDelayMilliseconds;
                if (anyControllerConnected)
                {
                    delayMilliseconds = PollingDelayMilliseconds;
                }

                WaitBeforeRetry(cancellationToken, delayMilliseconds);
            }
        }

        private static void DispatchWindowsButtons(uint newlyPressedMask, int controllerIndex)
        {
            if ((newlyPressedMask & (WindowsButtonOneMask | WindowsButtonEightMask)) != NoButtonsPressedMask)
            {
                TriggerAction(GamepadAction.Confirm, controllerIndex);
            }

            if ((newlyPressedMask & (WindowsButtonTwoMask | WindowsButtonSevenMask)) != NoButtonsPressedMask)
            {
                TriggerAction(GamepadAction.Cancel, controllerIndex);
            }

            if ((newlyPressedMask & WindowsButtonThreeMask) != NoButtonsPressedMask)
            {
                TriggerAction(GamepadAction.RollDice, controllerIndex);
            }

            if ((newlyPressedMask & WindowsButtonFourMask) != NoButtonsPressedMask)
            {
                TriggerAction(GamepadAction.SelectOption2, controllerIndex);
            }

            if ((newlyPressedMask & WindowsButtonFiveMask) != NoButtonsPressedMask)
            {
                TriggerAction(GamepadAction.SelectOption1, controllerIndex);
            }

            if ((newlyPressedMask & WindowsButtonSixMask) != NoButtonsPressedMask)
            {
                TriggerAction(GamepadAction.SelectOption3, controllerIndex);
            }
        }

        private static void DispatchWindowsDirections(WindowsJoyInfoEx joyInfo,
            ref ControllerDirectionState directionState)
        {
            int currentHorizontal = ResolveWindowsHorizontal(joyInfo);
            int currentVertical = ResolveWindowsVertical(joyInfo);

            if (currentHorizontal != directionState.Horizontal)
            {
                directionState.Horizontal = currentHorizontal;
                DispatchHorizontalAction(currentHorizontal, directionState.ControllerIndex);
            }

            if (currentVertical != directionState.Vertical)
            {
                directionState.Vertical = currentVertical;
                DispatchVerticalAction(currentVertical, directionState.ControllerIndex);
            }
        }

        private static int ResolveWindowsHorizontal(WindowsJoyInfoEx joyInfo)
        {
            if (joyInfo.Pov != WindowsPovCentered)
            {
                if (joyInfo.Pov == WindowsPovLeft)
                {
                    return DirectionNegative;
                }

                if (joyInfo.Pov == WindowsPovRight)
                {
                    return DirectionPositive;
                }

                return DirectionNeutral;
            }

            int horizontalOffset = (int)joyInfo.HorizontalPosition - (int)WindowsAxisCenter;
            if (horizontalOffset < -WindowsAxisDeadzone)
            {
                return DirectionNegative;
            }

            if (horizontalOffset > WindowsAxisDeadzone)
            {
                return DirectionPositive;
            }

            return DirectionNeutral;
        }

        private static int ResolveWindowsVertical(WindowsJoyInfoEx joyInfo)
        {
            if (joyInfo.Pov != WindowsPovCentered)
            {
                if (joyInfo.Pov == WindowsPovUp)
                {
                    return DirectionNegative;
                }

                if (joyInfo.Pov == WindowsPovDown)
                {
                    return DirectionPositive;
                }

                return DirectionNeutral;
            }

            int verticalOffset = (int)joyInfo.VerticalPosition - (int)WindowsAxisCenter;
            if (verticalOffset < -WindowsAxisDeadzone)
            {
                return DirectionNegative;
            }

            if (verticalOffset > WindowsAxisDeadzone)
            {
                return DirectionPositive;
            }

            return DirectionNeutral;
        }

        private static void DispatchHorizontalAction(int direction, int controllerIndex)
        {
            if (direction == DirectionNegative)
            {
                TriggerAction(GamepadAction.NavigateLeft, controllerIndex);
            }
            else if (direction == DirectionPositive)
            {
                TriggerAction(GamepadAction.NavigateRight, controllerIndex);
            }
        }

        private static void DispatchVerticalAction(int direction, int controllerIndex)
        {
            if (direction == DirectionNegative)
            {
                TriggerAction(GamepadAction.NavigateUp, controllerIndex);
            }
            else if (direction == DirectionPositive)
            {
                TriggerAction(GamepadAction.NavigateDown, controllerIndex);
            }
        }

        private static void WaitBeforeRetry(CancellationToken cancellationToken, int delayMilliseconds)
        {
            cancellationToken.WaitHandle.WaitOne(delayMilliseconds);
        }

        [DllImport("winmm.dll")]
        private static extern uint joyGetPosEx(uint joyId, ref WindowsJoyInfoEx joyInfo);

        private struct ControllerDirectionState
        {
            public int Horizontal;
            public int Vertical;
            public int ControllerIndex;
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
