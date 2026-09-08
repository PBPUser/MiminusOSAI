using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>«Часы и таймер» — the program that arrives from the store rather
/// than with the system. Its assembly is built into <c>store/</c>, so until
/// somebody installs it this class is never loaded and this id does not
/// exist.</summary>
public sealed class ClockAppProgram : IProgram
{
    public string Id => "clocktimer";
    public string NameKey => "clockapp.title";
    public IconId Icon => IconId.Clock;
    public bool Singleton => true;
    public OsWindow Create(ShellHost shell, VNode document) => new ClockWindow();
}
