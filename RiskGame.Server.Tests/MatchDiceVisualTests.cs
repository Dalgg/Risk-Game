extern alias Client;

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using RiskGame.Server.Tests.Builders;
using Xunit;
using DiceImageCatalog = Client::RiskGame.Services.DiceImageCatalog;
using PairOption = Client::RiskGame.Contracts.Match.PairOption;

namespace RiskGame.Server.Tests
{
    public sealed class MatchDiceVisualTests
    {
        private const int ExpectedFaceCount = 6;
        private const int ExpectedRolledDiceCount = 4;
        private const int ExpectedPairDiceCount = 4;
        private const int TooLowFace = 0;
        private const int TooHighFace = 7;
        private const int FirstDie = 1;
        private const int SecondDie = 2;
        private const int ThirdDie = 4;
        private const int FourthDie = 6;
        private const int FirstOptionIndex = 0;
        private const double FullOpacity = 1.0;
        private const string DiceImagesPanelName = "DiceImagesStackPanel";
        private const string FirstPairButtonName = "FirstPairButton";
        private const string RollDiceButtonName = "RollDiceButton";
        private const string StopTurnButtonName = "StopTurnButton";
        private const string LeaveMatchButtonName = "LeaveMatchButton";
        private const string LeaveConfirmOverlayName = "LeaveConfirmOverlay";
        private const string CancelLeaveButtonName = "CancelLeaveButton";
        private const string RollReadyClass = "RollReady";
        private const string StopReadyClass = "StopReady";

        [Fact]
        public void DiceImageCatalog_AllSixFaces_LoadDistinctImages()
        {
            ProfileTestApplication.Run(() =>
            {
                HashSet<IImage> images = new HashSet<IImage>();
                for (int face = DiceImageCatalog.MinimumFace; face <= DiceImageCatalog.MaximumFace; face++)
                {
                    images.Add(DiceImageCatalog.GetFaceImage(face));
                }

                Assert.Equal(ExpectedFaceCount, images.Count);
            });
        }

        [Theory]
        [InlineData(TooLowFace)]
        [InlineData(TooHighFace)]
        public void DiceImageCatalog_InvalidFace_ThrowsOutOfRange(int invalidFace)
        {
            ProfileTestApplication.Run(() =>
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => DiceImageCatalog.GetFaceImage(invalidFace));
            });
        }

        [Fact]
        public void MatchWindow_RolledDice_RendersFourFaceImages()
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchKeyboardBuilder setup = new MatchKeyboardBuilder();

                StackPanel dicePanel = setup.Window.FindControl<StackPanel>(DiceImagesPanelName)!;
                List<Image> renderedDice = dicePanel.Children.OfType<Image>().ToList();

                Assert.True(dicePanel.IsVisible);
                Assert.Equal(ExpectedRolledDiceCount, renderedDice.Count);
                Assert.Same(DiceImageCatalog.GetFaceImage(FirstDie), renderedDice[0].Source);
                Assert.Same(DiceImageCatalog.GetFaceImage(SecondDie), renderedDice[1].Source);
                Assert.Same(DiceImageCatalog.GetFaceImage(ThirdDie), renderedDice[2].Source);
                Assert.Same(DiceImageCatalog.GetFaceImage(FourthDie), renderedDice[3].Source);
                Assert.All(renderedDice, die => Assert.Equal(FullOpacity, die.Opacity));
            });
        }

        [Fact]
        public void MatchWindow_NoRoll_RendersFourIdlePreviewDice()
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchKeyboardBuilder setup = new MatchKeyboardBuilder();
                setup.Snapshot.Revision++;
                setup.Snapshot.Dice.Clear();
                setup.Snapshot.PairOptions.Clear();
                setup.ApplySnapshot();

                StackPanel dicePanel = setup.Window.FindControl<StackPanel>(DiceImagesPanelName)!;
                List<Image> previewDice = dicePanel.Children.OfType<Image>().ToList();

                Assert.True(dicePanel.IsVisible);
                Assert.Equal(ExpectedRolledDiceCount, previewDice.Count);
                Assert.All(previewDice, die => Assert.True(die.Opacity < FullOpacity));
            });
        }

        [Fact]
        public void MatchWindow_PairOptionWithDice_RendersDiceImagesAndAccessibleName()
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchKeyboardBuilder setup = new MatchKeyboardBuilder();
                PairOption firstOption = setup.Snapshot.PairOptions[FirstOptionIndex];
                firstOption.FirstPairDice.Add(FirstDie);
                firstOption.FirstPairDice.Add(SecondDie);
                firstOption.SecondPairDice.Add(ThirdDie);
                firstOption.SecondPairDice.Add(FourthDie);
                setup.Snapshot.Revision++;
                setup.ApplySnapshot();

                Button firstPairButton = setup.Window.FindControl<Button>(FirstPairButtonName)!;
                List<Image> buttonDice = CollectImages(firstPairButton.Content);

                Assert.Equal(ExpectedPairDiceCount, buttonDice.Count);
                Assert.Same(DiceImageCatalog.GetFaceImage(FirstDie), buttonDice[0].Source);
                Assert.Same(DiceImageCatalog.GetFaceImage(SecondDie), buttonDice[1].Source);
                Assert.Same(DiceImageCatalog.GetFaceImage(ThirdDie), buttonDice[2].Source);
                Assert.Same(DiceImageCatalog.GetFaceImage(FourthDie), buttonDice[3].Source);
                Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(firstPairButton)));
            });
        }

        [Fact]
        public void MatchWindow_TurnReadyAndCanStop_HighlightsRollAndStopButtons()
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchKeyboardBuilder setup = new MatchKeyboardBuilder();
                setup.Snapshot.Revision++;
                setup.Snapshot.AwaitingPairChoice = false;
                setup.Snapshot.CanStop = true;
                setup.ApplySnapshot();

                Button rollButton = setup.Window.FindControl<Button>(RollDiceButtonName)!;
                Button stopButton = setup.Window.FindControl<Button>(StopTurnButtonName)!;

                Assert.Contains(RollReadyClass, rollButton.Classes);
                Assert.Contains(StopReadyClass, stopButton.Classes);
            });
        }

        [Fact]
        public void MatchWindow_LeaveMatchClick_OpensConfirmationOverlayWithoutLeaving()
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchKeyboardBuilder setup = new MatchKeyboardBuilder();
                Button leaveButton = setup.Window.FindControl<Button>(LeaveMatchButtonName)!;
                Border overlay = setup.Window.FindControl<Border>(LeaveConfirmOverlayName)!;
                Button cancelButton = setup.Window.FindControl<Button>(CancelLeaveButtonName)!;

                Assert.False(overlay.IsVisible);

                leaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                Assert.True(overlay.IsVisible);

                cancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                Assert.False(overlay.IsVisible);
            });
        }

        private static List<Image> CollectImages(object? root)
        {
            List<Image> images = new List<Image>();
            if (root is Image image)
            {
                images.Add(image);
            }
            else if (root is Panel panel)
            {
                foreach (Control child in panel.Children)
                {
                    images.AddRange(CollectImages(child));
                }
            }
            else if (root is Decorator decorator)
            {
                images.AddRange(CollectImages(decorator.Child));
            }
            return images;
        }
    }
}
