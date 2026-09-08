using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>«Фотографии».</summary>
public sealed class PhotosProgram : IProgram
{
    public string Id => "photos";
    public string NameKey => "photos.title";
    public IconId Icon => IconId.MyPictures;
    public bool Singleton => true;
    public OsWindow Create(ShellHost shell, VNode document) => new PhotosWindow();
}
