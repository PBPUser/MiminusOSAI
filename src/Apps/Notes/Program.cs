using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>«Заметки».</summary>
public sealed class NotesProgram : IProgram
{
    public string Id => "notes";
    public string NameKey => "notes.title";
    public IconId Icon => IconId.TextFile;
    public bool Singleton => true;
    public OsWindow Create(ShellHost shell, VNode document) => new NotesWindow();
}
