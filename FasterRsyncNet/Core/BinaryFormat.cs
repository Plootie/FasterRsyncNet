using System.Collections.Immutable;
using System.Text;

namespace FasterRsyncNet.Core;

public static class BinaryFormat
{
    public static readonly ImmutableArray<byte> SignatureHeader = [.."FSRS"u8.ToArray()];
    public static readonly ImmutableArray<byte> DeltaHeader = [.."FSRD"u8.ToArray()];
    public static readonly byte FormatVersion = 1;
    public static readonly byte DataCommand = 0x80;
    public static readonly byte CopyCommand = 0x60;
}