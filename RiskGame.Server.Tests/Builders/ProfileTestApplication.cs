using System;
using System.Threading;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;

namespace RiskGame.Server.Tests.Builders
{
    public sealed class ProfileTestApplication : Application
    {
        private const string ResourceRoot = "avares://RiskGame.Client/";
        private const string ProfileImagesResourcePath = "Assets/ProfileImages/ProfileImages.axaml";
        private const string DiceImagesResourcePath = "Assets/DiceImages/DiceImages.axaml";
        private const string AppThemeStylePath = "Styles/AppTheme.axaml";
        private static readonly object testSessionLock = new object();
        private static readonly Lazy<HeadlessUnitTestSession> testSession =
            new Lazy<HeadlessUnitTestSession>(CreateTestSession);

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<ProfileTestApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
        }

        public static void Run(Action action)
        {
            lock (testSessionLock)
            {
                testSession.Value.Dispatch(action, CancellationToken.None).GetAwaiter().GetResult();
            }
        }

        private static HeadlessUnitTestSession CreateTestSession()
        {
            return HeadlessUnitTestSession.StartNew(typeof(ProfileTestApplication),
                AvaloniaTestIsolationLevel.PerAssembly);
        }

        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
            Resources.MergedDictionaries.Add(new ResourceInclude(new Uri(ResourceRoot))
            {
                Source = new Uri(ResourceRoot + ProfileImagesResourcePath)
            });
            Resources.MergedDictionaries.Add(new ResourceInclude(new Uri(ResourceRoot))
            {
                Source = new Uri(ResourceRoot + DiceImagesResourcePath)
            });
            Styles.Add(new StyleInclude(new Uri(ResourceRoot))
            {
                Source = new Uri(ResourceRoot + AppThemeStylePath)
            });
        }
    }
}
