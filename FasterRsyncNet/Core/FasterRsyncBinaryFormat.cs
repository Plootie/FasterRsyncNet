using System.Collections.Immutable;
using System.Text;

namespace FasterRsyncNet.Core;

public static class FasterRsyncBinaryFormat
{
    public static readonly ImmutableArray<byte> SignatureHeader = [..Encoding.ASCII.GetBytes("FSRS")];
    public static readonly ImmutableArray<byte> DeltaHeader = [..Encoding.ASCII.GetBytes("FSRD")];
}