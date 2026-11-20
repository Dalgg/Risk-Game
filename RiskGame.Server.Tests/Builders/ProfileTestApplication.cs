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

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<ProfileTestApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
        }

        public static void Run(Action action)
        {
            using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(ProfileTestApplication));
            session.Dispatch(action, CancellationToken.None).GetAwaiter().GetResult();
        }

        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
            Resources.MergedDictionaries.Add(new ResourceInclude(new Uri(ResourceRoot))
            {
                Source = new Uri(ResourceRoot + "Assets/ProfileImages/ProfileImages.axaml")
            });
            Styles.Add(new StyleInclude(new Uri(ResourceRoot))
            {
                Source = new Uri(ResourceRoot + "Styles/AppTheme.axaml")
            });
        }
    }
}
