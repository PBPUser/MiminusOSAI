using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>«Центр обновления МИМИНУС» — reads the version manifest published
/// in the project's GitHub repository.</summary>
public sealed class UpdateProgram : IProgram
{
    public string Id => "update";
    public string NameKey => "update.title";
    public IconId Icon => IconId.Shield;
    public bool Singleton => true;
    public OsWindow Create(ShellHost shell, VNode document) => new UpdateWindow(shell);
}
