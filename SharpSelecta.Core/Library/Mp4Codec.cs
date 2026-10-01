using System.Buffers.Binary;
using System.Text;

namespace SharpSelecta.Core.Library;

// ATL reports ALAC and AAC alike, so the codec is read from the MP4 itself: the first sample entry of the
// audio track's stsd box is "alac" or "mp4a".
internal static class Mp4Codec
{
    public const string Alac = "alac";
    public const string Aac = "mp4a";

    public static string? ReadAudioCodec(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            foreach (var moov in Boxes(stream, 0, stream.Length).Where(b => b.Type == "moov"))
            {
                foreach (var trak in Boxes(stream, moov.Start, moov.End).Where(b => b.Type == "trak"))
                {
                    var stsd = Descend(stream, trak, "mdia", "minf", "stbl", "stsd");
                    if (stsd is null)
                        continue;

                    // Version/flags (4) and entry count (4), then the first entry: size (4) and its type.
                    var entryType = ReadType(stream, stsd.Value.Start + 12);
                    if (entryType is Alac or Aac)
                        return entryType;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or EndOfStreamException)
        {
        }

        return null;
    }

    private static (long Start, long End)? Descend(Stream stream, (string Type, long Start, long End) box, params string[] path)
    {
        var current = (box.Start, box.End);
        foreach (var name in path)
        {
            var next = Boxes(stream, current.Item1, current.Item2).Cast<(string Type, long Start, long End)?>().FirstOrDefault(b => b!.Value.Type == name);
            if (next is null)
                return null;

            current = (next.Value.Start, next.Value.End);
        }

        return current;
    }

    // Child boxes between start and end: each has a 4-byte big-endian size and a 4-byte type; size 1 means a
    // 64-bit size follows, size 0 means "to the end".
    private static IEnumerable<(string Type, long Start, long End)> Boxes(Stream stream, long start, long end)
    {
        var position = start;
        var header = new byte[16];
        while (position + 8 <= end)
        {
            stream.Position = position;
            stream.ReadExactly(header, 0, 8);
            long size = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan());
            var type = Encoding.ASCII.GetString(header, 4, 4);
            var headerLength = 8;

            if (size == 1)
            {
                stream.ReadExactly(header, 8, 8);
                size = (long)BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(8));
                headerLength = 16;
            }
            else if (size == 0)
            {
                size = end - position;
            }

            if (size < headerLength || position + size > end)
                yield break;

            yield return (type, position + headerLength, position + size);
            position += size;
        }
    }

    private static string ReadType(Stream stream, long position)
    {
        stream.Position = position;
        Span<byte> type = stackalloc byte[4];
        stream.ReadExactly(type);
        return Encoding.ASCII.GetString(type);
    }
}
