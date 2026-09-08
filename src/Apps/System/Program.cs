using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>Командная строка.</summary>
public sealed class TerminalProgram : IProgram
{
    public string Id => "terminal";
    public string NameKey => "start.command_prompt";
    public IconId Icon => IconId.Terminal;
    public OsWindow Create(ShellHost shell, VNode document) => new TerminalWindow();
}

/// <summary>Свойства: Звуки и аудиоустройства.</summary>
public sealed class SoundProgram : IProgram
{
    public string Id => "sound";
    public string NameKey => "cpl.sounds_and_audio_devices";
    public IconId Icon => IconId.Volume;
    public OsWindow Create(ShellHost shell, VNode document) => new SoundPropertiesWindow();
}

/// <summary>Язык и региональные стандарты.</summary>
public sealed class LanguageProgram : IProgram
{
    public string Id => "language";
    public string NameKey => "cpl.regional_and_language_options";
    public IconId Icon => IconId.Flag;
    public OsWindow Create(ShellHost shell, VNode document) => new LanguageOptionsWindow();
}
