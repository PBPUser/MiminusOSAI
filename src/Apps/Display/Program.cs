using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>Свойства: Экран.</summary>
public sealed class DisplayProgram : IProgram
{
    public string Id => "display";
    public string NameKey => "start.display_properties";
    public IconId Icon => IconId.Display;
    public bool Singleton => true;
    public OsWindow Create(ShellHost shell, VNode document) => new DisplayPropertiesWindow();
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
