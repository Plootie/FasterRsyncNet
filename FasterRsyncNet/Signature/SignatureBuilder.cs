﻿using System.Buffers;
using System.Diagnostics;
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
    private readonly RollingChecksumOption _rollingChecksumOption;
    private readonly INonCryptographicHashingAlgorithm _chunkHasher;
    private readonly INonCryptographicHashingAlgorithm _fileHasher;
    private readonly IRollingChecksum _rollingChunkHasher;
    
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
            //_rollingChunkHasher = HashHelper.InstanceFromType<IRollingChecksum>(HashHelper.RollingChecksumMapper[_rollingChecksumOption]);
            //TODO: This feels like a terrible idea. This should be cleaned up
        }
    }

    public SignatureBuilder(NonCryptographicHashingAlgorithmOption hashingAlgorithmOption,
        RollingChecksumOption rollingChecksumOption, short chunkSize = DefaultChunkSize)
    {
        _hashingAlgorithmOption = hashingAlgorithmOption;
        _rollingChecksumOption = rollingChecksumOption;
        ChunkSize = chunkSize;

        Type hashingAlgorithmType = HashHelper.NonCryptographicHashingAlgorithmMapper[hashingAlgorithmOption];
        _chunkHasher = HashHelper.InstanceFromType<INonCryptographicHashingAlgorithm>(hashingAlgorithmType);
        _fileHasher = HashHelper.InstanceFromType<INonCryptographicHashingAlgorithm>(hashingAlgorithmType);

        Type rollingChecksumType = HashHelper.RollingChecksumMapper[rollingChecksumOption];
        _rollingChunkHasher = HashHelper.InstanceFromType<IRollingChecksum>(rollingChecksumType);
    }

    public void BuildSignature(Stream dataStream, ISignatureWriter sigWriter)
    {
        //We cannot know the metadata info before calculating the data ahead so we skip forward and will write at the end
        int hashLength = _chunkHasher.HashLengthInBytes;
        sigWriter.WriteMetadata(new SignatureMetadata(new byte[hashLength], NonCryptographicHashingAlgorithmOption.Unknown, RollingChecksumOption.Unknown, 0));
        
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
            Span<byte> fileBuffer = heapBuffer.AsSpan(0, bufferLength);
            ChunkSignature? lastChunk = null;
            
            int read;
            while ((read = dataStream.Read(fileBuffer)) > 0)
            {
                //Ceil means that when we hit the EOF and we get a partial chunk we still chunk those final bytes
                int chunksToProcess = (int)Math.Ceiling((double)read / ChunkSize);
                
                //I don't see an easy way to parallelize this loop. doing so would require multiple hashers
                for (int i = 0; i < chunksToProcess; i++)
                {
                    int chunkOffset = i * ChunkSize;
                    int chunkLength = Math.Min(ChunkSize, read - chunkOffset);
                    Span<byte> chunkBytes = fileBuffer.Slice(chunkOffset, chunkLength);
                    
                    _chunkHasher.Append(chunkBytes);
                    byte[] chunkHash = _chunkHasher.GetHashAndReset();
                    _fileHasher.Append(chunkHash.AsSpan());
                    uint rollingChecksum = _rollingChunkHasher.CalculateBlock(chunkBytes);

                    ChunkSignature chunkSig = new()
                    {
                        StartOffset = dataStream.Position - ChunkSize,
                        Hash = chunkHash,
                        RollingChecksum = rollingChecksum,
                        Length = (short)chunkLength
                    };
            
                    sigWriter.WriteChunk(chunkSig);
                    lastChunk = chunkSig;
                }
            }
            
            if(!lastChunk.HasValue)
                throw new InvalidDataException("No chunks were generated from input stream.");
            sigWriter.WriteFinalChunkData(lastChunk.Value);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(heapBuffer);
        }
    }

    private void WriteMetadata(ISignatureWriter sigWriter)
    {
        byte[] fileHash = _fileHasher.GetHashAndReset();
        SignatureMetadata metadata = new(fileHash, _hashingAlgorithmOption, _rollingChecksumOption, ChunkSize);
        sigWriter.WriteMetadata(metadata);
    }
}