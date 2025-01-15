using System.Buffers;
using System.Diagnostics;
using FasterRsyncNet.Chunk;
using FasterRsyncNet.Core;
using FasterRsyncNet.Hash;
using FasterRsyncNet.Signature;

namespace FasterRsyncNet.Delta;

public class DeltaBuilder
{
    public void BuildDelta(Stream newFileStream, Signature.Signature fileSignature, IDeltaWriter deltaWriter)
    {
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
            Span<byte> fileBufferSpan = heapBuffer.AsSpan(0, optimalBufferSize);
            Span<byte> hashBufferSpan = hashBuffer.AsSpan(0, normalChunkSize);
            long lastMatch = newFileStream.Position;
            long filePosition = newFileStream.Position;
            uint checksum = 1;
            int read;

            RingBuffer<byte> ringARingoBytes = new((uint)normalChunkSize + 1);
            while ((read = newFileStream.Read(fileBufferSpan)) > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    byte newByte = fileBufferSpan[i];
                    ReadOnlySpan<byte> newBytesAsSpan = fileBufferSpan.Slice(i, 1);
                    ringARingoBytes.Add(newByte);
                    filePosition++;
                    long bytesSinceLastMatch = filePosition - lastMatch;
                    
                    //TODO: Rework. There is no point checking the dictionary before we have ChunkSize bytes in the checksum
                    if (ringARingoBytes.Count == ringARingoBytes.Capacity)
                    {
                        checksum = roller.Rotate(checksum, ringARingoBytes.Take(), newByte, normalChunkSize);
                    }
                    else
                    {
                        checksum = roller.CalculateBlock(newBytesAsSpan, checksum);
                    }
                    
                    //We don't have enough bytes to bother checking
                    if (bytesSinceLastMatch < normalChunkSize)
                        continue;
                    
                    if (!chunkMap.TryGetValue(checksum, out List<ChunkSignature>? matchingChunks))
                        continue;
                    
                    //Double check that the match is correct
                    Span<byte> potentialHashMatch = hashBufferSpan.Slice(0, hashLength);
                    ringARingoBytes.CopyTo(hashBuffer);
                    hasher.Append(hashBuffer);
                    hasher.GetHashAndReset(potentialHashMatch);

                    ChunkSignature? matchingChunk = null;
                    foreach (ChunkSignature chunk in matchingChunks)
                    {
                        if (CompareHashes(chunk.Hash, potentialHashMatch))
                        {
                            matchingChunk = chunk;
                            break;
                        }
                    }

                    if (!matchingChunk.HasValue)
                        continue; //Rolling checksum match was just a hash collision
                    
                    
                    //A match has been found. We now need to write all data that preceded this match before writing the copy command
                    long lastMatchGap = filePosition - lastMatch;
                    if (lastMatchGap > normalChunkSize)
                    {
                        deltaWriter.WriteDataCommand(newFileStream, filePosition - lastMatchGap, normalChunkSize);
                    }
                    
                    //Now we write the copy for the match
                    deltaWriter.WriteCopyCommand(matchingChunk.Value.StartOffset, matchingChunk.Value.Length);
                    
                    //Reset the checksum and the trackers
                    checksum = 1;
                    ringARingoBytes.Clear();
                    lastMatch = filePosition;
                }
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
        bool ret = true;
        for (int i = 0; i < first.Length; i++)
        {
            if(first[i] == second[i]) continue;
            ret = false;
            break;
        }
        return ret;
    }
}