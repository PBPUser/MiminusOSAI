using System.Buffers.Binary;
using System.IO.Compression;
using Miminus.Platform;

namespace Miminus.Graphics;

/// <summary>Grabs the framebuffer and writes a PNG. Used by the --screenshot
/// switch, which renders a scripted number of frames and exits — handy for
/// eyeballing the shell without driving it by hand.</summary>
public static unsafe class Screenshot
{
    public static void Capture(int width, int height, string path)
    {
        GL.Finish();
        byte[] pixels = new byte[width * height * 4];
        GL.PixelStore(GL.PACK_ALIGNMENT, 1);
        fixed (byte* p = pixels)
            GL.ReadPixels(0, 0, width, height, GL.RGBA, GL.UNSIGNED_BYTE, p);

        // glReadPixels hands back rows bottom-up; PNG wants them top-down.
        byte[] flipped = new byte[pixels.Length];
        int stride = width * 4;
        for (int y = 0; y < height; y++)
            Array.Copy(pixels, (height - 1 - y) * stride, flipped, y * stride, stride);

        WritePng(path, width, height, flipped);
    }

    public static void WritePng(string path, int width, int height, byte[] rgba)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using var fs = File.Create(path);
        fs.Write(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A });

        // IHDR
        byte[] ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0), width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8;      // bit depth
        ihdr[9] = 6;      // colour type: truecolour + alpha
        ihdr[10] = 0;     // deflate
        ihdr[11] = 0;     // adaptive filtering
        ihdr[12] = 0;     // no interlace
        WriteChunk(fs, "IHDR", ihdr);

        // IDAT — one filter byte (0 = None) in front of each scanline.
        int stride = width * 4;
        byte[] raw = new byte[(stride + 1) * height];
        for (int y = 0; y < height; y++)
        {
            raw[y * (stride + 1)] = 0;
            Array.Copy(rgba, y * stride, raw, y * (stride + 1) + 1, stride);
        }

        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Fastest, leaveOpen: true))
            z.Write(raw, 0, raw.Length);
        WriteChunk(fs, "IDAT", ms.ToArray());

        WriteChunk(fs, "IEND", Array.Empty<byte>());
    }

    static void WriteChunk(Stream s, string type, byte[] data)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        s.Write(len);

        byte[] typeBytes = { (byte)type[0], (byte)type[1], (byte)type[2], (byte)type[3] };
        s.Write(typeBytes);
        s.Write(data);

        // The PNG CRC covers the chunk type followed by its data.
        uint crc = 0xFFFFFFFF;
        crc = Crc32Update(crc, typeBytes);
        crc = Crc32Update(crc, data);
        crc ^= 0xFFFFFFFF;

        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        s.Write(crcBytes);
    }

    static uint[] _crcTable;

    static uint Crc32Update(uint crc, byte[] data)
    {
        if (_crcTable == null)
        {
            _crcTable = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                _crcTable[n] = c;
            }
        }
        foreach (byte b in data)
            crc = _crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }
}
