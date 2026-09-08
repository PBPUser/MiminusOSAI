using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>«Параметры компьютера» — the settings the charms bar points at.</summary>
public sealed class PcSettingsProgram : IProgram
{
    public string Id => "pcsettings";
    public string NameKey => "pcs.title";
    public IconId Icon => IconId.PcSettings;
    public bool Singleton => true;
    public OsWindow Create(ShellHost shell, VNode document) => new PcSettingsWindow();
}
