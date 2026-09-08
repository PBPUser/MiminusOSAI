using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>«Погода».</summary>
public sealed class WeatherProgram : IProgram
{
    public string Id => "weather";
    public string NameKey => "weather.title";
    public IconId Icon => IconId.Weather;
    public bool Singleton => true;
    public OsWindow Create(ShellHost shell, VNode document) => new WeatherWindow();
}
