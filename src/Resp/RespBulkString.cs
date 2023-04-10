using System.Buffers;
using System.Text;

namespace LiteRPS.Resp;

public class RespBulkString : RespObject
{
    public ReadOnlySequence<byte> Value { get; }
    
    public RespBulkString(ReadOnlySequence<byte> value)
    {
        Value = value;
    }

    public override string ToString()
    {
        if (Value.IsSingleSegment)
        {
            return Encoding.UTF8.GetString(Value.FirstSpan);
        }

        var length = checked((int)Value.Length);
        var buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            Value.CopyTo(buffer);
            return Encoding.UTF8.GetString(buffer, 0, length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static implicit operator string(RespBulkString bulkString)
    {
        if (bulkString.Value.IsSingleSegment)
        {
            return Encoding.UTF8.GetString(bulkString.Value.FirstSpan);
        }

        var length = checked((int)bulkString.Value.Length);
        var buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            bulkString.Value.CopyTo(buffer);
            return Encoding.UTF8.GetString(buffer, 0, length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}