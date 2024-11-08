namespace FasterRsyncNet.Signature;

public interface ISignatureReader
{
    Signature ReadSignature();
    SignatureMetadata ReadSignatureMetadata();
}