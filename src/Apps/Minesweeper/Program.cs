using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>Сапер — "игра про смайлик".</summary>
public sealed class MinesweeperProgram : IProgram
{
    public string Id => "minesweeper";
    public string NameKey => "start.minesweeper";
    public IconId Icon => IconId.Minesweeper;
    public bool Singleton => true;
    public OsWindow Create(ShellHost shell, VNode document) => new MinesweeperWindow();
}
