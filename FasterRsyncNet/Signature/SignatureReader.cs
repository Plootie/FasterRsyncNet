using System.Buffers;
using System.Collections.Immutable;
using System.Data;
using FasterRsyncNet.Chunk;
using FasterRsyncNet.Core;

namespace FasterRsyncNet.Signature;

public class SignatureReader(Stream inputStream) : ISignatureReader
{
    public Stream BaseStream => _br.BaseStream;
    private readonly BinaryReader _br = new BinaryReader(inputStream);
    public bool CheckHeader()
    {
        long prevPosition = BaseStream.Position;
        BaseStream.Seek(0, SeekOrigin.Begin);
        
        Span<byte> headerBuffer = stackalloc byte[BinaryFormat.SignatureHeader.Length];
        int read = _br.Read(headerBuffer);
        
        if(prevPosition > 0)
            BaseStream.Seek(prevPosition, SeekOrigin.Begin);
        
        if(read != BinaryFormat.SignatureHeader.Length)
            throw new InvalidDataException("Signature header could not be read from the stream.");
        
        return headerBuffer.SequenceEqual(BinaryFormat.SignatureHeader.AsSpan());
    }

    public SignatureMetadata ReadMetadata()
    {
        if (BaseStream.Position != BinaryFormat.SignatureHeader.Length)
        {
            if (!BaseStream.CanSeek)
                throw new DataException("Stream is not in position to read metadata and is not seekable.");
            BaseStream.Seek(BinaryFormat.SignatureHeader.Length, SeekOrigin.Begin);
        }
        
        byte version = _br.ReadByte();
        if (version != BinaryFormat.FormatVersion)
            throw new InvalidDataException($"Signature version is {version} but expected {BinaryFormat.FormatVersion}");

        return new SignatureMetadata
        {
            Version = version,
            ChunkSize = _br.ReadUInt16(),
            ChunkCount = _br.ReadUInt64(),
            HashLength = _br.ReadUInt16(),
            HashAlgorithmIdentifier = _br.ReadString(),
            RollingHashAlgorithmIdentifier = _br.ReadString()
        };
    }

    public Signature ReadSignature()
    {
        throw new NotImplementedException();
    }

    public Signature ReadSignature(SignatureMetadata metadata)
    {
        //TODO: Find a way to not need to calculate this. Should we skip to begin and re-read???
        /*
        int metadataSize = sizeof(byte) + sizeof(ushort) * 2 + 
                           metadata.HashAlgorithmIdentifier.Length +
                           metadata.RollingHashAlgorithmIdentifier.Length;
        int targetStreamPosition = BinaryFormat.SignatureHeader.Length + metadataSize;
        if (BaseStream.Position != targetStreamPosition)
        {
            if(!BaseStream.CanSeek)
                throw new NotSupportedException("Stream is not in position to read chunk data and is not seekable.");
            //BaseStream.Seek(targetStreamPosition, SeekOrigin.Begin);
        }
        */
        
        List<ChunkSignature> chunks = new((int)metadata.ChunkCount);
        Span<byte> chunkHashBuffer = stackalloc byte[metadata.HashLength];
        for (ulong i = 0; i < metadata.ChunkCount; i++)
        {
            int debug = _br.Read(chunkHashBuffer);
            uint rollingChecksum = _br.ReadUInt32();
            ulong position = metadata.ChunkSize * i;
            byte[] chunkHash = chunkHashBuffer.ToArray();

            chunks.Add(new ChunkSignature()
            {
                Hash = [..chunkHash],
                Length = metadata.ChunkSize,
                Offset = position,
                RollingChecksum = rollingChecksum
            });
        }
    
        ushort len = _br.ReadUInt16();
        chunks[^1] = chunks[^1] with { Length = len };
        return new Signature
        {
            Chunks = [..chunks],
            Metadata = metadata
        };
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _br.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await Task.Run(() => _br.Dispose());
    }
}