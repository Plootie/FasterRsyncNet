using System.Buffers;
using FasterRsyncNet.Chunk;
using FasterRsyncNet.Hash;

namespace FasterRsyncNet.Signature;

public class SignatureBuilder
{
    //TODO: Look at maybe making these public so these limits are transparent to the user
    public const short MinChunkSize = 128;
    public const short DefaultChunkSize = 2048;
    //Is this max even sensible?
    public const short MaxChunkSize = 31 * 1024;

    private readonly short _chunkSize = DefaultChunkSize;
    //TODO: Storing these along with the instances feels clunky. We should probably rework this
    private readonly NonCryptographicHashingAlgorithmOption _hashingAlgorithmOption;
    private readonly INonCryptographicHashingAlgorithm _chunkHasher;
    private readonly INonCryptographicHashingAlgorithm _fileHasher;
    
    public short ChunkSize
    {
        get => _chunkSize;
        init
        {
            _chunkSize = value switch
            {
                < MinChunkSize => throw new ArgumentException($"Chunk size cannot be less than {MinChunkSize}"),
                > MaxChunkSize => throw new ArgumentException($"Chunk size cannot be exceed {MaxChunkSize}"),
                _ => value
            };
            //TODO: This feels like a terrible idea. This should be cleaned up
        }
    }

    public SignatureBuilder(NonCryptographicHashingAlgorithmOption hashingAlgorithmOption)
    {
        _hashingAlgorithmOption = hashingAlgorithmOption;
        
        Type hashingAlgorithmType = HashHelper.NonCryptographicHashingAlgorithmMapper[hashingAlgorithmOption];
        _chunkHasher = HashHelper.InstanceFromType<INonCryptographicHashingAlgorithm>(hashingAlgorithmType);
        _fileHasher = HashHelper.InstanceFromType<INonCryptographicHashingAlgorithm>(hashingAlgorithmType);
    }

    public void BuildSignature(Stream dataStream, ISignatureWriter sigWriter)
    {
        //We cannot know the metadata info before calculating the data ahead so we skip forward and will write at the end
        int hashLength = _chunkHasher.HashLengthInBytes;
        sigWriter.WriteMetadata(new SignatureMetadata(new byte[hashLength], NonCryptographicHashingAlgorithmOption.Unknown, 0));
        
        WriteChunkSignatures(sigWriter, dataStream);
        sigWriter.BaseStream.Seek(0, SeekOrigin.Begin);
        WriteMetadata(sigWriter);
    }

    private void WriteChunkSignatures(ISignatureWriter sigWriter, Stream dataStream)
    {
        dataStream.Seek(0, SeekOrigin.Begin);
        
        //TODO: In the future allow configuring of this as well as providing your own buffer
        int optimalBufferSize = Math.Max(8192, (int)ChunkSize);
        int maxChunksPerHeapBuffer = (int)Math.Floor((double)optimalBufferSize / ChunkSize);
        int bufferLength = ChunkSize * maxChunksPerHeapBuffer;
        byte[] heapBuffer = ArrayPool<byte>.Shared.Rent(bufferLength);

        try
        {
            //ArrayPool might return a buffer larger than requested so we need to trim it back down
            Span<byte> buffer = heapBuffer;
            buffer = buffer.Slice(0, bufferLength);
            
            int read;
            while ((read = dataStream.Read(buffer)) > 0)
            {
                //Console.WriteLine("Position {0} of {1}. Got {2} bytes", dataStream.Position, dataStream.Length, read);
                int chunksToProcess = (int)Math.Ceiling((double)read / ChunkSize);
                for (int i = 0; i < chunksToProcess; i++)
                {
                    Span<byte> chunkBytes = buffer.Slice(i * ChunkSize, ChunkSize);
                    _chunkHasher.Append(chunkBytes);
                    //TODO: This feels like a lot of data to transfer. Would it be better for us to use the hash of the chunk?
                    _fileHasher.Append(chunkBytes);
                    byte[] chunkHash = _chunkHasher.GetHashAndReset();

                    ChunkSignature chunkSig = new()
                    {
                        StartOffset = dataStream.Position - ChunkSize,
                        Hash = chunkHash,
                        Length = (short)read,
                    };
            
                    sigWriter.WriteChunk(chunkSig);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(heapBuffer);
        }
    }

    private void WriteMetadata(ISignatureWriter sigWriter)
    {
        byte[] fileHash = _fileHasher.GetHashAndReset();
        SignatureMetadata metadata = new(fileHash, _hashingAlgorithmOption);
        sigWriter.WriteMetadata(metadata);
    }
}