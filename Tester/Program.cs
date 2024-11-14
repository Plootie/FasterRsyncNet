using System.Diagnostics;
using FasterRsyncNet;
using FasterRsyncNet.Hash;
using FasterRsyncNet.Hash.HashingAlgorithms.NonCryptographic;
using FasterRsyncNet.Signature;

using FileStream inputStream = File.Open("testfile.bin", FileMode.Open);
using FileStream outputStream = File.Open("signature.sig", FileMode.OpenOrCreate, FileAccess.ReadWrite);

SignatureBuilder signatureBuilder =
    new SignatureBuilder(NonCryptographicHashingAlgorithmOption.XXHash64, RollingChecksumOption.Adler32)
    {
        ChunkSize = 128
    };
ISignatureWriter signatureWriter = new SignatureWriter(outputStream);

Stopwatch timer = Stopwatch.StartNew();
signatureBuilder.BuildSignature(inputStream, signatureWriter);
timer.Stop();
Console.WriteLine("Done! {0}ms / {1}s", timer.ElapsedMilliseconds, timer.ElapsedMilliseconds / 1000);

inputStream.Seek(0, SeekOrigin.Begin);
ISignatureReader signatureReader = new SignatureReader(outputStream);
Signature outputSignature = signatureReader.ReadSignature();

byte[] buffer = new byte[128];
_ = inputStream.Read(buffer, 0, buffer.Length);
XXHash64 hasher = new XXHash64();
hasher.Append(buffer);

byte[] comparisonHash = hasher.GetHashAndReset();

Console.WriteLine("Hashes match? {0}", comparisonHash.SequenceEqual(outputSignature.Chunks[0].Hash));
Console.ReadLine();