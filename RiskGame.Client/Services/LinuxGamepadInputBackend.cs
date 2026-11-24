using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace RiskGame.Services
{
    public sealed partial class LinuxGamepadInputBackend : IGamepadInputBackend
    {
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

        public void Run(Action<GamepadAction, int> onAction, CancellationToken cancellationToken)
        {
            for (int slotIndex = GamepadDirectionTracker.FirstControllerIndex;
                slotIndex < GamepadDirectionTracker.MaximumControllers; slotIndex++)
            {
                int capturedSlot = slotIndex;
                Task.Run(() => RunControllerSlot(capturedSlot, onAction, cancellationToken), cancellationToken);
            }
        }

        private static void RunControllerSlot(int controllerIndex, Action<GamepadAction, int> onAction,
            CancellationToken cancellationToken)
        {
            string devicePath = $"/dev/input/js{controllerIndex}";
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!File.Exists(devicePath))
                {
                    GamepadDirectionTracker.WaitBeforeRetry(
                        GamepadDirectionTracker.ReconnectDelayMilliseconds, cancellationToken);
                }
                else
                {
                    ReadJoystickStream(devicePath, controllerIndex, onAction, cancellationToken);
                }
            }
        }

        private static void ReadJoystickStream(string devicePath, int controllerIndex,
            Action<GamepadAction, int> onAction, CancellationToken cancellationToken)
        {
            byte[] packetBuffer = new byte[LinuxPacketSize];
            GamepadDirectionState directionState = new GamepadDirectionState
            {
                ControllerIndex = controllerIndex
            };

            try
            {
                using FileStream deviceStream = new FileStream(
                    devicePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                LinuxJoystickLayout layout = ReadJoystickLayout(deviceStream);
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
                        ProcessPacket(packetBuffer, layout, ref directionState, onAction);
                    }
                }
            }
            catch (IOException)
            {
                GamepadDirectionTracker.WaitBeforeRetry(
                    GamepadDirectionTracker.ReconnectDelayMilliseconds, cancellationToken);
            }
            catch (UnauthorizedAccessException unauthorizedException)
            {
                JsonLogger.Error(unauthorizedException, "Could not access joystick device {0}.", devicePath);
                GamepadDirectionTracker.WaitBeforeRetry(
                    GamepadDirectionTracker.ReconnectDelayMilliseconds, cancellationToken);
            }
        }

        private static LinuxJoystickLayout ReadJoystickLayout(FileStream deviceStream)
        {
            byte[] axisMap = new byte[LinuxAxisMapLength];
            ushort[] buttonMap = new ushort[LinuxButtonMapLength];
            SafeFileHandle fileHandle = deviceStream.SafeFileHandle;
            bool hasAxisMap = QueryLinuxAxisMap(fileHandle, LinuxIoctlGetAxisMap, axisMap) != IoctlFailureResult;
            bool hasButtonMap = QueryLinuxButtonMap(fileHandle, LinuxIoctlGetButtonMap, buttonMap)
                != IoctlFailureResult;

            return new LinuxJoystickLayout
            {
                AxisMap = axisMap,
                ButtonMap = buttonMap,
                HasAxisMap = hasAxisMap,
                HasButtonMap = hasButtonMap
            };
        }

        private static void ProcessPacket(byte[] packetBuffer, LinuxJoystickLayout layout,
            ref GamepadDirectionState directionState, Action<GamepadAction, int> onAction)
        {
            byte rawEventType = packetBuffer[EventTypeIndex];
            if ((rawEventType & LinuxInitFlag) == NoFlags)
            {
                short eventValue = BitConverter.ToInt16(packetBuffer, EventValueIndex);
                byte eventNumber = packetBuffer[EventNumberIndex];
                byte cleanEventType = (byte)(rawEventType & ~LinuxInitFlag);

                if (cleanEventType == LinuxButtonEvent && eventValue == ButtonPressedValue)
                {
                    GamepadAction buttonAction = ResolveButtonAction(eventNumber, layout);
                    onAction(buttonAction, directionState.ControllerIndex);
                }
                else if (cleanEventType == LinuxAxisEvent)
                {
                    ProcessAxis(eventNumber, eventValue, layout, ref directionState, onAction);
                }
            }
        }

        private static GamepadAction ResolveButtonAction(byte buttonNumber, LinuxJoystickLayout layout)
        {
            if (layout.HasButtonMap && buttonNumber < layout.ButtonMap.Length)
            {
                return MapButtonCode(layout.ButtonMap[buttonNumber]);
            }
            return MapFallbackButton(buttonNumber);
        }

        private static GamepadAction MapButtonCode(ushort buttonCode)
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

        private static GamepadAction MapFallbackButton(byte buttonNumber)
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

        private static void ProcessAxis(byte axisNumber, short axisValue, LinuxJoystickLayout layout,
            ref GamepadDirectionState directionState, Action<GamepadAction, int> onAction)
        {
            byte axisCode = ResolveAxisCode(axisNumber, layout);
            if (axisCode == LinuxAbsX)
            {
                int direction = GamepadDirectionTracker.GetAxisDirection(
                    axisValue, GamepadDirectionTracker.AxisThreshold);
                GamepadDirectionTracker.UpdateHorizontalState(
                    direction, ref directionState.StickHorizontal, directionState.ControllerIndex, onAction);
            }
            else if (axisCode == LinuxAbsY)
            {
                int direction = GamepadDirectionTracker.GetAxisDirection(
                    axisValue, GamepadDirectionTracker.AxisThreshold);
                GamepadDirectionTracker.UpdateVerticalState(
                    direction, ref directionState.StickVertical, directionState.ControllerIndex, onAction);
            }
            else if (axisCode == LinuxAbsHat0X)
            {
                int direction = GamepadDirectionTracker.GetAxisDirection(axisValue, HatThreshold);
                GamepadDirectionTracker.UpdateHorizontalState(
                    direction, ref directionState.DpadHorizontal, directionState.ControllerIndex, onAction);
            }
            else if (axisCode == LinuxAbsHat0Y)
            {
                int direction = GamepadDirectionTracker.GetAxisDirection(axisValue, HatThreshold);
                GamepadDirectionTracker.UpdateVerticalState(
                    direction, ref directionState.DpadVertical, directionState.ControllerIndex, onAction);
            }
        }

        private static byte ResolveAxisCode(byte axisNumber, LinuxJoystickLayout layout)
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

        [LibraryImport("libc", EntryPoint = "ioctl", SetLastError = true)]
        private static partial int QueryLinuxAxisMap(SafeFileHandle fileHandle, ulong request, [Out] byte[] axisMap);

        [LibraryImport("libc", EntryPoint = "ioctl", SetLastError = true)]
        private static partial int QueryLinuxButtonMap(
            SafeFileHandle fileHandle, ulong request, [Out] ushort[] buttonMap);

        private sealed class LinuxJoystickLayout
        {
            public byte[] AxisMap { get; init; } = Array.Empty<byte>();
            public ushort[] ButtonMap { get; init; } = Array.Empty<ushort>();
            public bool HasAxisMap { get; init; }
            public bool HasButtonMap { get; init; }
        }
    }
}
