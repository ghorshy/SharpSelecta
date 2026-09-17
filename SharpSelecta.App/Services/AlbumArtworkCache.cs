using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Media.Imaging;

namespace SharpSelecta.App.Services;

public static class AlbumArtworkCache
{
    private const int ThumbnailSize = 300;

    public static byte[]? GetOrCreate(string cacheDirectory, string albumKey, Func<byte[]?> loadOriginalArtwork)
    {
        var cachePath = GetCachePath(cacheDirectory, albumKey);
        if (File.Exists(cachePath))
        {
            return File.ReadAllBytes(cachePath);
        }

        var original = loadOriginalArtwork();
        if (original is null)
        {
            return null;
        }

        var thumbnail = CreateThumbnail(original);
        Directory.CreateDirectory(cacheDirectory);

        // Two AlbumGridViewModel instances (Library's and Recently Added's) can race to cache the
        // same album concurrently. Writing to a per-call temp file and renaming into place keeps
        // a concurrent reader/writer from ever seeing a torn file at cachePath.
        var tempPath = $"{cachePath}.tmp-{Guid.NewGuid():N}";
        try
        {
            File.WriteAllBytes(tempPath, thumbnail);
            File.Move(tempPath, cachePath, overwrite: true);
        }
        catch
        {
            File.Delete(tempPath);
            throw;
        }

        return thumbnail;
    }

    private static string GetCachePath(string cacheDirectory, string albumKey)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(albumKey.Trim().ToUpperInvariant())));
        return Path.Combine(cacheDirectory, $"{hash}.jpg");
    }

    private static byte[] CreateThumbnail(byte[] original)
    {
        using var sourceStream = new MemoryStream(original);
        using var bitmap = new Bitmap(sourceStream);

        var shorterSide = Math.Min(bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        var scale = (double)ThumbnailSize / shorterSide;
        var scaledSize = new PixelSize(
            Math.Max(1, (int)Math.Round(bitmap.PixelSize.Width * scale)),
            Math.Max(1, (int)Math.Round(bitmap.PixelSize.Height * scale)));

        using var scaled = bitmap.CreateScaledBitmap(scaledSize);
        using var outputStream = new MemoryStream();
        scaled.Save(outputStream, new JpegBitmapEncoderOptions { Quality = 85 });
        return outputStream.ToArray();
    }
}
