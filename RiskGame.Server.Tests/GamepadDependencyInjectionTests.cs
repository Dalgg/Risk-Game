extern alias Client;

using System;
using System.Threading;
using Xunit;
using GamepadAction = Client::RiskGame.Services.GamepadAction;
using GamepadActionEventArgs = Client::RiskGame.Services.GamepadActionEventArgs;
using GamepadBackendFactory = Client::RiskGame.Services.GamepadBackendFactory;
using GamepadService = Client::RiskGame.Services.GamepadService;
using IGamepadInputBackend = Client::RiskGame.Services.IGamepadInputBackend;
using LinuxGamepadInputBackend = Client::RiskGame.Services.LinuxGamepadInputBackend;
using NullGamepadInputBackend = Client::RiskGame.Services.NullGamepadInputBackend;
using WindowsGamepadInputBackend = Client::RiskGame.Services.WindowsGamepadInputBackend;

namespace RiskGame.Server.Tests
{
    public sealed class GamepadDependencyInjectionTests
    {
        private const int WaitTimeoutMilliseconds = 3000;
        private const int SecondControllerIndex = 1;

        [Fact]
        public void Factory_LinuxPlatform_ReturnsLinuxBackend()
        {
            IGamepadInputBackend backend = GamepadBackendFactory.Create(true, false);

            Assert.IsType<LinuxGamepadInputBackend>(backend);
        }

        [Fact]
        public void Factory_WindowsPlatform_ReturnsWindowsBackend()
        {
            IGamepadInputBackend backend = GamepadBackendFactory.Create(false, true);

            Assert.IsType<WindowsGamepadInputBackend>(backend);
        }

        [Fact]
        public void Factory_UnsupportedPlatform_ReturnsNullBackend()
        {
            IGamepadInputBackend backend = GamepadBackendFactory.Create(false, false);

            Assert.IsType<NullGamepadInputBackend>(backend);
        }

        [Fact]
        public void Initialize_NullBackend_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => GamepadService.Initialize(null!));
        }

        [Fact]
        public void Initialize_InjectedBackend_DispatchesActionsAndCancelsOnShutdown()
        {
            GamepadService.Shutdown();
            using FakeGamepadBackend fakeBackend = new FakeGamepadBackend();
            using ManualResetEventSlim actionReceived = new ManualResetEventSlim(false);
            GamepadAction capturedAction = GamepadAction.None;
            int capturedController = -1;

            EventHandler<GamepadActionEventArgs> handler = (sender, args) =>
            {
                capturedAction = args.Action;
                capturedController = args.ControllerIndex;
                actionReceived.Set();
            };

            GamepadService.ActionTriggered += handler;
            try
            {
                GamepadService.Initialize(fakeBackend);
                Assert.True(fakeBackend.WaitUntilStarted(WaitTimeoutMilliseconds));

                fakeBackend.Emit(GamepadAction.Confirm, SecondControllerIndex);
                Assert.True(actionReceived.Wait(WaitTimeoutMilliseconds));
                Assert.Equal(GamepadAction.Confirm, capturedAction);
                Assert.Equal(SecondControllerIndex, capturedController);

                GamepadService.Shutdown();
                Assert.True(fakeBackend.WaitUntilStopped(WaitTimeoutMilliseconds));
            }
            finally
            {
                GamepadService.ActionTriggered -= handler;
                GamepadService.Shutdown();
            }
        }

        private sealed class FakeGamepadBackend : IGamepadInputBackend, IDisposable
        {
            private readonly ManualResetEventSlim started = new ManualResetEventSlim(false);
            private readonly ManualResetEventSlim stopped = new ManualResetEventSlim(false);
            private Action<GamepadAction, int>? emitAction;

            public void Run(Action<GamepadAction, int> onAction, CancellationToken cancellationToken)
            {
                emitAction = onAction;
                started.Set();
                cancellationToken.WaitHandle.WaitOne();
                stopped.Set();
            }

            public bool WaitUntilStarted(int timeoutMilliseconds)
            {
                return started.Wait(timeoutMilliseconds);
            }

            public bool WaitUntilStopped(int timeoutMilliseconds)
            {
                return stopped.Wait(timeoutMilliseconds);
            }

            public void Emit(GamepadAction action, int controllerIndex)
            {
                Action<GamepadAction, int>? callback = emitAction;
                if (callback != null)
                {
                    callback(action, controllerIndex);
                }
            }

            public void Dispose()
            {
                started.Dispose();
                stopped.Dispose();
            }
        }
    }
}
