using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>«Моцелла Фигефох 3.6», the replacement browser from part 2.</summary>
public sealed class BrowserProgram : IProgram
{
    public string Id => "browser";
    public string NameKey => "browser.figefoch";
    public IconId Icon => IconId.Firefox;
    public OsWindow Create(ShellHost shell, VNode document)
        => new BrowserWindow(BrowserBrand.Figefoch);
}

/// <summary>«Орега», the older and slower browser from part 1.</summary>
public sealed class OregaProgram : IProgram
{
    public string Id => "orega";
    public string NameKey => "browser.orega";
    public IconId Icon => IconId.Opera;
    public OsWindow Create(ShellHost shell, VNode document)
        => new BrowserWindow(BrowserBrand.Orega);
}
