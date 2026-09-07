using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>«Всё в одном» — the utility that "is suitable for everything".</summary>
public sealed class AllInOneProgram : IProgram
{
    public string Id => "allinone";
    public string NameKey => "allinone.title";
    public IconId Icon => IconId.Settings;
    public bool Singleton => true;
    public OsWindow Create(ShellHost shell, VNode document) => new AllInOneWindow();
}

/// <summary>«Распознавание голоса», still unfinished.</summary>
public sealed class VoiceProgram : IProgram
{
    public string Id => "voice";
    public string NameKey => "voice.title";
    public IconId Icon => IconId.Volume;
    public bool Singleton => true;
    public OsWindow Create(ShellHost shell, VNode document) => new VoiceRecognitionWindow();
}
