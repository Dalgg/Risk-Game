using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using RiskGame.Contracts.Common;
using RiskGame.Contracts.Lobby;
using RiskGame.Resources;

namespace RiskGame.Views
{
    public partial class MatchWindow : Window
    {
        private const int PlayerNameFontSize = 16;
        private const int PlayerCardCornerRadius = 6;
        private const int CardHorizontalPadding = 12;
        private const int CardVerticalPadding = 10;
        private const int ColorIndicatorSize = 18;
        private const int ColorIndicatorSpacing = 10;
        private static readonly IBrush PlayerCardBackground = new SolidColorBrush(Color.Parse("#25293A"));
        private static readonly IBrush RedPlayerBrush = new SolidColorBrush(Color.Parse("#E5484D"));
        private static readonly IBrush BluePlayerBrush = new SolidColorBrush(Color.Parse("#3E63DD"));
        private static readonly IBrush GreenPlayerBrush = new SolidColorBrush(Color.Parse("#30A46C"));
        private static readonly IBrush YellowPlayerBrush = new SolidColorBrush(Color.Parse("#F5D90A"));

        public MatchWindow()
        {
            InitializeComponent();
        }

        public MatchWindow(int matchId, IReadOnlyList<LobbyPlayer> matchPlayers) : this()
        {
            MatchIdTextBlock.Text = string.Format(Strings.MatchIdFormat, matchId);
            RenderPlayers(matchPlayers);
        }

        private void RenderPlayers(IReadOnlyList<LobbyPlayer> matchPlayers)
        {
            MatchPlayersStackPanel.Children.Clear();

            foreach (LobbyPlayer matchPlayer in matchPlayers)
            {
                Border playerCard = CreatePlayerCard(matchPlayer);
                MatchPlayersStackPanel.Children.Add(playerCard);
            }
        }

        private static Border CreatePlayerCard(LobbyPlayer matchPlayer)
        {
            string displayName = Strings.LobbyDefaultPlayerName;
            if (matchPlayer.Player != null && !string.IsNullOrWhiteSpace(matchPlayer.Player.Nickname))
            {
                displayName = matchPlayer.Player.Nickname;
            }

            Border colorIndicator = new Border
            {
                Background = ResolvePlayerBrush(matchPlayer.Color),
                Width = ColorIndicatorSize,
                Height = ColorIndicatorSize,
                CornerRadius = new CornerRadius(ColorIndicatorSize),
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock nameBlock = new TextBlock
            {
                Text = displayName,
                Foreground = Brushes.White,
                FontSize = PlayerNameFontSize,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };

            StackPanel cardContent = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = ColorIndicatorSpacing
            };
            cardContent.Children.Add(colorIndicator);
            cardContent.Children.Add(nameBlock);

            return new Border
            {
                Background = PlayerCardBackground,
                CornerRadius = new CornerRadius(PlayerCardCornerRadius),
                Padding = new Thickness(CardHorizontalPadding, CardVerticalPadding),
                Child = cardContent
            };
        }

        private static IBrush ResolvePlayerBrush(PlayerColor playerColor)
        {
            switch (playerColor)
            {
                case PlayerColor.Blue:
                    return BluePlayerBrush;
                case PlayerColor.Green:
                    return GreenPlayerBrush;
                case PlayerColor.Yellow:
                    return YellowPlayerBrush;
                default:
                    return RedPlayerBrush;
            }
        }

        private void LeaveMatchClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            if (Application.Current != null &&
                Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                MainWindow mainWindow = new MainWindow();
                desktopLifetime.MainWindow = mainWindow;
                mainWindow.Show();
                Close();
            }
        }
    }
}