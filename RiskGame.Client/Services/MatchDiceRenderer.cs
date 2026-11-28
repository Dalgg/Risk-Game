using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using RiskGame.Contracts.Match;
using RiskGame.Resources;

namespace RiskGame.Services
{
    public static class MatchDiceRenderer
    {
        private const double RolledDieSize = 44;
        private const double PairDieSize = 26;
        private const double PairGroupSpacing = 5;
        private const double PairContentSpacing = 8;
        private const double BadgeSize = 22;
        private const double BadgeCornerRadius = 11;
        private const double BadgeFontSize = 12;
        private const double SumFontSize = 15;
        private const double DividerWidth = 1;
        private const double DividerHeight = 24;
        private static readonly IBrush BadgeBackground = new ImmutableSolidColorBrush(Color.Parse("#181A25"));
        private static readonly IBrush GoldTextBrush = new ImmutableSolidColorBrush(Color.Parse("#F5D90A"));
        private static readonly IBrush SelectedSumBrush = new ImmutableSolidColorBrush(Color.Parse("#181A25"));
        private static readonly IBrush DividerBrush = new ImmutableSolidColorBrush(Color.Parse("#555D7A"));
        private static readonly IBrush SelectedDividerBrush = new ImmutableSolidColorBrush(Color.Parse("#6E5414"));

        public static void RenderRolledDice(StackPanel container, IEnumerable<int> diceValues)
        {
            container.Children.Clear();
            foreach (int dieValue in diceValues)
            {
                if (DiceImageCatalog.IsValidFace(dieValue))
                {
                    container.Children.Add(CreateDieImage(dieValue, RolledDieSize));
                }
            }
        }

        public static Control CreatePairOptionContent(PairOption pairOption, bool isSelected)
        {
            IBrush sumBrush = GoldTextBrush;
            IBrush dividerBrush = DividerBrush;
            if (isSelected)
            {
                sumBrush = SelectedSumBrush;
                dividerBrush = SelectedDividerBrush;
            }

            StackPanel content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = PairContentSpacing,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            content.Children.Add(CreateIndexBadge(pairOption.PairIndex));
            content.Children.Add(CreatePairGroup(pairOption.FirstPairDice, pairOption.FirstSum, sumBrush));
            content.Children.Add(new Border
            {
                Width = DividerWidth,
                Height = DividerHeight,
                Background = dividerBrush,
                VerticalAlignment = VerticalAlignment.Center
            });
            content.Children.Add(CreatePairGroup(pairOption.SecondPairDice, pairOption.SecondSum, sumBrush));
            return content;
        }

        public static string FormatPairOptionLabel(PairOption pairOption)
        {
            return string.Format(Strings.MatchPairOptionFormat, new object[]
            {
                pairOption.PairIndex,
                string.Join(Strings.MatchPairSeparator, pairOption.FirstPairDice),
                pairOption.FirstSum,
                string.Join(Strings.MatchPairSeparator, pairOption.SecondPairDice),
                pairOption.SecondSum
            });
        }

        private static Border CreateIndexBadge(int pairIndex)
        {
            return new Border
            {
                Width = BadgeSize,
                Height = BadgeSize,
                CornerRadius = new CornerRadius(BadgeCornerRadius),
                Background = BadgeBackground,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = pairIndex.ToString(CultureInfo.CurrentCulture),
                    Foreground = GoldTextBrush,
                    FontSize = BadgeFontSize,
                    FontWeight = FontWeight.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }

        private static StackPanel CreatePairGroup(IEnumerable<int> diceValues, int sum, IBrush sumBrush)
        {
            StackPanel group = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = PairGroupSpacing,
                VerticalAlignment = VerticalAlignment.Center
            };
            foreach (int dieValue in diceValues)
            {
                if (DiceImageCatalog.IsValidFace(dieValue))
                {
                    group.Children.Add(CreateDieImage(dieValue, PairDieSize));
                }
            }
            group.Children.Add(new TextBlock
            {
                Text = "=" + sum.ToString(CultureInfo.CurrentCulture),
                Foreground = sumBrush,
                FontSize = SumFontSize,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center
            });
            return group;
        }

        private static Image CreateDieImage(int dieValue, double size)
        {
            string valueText = dieValue.ToString(CultureInfo.CurrentCulture);
            Image image = new Image
            {
                Source = DiceImageCatalog.GetFaceImage(dieValue),
                Width = size,
                Height = size,
                VerticalAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(image, valueText);
            AutomationProperties.SetName(image, valueText);
            return image;
        }
    }
}
