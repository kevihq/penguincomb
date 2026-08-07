using Avalonia;
using Avalonia.Headless;
using Honeycomb.App;

[assembly: AvaloniaTestApplication(typeof(Honeycomb.Tests.TestAppBuilder))]

namespace Honeycomb.Tests;

/// <summary>
/// Headless Avalonia platform (used by the xunit integration; the smoke tests in
/// AvaloniaSmokeTests bootstrap manually, but keeping this attribute is harmless).
/// </summary>
public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<Honeycomb.App.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
