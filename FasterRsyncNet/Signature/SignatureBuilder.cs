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
            ChunkCount = (ulong)Math.Ceiling((double)source.Length / ChunkSize),
            HashLength = (ushort)_hashingAlgorithm.HashLengthInBytes,
            HashAlgorithmIdentifier = _hashingAlgorithm.AlgorithmIdentifier,
            RollingHashAlgorithmIdentifier = _checksumAlgorithm.AlgorithmIdentifier,
        });
        
        BuildChunks(source, signatureWriter, progress);
    }

    //TODO: Clean up code formatting
    private void BuildChunks(Stream source, ISignatureWriter signatureWriter, IProgress<double>? progress = null)
    {
        byte[]? heapBuffer = null;
        if (MaxBufferSize > MaxStackBufferSize)
        {
            heapBuffer = ArrayPool<byte>.Shared.Rent((int)MaxBufferSize);
        }

        Span<byte> buffer = heapBuffer == null
            ? heapBuffer.AsSpan(0, (int)MaxBufferSize)
            : stackalloc byte[(int)MaxStackBufferSize];
        
        Span<byte> hashBuffer = stackalloc byte[_hashingAlgorithm.HashLengthInBytes];

        try
        {
            /*TODO: Should we do this? I cannot decide whether we should make it the users job to ensure the stream
             is in the correct position for them when passing us the source stream*/
            if (source.Position != 0 && !source.CanSeek)
            {
                throw new NotSupportedException("Stream is not seekable and not at the beginning.");
            }
            
            source.Seek(0, SeekOrigin.Begin);

            int submitted = 0;
            uint rollingChecksum = 1;

            int read;
            while ((read = source.Read(buffer)) > 0)
            {
                int index = 0;
                while (index < read)
                {
                    int bytesToTake = Math.Min(ChunkSize - submitted, read - index);
                    ReadOnlySpan<byte> chunkData = buffer.Slice(index, bytesToTake);
                    
                    _hashingAlgorithm.Append(chunkData);
                    rollingChecksum = _checksumAlgorithm.CalculateBlock(chunkData, rollingChecksum);
                    
                    submitted += bytesToTake;
                    index += bytesToTake;

                    if (submitted < ChunkSize) continue;

                    _hashingAlgorithm.GetHashAndReset(hashBuffer);
                    signatureWriter.WriteChunk(hashBuffer, rollingChecksum);
                    submitted = 0;
                    rollingChecksum = 1;
                }
                
                double progressFraction = (double)source.Position / source.Length;
                progress?.Report(progressFraction);
            }

            bool remaining = submitted > 0;
            int finalLength = ChunkSize;
            if (remaining)
            {
                _hashingAlgorithm.GetHashAndReset(hashBuffer);
                signatureWriter.WriteChunk(hashBuffer, rollingChecksum);
                finalLength = submitted;
            }
            signatureWriter.WriteFinalChunkLength((ushort)finalLength);
        }
        finally
        {
            if (heapBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(heapBuffer);
            }
        }
    }
}