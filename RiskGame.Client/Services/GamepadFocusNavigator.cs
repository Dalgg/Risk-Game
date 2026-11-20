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
        private const int EmptyCount = 0;
        private const int FirstIndex = 0;
        private const int NextStep = 1;
        private const double HalfFactor = 0.5;
        private const double MinimumAxisDelta = 4.0;
        private const double CrossAxisPenalty = 2.5;
        private const double RowBucketPixels = 24.0;
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
                    case GamepadAction.NavigateDown:
                    case GamepadAction.NavigateLeft:
                    case GamepadAction.NavigateRight:
                        MoveFocusSpatially(activeWindow, action);
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
            if (Application.Current != null
                && Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
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
            return resolvedWindow;
        }

        private static void MoveFocusSpatially(Window activeWindow, GamepadAction direction)
        {
            List<PositionedControl> candidates = CollectPositionedControls(activeWindow);
            if (candidates.Count > EmptyCount)
            {
                IInputElement? focusedElement = GetFocusedElement(activeWindow);
                PositionedControl? current = FindCurrentControl(candidates, focusedElement);
                Control targetControl = ResolveSpatialTarget(candidates, current, direction);
                targetControl.BringIntoView();
                targetControl.Focus(NavigationMethod.Tab);
            }
        }

        private static Control ResolveSpatialTarget(List<PositionedControl> candidates,
            PositionedControl? current, GamepadAction direction)
        {
            if (current == null)
            {
                return candidates[FirstIndex].Control;
            }

            PositionedControl? bestCandidate = null;
            double bestScore = double.MaxValue;
            foreach (PositionedControl candidate in candidates.Where(
                entry => !ReferenceEquals(entry.Control, current.Control)))
            {
                double score = ComputeDirectionScore(current.Center, candidate.Center, direction);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestCandidate = candidate;
                }
            }

            if (bestCandidate != null)
            {
                return bestCandidate.Control;
            }
            return current.Control;
        }

        private static double ComputeDirectionScore(Point origin, Point target, GamepadAction direction)
        {
            double deltaX = target.X - origin.X;
            double deltaY = target.Y - origin.Y;

            switch (direction)
            {
                case GamepadAction.NavigateLeft:
                    return ScoreAxisCandidate(-deltaX, Math.Abs(deltaY));
                case GamepadAction.NavigateRight:
                    return ScoreAxisCandidate(deltaX, Math.Abs(deltaY));
                case GamepadAction.NavigateUp:
                    return ScoreAxisCandidate(-deltaY, Math.Abs(deltaX));
                case GamepadAction.NavigateDown:
                    return ScoreAxisCandidate(deltaY, Math.Abs(deltaX));
                default:
                    return double.MaxValue;
            }
        }

        private static double ScoreAxisCandidate(double primaryDistance, double crossDistance)
        {
            if (primaryDistance <= MinimumAxisDelta)
            {
                return double.MaxValue;
            }
            return primaryDistance + (crossDistance * CrossAxisPenalty);
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
                MoveFocusSpatially(activeWindow, GamepadAction.NavigateDown);
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
                int nextIndex = (comboBox.SelectedIndex + NextStep) % comboBox.ItemCount;
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
            List<PositionedControl> controls = CollectPositionedControls(activeWindow);
            Button? cancelButton = null;
            PositionedControl? matchingControl = controls.FirstOrDefault(
                entry => entry.Control is Button button && IsCancelButton(button));
            if (matchingControl != null && matchingControl.Control is Button matchedButton)
            {
                cancelButton = matchedButton;
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

        private static List<PositionedControl> CollectPositionedControls(Window activeWindow)
        {
            List<PositionedControl> controls = new List<PositionedControl>();
            foreach (Visual descendant in activeWindow.GetVisualDescendants())
            {
                if (descendant is Control control && IsNavigableTarget(control))
                {
                    Point? center = TryResolveCenter(control, activeWindow);
                    if (center.HasValue)
                    {
                        controls.Add(new PositionedControl(control, center.Value));
                    }
                }
            }
            return controls
                .OrderBy(entry => Math.Floor(entry.Center.Y / RowBucketPixels))
                .ThenBy(entry => entry.Center.X)
                .ToList();
        }

        private static Point? TryResolveCenter(Control control, Window activeWindow)
        {
            Point localCenter = new Point(
                control.Bounds.Width * HalfFactor,
                control.Bounds.Height * HalfFactor);
            return control.TranslatePoint(localCenter, activeWindow);
        }

        private static PositionedControl? FindCurrentControl(List<PositionedControl> candidates,
            IInputElement? focusedElement)
        {
            PositionedControl? match = null;
            if (focusedElement is Control focusedControl)
            {
                match = candidates.FirstOrDefault(candidate => ReferenceEquals(candidate.Control, focusedControl));
            }
            return match;
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

        private sealed class PositionedControl
        {
            public Control Control { get; }
            public Point Center { get; }

            public PositionedControl(Control control, Point center)
            {
                Control = control;
                Center = center;
            }
        }
    }
}
