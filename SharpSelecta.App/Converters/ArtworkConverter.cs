using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace SharpSelecta.App.Converters;

public sealed class ArtworkConverter : IValueConverter
{
    public static readonly ArtworkConverter Instance = new();

    private const int MaxCachedBitmaps = 150;

    private readonly object _lock = new();
    private readonly Dictionary<byte[], LinkedListNode<(byte[] Key, Bitmap Bitmap)>> _index = new();
    private readonly LinkedList<(byte[] Key, Bitmap Bitmap)> _lruOrder = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] bytes)
            return null;

        lock (_lock)
        {
            if (_index.TryGetValue(bytes, out var node))
            {
                _lruOrder.Remove(node);
                _lruOrder.AddFirst(node);
                return node.Value.Bitmap;
            }

            var bitmap = new Bitmap(new MemoryStream(bytes));
            var newNode = _lruOrder.AddFirst((bytes, bitmap));
            _index[bytes] = newNode;

            if (_index.Count > MaxCachedBitmaps)
            {
                var oldest = _lruOrder.Last!;
                _lruOrder.RemoveLast();
                _index.Remove(oldest.Value.Key);
            }

            return bitmap;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
