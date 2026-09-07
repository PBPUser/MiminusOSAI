using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>«Зевште», the image editor that for now opens by way of Paint.</summary>
public sealed class PaintProgram : IProgram
{
    public string Id => "paint";
    public string NameKey => "paint.title";
    public IconId Icon => IconId.Paint;
    public OsWindow Create(ShellHost shell, VNode document) => new PaintWindow(document);
}
