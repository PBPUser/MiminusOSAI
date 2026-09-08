using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>The five-tab property sheet, kept: «Дополнительно» on the
/// personalisation window is this, and so is everything that asked for it
/// before the two were separated.</summary>
public sealed class DisplayPropertiesProgram : IProgram
{
    public string Id => "displayprops";
    public string NameKey => "start.display_properties";
    public IconId Icon => IconId.Display;
    public bool Singleton => true;
    public OsWindow Create(ShellHost shell, VNode document) => new DisplayPropertiesWindow();
}

/// <summary>«Заставка» — the screen-saver tab, on its own.</summary>
public sealed class ScreenSaverProgram : IProgram
{
    public string Id => "screensaver";
    public string NameKey => "display.screen_saver";
    public IconId Icon => IconId.Lock;
    public OsWindow Create(ShellHost shell, VNode document) => new DisplayPropertiesWindow(2);
}

/// <summary>Оформление → Эффекты.</summary>
public sealed class EffectsProgram : IProgram
{
    public string Id => "effects";
    public string NameKey => "effects.title";
    public IconId Icon => IconId.Display;
    public OsWindow Create(ShellHost shell, VNode document) => new EffectsWindow(shell.Settings);
}

/// <summary>Дополнительное оформление.</summary>
public sealed class AppearanceProgram : IProgram
{
    public string Id => "appearance";
    public string NameKey => "appearance.title";
    public IconId Icon => IconId.Display;
    public OsWindow Create(ShellHost shell, VNode document) => new AdvancedAppearanceWindow();
}

/// <summary>Параметры → Дополнительно.</summary>
public sealed class AdvancedDisplayProgram : IProgram
{
    public string Id => "advanced";
    public string NameKey => "advanced.title";
    public IconId Icon => IconId.Display;
    public OsWindow Create(ShellHost shell, VNode document) => new AdvancedSettingsWindow(shell.Settings);
}

/// <summary>«Свойства панели задач и меню "Пуск"».</summary>
public sealed class TaskbarPropertiesProgram : IProgram
{
    public string Id => "taskbarprops";
    public string NameKey => "tbprops.title";
    public IconId Icon => IconId.Settings;
    public bool Singleton => true;
    public OsWindow Create(ShellHost shell, VNode document) => new TaskbarPropertiesWindow(shell);
}
