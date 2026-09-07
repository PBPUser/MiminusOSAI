using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>Калькулятор Плюс.</summary>
public sealed class CalculatorProgram : IProgram
{
    public string Id => "calculator";
    public string NameKey => "start.calculator_plus";
    public IconId Icon => IconId.Calculator;
    public bool Singleton => true;
    public OsWindow Create(ShellHost shell, VNode document) => new CalculatorWindow();
}
