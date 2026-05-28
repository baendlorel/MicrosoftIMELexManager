using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MicrosoftIMELexManager.Models;

namespace MicrosoftIMELexManager.Services;

public sealed class IHFileService
{
    private const ushort Signature1 = 0xAA55;
    private const ushort SubType = 0x8088;
    private const int DataStart = 0x1400;
    private const int RecordSize = 60;
    private const int MaxWordLength = 20;

    public List<IHEntry> Read(string path)
    {
        var data = File.ReadAllBytes(path);

        ValidateHeader(data);

        uint recordCount = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x0C));
        var entries = new List<IHEntry>((int)recordCount);

        for (int i = 0; i < recordCount; i++)
        {
            int off = DataStart + i * RecordSize;
            if (off + RecordSize > data.Length) break;

            uint wordLen = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(off));
            if (wordLen == 0 || wordLen > MaxWordLength) continue;

            uint timestamp = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(off + 4));
            uint frequency = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(off + 8));

            string word = Encoding.Unicode.GetString(data, off + 0x0C, (int)wordLen * 2);

            entries.Add(new IHEntry
            {
                Word = word,
                Frequency = frequency,
                Timestamp = timestamp,
            });
        }

        return entries;
    }

    /// <summary>
    /// Write modified entries back. Uses in-place replacement of frequency fields
    /// to avoid rebuilding the entire file.
    /// </summary>
    public void Write(string sourcePath, string destPath, List<IHEntry> modifiedEntries)
    {
        var data = File.ReadAllBytes(sourcePath);

        ValidateHeader(data);

        uint recordCount = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x0C));

        // Build a lookup from word -> new frequency
        var freqMap = new Dictionary<string, uint>(modifiedEntries.Count);
        foreach (var entry in modifiedEntries)
            freqMap[entry.Word] = entry.Frequency;

        for (int i = 0; i < recordCount; i++)
        {
            int off = DataStart + i * RecordSize;
            if (off + RecordSize > data.Length) break;

            uint wordLen = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(off));
            if (wordLen == 0 || wordLen > MaxWordLength) continue;

            string word = Encoding.Unicode.GetString(data, off + 0x0C, (int)wordLen * 2);

            if (freqMap.TryGetValue(word, out uint newFreq))
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(off + 8), newFreq);
            }
        }

        File.WriteAllBytes(destPath, data);
    }

    /// <summary>
    /// Delete entries by marking their word length to 0 (soft delete by clearing frequency).
    /// </summary>
    public void DeleteEntries(string sourcePath, string destPath, HashSet<string> wordsToDelete)
    {
        var data = File.ReadAllBytes(sourcePath);

        ValidateHeader(data);

        uint recordCount = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x0C));

        for (int i = 0; i < recordCount; i++)
        {
            int off = DataStart + i * RecordSize;
            if (off + RecordSize > data.Length) break;

            uint wordLen = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(off));
            if (wordLen == 0 || wordLen > MaxWordLength) continue;

            string word = Encoding.Unicode.GetString(data, off + 0x0C, (int)wordLen * 2);

            if (wordsToDelete.Contains(word))
            {
                // Zero out the entire record
                Array.Clear(data, off, RecordSize);
            }
        }

        File.WriteAllBytes(destPath, data);
    }

    private static void ValidateHeader(byte[] data)
    {
        if (data.Length < DataStart)
            throw new InvalidDataException("File too small to be a valid IH.dat");

        ushort sig1 = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0));
        ushort sub = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(2));

        if (sig1 != Signature1 || sub != SubType)
            throw new InvalidDataException($"Not a valid IH.dat file (expected magic 0x{Signature1:X4}/0x{SubType:X4}, got 0x{sig1:X4}/0x{sub:X4})");
    }
}
