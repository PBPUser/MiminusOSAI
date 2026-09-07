using System.Buffers.Binary;
using System.IO.Compression;

namespace Miminus.Graphics;

/// <summary>Decodes real image files from a mounted host folder.
///
/// PNG and BMP are decoded here by hand — PNG because .NET already ships the
/// inflate half of it in <see cref="ZLibStream"/>, BMP because it is trivial.
/// Anything else (JPEG in particular) is reported as unsupported rather than
/// half-decoded, and the shell shows a placeholder instead.</summary>
public static class ImageLoader
{
    public static bool IsSupported(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".bmp";
    }

    public static bool IsKnownImage(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".bmp" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".tif" or ".tiff";
    }

    /// <summary>Loads an image into an ARGB pixel buffer, or returns null when the
    /// format is unsupported or the file is unreadable.</summary>
    public static uint[] Load(string path, out int width, out int height)
    {
        width = height = 0;
        try
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            byte[] bytes = File.ReadAllBytes(path);
            return ext switch
            {
                ".png" => DecodePng(bytes, out width, out height),
                ".bmp" => DecodeBmp(bytes, out width, out height),
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    // ---- PNG -------------------------------------------------------------

    static uint[] DecodePng(byte[] data, out int width, out int height)
    {
        width = height = 0;
        if (data.Length < 8 || data[0] != 0x89 || data[1] != 'P' || data[2] != 'N' || data[3] != 'G')
            return null;

        int pos = 8;
        int w = 0, h = 0, bitDepth = 0, colorType = 0, interlace = 0;
        byte[] palette = null, transparency = null;
        using var idat = new MemoryStream();

        while (pos + 8 <= data.Length)
        {
            int len = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(pos));
            string type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
            int body = pos + 8;
            if (len < 0 || body + len > data.Length) break;

            switch (type)
            {
                case "IHDR":
                    w = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(body));
                    h = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(body + 4));
                    bitDepth = data[body + 8];
                    colorType = data[body + 9];
                    interlace = data[body + 12];
                    break;
                case "PLTE":
                    palette = data[body..(body + len)];
                    break;
                case "tRNS":
                    transparency = data[body..(body + len)];
                    break;
                case "IDAT":
                    idat.Write(data, body, len);
                    break;
                case "IEND":
                    pos = data.Length;
                    break;
            }
            pos = body + len + 4;   // skip the CRC
        }

        // Interlaced and 16-bit-per-sample files are rare enough to decline.
        if (w <= 0 || h <= 0 || interlace != 0 || bitDepth is not (1 or 2 or 4 or 8))
            return null;

        int channels = colorType switch
        {
            0 => 1,   // greyscale
            2 => 3,   // truecolour
            3 => 1,   // palette index
            4 => 2,   // greyscale + alpha
            6 => 4,   // truecolour + alpha
            _ => 0,
        };
        if (channels == 0) return null;
        if (colorType == 3 && palette == null) return null;

        idat.Position = 0;
        byte[] raw;
        using (var z = new ZLibStream(idat, CompressionMode.Decompress))
        using (var outMs = new MemoryStream())
        {
            z.CopyTo(outMs);
            raw = outMs.ToArray();
        }

        int bitsPerPixel = channels * bitDepth;
        int stride = (w * bitsPerPixel + 7) / 8;
        int filterUnit = Math.Max(1, bitsPerPixel / 8);
        if (raw.Length < (stride + 1) * h) return null;

        // Undo the per-scanline filters in place.
        var cur = new byte[stride];
        var prev = new byte[stride];
        var pixels = new uint[w * h];

        for (int y = 0; y < h; y++)
        {
            int rowStart = y * (stride + 1);
            byte filter = raw[rowStart];
            Buffer.BlockCopy(raw, rowStart + 1, cur, 0, stride);

            for (int i = 0; i < stride; i++)
            {
                int a = i >= filterUnit ? cur[i - filterUnit] : 0;
                int b = prev[i];
                int c = i >= filterUnit ? prev[i - filterUnit] : 0;
                cur[i] = filter switch
                {
                    1 => (byte)(cur[i] + a),
                    2 => (byte)(cur[i] + b),
                    3 => (byte)(cur[i] + (a + b) / 2),
                    4 => (byte)(cur[i] + Paeth(a, b, c)),
                    _ => cur[i],
                };
            }

            EmitRow(cur, pixels, y, w, bitDepth, colorType, palette, transparency);
            (prev, cur) = (cur, prev);
        }

        width = w;
        height = h;
        return pixels;
    }

    static int Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    static void EmitRow(byte[] row, uint[] dest, int y, int w, int bitDepth, int colorType,
                        byte[] palette, byte[] trns)
    {
        int o = y * w;

        // Sub-byte depths only occur for greyscale and palette images.
        if (bitDepth < 8)
        {
            int perByte = 8 / bitDepth;
            int mask = (1 << bitDepth) - 1;
            int maxVal = mask;
            for (int x = 0; x < w; x++)
            {
                int shift = 8 - bitDepth * (x % perByte + 1);
                int v = (row[x / perByte] >> shift) & mask;
                if (colorType == 3) dest[o + x] = FromPalette(v, palette, trns);
                else
                {
                    byte g = (byte)(v * 255 / maxVal);
                    dest[o + x] = Pack(g, g, g, 255);
                }
            }
            return;
        }

        switch (colorType)
        {
            case 0:
                for (int x = 0; x < w; x++)
                {
                    byte g = row[x];
                    dest[o + x] = Pack(g, g, g, 255);
                }
                break;
            case 2:
                for (int x = 0; x < w; x++)
                    dest[o + x] = Pack(row[x * 3], row[x * 3 + 1], row[x * 3 + 2], 255);
                break;
            case 3:
                for (int x = 0; x < w; x++)
                    dest[o + x] = FromPalette(row[x], palette, trns);
                break;
            case 4:
                for (int x = 0; x < w; x++)
                {
                    byte g = row[x * 2];
                    dest[o + x] = Pack(g, g, g, row[x * 2 + 1]);
                }
                break;
            default:
                for (int x = 0; x < w; x++)
                    dest[o + x] = Pack(row[x * 4], row[x * 4 + 1], row[x * 4 + 2], row[x * 4 + 3]);
                break;
        }
    }

    static uint FromPalette(int index, byte[] palette, byte[] trns)
    {
        int p = index * 3;
        if (palette == null || p + 2 >= palette.Length) return 0xFF000000;
        byte a = trns != null && index < trns.Length ? trns[index] : (byte)255;
        return Pack(palette[p], palette[p + 1], palette[p + 2], a);
    }

    // ---- BMP -------------------------------------------------------------

    static uint[] DecodeBmp(byte[] data, out int width, out int height)
    {
        width = height = 0;
        if (data.Length < 54 || data[0] != 'B' || data[1] != 'M') return null;

        int dataOffset = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(10));
        int headerSize = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(14));
        int w = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(18));
        int h = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(22));
        int bpp = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(28));
        int compression = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(30));

        if (w <= 0 || compression != 0 || bpp is not (8 or 24 or 32)) return null;

        bool bottomUp = h > 0;
        h = Math.Abs(h);
        if (w > 8192 || h > 8192) return null;

        // 8-bit files carry their palette between the header and the pixel data.
        byte[] palette = null;
        if (bpp == 8)
        {
            int paletteStart = 14 + headerSize;
            int count = Math.Min(256, (dataOffset - paletteStart) / 4);
            if (count <= 0) return null;
            palette = new byte[count * 4];
            Array.Copy(data, paletteStart, palette, 0, palette.Length);
        }

        int stride = (w * bpp / 8 + 3) & ~3;
        if (dataOffset + stride * h > data.Length) return null;

        var pixels = new uint[w * h];
        for (int y = 0; y < h; y++)
        {
            int srcRow = dataOffset + (bottomUp ? h - 1 - y : y) * stride;
            int o = y * w;
            for (int x = 0; x < w; x++)
            {
                switch (bpp)
                {
                    case 8:
                    {
                        int idx = data[srcRow + x] * 4;
                        pixels[o + x] = idx + 2 < palette.Length
                            ? Pack(palette[idx + 2], palette[idx + 1], palette[idx], 255)
                            : 0xFF000000;
                        break;
                    }
                    case 24:
                    {
                        int p = srcRow + x * 3;
                        pixels[o + x] = Pack(data[p + 2], data[p + 1], data[p], 255);
                        break;
                    }
                    default:
                    {
                        int p = srcRow + x * 4;
                        pixels[o + x] = Pack(data[p + 2], data[p + 1], data[p], data[p + 3]);
                        break;
                    }
                }
            }
        }

        width = w;
        height = h;
        return pixels;
    }

    static uint Pack(byte r, byte g, byte b, byte a)
        => (uint)r | ((uint)g << 8) | ((uint)b << 16) | ((uint)a << 24);
}
