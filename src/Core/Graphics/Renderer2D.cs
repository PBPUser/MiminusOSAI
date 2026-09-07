using System.Runtime.InteropServices;
using Miminus.Platform;

namespace Miminus.Graphics;

/// <summary>The one and only drawing surface in the app.
///
/// Everything — window chrome, icons, glyphs, the mouse pointer — becomes quads
/// in a single vertex stream. Each vertex carries enough state (shape centre,
/// half-extents, corner radius, border width) that the fragment shader can do a
/// signed-distance rounded rectangle without a second pipeline, and untextured
/// draws simply flag the sampler off, so solid fills never break the batch.</summary>
public sealed unsafe class Renderer2D : IDisposable
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct Vertex
    {
        public float X, Y;
        public float U, V;
        public uint Color;
        public uint Border;
        public float CX, CY, HX, HY;      // shape centre + half extents
        public float Radius, BorderW, Mode, TexWeight;
        public float ClipL, ClipT, ClipR, ClipB;
    }

    const int MaxQuads = 8192;
    const int MaxVerts = MaxQuads * 4;
    const int MaxIndices = MaxQuads * 6;

    readonly Vertex[] _verts = new Vertex[MaxVerts];
    int _vertCount;
    int _quadCount;

    uint _vao, _vbo, _ibo;
    readonly Shader _shader;
    readonly Texture _white;
    uint _currentTex;

    int _screenW, _screenH;

    readonly List<Rect> _clipStack = new();

    public int DrawCalls { get; private set; }
    public int QuadsThisFrame { get; private set; }

    const string VertSrc = @"#version 330 core
layout(location=0) in vec2 aPos;
layout(location=1) in vec2 aUV;
layout(location=2) in vec4 aColor;
layout(location=3) in vec4 aBorderColor;
layout(location=4) in vec4 aShape;
layout(location=5) in vec4 aParams;
layout(location=6) in vec4 aClip;

uniform vec2 uScreen;

out vec2 vUV;
out vec4 vColor;
out vec4 vBorder;
out vec4 vShape;
out vec4 vParams;
out vec4 vClip;
out vec2 vPos;

void main()
{
    vUV = aUV;
    vColor = aColor;
    vBorder = aBorderColor;
    vShape = aShape;
    vParams = aParams;
    vClip = aClip;
    vPos = aPos;
    vec2 ndc = vec2(aPos.x / uScreen.x * 2.0 - 1.0, 1.0 - aPos.y / uScreen.y * 2.0);
    gl_Position = vec4(ndc, 0.0, 1.0);
}
";

    const string FragSrc = @"#version 330 core
in vec2 vUV;
in vec4 vColor;
in vec4 vBorder;
in vec4 vShape;
in vec4 vParams;
in vec4 vClip;
in vec2 vPos;

uniform sampler2D uTex;

out vec4 FragColor;

// Signed distance to a rounded box centred at the origin.
float sdRoundBox(vec2 p, vec2 b, float r)
{
    vec2 q = abs(p) - b + r;
    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
}

void main()
{
    // Clipping is per-fragment rather than via glScissor: a scissor change would
    // force the batch to flush, and the UI changes clip constantly.
    if (vPos.x < vClip.x || vPos.y < vClip.y || vPos.x > vClip.z || vPos.y > vClip.w)
        discard;

    vec4 col = vColor;
    if (vParams.w > 0.5)
        col *= texture(uTex, vUV);

    if (vParams.z > 0.5)
    {
        vec2 p = vPos - vShape.xy;
        vec2 b = vShape.zw;
        float r = clamp(vParams.x, 0.0, min(b.x, b.y));
        float d = sdRoundBox(p, b, r);

        float aa = 0.7;
        float fill = 1.0 - smoothstep(-aa, aa, d);
        float bw = vParams.y;

        if (bw > 0.0)
        {
            float inner = 1.0 - smoothstep(-aa, aa, d + bw);
            vec4 res;
            res.rgb = mix(vBorder.rgb, col.rgb, inner);
            res.a = mix(vBorder.a, col.a, inner) * fill;
            col = res;
        }
        else
        {
            col.a *= fill;
        }
    }

    if (col.a <= 0.002) discard;
    FragColor = col;
}
";

    public Renderer2D()
    {
        _shader = new Shader("renderer2d", VertSrc, FragSrc);
        _white = Texture.White1x1();

        _vao = GL.GenVertexArray();
        GL.BindVertexArray(_vao);

        _vbo = GL.GenBuffer();
        GL.BindBuffer(GL.ARRAY_BUFFER, _vbo);
        GL.BufferData(GL.ARRAY_BUFFER, MaxVerts * sizeof(Vertex), null, GL.DYNAMIC_DRAW);

        // Quad indices never change, so they are uploaded once and reused.
        uint[] indices = new uint[MaxIndices];
        for (int i = 0, v = 0; i < MaxIndices; i += 6, v += 4)
        {
            indices[i + 0] = (uint)(v + 0);
            indices[i + 1] = (uint)(v + 1);
            indices[i + 2] = (uint)(v + 2);
            indices[i + 3] = (uint)(v + 0);
            indices[i + 4] = (uint)(v + 2);
            indices[i + 5] = (uint)(v + 3);
        }
        _ibo = GL.GenBuffer();
        GL.BindBuffer(GL.ELEMENT_ARRAY_BUFFER, _ibo);
        fixed (uint* pi = indices)
            GL.BufferData(GL.ELEMENT_ARRAY_BUFFER, MaxIndices * sizeof(uint), pi, GL.STATIC_DRAW);

        int stride = sizeof(Vertex);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, GL.FLOAT, false, stride, 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, GL.FLOAT, false, stride, 8);
        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(2, 4, GL.UNSIGNED_BYTE, true, stride, 16);
        GL.EnableVertexAttribArray(3);
        GL.VertexAttribPointer(3, 4, GL.UNSIGNED_BYTE, true, stride, 20);
        GL.EnableVertexAttribArray(4);
        GL.VertexAttribPointer(4, 4, GL.FLOAT, false, stride, 24);
        GL.EnableVertexAttribArray(5);
        GL.VertexAttribPointer(5, 4, GL.FLOAT, false, stride, 40);
        GL.EnableVertexAttribArray(6);
        GL.VertexAttribPointer(6, 4, GL.FLOAT, false, stride, 56);

        GL.BindVertexArray(0);
    }

    // ---- frame -----------------------------------------------------------

    public void Begin(int screenW, int screenH)
    {
        _screenW = screenW;
        _screenH = screenH;
        DrawCalls = 0;
        QuadsThisFrame = 0;
        _clipStack.Clear();
        SetClip(FullScreen);
        _currentTex = _white.Id;

        GL.Viewport(0, 0, screenW, screenH);
        GL.Disable(GL.DEPTH_TEST);
        GL.Enable(GL.BLEND);
        GL.BlendFuncSeparate(GL.SRC_ALPHA, GL.ONE_MINUS_SRC_ALPHA, GL.ONE, GL.ONE_MINUS_SRC_ALPHA);

        _shader.Use();
        _shader.Set("uScreen", (float)screenW, (float)screenH);
        _shader.Set("uTex", 0);
    }

    public void End() => Flush();

    public void Clear(Color c)
    {
        GL.ClearColor(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
        GL.Clear(GL.COLOR_BUFFER_BIT);
    }

    public void Flush()
    {
        if (_quadCount == 0) return;

        GL.BindVertexArray(_vao);
        GL.BindBuffer(GL.ARRAY_BUFFER, _vbo);
        fixed (Vertex* pv = _verts)
            GL.BufferSubData(GL.ARRAY_BUFFER, 0, _vertCount * sizeof(Vertex), pv);

        _shader.Use();
        _shader.Set("uScreen", (float)_screenW, (float)_screenH);
        GL.ActiveTexture(GL.TEXTURE0);
        GL.BindTexture(GL.TEXTURE_2D, _currentTex);
        GL.DrawElements(GL.TRIANGLES, _quadCount * 6, GL.UNSIGNED_INT, 0);
        GL.BindVertexArray(0);

        DrawCalls++;
        QuadsThisFrame += _quadCount;
        _vertCount = 0;
        _quadCount = 0;
    }

    void UseTexture(uint id)
    {
        if (id != _currentTex)
        {
            Flush();
            _currentTex = id;
        }
    }

    // ---- clipping --------------------------------------------------------

    public Rect CurrentClip => _clipStack.Count > 0 ? _clipStack[^1] : new Rect(0, 0, _screenW, _screenH);

    public void PushClip(Rect r)
    {
        Rect eff = _clipStack.Count > 0 ? r.Intersect(_clipStack[^1]) : r;
        _clipStack.Add(eff);
        SetClip(eff);
    }

    public void PopClip()
    {
        if (_clipStack.Count == 0) return;
        _clipStack.RemoveAt(_clipStack.Count - 1);
        SetClip(_clipStack.Count > 0 ? _clipStack[^1] : FullScreen);
    }

    Rect FullScreen => new(0, 0, _screenW, _screenH);

    // Current clip, written into every vertex. Changing it costs nothing.
    float _clipL, _clipT, _clipR, _clipB;

    void SetClip(Rect r)
    {
        _clipL = r.X;
        _clipT = r.Y;
        _clipR = r.Right;
        _clipB = r.Bottom;
    }

    /// <summary>True when the rect is entirely outside the active clip, so callers
    /// can skip whole subtrees of drawing.</summary>
    public bool IsClipped(Rect r)
    {
        if (_clipStack.Count == 0) return false;
        return !r.Intersects(_clipStack[^1]);
    }

    // ---- primitives ------------------------------------------------------

    void PushQuad(
        float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3,
        float u0, float v0, float u1, float v1,
        uint c0, uint c1, uint c2, uint c3,
        uint border, float cx, float cy, float hx, float hy,
        float radius, float borderW, float mode, float texWeight)
    {
        if (_quadCount >= MaxQuads) Flush();

        int i = _vertCount;
        ref Vertex a = ref _verts[i + 0];
        ref Vertex b = ref _verts[i + 1];
        ref Vertex c = ref _verts[i + 2];
        ref Vertex d = ref _verts[i + 3];

        a.X = x0; a.Y = y0; a.U = u0; a.V = v0; a.Color = c0;
        b.X = x1; b.Y = y1; b.U = u1; b.V = v0; b.Color = c1;
        c.X = x2; c.Y = y2; c.U = u1; c.V = v1; c.Color = c2;
        d.X = x3; d.Y = y3; d.U = u0; d.V = v1; d.Color = c3;

        a.Border = b.Border = c.Border = d.Border = border;
        a.CX = b.CX = c.CX = d.CX = cx;
        a.CY = b.CY = c.CY = d.CY = cy;
        a.HX = b.HX = c.HX = d.HX = hx;
        a.HY = b.HY = c.HY = d.HY = hy;
        a.Radius = b.Radius = c.Radius = d.Radius = radius;
        a.BorderW = b.BorderW = c.BorderW = d.BorderW = borderW;
        a.Mode = b.Mode = c.Mode = d.Mode = mode;
        a.TexWeight = b.TexWeight = c.TexWeight = d.TexWeight = texWeight;
        a.ClipL = b.ClipL = c.ClipL = d.ClipL = _clipL;
        a.ClipT = b.ClipT = c.ClipT = d.ClipT = _clipT;
        a.ClipR = b.ClipR = c.ClipR = d.ClipR = _clipR;
        a.ClipB = b.ClipB = c.ClipB = d.ClipB = _clipB;

        _vertCount += 4;
        _quadCount++;
    }

    /// <summary>Solid rectangle.</summary>
    public void FillRect(Rect r, Color color)
    {
        if (r.W <= 0 || r.H <= 0 || color.A == 0) return;
        uint c = color.Packed;
        PushQuad(r.X, r.Y, r.Right, r.Y, r.Right, r.Bottom, r.X, r.Bottom,
                 0, 0, 1, 1, c, c, c, c, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    public void FillRect(float x, float y, float w, float h, Color color)
        => FillRect(new Rect(x, y, w, h), color);

    /// <summary>Vertical gradient — the backbone of every XP title bar and button.</summary>
    public void FillRectV(Rect r, Color top, Color bottom)
    {
        if (r.W <= 0 || r.H <= 0) return;
        uint t = top.Packed, b = bottom.Packed;
        PushQuad(r.X, r.Y, r.Right, r.Y, r.Right, r.Bottom, r.X, r.Bottom,
                 0, 0, 1, 1, t, t, b, b, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    /// <summary>Horizontal gradient.</summary>
    public void FillRectH(Rect r, Color left, Color right)
    {
        if (r.W <= 0 || r.H <= 0) return;
        uint l = left.Packed, rr = right.Packed;
        PushQuad(r.X, r.Y, r.Right, r.Y, r.Right, r.Bottom, r.X, r.Bottom,
                 0, 0, 1, 1, l, rr, rr, l, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    /// <summary>Four independently coloured corners.</summary>
    public void FillRectQuad(Rect r, Color tl, Color tr, Color br, Color bl)
    {
        if (r.W <= 0 || r.H <= 0) return;
        PushQuad(r.X, r.Y, r.Right, r.Y, r.Right, r.Bottom, r.X, r.Bottom,
                 0, 0, 1, 1, tl.Packed, tr.Packed, br.Packed, bl.Packed,
                 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    /// <summary>1px-per-side rectangle outline drawn as four fills.</summary>
    public void DrawRect(Rect r, Color color, float thickness = 1f)
    {
        if (r.W <= 0 || r.H <= 0) return;
        FillRect(new Rect(r.X, r.Y, r.W, thickness), color);
        FillRect(new Rect(r.X, r.Bottom - thickness, r.W, thickness), color);
        FillRect(new Rect(r.X, r.Y + thickness, thickness, r.H - thickness * 2), color);
        FillRect(new Rect(r.Right - thickness, r.Y + thickness, thickness, r.H - thickness * 2), color);
    }

    /// <summary>Rounded rectangle with optional border, evaluated as an SDF so the
    /// corners stay clean at any radius.</summary>
    public void RoundedRect(Rect r, float radius, Color fill, Color border = default, float borderWidth = 0)
    {
        if (r.W <= 0 || r.H <= 0) return;
        // Pad by 1px so the antialiased edge is not clipped by the quad itself.
        Rect q = r.Inflate(1);
        uint c = fill.Packed;
        PushQuad(q.X, q.Y, q.Right, q.Y, q.Right, q.Bottom, q.X, q.Bottom,
                 0, 0, 1, 1, c, c, c, c, border.Packed,
                 r.CenterX, r.CenterY, r.W * 0.5f, r.H * 0.5f,
                 radius, borderWidth, 1, 0);
    }

    /// <summary>Rounded rectangle with a vertical gradient body.</summary>
    public void RoundedRectV(Rect r, float radius, Color top, Color bottom, Color border = default, float borderWidth = 0)
    {
        if (r.W <= 0 || r.H <= 0) return;
        Rect q = r.Inflate(1);
        uint t = top.Packed, b = bottom.Packed;
        PushQuad(q.X, q.Y, q.Right, q.Y, q.Right, q.Bottom, q.X, q.Bottom,
                 0, 0, 1, 1, t, t, b, b, border.Packed,
                 r.CenterX, r.CenterY, r.W * 0.5f, r.H * 0.5f,
                 radius, borderWidth, 1, 0);
    }

    public void FillCircle(float cx, float cy, float radius, Color color)
        => RoundedRect(new Rect(cx - radius, cy - radius, radius * 2, radius * 2), radius, color);

    public void DrawCircle(float cx, float cy, float radius, Color color, float thickness = 1f)
        => RoundedRect(new Rect(cx - radius, cy - radius, radius * 2, radius * 2), radius,
                       Color.Transparent, color, thickness);

    /// <summary>Arbitrary-angle line, emitted as an oriented quad.</summary>
    public void Line(float x1, float y1, float x2, float y2, Color color, float thickness = 1f)
    {
        float dx = x2 - x1, dy = y2 - y1;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.0001f) return;
        float nx = -dy / len * thickness * 0.5f;
        float ny = dx / len * thickness * 0.5f;
        uint c = color.Packed;
        PushQuad(x1 + nx, y1 + ny, x2 + nx, y2 + ny, x2 - nx, y2 - ny, x1 - nx, y1 - ny,
                 0, 0, 1, 1, c, c, c, c, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    public void HLine(float x, float y, float w, Color color) => FillRect(new Rect(x, y, w, 1), color);
    public void VLine(float x, float y, float h, Color color) => FillRect(new Rect(x, y, 1, h), color);

    /// <summary>Filled triangle (menu arrows, the Minesweeper flag, scrollbar glyphs).</summary>
    public void FillTriangle(float x0, float y0, float x1, float y1, float x2, float y2, Color color)
    {
        uint c = color.Packed;
        // Degenerate quad: the last vertex repeats the third.
        PushQuad(x0, y0, x1, y1, x2, y2, x2, y2, 0, 0, 1, 1, c, c, c, c,
                 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    // ---- textures --------------------------------------------------------

    public void DrawTexture(Texture tex, Rect dest, Color tint)
        => DrawTexture(tex, dest, 0, 0, 1, 1, tint);

    public void DrawTexture(Texture tex, Rect dest)
        => DrawTexture(tex, dest, 0, 0, 1, 1, Color.White);

    /// <summary>Draws a sub-rectangle of a texture given normalised UVs.</summary>
    public void DrawTexture(Texture tex, Rect dest, float u0, float v0, float u1, float v1, Color tint)
    {
        if (tex == null || dest.W <= 0 || dest.H <= 0 || tint.A == 0) return;
        UseTexture(tex.Id);
        uint c = tint.Packed;
        PushQuad(dest.X, dest.Y, dest.Right, dest.Y, dest.Right, dest.Bottom, dest.X, dest.Bottom,
                 u0, v0, u1, v1, c, c, c, c, 0, 0, 0, 0, 0, 0, 0, 0, 1);
    }

    /// <summary>Draws a sub-rectangle given source pixels rather than UVs.</summary>
    public void DrawTexturePx(Texture tex, Rect dest, Rect srcPixels, Color tint)
    {
        if (tex == null) return;
        float iw = 1f / tex.Width, ih = 1f / tex.Height;
        DrawTexture(tex, dest,
            srcPixels.X * iw, srcPixels.Y * ih,
            srcPixels.Right * iw, srcPixels.Bottom * ih, tint);
    }

    /// <summary>Tiles a texture across <paramref name="dest"/> at its native size.</summary>
    public void DrawTextureTiled(Texture tex, Rect dest, Color tint)
    {
        if (tex == null) return;
        UseTexture(tex.Id);
        float tu = dest.W / tex.Width;
        float tv = dest.H / tex.Height;
        tex.SetWrap(true);
        uint c = tint.Packed;
        PushQuad(dest.X, dest.Y, dest.Right, dest.Y, dest.Right, dest.Bottom, dest.X, dest.Bottom,
                 0, 0, tu, tv, c, c, c, c, 0, 0, 0, 0, 0, 0, 0, 0, 1);
        Flush();
        tex.SetWrap(false);
    }

    public void Dispose()
    {
        _shader?.Dispose();
        _white?.Dispose();
        if (_vao != 0) { GL.DeleteVertexArray(_vao); _vao = 0; }
        if (_vbo != 0) { GL.DeleteBuffer(_vbo); _vbo = 0; }
        if (_ibo != 0) { GL.DeleteBuffer(_ibo); _ibo = 0; }
    }
}
