namespace LiteRPS.Resp;

public sealed class RespArray : RespObject
{
    public List<RespObject>? Items { get; init; }
}