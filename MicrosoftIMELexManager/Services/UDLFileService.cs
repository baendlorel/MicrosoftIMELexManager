using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
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
            if (data[off + 0x0B] != ValidMarker) continue;

            byte wordLen = data[off + 0x0A];
            if (wordLen == 0 || wordLen > MaxWordLength) continue;

            uint timestamp = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(off));
            string word = Encoding.Unicode.GetString(data, off + 0x0C, wordLen * 2);

            // Pinyin indices start after the word
            int pinyinStart = off + 0x0C + wordLen * 2;
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

    /// <summary>
    /// Delete entries by setting their Marker byte (offset +0x0B) from 0x5A to 0x00.
    /// </summary>
    public void DeleteEntries(string sourcePath, string destPath, HashSet<int> recordIndices)
    {
        var data = File.ReadAllBytes(sourcePath);

        ValidateHeader(data);

        foreach (int idx in recordIndices)
        {
            int off = DataStart + idx * RecordSize;
            if (off + 0x0B >= data.Length) continue;

            // Only mark as deleted if currently valid
            if (data[off + 0x0B] == ValidMarker)
            {
                data[off + 0x0B] = 0x00;
            }
        }

        File.WriteAllBytes(destPath, data);
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
