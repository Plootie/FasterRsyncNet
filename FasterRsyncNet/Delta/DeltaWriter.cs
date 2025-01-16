using System.Buffers;
using System.Buffers.Binary;

namespace FasterRsyncNet.Delta;

public class DeltaWriter : IDeltaWriter
{
    private readonly Stream _deltaStream;
    private readonly BinaryWriter _binaryWriter;

    public DeltaWriter(Stream deltaStream)
    {
        _deltaStream = deltaStream;
        _binaryWriter = new BinaryWriter(_deltaStream);
    }

    public void WriteDataCommand(Stream source, long position, long length)
    {
        _binaryWriter.Write(0x80);
        _binaryWriter.Write(length);
        
        long originalPosition = source.Position;
        int targetBufferSize = (int)Math.Min(length, 4096);
        byte[] heapBuffer = ArrayPool<byte>.Shared.Rent(targetBufferSize);
        try
        {
            Span<byte> spanBuffer = heapBuffer.AsSpan(0, targetBufferSize);
            source.Seek(position, SeekOrigin.Begin);

            int read;
            long soFar = 0;
            while ((read = source.Read(spanBuffer.Slice(0, (int)Math.Min(length - soFar, spanBuffer.Length)))) > 0)
            {
                soFar += read;
                _binaryWriter.Write(spanBuffer.Slice(0, read));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(heapBuffer);
            source.Seek(originalPosition, SeekOrigin.Begin);
        }
    }

    public void WriteCopyCommand(long position, long length)
    {
        Span<byte> tmpBuffer = stackalloc byte[20];
        BinaryPrimitives.WriteInt32LittleEndian(tmpBuffer, 0x60);
        BinaryPrimitives.WriteInt64LittleEndian(tmpBuffer.Slice(4, 8), position);
        BinaryPrimitives.WriteInt64LittleEndian(tmpBuffer.Slice(12, 8), length);
        _binaryWriter.Write(tmpBuffer);
    }
}