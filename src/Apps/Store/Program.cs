using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>«Магазин Миминус».</summary>
public sealed class StoreProgram : IProgram
{
    public string Id => "store";
    public string NameKey => "store.title";
    public IconId Icon => IconId.Store;
    public bool Singleton => true;
    public OsWindow Create(ShellHost shell, VNode document) => new StoreWindow();
}
