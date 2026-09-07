namespace Miminus.Graphics;

/// <summary>One texture holding the glyphs of every font in the OS.
///
/// Each <see cref="Font"/> used to own its atlas, which meant a run of Tahoma
/// followed by a run of Consolas broke the vertex batch — a frame with mixed
/// text cost hundreds of draw calls. Packing every face into one shared texture
/// means text never breaks the batch at all, and only genuine pictures
/// (wallpaper, icons with textures, the Paint canvas) cause a flush.
///
/// Space is handed out with a shelf packer: glyphs fill a row until it is full,
/// then a new row starts below the tallest glyph so far.</summary>
public sealed class GlyphAtlas : IDisposable
{
    public const int DefaultSize = 2048;

    readonly uint[] _pixels;
    readonly int _size;
    Texture _texture;
    bool _dirty = true;

    int _penX, _penY, _rowHeight;

    static GlyphAtlas _shared;

    /// <summary>The atlas every font allocates from.</summary>
    public static GlyphAtlas Shared => _shared ??= new GlyphAtlas(DefaultSize);

    public GlyphAtlas(int size)
    {
        _size = size;
        _pixels = new uint[size * size];

        // A solid white texel at the origin lets callers draw plain quads with
        // this texture bound, without breaking the batch.
        _pixels[0] = 0xFFFFFFFF;
        _penX = 2;
        _penY = 0;
        _rowHeight = 1;
    }

    public int Size => _size;

    public Texture Texture
    {
        get
        {
            _texture ??= new Texture(_size, _size, _pixels, linear: false);
            if (_dirty)
            {
                _texture.Update(0, 0, _size, _size, _pixels);
                _dirty = false;
            }
            return _texture;
        }
    }

    /// <summary>Reserves a w×h cell. Returns false when the atlas is full.</summary>
    public bool Allocate(int w, int h, out int x, out int y)
    {
        if (_penX + w + 1 >= _size)
        {
            _penX = 0;
            _penY += _rowHeight + 1;
            _rowHeight = 0;
        }
        if (_penY + h + 1 >= _size)
        {
            x = y = 0;
            return false;
        }

        x = _penX;
        y = _penY;
        _penX += w + 1;
        if (h > _rowHeight) _rowHeight = h;
        return true;
    }

    /// <summary>Writes one glyph texel.
    ///
    /// Coverage is stored per channel rather than as a single alpha: with
    /// greyscale or no smoothing all three are equal and the result is a plain
    /// mask, but under ClearType they carry GDI's subpixel values, and the
    /// shader's colour-times-texel multiply then reproduces the colour fringing
    /// for free.</summary>
    public void SetTexel(int x, int y, uint r, uint g, uint b)
    {
        uint a = Math.Max(r, Math.Max(g, b));
        _pixels[y * _size + x] = r | (g << 8) | (b << 16) | (a << 24);
        _dirty = true;
    }

    /// <summary>Drops every packed glyph so the atlas can be rebuilt, which is
    /// what a change of font smoothing needs.</summary>
    public void Reset()
    {
        Array.Clear(_pixels);
        _pixels[0] = 0xFFFFFFFF;
        _penX = 2;
        _penY = 0;
        _rowHeight = 1;
        _dirty = true;
        Version++;
    }

    /// <summary>Incremented by Reset so fonts know to drop their caches.</summary>
    public int Version { get; private set; }

    public void MarkDirty() => _dirty = true;

    /// <summary>How much of the atlas has been handed out, for diagnostics.</summary>
    public float UsedFraction => (_penY + _rowHeight) / (float)_size;

    public void Dispose()
    {
        _texture?.Dispose();
        _texture = null;
        if (ReferenceEquals(_shared, this)) _shared = null;
    }
}
