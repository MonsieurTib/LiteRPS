namespace LiteRPS.Resp;

public class RespInt : RespObject
{
    public int Value { get; }

    public RespInt(int value)
    {
        Value = value;
    }

    public static implicit operator int(RespInt respInt)
    {
        return respInt.Value;
    }
}