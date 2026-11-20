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
        private const short HatThreshold = 0;
        private const int LinuxAxisMapLength = 64;
        private const int LinuxButtonMapLength = 512;
        private const ulong LinuxIoctlGetAxisMap = 0x80406A32UL;
        private const ulong LinuxIoctlGetButtonMap = 0x84006A34UL;
        private const int IoctlFailureResult = -1;
        private const byte LinuxAbsX = 0;
        private const byte LinuxAbsY = 1;
        private const byte LinuxAbsHat0X = 16;
        private const byte LinuxAbsHat0Y = 17;
        private const ushort LinuxKeyBack = 158;
        private const ushort LinuxBtnSouth = 304;
        private const ushort LinuxBtnEast = 305;
        private const ushort LinuxBtnNorth = 307;
        private const ushort LinuxBtnWest = 308;
        private const ushort LinuxBtnLeftShoulder = 310;
        private const ushort LinuxBtnRightShoulder = 311;
        private const ushort LinuxBtnSelect = 314;
        private const ushort LinuxBtnStart = 315;
        private const ushort LinuxBtnDpadUp = 544;
        private const ushort LinuxBtnDpadDown = 545;
        private const ushort LinuxBtnDpadLeft = 546;
        private const ushort LinuxBtnDpadRight = 547;
        private const byte FallbackButtonSouth = 0;
        private const byte FallbackButtonEast = 1;
        private const byte FallbackButtonWest = 2;
        private const byte FallbackButtonNorth = 3;
        private const byte FallbackButtonLeftShoulder = 4;
        private const byte FallbackButtonRightShoulder = 5;
        private const byte FallbackButtonSelect = 6;
        private const byte FallbackButtonStart = 7;
        private const byte FallbackAxisLeftStickHorizontal = 0;
        private const byte FallbackAxisLeftStickVertical = 1;
        private const byte FallbackAxisDpadHorizontal = 6;
        private const byte FallbackAxisDpadVertical = 7;
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
                    Task.Run(() => RunHardwareLoop(cancellationToken), cancellationToken);
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
            for (int slotIndex = FirstControllerIndex; slotIndex < MaximumControllers; slotIndex++)
            {
                int capturedSlot = slotIndex;
                Task.Run(() => RunLinuxControllerSlot(capturedSlot, cancellationToken), cancellationToken);
            }
        }

        private static void RunLinuxControllerSlot(int controllerIndex, CancellationToken cancellationToken)
        {
            string devicePath = $"/dev/input/js{controllerIndex}";
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!File.Exists(devicePath))
                {
                    WaitBeforeRetry(cancellationToken, ReconnectDelayMilliseconds);
                }
                else
                {
                    ReadLinuxJoystickStream(devicePath, controllerIndex, cancellationToken);
                }
            }
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
                LinuxJoystickLayout layout = ReadLinuxJoystickLayout(deviceStream);
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
                        ProcessLinuxPacket(packetBuffer, layout, ref directionState);
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

        private static LinuxJoystickLayout ReadLinuxJoystickLayout(FileStream deviceStream)
        {
            byte[] axisMap = new byte[LinuxAxisMapLength];
            ushort[] buttonMap = new ushort[LinuxButtonMapLength];
            int fileDescriptor = (int)deviceStream.SafeFileHandle.DangerousGetHandle();
            bool hasAxisMap = QueryLinuxAxisMap(fileDescriptor, LinuxIoctlGetAxisMap, axisMap) != IoctlFailureResult;
            bool hasButtonMap = QueryLinuxButtonMap(fileDescriptor, LinuxIoctlGetButtonMap, buttonMap)
                != IoctlFailureResult;

            return new LinuxJoystickLayout
            {
                AxisMap = axisMap,
                ButtonMap = buttonMap,
                HasAxisMap = hasAxisMap,
                HasButtonMap = hasButtonMap
            };
        }

        private static void ProcessLinuxPacket(byte[] packetBuffer, LinuxJoystickLayout layout,
            ref ControllerDirectionState directionState)
        {
            byte rawEventType = packetBuffer[EventTypeIndex];
            if ((rawEventType & LinuxInitFlag) == NoFlags)
            {
                short eventValue = BitConverter.ToInt16(packetBuffer, EventValueIndex);
                byte eventNumber = packetBuffer[EventNumberIndex];
                byte cleanEventType = (byte)(rawEventType & ~LinuxInitFlag);

                if (cleanEventType == LinuxButtonEvent && eventValue == ButtonPressedValue)
                {
                    GamepadAction buttonAction = ResolveLinuxButtonAction(eventNumber, layout);
                    TriggerAction(buttonAction, directionState.ControllerIndex);
                }
                else if (cleanEventType == LinuxAxisEvent)
                {
                    ProcessLinuxAxis(eventNumber, eventValue, layout, ref directionState);
                }
            }
        }

        private static GamepadAction ResolveLinuxButtonAction(byte buttonNumber, LinuxJoystickLayout layout)
        {
            if (layout.HasButtonMap && buttonNumber < layout.ButtonMap.Length)
            {
                return MapLinuxButtonCode(layout.ButtonMap[buttonNumber]);
            }
            return MapFallbackLinuxButton(buttonNumber);
        }

        private static GamepadAction MapLinuxButtonCode(ushort buttonCode)
        {
            switch (buttonCode)
            {
                case LinuxBtnSouth:
                case LinuxBtnStart:
                    return GamepadAction.Confirm;
                case LinuxBtnEast:
                case LinuxBtnSelect:
                case LinuxKeyBack:
                    return GamepadAction.Cancel;
                case LinuxBtnWest:
                    return GamepadAction.RollDice;
                case LinuxBtnNorth:
                    return GamepadAction.SelectOption2;
                case LinuxBtnLeftShoulder:
                    return GamepadAction.SelectOption1;
                case LinuxBtnRightShoulder:
                    return GamepadAction.SelectOption3;
                case LinuxBtnDpadUp:
                    return GamepadAction.NavigateUp;
                case LinuxBtnDpadDown:
                    return GamepadAction.NavigateDown;
                case LinuxBtnDpadLeft:
                    return GamepadAction.NavigateLeft;
                case LinuxBtnDpadRight:
                    return GamepadAction.NavigateRight;
                default:
                    return GamepadAction.None;
            }
        }

        private static GamepadAction MapFallbackLinuxButton(byte buttonNumber)
        {
            switch (buttonNumber)
            {
                case FallbackButtonSouth:
                case FallbackButtonStart:
                    return GamepadAction.Confirm;
                case FallbackButtonEast:
                case FallbackButtonSelect:
                    return GamepadAction.Cancel;
                case FallbackButtonWest:
                    return GamepadAction.RollDice;
                case FallbackButtonNorth:
                    return GamepadAction.SelectOption2;
                case FallbackButtonLeftShoulder:
                    return GamepadAction.SelectOption1;
                case FallbackButtonRightShoulder:
                    return GamepadAction.SelectOption3;
                default:
                    return GamepadAction.None;
            }
        }

        private static void ProcessLinuxAxis(byte axisNumber, short axisValue, LinuxJoystickLayout layout,
            ref ControllerDirectionState directionState)
        {
            byte axisCode = ResolveLinuxAxisCode(axisNumber, layout);
            if (axisCode == LinuxAbsX)
            {
                UpdateHorizontalState(GetAxisDirection(axisValue, AxisThreshold), ref directionState.StickHorizontal,
                    directionState.ControllerIndex);
            }
            else if (axisCode == LinuxAbsY)
            {
                UpdateVerticalState(GetAxisDirection(axisValue, AxisThreshold), ref directionState.StickVertical,
                    directionState.ControllerIndex);
            }
            else if (axisCode == LinuxAbsHat0X)
            {
                UpdateHorizontalState(GetAxisDirection(axisValue, HatThreshold), ref directionState.DpadHorizontal,
                    directionState.ControllerIndex);
            }
            else if (axisCode == LinuxAbsHat0Y)
            {
                UpdateVerticalState(GetAxisDirection(axisValue, HatThreshold), ref directionState.DpadVertical,
                    directionState.ControllerIndex);
            }
        }

        private static byte ResolveLinuxAxisCode(byte axisNumber, LinuxJoystickLayout layout)
        {
            if (layout.HasAxisMap && axisNumber < layout.AxisMap.Length)
            {
                return layout.AxisMap[axisNumber];
            }
            switch (axisNumber)
            {
                case FallbackAxisLeftStickHorizontal:
                    return LinuxAbsX;
                case FallbackAxisLeftStickVertical:
                    return LinuxAbsY;
                case FallbackAxisDpadHorizontal:
                    return LinuxAbsHat0X;
                case FallbackAxisDpadVertical:
                    return LinuxAbsHat0Y;
                default:
                    return byte.MaxValue;
            }
        }

        private static void UpdateHorizontalState(int currentDirection, ref int previousDirection, int controllerIndex)
        {
            if (currentDirection != previousDirection)
            {
                previousDirection = currentDirection;
                DispatchHorizontalAction(currentDirection, controllerIndex);
            }
        }

        private static void UpdateVerticalState(int currentDirection, ref int previousDirection, int controllerIndex)
        {
            if (currentDirection != previousDirection)
            {
                previousDirection = currentDirection;
                DispatchVerticalAction(currentDirection, controllerIndex);
            }
        }

        private static int GetAxisDirection(short axisValue, short threshold)
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
                        directionStates[controllerIndex].StickHorizontal = DirectionNeutral;
                        directionStates[controllerIndex].StickVertical = DirectionNeutral;
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

            UpdateHorizontalState(currentHorizontal, ref directionState.StickHorizontal, directionState.ControllerIndex);
            UpdateVerticalState(currentVertical, ref directionState.StickVertical, directionState.ControllerIndex);
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

        [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
        private static extern int QueryLinuxAxisMap(int fileDescriptor, ulong request, [Out] byte[] axisMap);

        [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
        private static extern int QueryLinuxButtonMap(int fileDescriptor, ulong request, [Out] ushort[] buttonMap);

        [DllImport("winmm.dll")]
        private static extern uint joyGetPosEx(uint joyId, ref WindowsJoyInfoEx joyInfo);

        private sealed class LinuxJoystickLayout
        {
            public byte[] AxisMap { get; init; } = Array.Empty<byte>();
            public ushort[] ButtonMap { get; init; } = Array.Empty<ushort>();
            public bool HasAxisMap { get; init; }
            public bool HasButtonMap { get; init; }
        }

        private struct ControllerDirectionState
        {
            public int StickHorizontal;
            public int StickVertical;
            public int DpadHorizontal;
            public int DpadVertical;
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
