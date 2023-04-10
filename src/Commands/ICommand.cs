using System.Buffers;

namespace LiteRPS.Commands;

public interface ICommand
{
    int Write(IBufferWriter<byte> writer);
    Task WaitToCompleteAsync();
    void Complete();
}