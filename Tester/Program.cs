using System.Diagnostics;
using FasterRsyncNet.Chunk;
using FasterRsyncNet.Core;
using FasterRsyncNet.Delta;
using FasterRsyncNet.Hash;
using FasterRsyncNet.Hash.HashingAlgorithms.Rolling;
using FasterRsyncNet.Signature;

using FileStream inputStream = File.OpenRead("testfile.bin");

string[] sizes = { "B", "KB", "MB", "GB", "TB" };
double len = inputStream.Length;
int order = 0;
while (len >= 1024 && order < sizes.Length - 1) {
    order++;
    len /= 1024;
}
string inputFileSize = $"{len:0.##} {sizes[order]}";


Stopwatch stopwatch = new();
using (FileStream outputStream = File.Open("signature.sig", FileMode.Create))
{
    ISignatureWriter signatureWriter = new SignatureWriter(outputStream);
    stopwatch.Start();
    SignatureBuilder sigBuilder = new SignatureBuilder(NonCryptographicHashingAlgorithmOption.XXHash64, RollingChecksumOption.Adler32)
    {
        ChunkSize = SignatureBuilder.MinChunkSize
    };
    sigBuilder.BuildSignature(inputStream, signatureWriter);
}
stopwatch.Stop();
Console.WriteLine("Input size: {0}\nExecution time: {1}ms", inputFileSize, stopwatch.Elapsed.TotalMilliseconds);
GC.Collect();


//Thread.Sleep(1000);
//Console.ReadLine();


using FileStream signatureStream = File.OpenRead("signature.sig");
ISignatureReader signatureReader = new SignatureReader(signatureStream);
Signature signature = signatureReader.ReadSignature();
//Console.Read();

//Delta tests
stopwatch.Reset();
stopwatch.Start();
DeltaBuilder deltaBuilder = new DeltaBuilder();
deltaBuilder.BuildDelta(File.OpenRead("testfile.bin"), signature, new DeltaWriter(File.OpenWrite("testDelta.delta")));
Console.WriteLine(stopwatch.Elapsed.TotalMilliseconds);
//Console.ReadLine();