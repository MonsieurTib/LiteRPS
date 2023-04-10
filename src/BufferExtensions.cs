using System.Buffers;

namespace LiteRPS;

internal static class BufferExtensions
{
    internal static ReadOnlySpan<byte> ToSpan(in this ReadOnlySequence<byte> buffer) => buffer.IsSingleSegment ? buffer.First.Span : buffer.ToArray();
}