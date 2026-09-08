using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>«Центр специальных возможностей».</summary>
public sealed class EaseOfAccessProgram : IProgram
{
    public string Id => "access";
    public string NameKey => "access.title";
    public IconId Icon => IconId.Access;
    public bool Singleton => true;
    public OsWindow Create(ShellHost shell, VNode document) => new EaseOfAccessWindow();
}

/// <summary>«Электропитание».</summary>
public sealed class PowerOptionsProgram : IProgram
{
    public string Id => "power";
    public string NameKey => "power.title";
    public IconId Icon => IconId.Power;
    public bool Singleton => true;
    public OsWindow Create(ShellHost shell, VNode document) => new PowerOptionsWindow();
}
