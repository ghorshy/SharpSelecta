using System.Buffers.Binary;

namespace SharpSelecta.Integrations.AlbumArt;

// Reads width/height from a JPEG or PNG header; null for anything else (so a service answering with
// an HTML error page or an unsupported format is never taken for a cover).
internal static class ImageSize
{
    public static (int Width, int Height)? TryRead(ReadOnlySpan<byte> bytes) =>
        bytes switch
        {
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] when bytes.Length >= 24 =>
                ((int)BinaryPrimitives.ReadUInt32BigEndian(bytes[16..]), (int)BinaryPrimitives.ReadUInt32BigEndian(bytes[20..])),
            [0xFF, 0xD8, ..] => ReadJpeg(bytes),
            _ => null,
        };

    private static (int, int)? ReadJpeg(ReadOnlySpan<byte> bytes)
    {
        var i = 2;
        while (i + 9 < bytes.Length)
        {
            if (bytes[i] != 0xFF)
                return null;

            var marker = bytes[i + 1];
            // Start-of-frame markers (not DHT/JPG/DAC) carry the dimensions.
            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
                return (BinaryPrimitives.ReadUInt16BigEndian(bytes[(i + 7)..]), BinaryPrimitives.ReadUInt16BigEndian(bytes[(i + 5)..]));

            i += 2 + BinaryPrimitives.ReadUInt16BigEndian(bytes[(i + 2)..]);
        }

        return null;
    }
}
