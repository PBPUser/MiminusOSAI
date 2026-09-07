using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>«Что нового в МИМИНУС ОС» — the tour shown once after an update,
/// and available afterwards from the Start menu.</summary>
public sealed class WhatsNewProgram : IProgram
{
    public string Id => "whatsnew";
    public string NameKey => "whatsnew.title";
    public IconId Icon => IconId.Star;
    public bool Singleton => true;
    public OsWindow Create(ShellHost shell, VNode document) => new WhatsNewWindow(shell);
}
