using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(TaskApp.Tests.AvaloniaTestApp))]

namespace TaskApp.Tests;

public class AvaloniaTestApp : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        var appRoot = new Uri("avares://TaskApp/");
        Styles.Add(new StyleInclude(appRoot) { Source = new Uri(appRoot, "Styles/AppStyles.axaml") });
        Resources.ThemeDictionaries.Add(App.ForestTheme, new ResourceInclude(appRoot)
        {
            Source = new Uri(appRoot, "Styles/ForestTheme.axaml")
        });
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<AvaloniaTestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
