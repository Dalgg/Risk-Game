using System;
using System.Collections.Generic;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Threading;
using RiskGame.Services;
using RiskGame.ViewModels;

namespace RiskGame.Views
{
    public partial class LeaderboardWindow : Window
    {
        private const int FirstAction = 0;
        private const int NextAction = 1;
        private const int PreviousAction = -1;
        private Button[] navigationButtons = Array.Empty<Button>();
        private int focusedIndex = FirstAction;
        private bool hasClosed;

        public LeaderboardWindow()
        {
            InitializeComponent();
        }

        protected override void OnOpened(EventArgs eventArgs)
        {
            base.OnOpened(eventArgs);
            navigationButtons = new Button[] { GlobalButton, FriendsButton, SortButton, RefreshButton, BackButton };
            GamepadService.ActionTriggered += OnGamepadAction;
            GlobalButton.Focus();
        }

        protected override void OnClosed(EventArgs eventArgs)
        {
            hasClosed = true;
            GamepadService.ActionTriggered -= OnGamepadAction;
            if (DataContext is LeaderboardViewModel viewModel)
            {
                viewModel.Deactivate();
            }
            base.OnClosed(eventArgs);
        }

        private void OnGamepadAction(object? sender, GamepadActionEventArgs eventArgs)
        {
            Dispatcher.UIThread.Post(() => HandleGamepadAction(eventArgs.Action));
        }

        private void HandleGamepadAction(GamepadAction action)
        {
            if (!hasClosed)
            {
                switch (action)
                {
                    case GamepadAction.NavigateLeft:
                    case GamepadAction.NavigateUp:
                        MoveFocus(PreviousAction);
                        break;
                    case GamepadAction.NavigateRight:
                    case GamepadAction.NavigateDown:
                        MoveFocus(NextAction);
                        break;
                    case GamepadAction.Confirm:
                        ConfirmSelection();
                        break;
                    case GamepadAction.Cancel:
                        if (DataContext is LeaderboardViewModel viewModel)
                        {
                            viewModel.BackCommand.Execute(null);
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        private void MoveFocus(int direction)
        {
            List<Button> buttons = new List<Button>();
            foreach (Button button in navigationButtons)
            {
                if (button.IsEnabled)
                {
                    buttons.Add(button);
                }
            }
            if (buttons.Count > FirstAction)
            {
                focusedIndex = (focusedIndex + direction + buttons.Count) % buttons.Count;
                buttons[focusedIndex].Focus();
            }
        }

        private void ConfirmSelection()
        {
            if (FocusManager != null)
            {
                if (FocusManager.GetFocusedElement() is Button button)
                {
                    ICommand? command = button.Command;
                    if (command != null && button.IsEnabled)
                    {
                        command.Execute(button.CommandParameter);
                    }
                }
            }
        }
    }
}