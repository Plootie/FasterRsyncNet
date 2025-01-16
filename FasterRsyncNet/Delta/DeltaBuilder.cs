using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FasterRsyncNet.Chunk;
using FasterRsyncNet.Core;
using FasterRsyncNet.Hash;

namespace FasterRsyncNet.Delta;

public class DeltaBuilder
{
    public void BuildDelta(Stream newFileStream, Signature.Signature fileSignature, IDeltaWriter deltaWriter)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Dictionary<uint, List<ChunkSignature>> chunkMap = new();
        foreach (ChunkSignature chunk in fileSignature.Chunks)
        {
            if (chunkMap.TryGetValue(chunk.RollingChecksum, out List<ChunkSignature>? value))
            {
                value.Add(chunk);
            }
            else
            {
                chunkMap.Add(chunk.RollingChecksum, [chunk]);
            }
        }

        stopwatch.Stop();
        Console.WriteLine("Dictionary build too {0}ms", stopwatch.ElapsedMilliseconds);

        INonCryptographicHashingAlgorithm hasher = fileSignature.HashAlgorithm;
        IRollingChecksum roller = fileSignature.RollingChecksum;
        
        //Due to how this is calculated all chunks will be the same length except for the last which will be
        //Between 1 and the chunk size
        int normalChunkSize = fileSignature.Chunks.First().Length;
        int finalChunkSize = fileSignature.Chunks.Last().Length;
        int hashLength = fileSignature.HashAlgorithm.HashLengthInBytes;
        
        int optimalBufferSize = Math.Max(8192, (int)fileSignature.Chunks[0].Length);
        byte[] heapBuffer = ArrayPool<byte>.Shared.Rent(optimalBufferSize);
        byte[] hashBuffer = ArrayPool<byte>.Shared.Rent(normalChunkSize);
        try
        {
            Span<byte> fileBufferSpan = heapBuffer.AsSpan(0, normalChunkSize);
            Span<byte> hashBufferSpan = hashBuffer.AsSpan(0, normalChunkSize);
            long lastMatch = newFileStream.Position;
            long filePosition = newFileStream.Position;
            uint checksum = 1;

            RingBuffer<byte> ringBuffer = new((uint)normalChunkSize + 1);
            while (true)
            {
                //The idea behind this is if we are coming from a new match we want to read normalChunkSize of bytes in one go
                int goalToRead = Math.Min(ringBuffer.Capacity - ringBuffer.Count, normalChunkSize);
                Span<byte> chunkBuffer = fileBufferSpan.Slice(0, goalToRead);
                int read = newFileStream.Read(chunkBuffer);
                ringBuffer.Add(chunkBuffer);
                if (read == 0)
                    break;

                if (ringBuffer.Count == ringBuffer.Capacity)
                {
                    checksum = roller.Rotate(checksum, ringBuffer.Take(), chunkBuffer[0], normalChunkSize);
                }
                else
                {
                    checksum = roller.CalculateBlock(chunkBuffer, checksum);
                }
                
                if(!chunkMap.TryGetValue(checksum, out List<ChunkSignature>? chunks))
                    continue;
                
                Span<byte> potentialHashMatch = hashBufferSpan.Slice(0, hashLength);
                ringBuffer.CopyTo(hashBufferSpan);
                hasher.Append(hashBufferSpan);
                hasher.GetHashAndReset(potentialHashMatch);

                ChunkSignature? matchingChunk = null;
                foreach (ChunkSignature chunk in chunks)
                {
                    if (CompareHashes(chunk.Hash, potentialHashMatch))
                    {
                        matchingChunk = chunk;
                        break;
                    }
                }

                if (!matchingChunk.HasValue)
                    continue;

                long lastMatchGap = newFileStream.Position - lastMatch;
                if (lastMatchGap > normalChunkSize)
                {
                    deltaWriter.WriteDataCommand(newFileStream, filePosition - lastMatch, lastMatch - normalChunkSize);
                }
                
                deltaWriter.WriteCopyCommand(matchingChunk.Value.StartOffset, matchingChunk.Value.Length);

                checksum = 1;
                ringBuffer.Clear();
                lastMatch = newFileStream.Position;
            }
            
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(heapBuffer);
            ArrayPool<byte>.Shared.Return(hashBuffer);
        }
    }

    private static bool CompareHashes(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        //TODO: Implement a solution that works for more sizes of hashes
        if(first.Length == 8)
            return Unsafe.ReadUnaligned<ulong>(ref MemoryMarshal.GetReference(first)) ==
                   Unsafe.ReadUnaligned<ulong>(ref MemoryMarshal.GetReference(second));
        
        for (int i = 0; i < first.Length; i++)
        {
            if (first[i] != second[i])
                return false;
        }

        return true;
    }
}