using System.Buffers;
using System.Collections.Immutable;
using FasterRsyncNet.Chunk;
using FasterRsyncNet.Core;
using FasterRsyncNet.Hash;

namespace FasterRsyncNet.Signature;

//TODO: Implement async options
public class SignatureReader(Stream signatureStream) : ISignatureReader, IDisposable
{
    private readonly BinaryReader _reader = new(signatureStream);
    public Signature ReadSignature()
    {
        signatureStream.Seek(0 , SeekOrigin.Begin);
        SignatureMetadata metadata = ReadSignatureMetadata();
        Signature signature = new(metadata);
        signature.Chunks = [..ReadChunks(signature)];
        return signature;
    }

    public SignatureMetadata ReadSignatureMetadata()
    {
        //I would love to load the entire metadata into a single array pool array but the hash length will be unknown
        byte[] magicByteBuffer = ArrayPool<byte>.Shared.Rent(FasterRsyncBinaryFormat.SignatureHeader.Length);
        try
        {
            _ = _reader.Read(magicByteBuffer, 0, FasterRsyncBinaryFormat.SignatureHeader.Length);

            for (int i = 0; i < FasterRsyncBinaryFormat.SignatureHeader.Length; i++)
            {
                if(FasterRsyncBinaryFormat.SignatureHeader[i] != magicByteBuffer[i])
                    throw new InvalidDataException("The provided file indicates it is of the wrong format.");
            }
            
            byte metadataVersion = _reader.ReadByte();
            //TODO: Rework this so each reader has it's own version instead of the project current
            if (metadataVersion != SignatureMetadata.SignatureMetadataVersion)
                throw new InvalidDataException(
                    $"The provided signature is of a different version. Got: {metadataVersion}, Expected: {SignatureMetadata.SignatureMetadataVersion}");

            NonCryptographicHashingAlgorithmOption
                ncOption = (NonCryptographicHashingAlgorithmOption)_reader.ReadByte();

            Type ncType = HashHelper.NonCryptographicHashingAlgorithmMapper[ncOption];
            INonCryptographicHashingAlgorithm ncAlgorithm =
                HashHelper.InstanceFromType<INonCryptographicHashingAlgorithm>(ncType);
            
            RollingChecksumOption rcOption = (RollingChecksumOption)_reader.ReadByte();
            
            short chunkSize = _reader.ReadInt16();
            byte[] hash = _reader.ReadBytes(ncAlgorithm.HashLengthInBytes);
            
            return new SignatureMetadata(hash, ncOption, rcOption, chunkSize, metadataVersion);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(magicByteBuffer);
        }
    }

    private ChunkSignature[] ReadChunks(Signature signature)
    {
        long start = 0;
        
        long remainingBytes = _reader.BaseStream.Length - _reader.BaseStream.Position;
        int chunkLength = signature.HashAlgorithm.HashLengthInBytes + ChunkSignature.ChunkSize;
        //TODO: Fix this line
        /*if(remainingBytes % chunkLength != 0)
            throw new InvalidDataException("The provided signature has malformed chunks.");*/
        
        long expectedChunks = (int)Math.Floor(remainingBytes / (double)chunkLength);
        ChunkSignature[] signatures = new ChunkSignature[expectedChunks];
        int hashLength = signature.HashAlgorithm.HashLengthInBytes;
        for (long i = 0; i < expectedChunks - 1; i++)
        {
            byte[] hash = _reader.ReadBytes(hashLength);
            uint rollingChecksum = _reader.ReadUInt32();

            ChunkSignature chunk = new()
            {
                StartOffset = start,
                Length = signature.Metadata.ChunkSize,
                RollingChecksum = rollingChecksum,
                Hash = hash
            };
            signatures[i] = chunk;
            
            start += chunk.Length;
        }
        
        //Final chunk contain more info we need to read
        byte[] finalHash = _reader.ReadBytes(signature.HashAlgorithm.HashLengthInBytes);
        uint finalRollingChecksum = _reader.ReadUInt32();
        short finalChunkLength = _reader.ReadInt16();
        
        ChunkSignature finalChunk = new()
        {
            StartOffset = start,
            Length = finalChunkLength,
            RollingChecksum = finalRollingChecksum,
            Hash = finalHash
        };
        
        signatures[^1] = finalChunk;
        
        return signatures;
    }

    public void Dispose()
    {
        _reader.Dispose();
        GC.SuppressFinalize(this);
    }
}