using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>EXCE1 — "принципиально новый офис".</summary>
public sealed class SpreadsheetProgram : IProgram
{
    public string Id => "spreadsheet";
    public string NameKey => "sheet.miminus_sheet";
    public IconId Icon => IconId.Spreadsheet;
    public OsWindow Create(ShellHost shell, VNode document) => new SpreadsheetWindow(document);
}
