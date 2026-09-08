using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>Панель управления, in its own assembly.</summary>
public sealed class ControlPanelProgram : IProgram
{
    public string Id => "controlpanel";
    public string NameKey => "start.control_panel";
    public IconId Icon => IconId.ControlPanel;
    public bool Singleton => true;
    public OsWindow Create(ShellHost shell, VNode document) => new ControlPanelWindow();
}

/// <summary>«Персонализация» — the Control Panel, opened at that page. It is
/// the same frame under a different name, which is what version 8 did: the
/// window the desktop menu opens is the Control Panel standing somewhere
/// else.</summary>
public sealed class PersonalisationProgram : IProgram
{
    public string Id => "personalise";
    public string NameKey => "person.title";
    public IconId Icon => IconId.Display;
    public OsWindow Create(ShellHost shell, VNode document)
        => new ControlPanelWindow(ControlPanelWindow.Page.Personalise);
}

/// <summary>«Разрешение экрана» — likewise.</summary>
public sealed class ScreenResolutionProgram : IProgram
{
    public string Id => "screenres";
    public string NameKey => "screen.title";
    public IconId Icon => IconId.Devices;
    public OsWindow Create(ShellHost shell, VNode document)
        => new ControlPanelWindow(ControlPanelWindow.Page.Screen);
}

/// <summary>The old «Свойства: Экран» shortcut, which now leads to the
/// personalisation page like everything else that used to open the sheet.</summary>
public sealed class DisplayShortcutProgram : IProgram
{
    public string Id => "display";
    public string NameKey => "person.title";
    public IconId Icon => IconId.Display;
    public OsWindow Create(ShellHost shell, VNode document)
        => new ControlPanelWindow(ControlPanelWindow.Page.Personalise);
}

/// <summary>«Центр обновления МИМИНУС» — the same frame again, standing on the
/// update page. It was a window of its own until version 8; seven had already
/// made it a Control Panel page everywhere but here.</summary>
public sealed class UpdateProgram : IProgram
{
    public string Id => "update";
    public string NameKey => "update.title";
    public IconId Icon => IconId.Shield;
    public bool Singleton => true;
    public OsWindow Create(ShellHost shell, VNode document)
        => new ControlPanelWindow(ControlPanelWindow.Page.Update);
}
