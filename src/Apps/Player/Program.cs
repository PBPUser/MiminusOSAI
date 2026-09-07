using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>Проигрыватель Миминус, including the BolgenOS TV clip.</summary>
public sealed class MediaPlayerProgram : IProgram
{
    public string Id => "player";
    public string NameKey => "start.miminus_media_player";
    public IconId Icon => IconId.MediaPlayer;
    public OsWindow Create(ShellHost shell, VNode document) => new MediaPlayerWindow(document);
}
