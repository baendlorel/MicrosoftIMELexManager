using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MicrosoftIMELexManager.Models;

namespace MicrosoftIMELexManager.Services;

public sealed class LexFileService
{
    private const string Magic = "mschxudp";
    private const int OffsetTableStart = 0x40;

    public List<LexEntry> Read(string path)
    {
        var data = File.ReadAllBytes(path);
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);

        // Validate magic
        var magicBytes = reader.ReadBytes(8);
        if (Encoding.ASCII.GetString(magicBytes) != Magic)
            throw new InvalidDataException("Not a valid .lex file (bad magic)");

        // Read header
        _ = reader.ReadUInt16(); // Version
        _ = reader.ReadUInt16(); // Reserved
        _ = reader.ReadUInt32(); // Flags
        uint offsetTableStart = reader.ReadUInt32();
        uint recordStart = reader.ReadUInt32();
        uint totalSize = reader.ReadUInt32();
        uint phraseCount = reader.ReadUInt32();
        _ = reader.ReadUInt32(); // Timestamp/Counter
        _ = reader.ReadBytes(32); // Reserved

        if (offsetTableStart != OffsetTableStart)
            throw new InvalidDataException($"Unexpected offset table start: 0x{offsetTableStart:X}");

        if (recordStart > data.Length)
            throw new InvalidDataException("Record area starts beyond the end of file");

        if (phraseCount > 0 && recordStart < OffsetTableStart + phraseCount * 4)
            throw new InvalidDataException("Offset table overlaps record area");

        // Read offset table (phraseCount entries)
        ms.Position = OffsetTableStart;
        uint[] offsets = new uint[phraseCount];
        for (int i = 0; i < offsets.Length; i++)
            offsets[i] = reader.ReadUInt32();

        var entries = new List<LexEntry>((int)phraseCount);

        for (int i = 0; i < phraseCount; i++)
        {
            long segStart = recordStart + offsets[i];
            long blockEnd = i + 1 < phraseCount
                ? recordStart + offsets[i + 1]
                : Math.Min(totalSize, (uint)data.Length);
            long blockLen = blockEnd - segStart;

            if (segStart >= data.Length || blockEnd > data.Length || blockLen < 16) continue;

            // Skip records explicitly marked deleted in the record header.
            if (data[segStart + 9] != 0x00) continue;

            // Parse record header (16 bytes)
            // Offset +0: RecordType (uint16)
            // Offset +2: HeaderSize (uint16)
            // Offset +4: PinyinBlockSize (uint16)
            // Offset +6: CandidateIndex (uint32)
            // Offset +10: Flags (uint16)
            // Offset +12: TypeCode (uint16)
            // Offset +14: TailByte1, TailByte2

            uint candidateIndex = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)segStart + 6, 4));

            // Body starts at segStart + 16
            long bodyStart = segStart + 16;
            long bodyLen = blockLen - 16;

            // Split body by double-null (0x0000) into pinyin and phrase
            var body = data.AsSpan((int)bodyStart, (int)bodyLen);
            var parts = SplitByDoubleNull(body);

            if (parts.Count < 2) continue;

            string pinyin = Encoding.Unicode.GetString(parts[0]);
            string phrase = Encoding.Unicode.GetString(parts[1]);

            // CandidateIndex is stored as 0x00000601 for position 1, etc.
            // Extract position from the first byte
            int pos = (int)(candidateIndex & 0xFF);
            if (pos < 1 || pos > 9) pos = 1;

            entries.Add(new LexEntry
            {
                Pinyin = pinyin,
                Phrase = phrase,
                CandidateIndex = pos,
            });
        }

        return entries;
    }

    public void Write(string path, List<LexEntry> entries)
    {
        var sorted = entries
            .Select(e => new LexEntry
            {
                Pinyin = e.Pinyin.ToLowerInvariant(),
                Phrase = e.Phrase,
                CandidateIndex = e.CandidateIndex,
            })
            .OrderBy(e => e.Pinyin, StringComparer.Ordinal)
            .ThenBy(e => e.CandidateIndex)
            .ThenBy(e => e.Phrase, StringComparer.CurrentCulture)
            .ToList();

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Build all record segments first
        var segments = new List<byte[]>(sorted.Count);
        foreach (var entry in sorted)
        {
            segments.Add(BuildRecord(entry));
        }

        uint phraseCount = (uint)sorted.Count;

        // Write header
        writer.Write(Encoding.ASCII.GetBytes(Magic));   // 0x00: Magic (8 bytes)
        writer.Write((ushort)2);                         // 0x08: Version
        writer.Write((ushort)0x0060);                    // 0x0A: Reserved
        writer.Write((uint)1);                           // 0x0C: Flags
        writer.Write((uint)OffsetTableStart);            // 0x10: OffsetTableStart

        uint recordStart = (uint)(OffsetTableStart + phraseCount * 4);
        writer.Write(recordStart);                       // 0x14: RecordStart

        uint totalDataSize = 0;
        foreach (var seg in segments) totalDataSize += (uint)seg.Length;
        writer.Write(recordStart + totalDataSize);      // 0x18: TotalFileSize

        writer.Write(phraseCount);                       // 0x1C: PhraseCount
        writer.Write((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds()); // 0x20: Timestamp
        writer.Write(new byte[32]);                      // 0x24: Reserved (32 bytes)

        // Write offset table (phraseCount entries)
        uint cumulativeOffset = 0;
        for (int i = 0; i < segments.Count; i++)
        {
            writer.Write(cumulativeOffset);
            cumulativeOffset += (uint)segments[i].Length;
        }

        // Write data area
        foreach (var seg in segments)
            writer.Write(seg);

        writer.Flush();
        File.WriteAllBytes(path, ms.ToArray());
    }

    private static byte[] BuildRecord(LexEntry entry)
    {
        byte[] pinyinBytes = Encoding.Unicode.GetBytes(entry.Pinyin);
        byte[] phraseBytes = Encoding.Unicode.GetBytes(entry.Phrase);

        ushort pinyinBlockSize = (ushort)(16 + pinyinBytes.Length + 2);

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Record header (16 bytes)
        writer.Write((ushort)0x0010);                              // RecordType
        writer.Write((ushort)0x0010);                              // HeaderSize
        writer.Write(pinyinBlockSize);                              // PinyinBlockSize
        writer.Write((uint)(0x00000600 | (entry.CandidateIndex & 0xFF))); // CandidateIndex
        writer.Write((ushort)0x0000);                              // Flags
        writer.Write((ushort)0x2AC2);                              // TypeCode
        writer.Write((byte)0xB2);                                  // TailByte1
        writer.Write((byte)0x2F);                                  // TailByte2

        // Pinyin (UTF-16LE + null terminator)
        writer.Write(pinyinBytes);
        writer.Write((ushort)0); // null terminator

        // Phrase (UTF-16LE + null terminator)
        writer.Write(phraseBytes);
        writer.Write((ushort)0); // null terminator

        return ms.ToArray();
    }

    private static List<byte[]> SplitByDoubleNull(ReadOnlySpan<byte> buf)
    {
        var parts = new List<byte[]>();
        int start = 0;

        for (int i = 0; i + 1 < buf.Length; i += 2)
        {
            if (buf[i] == 0 && buf[i + 1] == 0)
            {
                if (i - start >= 2)
                    parts.Add(buf.Slice(start, i - start).ToArray());
                start = i + 2;
            }
        }

        // Trailing data without null terminator
        if (start < buf.Length && buf.Length - start >= 2)
            parts.Add(buf.Slice(start).ToArray());

        return parts;
    }
}
