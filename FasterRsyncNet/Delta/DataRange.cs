namespace FasterRsyncNet.Delta;

public readonly record struct DataRange(long Position, long Length)
{
    public required long Position { get; init; } = Position;
    public required long Length { get; init; } = Length;
}