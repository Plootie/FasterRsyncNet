namespace FasterRsyncNet.Delta;

public interface IDeltaWriter
{
    void WriteDataCommand(Stream source, long position, long length);
    void WriteCopyCommand(long position, long length);
}