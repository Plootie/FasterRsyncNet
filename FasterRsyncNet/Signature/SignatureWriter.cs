using FasterRsyncNet.Chunk;
using FasterRsyncNet.Core;

namespace FasterRsyncNet.Signature;

public class SignatureWriter(Stream signatureStream) : ISignatureWriter
{
    private readonly BinaryWriter _writer = new(signatureStream);
    public Stream BaseStream { get; } = signatureStream;

    private static void WritePartialMetadata(BinaryWriter binaryWriter, SignatureMetadata metadata)
    {
        binaryWriter.Write(FasterRsyncBinaryFormat.SignatureHeader.ToArray());
        binaryWriter.Write(metadata.Version);
        binaryWriter.Write((byte)metadata.NonCryptographicHashingAlgorithmOption);
    }

    public void WriteMetadata(SignatureMetadata metadata)
    {
        WritePartialMetadata(_writer, metadata);
        _writer.Write(metadata.Hash);
    }

    //TODO: Benchmark this. I actually kinda doubt this will help performance even with larger hash sizes
    public async Task WriteMetadataAsync(SignatureMetadata metadata)
    {
        WritePartialMetadata(_writer, metadata);
        await BaseStream.WriteAsync(metadata.Hash).ConfigureAwait(false);
    }
    
    public void WriteChunk(ChunkSignature chunk)
    {
        _writer.Write(chunk.Length);
        _writer.Write(chunk.Hash);
    }

    //TODO: Same as above. I doubt this is helping performance
    public async Task WriteChunkAsync(ChunkSignature chunk)
    {
        _writer.Write(chunk.Length);
        await BaseStream.WriteAsync(chunk.Hash).ConfigureAwait(false);
    }

    void IDisposable.Dispose()
    {
        _writer.Dispose();
        //Rider screams at me about not having this, The docs say it has no affect if there is no finalizer though
        GC.SuppressFinalize(this);
    }
    
    public async ValueTask DisposeAsync()
    {
        await _writer.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}