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
        Span<byte> headerBuffer = stackalloc byte[BinaryFormat.SignatureHeader.Length];
        int read = _br.Read(headerBuffer);
        if(read != BinaryFormat.SignatureHeader.Length)
            throw new InvalidDataException("Signature header length could not be read from the stream.");
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
        //TODO: Clean this up
        int metadataSize = sizeof(byte) + (sizeof(ushort) * 2) + metadata.HashAlgorithmIdentifier.Length + metadata.RollingHashAlgorithmIdentifier.Length;
        int headerSize = BinaryFormat.SignatureHeader.Length;
        int targetStreamPosition = metadataSize + headerSize + 1;
        if (BaseStream.Position != targetStreamPosition)
        {
            if(!BaseStream.CanSeek)
                throw new DataException("Stream is not in position to read chunk data and is not seekable.");
            BaseStream.Seek(targetStreamPosition, SeekOrigin.Begin);
        }
        List<ChunkSignature> chunks = new List<ChunkSignature>((int)metadata.ChunkCount);

        const int largestBufferSize = 4096;
        //TODO: Clean up this buffer allocation
        int idealBufferSize = metadata.ChunkSize * (int)Math.Floor((double)largestBufferSize / metadata.ChunkSize);
        byte[] heapBuffer = ArrayPool<byte>.Shared.Rent(idealBufferSize);
        //TODO: TRY-CATCH!
        Span<byte> chunkBuffer = heapBuffer.AsSpan(0, idealBufferSize);

        //The idea behind this is to share references to hashes that have already been read instead of keeping
        //Duplicate hash arrays in memory. This makes reading more expensive for a potentially smaller object.
        HashSet<byte[]> chunkHashes = new HashSet<byte[]>(new ByteArrayComparer());
        byte[] temporaryHashBuffer = new byte[metadata.HashLength];
        int read;
        //TODO: Potentially replace the chunks.Count with it's own counter to skirt the property penalty
        while ((read = BaseStream.Read(chunkBuffer)) > 0)
        {
            for (int i = 0; i < read / metadata.ChunkSize; i++)
            {
                ReadOnlySpan<byte> chunkData = chunkBuffer.Slice(i * metadata.ChunkSize, metadata.ChunkSize);
                
                chunkData[..metadata.HashLength].CopyTo(temporaryHashBuffer);
                
                byte[] chunkHash; //C# is odd and despite this having the MaybeNullWhen(false) attrib and this being guarded
                //by a statement to cover the possibility for null, it still warns about potentially casting to null
                if (!chunkHashes.TryGetValue(temporaryHashBuffer, out chunkHash))
                {
                    chunkHash = new byte[metadata.HashLength];
                    Array.Copy(temporaryHashBuffer, 0, chunkHash, 0, metadata.HashLength);
                    chunkHashes.Add(chunkHash);
                }

                uint checksum = BitConverter.ToUInt32(chunkData[(metadata.HashLength + 1)..]);

                ChunkSignature chunkSig = new()
                {
                    Hash = chunkHash,
                    Length = metadata.ChunkSize,
                    Offset = (ulong)i * metadata.ChunkSize,
                    RollingChecksum = checksum
                };
                
                chunks.Add(chunkSig);
            }
        }
        
        //Process final chunk
        //TODO: Rework this so we don't have to retroactively do this (Also i believe rarely this can fail currently!)
        ChunkSignature lastChunk = chunks[^1];
        chunks[^1] = lastChunk with { Length = BitConverter.ToUInt16(chunkBuffer.Slice(read - sizeof(ushort), sizeof(ushort))) };
        return new Signature(metadata, [..chunks]);
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