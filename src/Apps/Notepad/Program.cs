using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>Блокнот, which doubles as the Grevtsov antivirus when it happens to
/// have АНТИВИРУС ГРЕВЦОВА.txt open.</summary>
public sealed class NotepadProgram : IProgram
{
    public string Id => "notepad";
    public string NameKey => "start.notepad";
    public IconId Icon => IconId.Notepad;
    public OsWindow Create(ShellHost shell, VNode document) => new NotepadWindow(document);
}
