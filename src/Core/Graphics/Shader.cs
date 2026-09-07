using Miminus.Platform;

namespace Miminus.Graphics;

/// <summary>A linked GLSL program plus a small uniform-location cache.</summary>
public sealed class Shader : IDisposable
{
    public uint Program { get; private set; }
    readonly Dictionary<string, int> _uniforms = new();
    readonly string _label;

    public Shader(string label, string vertexSource, string fragmentSource)
    {
        _label = label;

        uint vs = GL.CreateShader(GL.VERTEX_SHADER);
        GL.ShaderSource(vs, vertexSource);
        GL.CompileShader(vs, label + ".vert");

        uint fs = GL.CreateShader(GL.FRAGMENT_SHADER);
        GL.ShaderSource(fs, fragmentSource);
        GL.CompileShader(fs, label + ".frag");

        Program = GL.CreateProgram();
        GL.AttachShader(Program, vs);
        GL.AttachShader(Program, fs);
        GL.LinkProgram(Program);
        GL.CheckLink(Program, label);

        GL.DeleteShader(vs);
        GL.DeleteShader(fs);
    }

    public void Use() => GL.UseProgram(Program);

    public int Loc(string name)
    {
        if (_uniforms.TryGetValue(name, out int loc)) return loc;
        loc = GL.GetUniformLocation(Program, name);
        _uniforms[name] = loc;
        return loc;
    }

    public void Set(string name, int v) => GL.Uniform1i(Loc(name), v);
    public void Set(string name, float v) => GL.Uniform1f(Loc(name), v);
    public void Set(string name, float a, float b) => GL.Uniform2f(Loc(name), a, b);
    public void Set(string name, float a, float b, float c) => GL.Uniform3f(Loc(name), a, b, c);
    public void Set(string name, float a, float b, float c, float d) => GL.Uniform4f(Loc(name), a, b, c, d);
    public void Set(string name, Color c) => GL.Uniform4f(Loc(name), c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);

    public override string ToString() => $"Shader({_label})";

    public void Dispose()
    {
        if (Program != 0) { GL.DeleteProgram(Program); Program = 0; }
    }
}
