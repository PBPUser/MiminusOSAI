using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>«Дефрагментация диска».</summary>
public sealed class DefragProgram : IProgram
{
    public string Id => "defrag";
    public string NameKey => "defrag.title";
    public IconId Icon => IconId.DriveHdd;
    public bool Singleton => true;
    public OsWindow Create(ShellHost shell, VNode document) => new DefragWindow();
}

/// <summary>«Таблица символов».</summary>
public sealed class CharMapProgram : IProgram
{
    public string Id => "charmap";
    public string NameKey => "charmap.title";
    public IconId Icon => IconId.TextFile;
    public bool Singleton => true;
    public OsWindow Create(ShellHost shell, VNode document) => new CharMapWindow();
}

/// <summary>«Диспетчер устройств».</summary>
public sealed class DeviceManagerProgram : IProgram
{
    public string Id => "devmgr";
    public string NameKey => "devmgr.title";
    public IconId Icon => IconId.MyComputer;
    public bool Singleton => true;
    public OsWindow Create(ShellHost shell, VNode document) => new DeviceManagerWindow();
}

/// <summary>«Редактор реестра».</summary>
public sealed class RegeditProgram : IProgram
{
    public string Id => "regedit";
    public string NameKey => "regedit.title";
    public IconId Icon => IconId.Registry;
    public bool Singleton => true;
    public OsWindow Create(ShellHost shell, VNode document) => new RegeditWindow();
}
