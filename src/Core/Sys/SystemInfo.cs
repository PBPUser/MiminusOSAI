using Miminus.Platform;

namespace Miminus.Sys;

/// <summary>Read-only facts about the machine, for the About box and the
/// display Advanced sheet.
///
/// Programs live in their own assemblies and have no business touching the GL
/// bindings directly, so the few strings they want are surfaced here.</summary>
public static class SystemInfo
{
    public static string Renderer => GL.GetString(GL.RENDERER);
    public static string GlVersion => GL.GetString(GL.VERSION);
    public static string GlslVersion => GL.GetString(GL.SHADING_LANGUAGE_VERSION);
    public static string Vendor => GL.GetString(GL.VENDOR);
}
