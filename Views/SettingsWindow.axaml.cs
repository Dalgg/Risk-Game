using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using RiskGame.Resources;

namespace RiskGame.Views
{

    public partial class SettingsWindow : Window
    {

        private const int SpanishOptionIndex = 0;

        private const int EnglishOptionIndex = 1;

        public SettingsWindow()
        {
            InitializeComponent();

            bool isEnglishActive = Strings.Culture != null && Strings.Culture.Name == "en-US";
            LanguageComboBox.SelectedIndex = isEnglishActive ? EnglishOptionIndex : SpanishOptionIndex;
        }

        private void OnLanguageChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox comboBox)
            {
                return;
            }

            switch (comboBox.SelectedIndex)
            {
                case SpanishOptionIndex:
                    Strings.Culture = new CultureInfo("es");
                    break;

                case EnglishOptionIndex:
                    Strings.Culture = new CultureInfo("en-US");
                    break;
            }
        }

        private void OnBackClick(object? sender, RoutedEventArgs e)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                MainWindow mainWindow = new MainWindow();
                desktopLifetime.MainWindow = mainWindow;
                mainWindow.Show();
                Close();
            }
        }
    }
}