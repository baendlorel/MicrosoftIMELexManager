using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MicrosoftIMELexManager.Data;
using MicrosoftIMELexManager.Models;

namespace MicrosoftIMELexManager.Services;

public sealed class UDLFileService
{
    private const ushort Signature1 = 0xAA55;
    private const ushort SubType = 0x8188;
    private const int DataStart = 0x2400;
    private const int RecordSize = 60;
    private const int MaxWordLength = 12;
    private const int WordLengthOffset = 0x0A;
    private const int MarkerOffset = 0x0B;
    private const int ContentOffset = 0x0C;
    private const byte ValidMarker = 0x5A;

    public List<UDLEntry> Read(string path)
    {
        var data = File.ReadAllBytes(path);

        ValidateHeader(data);

        uint wordCount = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x0C));
        var entries = new List<UDLEntry>((int)wordCount);

        for (int i = 0; i < wordCount; i++)
        {
            int off = DataStart + i * RecordSize;
            if (off + RecordSize > data.Length) break;

            // Check valid marker
            if (data[off + MarkerOffset] != ValidMarker) continue;

            byte wordLen = data[off + WordLengthOffset];
            if (wordLen == 0 || wordLen > MaxWordLength) continue;

            uint timestamp = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(off));
            string word = Encoding.Unicode.GetString(data, off + ContentOffset, wordLen * 2);

            // Pinyin indices start after the word
            int pinyinStart = off + ContentOffset + wordLen * 2;
            var pinyinIndices = new short[wordLen];
            for (int j = 0; j < wordLen; j++)
            {
                int idxOff = pinyinStart + j * 2;
                if (idxOff + 2 > data.Length) break;
                pinyinIndices[j] = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(idxOff, 2));
            }

            string pinyinText = PinyinTable.DecodeAll(pinyinIndices);

            entries.Add(new UDLEntry
            {
                Word = word,
                PinyinText = pinyinText,
                Timestamp = timestamp,
                RecordIndex = i,
            });
        }

        return entries;
    }

    public void Write(string sourcePath, string destPath, IReadOnlyCollection<UDLEntry> entries, IReadOnlySet<int> deletedRecordIndices)
    {
        var data = File.ReadAllBytes(sourcePath);

        ValidateHeader(data);

        uint wordCount = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x0C));
        var assignedEntries = entries
            .Where(entry => !entry.IsNew)
            .ToDictionary(entry => entry.RecordIndex);
        var newEntries = entries.Where(entry => entry.IsNew).ToList();
        var reusableSlots = new Queue<int>();

        for (int i = 0; i < wordCount; i++)
        {
            int off = DataStart + i * RecordSize;
            if (off + RecordSize > data.Length)
            {
                break;
            }

            if (deletedRecordIndices.Contains(i))
            {
                Array.Clear(data, off, RecordSize);
                reusableSlots.Enqueue(i);
                continue;
            }

            if (data[off + MarkerOffset] != ValidMarker)
            {
                reusableSlots.Enqueue(i);
                continue;
            }

            if (!assignedEntries.TryGetValue(i, out var entry))
            {
                continue;
            }

            WriteRecord(data, off, entry);
        }

        foreach (var entry in newEntries)
        {
            int recordIndex;
            if (reusableSlots.Count > 0)
            {
                recordIndex = reusableSlots.Dequeue();
            }
            else
            {
                recordIndex = checked((int)wordCount);
                wordCount++;
                EnsureCapacity(ref data, wordCount);
            }

            var entryToWrite = new UDLEntry
            {
                DisplayIndex = entry.DisplayIndex,
                Word = entry.Word,
                PinyinText = entry.PinyinText,
                Timestamp = entry.Timestamp == 0 ? CreateTimestamp() : entry.Timestamp,
                RecordIndex = recordIndex,
            };

            WriteRecord(data, DataStart + recordIndex * RecordSize, entryToWrite);
        }

        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x0C), wordCount);

        File.WriteAllBytes(destPath, data);
    }

    private static void EnsureCapacity(ref byte[] data, uint wordCount)
    {
        int requiredLength = checked(DataStart + (int)wordCount * RecordSize);
        if (requiredLength > data.Length)
        {
            Array.Resize(ref data, requiredLength);
        }
    }

    private static uint CreateTimestamp()
    {
        return MicrosoftImeTimestamp.GetCurrentSecondsSinceEpoch2000();
    }

    private static void WriteRecord(byte[] data, int offset, UDLEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var word = entry.Word.Trim();
        if (word.Length == 0 || word.Length > MaxWordLength)
        {
            throw new InvalidDataException($"UDL 词语长度必须在 1 到 {MaxWordLength} 个字符之间: {entry.Word}");
        }

        var pinyinIndices = PinyinTable.EncodeAll(entry.PinyinText);
        if (pinyinIndices.Length != word.Length)
        {
            throw new InvalidDataException($"词语“{word}”的拼音音节数必须与字符数一致。当前字符数={word.Length}，拼音数={pinyinIndices.Length}");
        }

        Array.Clear(data, offset, RecordSize);

        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), entry.Timestamp);
        data[offset + WordLengthOffset] = (byte)word.Length;
        data[offset + MarkerOffset] = ValidMarker;

        Encoding.Unicode.GetBytes(word, data.AsSpan(offset + ContentOffset, word.Length * 2));

        int pinyinStart = offset + ContentOffset + word.Length * 2;
        for (int i = 0; i < pinyinIndices.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(pinyinStart + i * 2, 2), pinyinIndices[i]);
        }
    }

    private static void ValidateHeader(byte[] data)
    {
        if (data.Length < DataStart)
            throw new InvalidDataException("File too small to be a valid UDL.dat");

        ushort sig1 = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0));
        ushort sub = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(2));

        if (sig1 != Signature1 || sub != SubType)
            throw new InvalidDataException($"Not a valid UDL.dat file (expected magic 0x{Signature1:X4}/0x{SubType:X4}, got 0x{sig1:X4}/0x{sub:X4})");
    }
}
