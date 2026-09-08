using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>Диспетчер задач. Its own assembly, so the window that lists what is
/// loaded is itself something the system loads on demand and lets go of.</summary>
public sealed class TaskManagerProgram : IProgram
{
    public string Id => "taskmgr";
    public string NameKey => "taskbar.task_manager";
    public IconId Icon => IconId.Settings;
    public bool Singleton => true;
    public OsWindow Create(ShellHost shell, VNode document) => new TaskManagerWindow();
}
