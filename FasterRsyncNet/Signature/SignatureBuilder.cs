using System.Buffers;
using FasterRsyncNet.Chunk;
using FasterRsyncNet.Core;
using FasterRsyncNet.Hashing.NonCryptographic;
using FasterRsyncNet.Hashing.Rolling;

namespace FasterRsyncNet.Signature;

public class SignatureBuilder
{
    private readonly INonCryptographicHashingAlgorithm _hashingAlgorithm;
    private readonly IRollingChecksumAlgorithm _checksumAlgorithm;
    private readonly ushort _chunkSize;
    public ushort ChunkSize
    {
        get => _chunkSize;
        init
        {
            if(value < 128) throw new ArgumentOutOfRangeException(nameof(value), $"{nameof(ChunkSize)} must be >= 128");
            _chunkSize = value;
        }
    }

    public uint MaxStackBufferSize { get; init; } = 256; //Conservative stack size
    public uint MaxBufferSize { get; init; } = 4096; //NTFS cluster size
    
    public SignatureBuilder(INonCryptographicHashingAlgorithm hashingAlgorithm,
        IRollingChecksumAlgorithm rollingChecksumAlgorithm, ushort chunkSize = 1024)
    {
        _hashingAlgorithm = hashingAlgorithm;
        _checksumAlgorithm = rollingChecksumAlgorithm;
        _chunkSize = chunkSize;
    }

    public void BuildSignature(Stream source, ISignatureWriter signatureWriter, IProgress<double>? progress = null)
    {
        signatureWriter.WriteHeader();
        
        signatureWriter.WriteMetadata(new SignatureMetadata
        {
            Version = BinaryFormat.FormatVersion,
            ChunkSize = ChunkSize,
            HashAlgorithmIdentifier = _hashingAlgorithm.AlgorithmIdentifier,
            RollingHashAlgorithmIdentifier = _checksumAlgorithm.AlgorithmIdentifier,
        });
        
        WriteChunks(source, signatureWriter, progress);
    }

    //TODO: Clean up code formatting
    private void WriteChunks(Stream source, ISignatureWriter signatureWriter, IProgress<double>? progress = null)
    {
        byte[]? heapBuffer =
            MaxBufferSize > MaxStackBufferSize ? ArrayPool<byte>.Shared.Rent((int)MaxBufferSize) : null;
        Span<byte> buffer = heapBuffer == null
            ? stackalloc byte[(int)MaxStackBufferSize]
            : heapBuffer.AsSpan(0, (int)MaxBufferSize);
        
        //TODO: Enforce limits for size of the hash buffer
        Span<byte> hashBuffer = stackalloc byte[_hashingAlgorithm.HashLengthInBytes];
        
        try
        {
            if(source.Position != 0 && source.CanSeek)
                source.Seek(0, SeekOrigin.Begin);
            
            ushort bytesSubmitted = 0;
            uint rollingChecksum = 1;

            int read;
            while ((read = source.Read(buffer)) > 0)
            {
                uint index = 0;
                while (index < read)
                {
                    long bytesToTake = Math.Min(read - index, ChunkSize - bytesSubmitted);
                    Span<byte> chunkData = buffer.Slice((int)index, (int)bytesToTake);
                    
                    _hashingAlgorithm.Append(chunkData);
                    rollingChecksum = _checksumAlgorithm.CalculateBlock(chunkData, rollingChecksum);
                    
                    bytesSubmitted += (ushort)bytesToTake;
                    index += (uint)chunkData.Length;
                    if (bytesSubmitted < ChunkSize) continue;

                    _hashingAlgorithm.GetHashAndReset(hashBuffer);
                    signatureWriter.WriteChunk(hashBuffer, rollingChecksum);
                    rollingChecksum = 1;
                    bytesSubmitted = 0;
                }
                progress?.Report((double)source.Position / source.Length);
            }

            if (bytesSubmitted <= 0) return;
            _hashingAlgorithm.GetHashAndReset(hashBuffer);
            signatureWriter.WriteFinalChunk(hashBuffer, rollingChecksum, bytesSubmitted);
        }
        finally
        {
            if(heapBuffer != null)
                ArrayPool<byte>.Shared.Return(heapBuffer);
        }
    }
}