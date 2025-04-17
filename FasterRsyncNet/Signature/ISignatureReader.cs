namespace FasterRsyncNet.Signature;

public interface ISignatureReader : IDisposable, IAsyncDisposable
{
    public Stream BaseStream { get; }
    public bool CheckHeader();
    public SignatureMetadata ReadMetadata();
    public Signature ReadSignature(SignatureMetadata metadata);
}