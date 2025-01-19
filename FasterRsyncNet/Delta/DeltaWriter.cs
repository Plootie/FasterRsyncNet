using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
        Span<byte> tmpBuffer = stackalloc byte[sizeof(byte) + (sizeof(long) * 2)];

        Span<byte> commandSpan = tmpBuffer.Slice(0, sizeof(byte));
        Span<byte> positionSpan = tmpBuffer.Slice(sizeof(byte), sizeof(long));
        Span<byte> lengthSpan = tmpBuffer.Slice(sizeof(byte) + sizeof(long), sizeof(long));

        byte commandByte = 0x60;
        
        if (!BitConverter.IsLittleEndian)
        {
            position = BinaryPrimitives.ReverseEndianness(position);
            length = BinaryPrimitives.ReverseEndianness(length);
        }
        
        MemoryMarshal.Write(commandSpan, in commandByte);
        MemoryMarshal.Write(positionSpan, in position);
        MemoryMarshal.Write(lengthSpan, in length);
        
        _binaryWriter.Write(tmpBuffer);
    }
}