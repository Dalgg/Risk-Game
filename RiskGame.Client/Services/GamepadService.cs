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
        private const int LinuxPacketSize = 8;
        private const int EventValueIndex = 4;
        private const int EventTypeIndex = 6;
        private const int EventNumberIndex = 7;
        private const byte LinuxButtonEvent = 0x01;
        private const byte LinuxAxisEvent = 0x02;
        private const byte LinuxInitFlag = 0x80;
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
        private static bool isConnected;

        public static bool IsConnected => isConnected;

        public static event EventHandler<GamepadActionEventArgs>? ActionTriggered;
        public static event EventHandler<bool>? ConnectionChanged;

        private const int NoControllerFound = -1;
        private const int FirstControllerIndex = 0;

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

        public static void TriggerAction(GamepadAction action, int controllerIndex = 0)
        {
            EventHandler<GamepadActionEventArgs>? actionHandler = ActionTriggered;
            if (action != GamepadAction.None && actionHandler != null)
            {
                actionHandler.Invoke(null, new GamepadActionEventArgs(action, controllerIndex));
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

                UpdateConnectionState(false);
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
                int controllerIndex = FindLinuxJoystickIndex();
                if (controllerIndex != NoControllerFound)
                {
                    string devicePath = $"/dev/input/js{controllerIndex}";
                    UpdateConnectionState(true);
                    ReadLinuxJoystickStream(devicePath, controllerIndex, cancellationToken);
                    UpdateConnectionState(false);
                }
                else
                {
                    UpdateConnectionState(false);
                    WaitBeforeRetry(cancellationToken, ReconnectDelayMilliseconds);
                }
            }
        }

        private static int FindLinuxJoystickIndex()
        {
            int controllerIndex = NoControllerFound;
            for (int candidateIndex = FirstControllerIndex; candidateIndex < MaximumControllers; candidateIndex++)
            {
                string candidatePath = $"/dev/input/js{candidateIndex}";
                if (File.Exists(candidatePath))
                {
                    controllerIndex = candidateIndex;
                    break;
                }
            }
            return controllerIndex;
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

                while (!cancellationToken.IsCancellationRequested)
                {
                    int bytesRead = deviceStream.Read(packetBuffer, 0, LinuxPacketSize);
                    if (bytesRead < LinuxPacketSize)
                    {
                        break;
                    }

                    byte rawEventType = packetBuffer[EventTypeIndex];
                    if ((rawEventType & LinuxInitFlag) != 0)
                    {
                        continue;
                    }

                    short eventValue = BitConverter.ToInt16(packetBuffer, EventValueIndex);
                    byte eventNumber = packetBuffer[EventNumberIndex];
                    byte cleanEventType = (byte)(rawEventType & ~LinuxInitFlag);

                    if (cleanEventType == LinuxButtonEvent && eventValue == ButtonPressedValue)
                    {
                        GamepadAction buttonAction = MapLinuxButton(eventNumber);
                        TriggerAction(buttonAction, controllerIndex);
                    }
                    else if (cleanEventType == LinuxAxisEvent)
                    {
                        ProcessLinuxAxis(eventNumber, eventValue, ref directionState);
                    }
                }
            }
            catch (IOException)
            {
                WaitBeforeRetry(cancellationToken, ReconnectDelayMilliseconds);
            }
            catch (UnauthorizedAccessException)
            {
                WaitBeforeRetry(cancellationToken, ReconnectDelayMilliseconds);
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
                    if (currentHorizontal == DirectionNegative)
                    {
                        TriggerAction(GamepadAction.NavigateLeft, directionState.ControllerIndex);
                    }
                    else if (currentHorizontal == DirectionPositive)
                    {
                        TriggerAction(GamepadAction.NavigateRight, directionState.ControllerIndex);
                    }
                }
            }
            else if (axisNumber == AxisLeftStickVertical || axisNumber == AxisDpadVertical)
            {
                int currentVertical = GetAxisDirection(axisValue);
                if (currentVertical != directionState.Vertical)
                {
                    directionState.Vertical = currentVertical;
                    if (currentVertical == DirectionNegative)
                    {
                        TriggerAction(GamepadAction.NavigateUp, directionState.ControllerIndex);
                    }
                    else if (currentVertical == DirectionPositive)
                    {
                        TriggerAction(GamepadAction.NavigateDown, directionState.ControllerIndex);
                    }
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

                for (uint controllerId = 0; controllerId < MaximumControllers; controllerId++)
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
                        previousButtons[controllerIndex] = 0;
                        directionStates[controllerIndex].Horizontal = DirectionNeutral;
                        directionStates[controllerIndex].Vertical = DirectionNeutral;
                        continue;
                    }

                    anyControllerConnected = true;
                    uint newlyPressed = joyInfo.Buttons & ~previousButtons[controllerIndex];
                    previousButtons[controllerIndex] = joyInfo.Buttons;

                    DispatchWindowsButtons(newlyPressed, controllerIndex);
                    DispatchWindowsDirections(joyInfo, ref directionStates[controllerIndex]);
                }

                UpdateConnectionState(anyControllerConnected);
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
            if ((newlyPressedMask & (WindowsButtonOneMask | WindowsButtonEightMask)) != 0)
            {
                TriggerAction(GamepadAction.Confirm, controllerIndex);
            }

            if ((newlyPressedMask & (WindowsButtonTwoMask | WindowsButtonSevenMask)) != 0)
            {
                TriggerAction(GamepadAction.Cancel, controllerIndex);
            }

            if ((newlyPressedMask & WindowsButtonThreeMask) != 0)
            {
                TriggerAction(GamepadAction.RollDice, controllerIndex);
            }

            if ((newlyPressedMask & WindowsButtonFourMask) != 0)
            {
                TriggerAction(GamepadAction.SelectOption2, controllerIndex);
            }

            if ((newlyPressedMask & WindowsButtonFiveMask) != 0)
            {
                TriggerAction(GamepadAction.SelectOption1, controllerIndex);
            }

            if ((newlyPressedMask & WindowsButtonSixMask) != 0)
            {
                TriggerAction(GamepadAction.SelectOption3, controllerIndex);
            }
        }

        private static void DispatchWindowsDirections(WindowsJoyInfoEx joyInfo,
            ref ControllerDirectionState directionState)
        {
            int currentHorizontal = DirectionNeutral;
            int currentVertical = DirectionNeutral;

            if (joyInfo.Pov != WindowsPovCentered)
            {
                if (joyInfo.Pov == WindowsPovLeft)
                {
                    currentHorizontal = DirectionNegative;
                }
                else if (joyInfo.Pov == WindowsPovRight)
                {
                    currentHorizontal = DirectionPositive;
                }
                else if (joyInfo.Pov == WindowsPovUp)
                {
                    currentVertical = DirectionNegative;
                }
                else if (joyInfo.Pov == WindowsPovDown)
                {
                    currentVertical = DirectionPositive;
                }
            }
            else
            {
                int deltaX = (int)joyInfo.Xpos - (int)WindowsAxisCenter;
                int deltaY = (int)joyInfo.Ypos - (int)WindowsAxisCenter;

                if (deltaX < -WindowsAxisDeadzone)
                {
                    currentHorizontal = DirectionNegative;
                }
                else if (deltaX > WindowsAxisDeadzone)
                {
                    currentHorizontal = DirectionPositive;
                }

                if (deltaY < -WindowsAxisDeadzone)
                {
                    currentVertical = DirectionNegative;
                }
                else if (deltaY > WindowsAxisDeadzone)
                {
                    currentVertical = DirectionPositive;
                }
            }

            if (currentHorizontal != directionState.Horizontal)
            {
                directionState.Horizontal = currentHorizontal;
                if (currentHorizontal == DirectionNegative)
                {
                    TriggerAction(GamepadAction.NavigateLeft, directionState.ControllerIndex);
                }
                else if (currentHorizontal == DirectionPositive)
                {
                    TriggerAction(GamepadAction.NavigateRight, directionState.ControllerIndex);
                }
            }

            if (currentVertical != directionState.Vertical)
            {
                directionState.Vertical = currentVertical;
                if (currentVertical == DirectionNegative)
                {
                    TriggerAction(GamepadAction.NavigateUp, directionState.ControllerIndex);
                }
                else if (currentVertical == DirectionPositive)
                {
                    TriggerAction(GamepadAction.NavigateDown, directionState.ControllerIndex);
                }
            }
        }

        private struct ControllerDirectionState
        {
            public int Horizontal;
            public int Vertical;
            public int ControllerIndex;
        }

        private static void UpdateConnectionState(bool connected)
        {
            if (isConnected != connected)
            {
                isConnected = connected;
                EventHandler<bool>? connectionHandler = ConnectionChanged;
                if (connectionHandler != null)
                {
                    connectionHandler.Invoke(null, isConnected);
                }
            }
        }

        private static void WaitBeforeRetry(CancellationToken cancellationToken, int delayMilliseconds)
        {
            cancellationToken.WaitHandle.WaitOne(delayMilliseconds);
        }

        [DllImport("winmm.dll")]
        private static extern uint joyGetPosEx(uint joyId, ref WindowsJoyInfoEx joyInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowsJoyInfoEx
        {
            public uint Size;
            public uint Flags;
            public uint Xpos;
            public uint Ypos;
            public uint Zpos;
            public uint Rpos;
            public uint Upos;
            public uint Vpos;
            public uint Buttons;
            public uint ButtonNumber;
            public uint Pov;
            public uint Reserved1;
            public uint Reserved2;
        }
    }
}
