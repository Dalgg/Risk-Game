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
            if (isEnglishActive)
            {
                LanguageComboBox.SelectedIndex = EnglishOptionIndex;
            }
            else
            {
                LanguageComboBox.SelectedIndex = SpanishOptionIndex;
            }
        }

        private void OnLanguageChanged(object? sender, SelectionChangedEventArgs selectionChangedEventArgs)
        {
            if (sender is ComboBox comboBox)
            {
                if (comboBox.SelectedIndex == SpanishOptionIndex)
                {
                    Strings.Culture = new CultureInfo("es");
                }
                else if (comboBox.SelectedIndex == EnglishOptionIndex)
                {
                    Strings.Culture = new CultureInfo("en-US");
                }
            }
        }

        private void BackClick(object? sender, RoutedEventArgs routedEventArgs)
        {
            if (Application.Current != null && Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
            {
                MainWindow mainWindow = new MainWindow();
                desktopLifetime.MainWindow = mainWindow;
                mainWindow.Show();
                Close();
            }
        }
    }
}