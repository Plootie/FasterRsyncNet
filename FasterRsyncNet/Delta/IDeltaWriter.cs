namespace FasterRsyncNet.Delta;

public interface IDeltaWriter
{
    //TODO: Use DataRange struct instead
    public void WriteData(Stream source, DataRange range);
    public void WriteData(ReadOnlySpan<byte> source);
    public void WriteCopy(DataRange range);
}