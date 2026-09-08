using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

// A custom program for МИМИНУС ОС.
//
// Build it against Miminus.Core.dll and drop the result into the OS's apps/
// folder — either beside the built-in programs or in a subfolder of its own,
// next to anything it depends on. The shell finds it by reflection at startup,
// remembers what it declares, and loads the assembly only when the program is
// actually run. Nothing else has to be edited to add it.
//
//   dotnet build samples/HelloProgram -c Release
//   copy the DLL into src/Host/bin/Debug/net10.0-windows/apps/
//
namespace Hello;

/// <summary>Tells the shell that this assembly offers a program.</summary>
public sealed class HelloProgram : IProgram
{
    /// <summary>Name used by --open, shortcuts and the Start menu.</summary>
    public string Id => "hello";

    /// <summary>Programs that ship with the system use a catalogue key here.
    /// A custom program can pass its own text instead: anything that is not a
    /// known key is shown as it is.</summary>
    public string NameKey => "Привет из своей программы";

    public IconId Icon => IconId.Star;

    /// <summary>Launching again focuses the window already open.</summary>
    public bool Singleton => true;

    public OsWindow Create(ShellHost shell, VNode document) => new HelloWindow();
}

/// <summary>The window. Drawing works the same way it does inside the system:
/// an immediate-mode pass over the client rectangle, using the shell's theme so
/// the program follows whatever МИМИНУС is wearing.</summary>
public sealed class HelloWindow : OsWindow
{
    int _clicks;

    public override string Title => "Привет";
    public override float MinWidth => 300;
    public override float MinHeight => 180;

    public HelloWindow()
    {
        Icon = IconId.Star;
        Bounds = new Rect(0, 0, 380, 240);
    }

    public override void OnOpened(UiContext c) => CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);

    public override void DrawClient(UiContext c, Rect client)
    {
        var t = c.Theme;
        c.R.FillRect(client, t.Face);

        var area = client.Deflate(16);

        Icons.Draw(c.R, IconId.Star, new Rect(area.X, area.Y, 32, 32));
        c.F.UiBold.Draw(c.R, "Своя программа", area.X + 42, area.Y + 4, t.Text);
        c.F.Small.Draw(c.R, "Загружена из apps/ по требованию",
                       area.X + 42, area.Y + 6 + c.F.UiBold.Height, t.TextDisabled);
        area.CutTop(52);

        c.F.Ui.Draw(c.R, _clicks == 0 ? "Нажмите кнопку." : $"Нажатий: {_clicks}",
                    area.X, area.Y, t.Text);
        area.CutTop(c.F.Ui.Height + 14);

        if (W.Button(c, Id + ".press", new Rect(area.X, area.Y, 140, 24), "Нажать"))
            _clicks++;
    }
}
