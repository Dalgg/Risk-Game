using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RiskGame.Views;

namespace RiskGame.Services
{
    public static class GamepadFocusNavigator
    {
        private const int PreviousDirection = -1;
        private const int NextDirection = 1;
        private const int FirstIndex = 0;
        private const int NoControlIndex = -1;
        private const int EmptyCount = 0;
        private static readonly object SyncRoot = new object();
        private static bool isInitialized;

        public static void Initialize()
        {
            lock (SyncRoot)
            {
                if (!isInitialized)
                {
                    GamepadService.ActionTriggered += OnGamepadAction;
                    isInitialized = true;
                }
            }
        }

        public static void Shutdown()
        {
            lock (SyncRoot)
            {
                if (isInitialized)
                {
                    GamepadService.ActionTriggered -= OnGamepadAction;
                    isInitialized = false;
                }
            }
        }

        private static void OnGamepadAction(object? sender, GamepadActionEventArgs eventArgs)
        {
            Dispatcher.UIThread.Post(() => HandleAction(eventArgs.Action));
        }

        private static void HandleAction(GamepadAction action)
        {
            Window? activeWindow = ResolveActiveWindow();
            if (activeWindow != null)
            {
                switch (action)
                {
                    case GamepadAction.NavigateUp:
                    case GamepadAction.NavigateLeft:
                        MoveFocus(activeWindow, PreviousDirection);
                        break;
                    case GamepadAction.NavigateDown:
                    case GamepadAction.NavigateRight:
                        MoveFocus(activeWindow, NextDirection);
                        break;
                    case GamepadAction.Confirm:
                        ActivateFocusedControl(activeWindow);
                        break;
                    case GamepadAction.Cancel:
                        HandleCancelAction(activeWindow);
                        break;
                    case GamepadAction.RollDice:
                    case GamepadAction.SelectOption1:
                    case GamepadAction.SelectOption2:
                    case GamepadAction.SelectOption3:
                    case GamepadAction.None:
                    default:
                        break;
                }
            }
        }

        private static Window? ResolveActiveWindow()
        {
            Window? resolvedWindow = null;
            if (Application.Current != null)
            {
                if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
                {
                    resolvedWindow = desktopLifetime.Windows.FirstOrDefault(window => window.IsActive);
                    if (resolvedWindow == null)
                    {
                        resolvedWindow = desktopLifetime.Windows.LastOrDefault(window => window.IsVisible);
                    }
                    if (resolvedWindow == null)
                    {
                        resolvedWindow = desktopLifetime.MainWindow;
                    }
                }
            }
            return resolvedWindow;
        }

        private static void MoveFocus(Window activeWindow, int direction)
        {
            List<Control> controls = CollectFocusableControls(activeWindow);
            if (controls.Count > EmptyCount)
            {
                IInputElement? focusedElement = GetFocusedElement(activeWindow);
                int currentIndex = FindFocusedControlIndex(controls, focusedElement);
                int nextIndex = ResolveNextIndex(currentIndex, direction, controls.Count);
                Control targetControl = controls[nextIndex];
                targetControl.BringIntoView();
                targetControl.Focus(NavigationMethod.Tab);
            }
        }

        private static void ActivateFocusedControl(Window activeWindow)
        {
            IInputElement? focusedElement = GetFocusedElement(activeWindow);
            if (focusedElement is Control focusedControl && focusedControl.IsEffectivelyEnabled)
            {
                TriggerControlInteraction(focusedControl);
            }
            else
            {
                MoveFocus(activeWindow, NextDirection);
            }
        }

        private static void TriggerControlInteraction(Control focusedControl)
        {
            if (focusedControl is Button button)
            {
                InvokeButton(button);
            }
            else if (focusedControl is ComboBox comboBox)
            {
                CycleComboBoxSelection(comboBox);
            }
            else if (focusedControl is TabItem tabItem)
            {
                tabItem.IsSelected = true;
            }
            else if (focusedControl is ListBoxItem listBoxItem)
            {
                listBoxItem.IsSelected = true;
            }
        }

        private static void InvokeButton(Button button)
        {
            ButtonAutomationPeer peer = new ButtonAutomationPeer(button);
            if (peer is IInvokeProvider invokeProvider)
            {
                invokeProvider.Invoke();
            }
        }

        private static void CycleComboBoxSelection(ComboBox comboBox)
        {
            if (comboBox.ItemCount > EmptyCount)
            {
                int nextIndex = (comboBox.SelectedIndex + NextDirection) % comboBox.ItemCount;
                comboBox.SelectedIndex = nextIndex;
            }
        }

        private static void HandleCancelAction(Window activeWindow)
        {
            if (!(activeWindow is MatchWindow))
            {
                Button? cancelButton = FindCancelButton(activeWindow);
                if (cancelButton != null)
                {
                    InvokeButton(cancelButton);
                }
            }
        }

        private static Button? FindCancelButton(Window activeWindow)
        {
            List<Control> controls = CollectFocusableControls(activeWindow);
            Button? cancelButton = null;
            foreach (Control control in controls)
            {
                if (cancelButton == null && control is Button button)
                {
                    if (IsCancelButton(button))
                    {
                        cancelButton = button;
                    }
                }
            }
            return cancelButton;
        }

        private static bool IsCancelButton(Button button)
        {
            if (button.IsCancel)
            {
                return true;
            }
            return button.HotKey != null && button.HotKey.Key == Key.Escape;
        }

        private static IInputElement? GetFocusedElement(Window activeWindow)
        {
            IInputElement? focusedElement = null;
            IFocusManager? focusManager = activeWindow.FocusManager;
            if (focusManager != null)
            {
                focusedElement = focusManager.GetFocusedElement();
            }
            return focusedElement;
        }

        private static List<Control> CollectFocusableControls(Window activeWindow)
        {
            List<Control> controls = new List<Control>();
            foreach (Visual descendant in activeWindow.GetVisualDescendants())
            {
                if (descendant is Control control && IsNavigableTarget(control))
                {
                    controls.Add(control);
                }
            }
            return controls.OrderBy(control => control.TabIndex).ToList();
        }

        private static bool IsNavigableTarget(Control control)
        {
            bool isInteractiveType = IsInteractiveControlType(control) && control.TemplatedParent == null;
            if (!isInteractiveType)
            {
                return false;
            }
            bool isVisibleAndEnabled = control.IsEffectivelyVisible && control.IsEffectivelyEnabled;
            return isVisibleAndEnabled && control.Focusable;
        }

        private static bool IsInteractiveControlType(Control control)
        {
            if (control is RepeatButton)
            {
                return false;
            }
            bool isActionControl = control is Button || control is TextBox || control is ComboBox;
            bool isSelectionControl = control is TabItem || control is ListBoxItem;
            return isActionControl || isSelectionControl;
        }

        private static int FindFocusedControlIndex(List<Control> controls, IInputElement? focusedElement)
        {
            if (focusedElement is Control focusedControl)
            {
                for (int index = FirstIndex; index < controls.Count; index++)
                {
                    if (ReferenceEquals(controls[index], focusedControl))
                    {
                        return index;
                    }
                }
            }
            return NoControlIndex;
        }

        private static int ResolveNextIndex(int currentIndex, int direction, int count)
        {
            if (currentIndex == NoControlIndex)
            {
                if (direction < FirstIndex)
                {
                    return count - NextDirection;
                }
                return FirstIndex;
            }
            return (currentIndex + direction + count) % count;
        }
    }
}
