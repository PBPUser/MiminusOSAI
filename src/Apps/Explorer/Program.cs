using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>Проводник, opening a folder.</summary>
public sealed class ExplorerProgram : IProgram
{
    public string Id => "explorer";
    public string NameKey => "start.windows_explorer";
    public IconId Icon => IconId.Folder;
    public OsWindow Create(ShellHost shell, VNode document)
        => new ExplorerWindow(document is { IsContainer: true } ? document : shell.Fs.Desktop);
}

/// <summary>The same window rooted at My Computer.</summary>
public sealed class MyComputerProgram : IProgram
{
    public string Id => "mycomputer";
    public string NameKey => "start.my_computer";
    public IconId Icon => IconId.MyComputer;
    public OsWindow Create(ShellHost shell, VNode document) => new ExplorerWindow(shell.Fs.MyComputer);
}
