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
    private const int HeaderSize = 72; // 0x48, includes FirstRecordOffset

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
        _ = reader.ReadUInt32(); // BaseOffset
        _ = reader.ReadUInt32(); // OffsetTableTotal
        _ = reader.ReadUInt32(); // TotalDataSize
        uint phraseCount = reader.ReadUInt32();
        _ = reader.ReadUInt32(); // Timestamp/Counter
        _ = reader.ReadBytes(32); // Reserved
        _ = reader.ReadUInt32(); // FirstRecordOffset (always 0)

        // Read offset table (phraseCount - 1 entries)
        uint[] offsets = new uint[phraseCount - 1];
        for (int i = 0; i < offsets.Length; i++)
            offsets[i] = reader.ReadUInt32();

        // Data area starts after offset table
        long firstBlockPos = HeaderSize + 4L * (phraseCount - 1);

        var entries = new List<LexEntry>((int)phraseCount);
        long lastPos = 0;

        for (int i = 0; i < phraseCount; i++)
        {
            long blockEnd;
            if (i < phraseCount - 1)
            {
                blockEnd = offsets[i];
            }
            else
            {
                blockEnd = data.Length - firstBlockPos;
            }

            long blockLen = blockEnd - lastPos;
            long segStart = firstBlockPos + lastPos;
            lastPos = blockEnd;

            if (blockLen < 16) continue;

            // Check deleted flag: byte[9] of record
            if (data[segStart + 9] != 0x00) continue;

            // Parse record header (16 bytes)
            // Offset +0: RecordType (uint16)
            // Offset +2: HeaderSize (uint16)
            // Offset +4: PinyinBlockSize (uint16)
            // Offset +6: CandidateIndex (uint32)
            // Offset +10: Flags (uint16)
            // Offset +12: TypeCode (uint16)
            // Offset +14: TailByte1, TailByte2

            ushort pinyinBlockSize = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan((int)segStart + 4, 2));
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
        // Sort entries by pinyin byte order (matching original file ordering)
        var sorted = entries.OrderBy(e => e.Pinyin, StringComparer.Ordinal).ToList();

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
        writer.Write((uint)0x40);                        // 0x10: BaseOffset

        uint offsetTableSize = (uint)(4 * (phraseCount > 0 ? phraseCount - 1 : 0));
        writer.Write((uint)(0x40 + offsetTableSize));   // 0x14: OffsetTableTotal (header + offset table)

        uint totalDataSize = 0;
        foreach (var seg in segments) totalDataSize += (uint)seg.Length;
        writer.Write(totalDataSize);                     // 0x18: TotalDataSize

        writer.Write(phraseCount);                       // 0x1C: PhraseCount
        writer.Write((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds()); // 0x20: Timestamp
        writer.Write(new byte[32]);                      // 0x24: Reserved (32 bytes)
        writer.Write((uint)0);                           // 0x40: FirstRecordOffset

        // Write offset table (phraseCount - 1 entries)
        uint cumulativeOffset = 0;
        for (int i = 0; i < segments.Count - 1; i++)
        {
            cumulativeOffset += (uint)segments[i].Length;
            writer.Write(cumulativeOffset);
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
