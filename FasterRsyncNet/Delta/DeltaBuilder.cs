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
        #region Dictionary
        Stopwatch stopwatch = Stopwatch.StartNew();
        Dictionary<uint, List<ChunkSignature>> chunkMap = new();
        foreach (ChunkSignature chunk in fileSignature.Chunks)
        {
            if (chunkMap.TryGetValue(chunk.RollingChecksum, out List<ChunkSignature>? value))
            {
                bool unique = true;
                foreach (ChunkSignature chunkSignature in value)
                {
                    //TODO: Remove this. This is a bandaid fix for a larger issue. Duplicate chunks with different positions
                    //Are all saved in the signature, This is a waste of disk space, memory and CPU time. This should be resolved
                    if (!CompareHashes(chunkSignature.Hash, chunk.Hash)) continue;
                    unique = false;
                    break;
                }
                if(unique)
                    value.Add(chunk);
            }
            else
            {
                chunkMap.Add(chunk.RollingChecksum, [chunk]);
            }
        }

        stopwatch.Stop();
        Console.WriteLine("Dictionary build too {0}ms", stopwatch.ElapsedMilliseconds);
        #endregion
        
        INonCryptographicHashingAlgorithm hasher = fileSignature.HashAlgorithm;
        IRollingChecksum roller = fileSignature.RollingChecksum;
        
        //Due to how this is calculated all chunks will be the same length except for the last which will be
        //Between 1 and the chunk size
        #region Setup
        int normalChunkSize = fileSignature.Chunks.First().Length;
        int finalChunkSize = fileSignature.Chunks.Last().Length;
        int hashLength = fileSignature.HashAlgorithm.HashLengthInBytes;
        
        int optimalBufferSize = Math.Max(8192, (int)fileSignature.Chunks[0].Length);
        byte[] heapBuffer = ArrayPool<byte>.Shared.Rent(optimalBufferSize);
        byte[] fileByteBuffer = ArrayPool<byte>.Shared.Rent(normalChunkSize);
        #endregion
        
        try
        {
            Span<byte> fileBufferSpan = heapBuffer.AsSpan(0, normalChunkSize);
            Span<byte> fileByteBufferSpan = fileByteBuffer.AsSpan(0, normalChunkSize);
            Span<byte> fileHashBufferSpan = stackalloc byte[hashLength];
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
                filePosition += read;
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
                
                ringBuffer.CopyTo(fileByteBufferSpan);
                hasher.Append(fileByteBufferSpan);
                hasher.GetHashAndReset(fileHashBufferSpan);

                ChunkSignature? matchingChunk = null;
                foreach (ChunkSignature chunk in chunks)
                {
                    if (CompareHashes(chunk.Hash, fileHashBufferSpan))
                    {
                        matchingChunk = chunk;
                        break;
                    }
                }

                if (!matchingChunk.HasValue)
                    continue;

                long lastMatchGap = filePosition - lastMatch;
                if (lastMatchGap > normalChunkSize)
                {
                    deltaWriter.WriteDataCommand(newFileStream, filePosition - lastMatch, lastMatch - normalChunkSize);
                }
                
                deltaWriter.WriteCopyCommand(matchingChunk.Value.StartOffset, matchingChunk.Value.Length);

                checksum = 1;
                ringBuffer.Clear();
                lastMatch = filePosition;
            }
            
            //TODO: Perform additional logic to find the "final" chunk, which potentially differs in size from the others
            //To do this we need to backtrack to the last match and slice forward with a smaller window matching its size
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(heapBuffer);
            ArrayPool<byte>.Shared.Return(fileByteBuffer);
        }
    }

    private static bool CompareHashes(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        ref byte h1 = ref MemoryMarshal.GetReference(first);
        ref byte h2 = ref MemoryMarshal.GetReference(second);

        int index = 0;
        int maxIndex = (first.Length / sizeof(ulong)) * sizeof(ulong);
        while (index < maxIndex)
        {
            ulong v1 = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref h1, index));
            ulong v2 = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref h2, index));
            if (v1 != v2) return false;
            index += sizeof(ulong);
        }

        for (; index < maxIndex; index++)
        {
            byte b1 = Unsafe.Add(ref h1, index);
            byte b2 = Unsafe.Add(ref h2, index);
            if (b1 != b2) return false;
            index += sizeof(byte);
        }

        return true;
    }
}