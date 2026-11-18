using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using RiskGame.Services;
using RiskGame.ViewModels;

namespace RiskGame.Views
{
    public partial class MainWindow : Window
    {
        private const int PreviousDirection = -1;
        private const int NextDirection = 1;
        private int focusedButtonIndex;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            GamepadService.ActionTriggered += OnGamepadAction;
        }

        protected override void OnClosed(EventArgs eventArgs)
        {
            GamepadService.ActionTriggered -= OnGamepadAction;
            base.OnClosed(eventArgs);
        }

        private void OnGamepadAction(object? sender, GamepadActionEventArgs eventArgs)
        {
            Dispatcher.UIThread.Post(() => HandleGamepadAction(eventArgs.Action));
        }

        private void HandleGamepadAction(GamepadAction action)
        {
            switch (action)
            {
                case GamepadAction.NavigateUp:
                case GamepadAction.NavigateLeft:
                    MoveButtonFocus(PreviousDirection);
                    break;
                case GamepadAction.NavigateDown:
                case GamepadAction.NavigateRight:
                    MoveButtonFocus(NextDirection);
                    break;
                case GamepadAction.Confirm:
                case GamepadAction.RollDice:
                    ActivateFocusedButton();
                    break;
                case GamepadAction.Cancel:
                    Close();
                    break;
            }
        }

        private void MoveButtonFocus(int step)
        {
            List<Button> buttons = GetNavigableButtons();
            if (buttons.Count == 0)
            {
                return;
            }

            focusedButtonIndex = (focusedButtonIndex + step + buttons.Count) % buttons.Count;
            buttons[focusedButtonIndex].Focus();
        }

        private void ActivateFocusedButton()
        {
            List<Button> buttons = GetNavigableButtons();
            if (buttons.Count == 0)
            {
                return;
            }

            if (focusedButtonIndex < 0 || focusedButtonIndex >= buttons.Count)
            {
                focusedButtonIndex = 0;
            }

            Button selectedButton = buttons[focusedButtonIndex];
            if (ReferenceEquals(selectedButton, RoomsButton))
            {
                OnRoomsClick(RoomsButton, new RoutedEventArgs());
            }
            else if (ReferenceEquals(selectedButton, ExitButton))
            {
                OnExitClick(ExitButton, new RoutedEventArgs());
            }
            else if (ReferenceEquals(selectedButton, LeaderboardButton))
            {
                OnLeaderboardClick(LeaderboardButton, new RoutedEventArgs());
            }
            else if (ReferenceEquals(selectedButton, FriendsButton))
            {
                OnFriendsClick(FriendsButton, new RoutedEventArgs());
            }
            else if (ReferenceEquals(selectedButton, SettingsButton))
            {
                OnSettingsClick(SettingsButton, new RoutedEventArgs());
            }
            else if (ReferenceEquals(selectedButton, InboxButton))
            {
                OnInboxClick(InboxButton, new RoutedEventArgs());
            }
            else if (ReferenceEquals(selectedButton, LoginButton))
            {
                OnLoginClick(LoginButton, new RoutedEventArgs());
            }
            else if (ReferenceEquals(selectedButton, ProfileButton) && DataContext is MainViewModel viewModel)
            {
                viewModel.OpenProfileCommand.Execute(null);
            }
        }

        private List<Button> GetNavigableButtons()
        {
            Button[] candidateButtons =
            {
                RoomsButton,
                LeaderboardButton,
                FriendsButton,
                SettingsButton,
                InboxButton,
                LoginButton,
                ProfileButton,
                ExitButton
            };

            List<Button> visibleButtons = new List<Button>();
            foreach (Button candidate in candidateButtons)
            {
                if (candidate.IsVisible && candidate.IsEnabled)
                {
                    visibleButtons.Add(candidate);
                }
            }

            return visibleButtons;
        }

        private void ChangeWindow(Window newWindow)
        {
            if (Application.Current != null
            && Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                desktopLifetime.MainWindow = newWindow;
                newWindow.Show();
                Close();
            }
        }

        private void OnSettingsClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            ChangeWindow(new SettingsWindow());
        }

        private void OnFriendsClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            SessionUser? currentSessionUser = SessionService.CurrentUser;
            if (currentSessionUser == null || currentSessionUser.IsGuest)
            {
                ChangeWindow(new LoginWindow());
            }
            else
            {
                ChangeWindow(new FriendsWindow());
            }
        }

        private void OnInboxClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            SessionUser? currentSessionUser = SessionService.CurrentUser;
            if (currentSessionUser == null || currentSessionUser.IsGuest)
            {
                ChangeWindow(new LoginWindow());
            }
            else
            {
                ChangeWindow(new InboxWindow());
            }
        }

        private void OnLoginClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            SessionService.Logout();
            ChangeWindow(new LoginWindow());
        }

        private void OnRoomsClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            ChangeWindow(new RoomsWindow());
        }

        private void OnLeaderboardClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            ChangeWindow(new LeaderboardWindow());
        }

        private void OnExitClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            Close();
        }

    }
}
