namespace Miminus.Shell;

/// <summary>Implemented by a window that can start the antivirus scan without
/// the shell having to know its concrete type.</summary>
public interface IScannable
{
    void BeginScan();
}
