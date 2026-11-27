using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using RiskGame.Resources;
using RiskGame.Services;
using RiskGame.ViewModels;
using RiskGame.Views;

namespace RiskGame
{
    public partial class App : Application
    {
        private bool isShuttingDown;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            RiskGame.Resources.Strings.Culture = new CultureInfo("en-US");
            IGamepadInputBackend gamepadBackend = GamepadBackendFactory.CreateDefault();
            GamepadService.Initialize(gamepadBackend);
            GamepadFocusNavigator.Initialize();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                desktopLifetime.Exit += OnApplicationExit;
                SessionService.SessionExpired += OnSessionExpired;
                desktopLifetime.MainWindow = new MainWindow
                {
                    DataContext = new MainViewModel(),
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void OnApplicationExit(object? sender, ControlledApplicationLifetimeExitEventArgs exitEventArgs)
        {
            isShuttingDown = true;
            SessionService.SessionExpired -= OnSessionExpired;
            GamepadFocusNavigator.Shutdown();
            GamepadService.Shutdown();
            SessionService.Logout();
        }

        private void OnSessionExpired(object? sender, EventArgs eventArgs)
        {
            Dispatcher.UIThread.Post(OpenLoginAfterSessionExpiry);
        }

        private void OpenLoginAfterSessionExpiry()
        {
            SessionUser? currentSession = SessionService.CurrentUser;
            bool hasNewSession = currentSession != null && !currentSession.IsGuest;
            if (!isShuttingDown && !hasNewSession
                && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                List<Window> previousWindows = desktopLifetime.Windows.ToList();
                LoginWindow loginWindow = new LoginWindow(Strings.SessionExpiredMessage, true);
                desktopLifetime.MainWindow = loginWindow;
                loginWindow.Show();
                foreach (Window previousWindow in previousWindows)
                {
                    previousWindow.Close();
                }
            }
        }
    }
}
