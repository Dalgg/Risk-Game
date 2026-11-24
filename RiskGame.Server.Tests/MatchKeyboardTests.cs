extern alias Client;

using Avalonia.Controls;
using Avalonia.Input;
using RiskGame.Server.Tests.Builders;
using Xunit;
using PairChoice = Client::RiskGame.Contracts.Match.PairChoice;

namespace RiskGame.Server.Tests
{
    public sealed class MatchKeyboardTests
    {
        private const int FirstPair = 1;
        private const int SecondPair = 2;
        private const int ThirdPair = 3;
        private const int FirstOptionIndex = 0;
        private const string FirstColumnChoiceButtonName = "FirstColumnChoiceButton";
        private const string ColumnChoicePanelName = "ColumnChoicePanel";

        [Theory]
        [InlineData(Key.D1, FirstPair)]
        [InlineData(Key.D2, SecondPair)]
        [InlineData(Key.D3, ThirdPair)]
        [InlineData(Key.NumPad1, FirstPair)]
        [InlineData(Key.NumPad2, SecondPair)]
        [InlineData(Key.NumPad3, ThirdPair)]
        public void NumberKey_AvailableCombination_SelectsMatchingPair(Key key, int expectedPair)
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchKeyboardBuilder setup = new MatchKeyboardBuilder();

                setup.PressKey(key);
                PairChoice choice = (PairChoice)setup.Window.FindControl<Button>(FirstColumnChoiceButtonName)!.Tag!;

                Assert.Equal(expectedPair, choice.PairIndex);
            });
        }

        [Fact]
        public void NumberKey_AnotherPlayersTurn_DoesNotSelectPair()
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchKeyboardBuilder setup = new MatchKeyboardBuilder();
                setup.Snapshot.CurrentTurnUserId = MatchKeyboardBuilder.OtherUserId;
                setup.ApplySnapshot();

                setup.PressKey(Key.D1);

                Assert.False(setup.Window.FindControl<StackPanel>(ColumnChoicePanelName)!.IsVisible);
            });
        }

        [Fact]
        public void NumberKey_UnplayableCombination_DoesNotSelectPair()
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchKeyboardBuilder setup = new MatchKeyboardBuilder();
                setup.Snapshot.PairOptions[FirstOptionIndex].IsPlayable = false;
                setup.ApplySnapshot();

                setup.PressKey(Key.D1);

                Assert.False(setup.Window.FindControl<StackPanel>(ColumnChoicePanelName)!.IsVisible);
            });
        }

        [Theory]
        [InlineData(KeyModifiers.Control)]
        [InlineData(KeyModifiers.Alt)]
        [InlineData(KeyModifiers.Shift)]
        public void NumberKey_WithModifier_DoesNotSelectPair(KeyModifiers modifiers)
        {
            ProfileTestApplication.Run(() =>
            {
                using MatchKeyboardBuilder setup = new MatchKeyboardBuilder();

                setup.PressKey(Key.D1, modifiers);

                Assert.False(setup.Window.FindControl<StackPanel>(ColumnChoicePanelName)!.IsVisible);
            });
        }
    }
}
