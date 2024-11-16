using System.IO.Hashing;
using FasterRsyncNet.Hash;

namespace FasterRsyncNet.Signature;

//TODO: Finish this implementation. Putting it on the backburner to finish up the rest of the code first
public class SignatureStream : Stream
{
    private const int MaxAllowedBytesPerRead = 128 * 1024;
    private const short MinChunkSize = 128;
    private const short MaxChunkSize = 31 * 1024;
    private const short DefaultChunkSize = 2048;

    private readonly short _chunkSize;

    //TODO: Replace this with an interface
    private readonly NonCryptographicHashAlgorithm _nonCryptographicHashAlgorithm;
    
    private readonly Stream _underlyingStream;
    private short _bufferedBytes = 0;

    //TODO: Remove this. Reference the BufferedStream source for how they buffer their streams.
    private byte[] _outputBuffer = [];
    private readonly long _position = 0;

    //TODO: Replace with primary constructor if no additional logic is needed
    public SignatureStream(Stream inputStream, NonCryptographicHashAlgorithm hashAlgorithm, short chunkSize)
    {
        _underlyingStream = inputStream;
        _nonCryptographicHashAlgorithm = hashAlgorithm;
        _chunkSize = chunkSize;

        //TODO: Factor in the header size
        int hashLength = _nonCryptographicHashAlgorithm.HashLengthInBytes;
        long projectedLength = _underlyingStream.Length / chunkSize * hashLength;
        Length = projectedLength;

        inputStream.Seek(0, SeekOrigin.Begin);
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length { get; }

    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count > buffer.Length - offset)
            throw new ArgumentException("Buffer is too small for requested operation.");
        throw new NotImplementedException();
    }

    public override void Flush()
    {
        throw new NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }

    public override void SetLength(long value)
    {
        throw new NotImplementedException();
    }
}