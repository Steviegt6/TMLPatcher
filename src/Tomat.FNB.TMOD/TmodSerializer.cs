﻿using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;

using Tomat.FNB.Common.BinaryData;

namespace Tomat.FNB.TMOD;

/// <summary>
///     Serialization utilities for <see cref="ITmodFile"/> implementations.
///     <br />
///     Provides utilities for reading and writing <c>.tmod</c> archives,
///     including handling special tModLoader-defined file formats.
/// </summary>
public static class TmodSerializer
{
#region Write
    /// <summary>
    ///     Writes the <c>.tmod</c> archive to a stream.
    /// </summary>
    /// <param name="tmod">The <c>.tmod</c> archive.</param>
    /// <param name="stream">The stream to write ot.</param>
    public static void Write(ITmodFile tmod, Stream stream)
    {
        var writer = new BinaryWriter(stream);

        try
        {
            writer.Write(TMOD_HEADER);
            writer.Write(tmod.ModLoaderVersion);

            var hashStartPos = stream.Position;
            {
                writer.Write(new byte[HASH_LENGTH]);
                writer.Write(new byte[SIGNATURE_LENGTH]);
                writer.Write(0);
            }
            var hashEndPos = stream.Position;

            var isLegacy = Version.Parse(tmod.ModLoaderVersion) < VERSION_0_11_0_0;
            if (isLegacy)
            {
                var ms = new MemoryStream();
                var ds = new DeflateStream(ms, CompressionMode.Compress, true);
                writer = new BinaryWriter(ds);
            }

            writer.Write(tmod.Name);
            writer.Write(tmod.Version);
            writer.Write(tmod.Entries.Count);

            if (isLegacy)
            {
                foreach (var entry in tmod.Entries)
                {
                    Debug.Assert(entry.Data is not null, $"{entry.Path} has null data!");
                    Debug.Assert(entry.Length <= int.MaxValue);

                    writer.Write(entry.Path);
                    writer.Write((int)entry.Length);
                    entry.Data.Write(writer);
                }
            }
            else
            {
                foreach (var entry in tmod.Entries)
                {
                    Debug.Assert(entry.CompressedLength <= int.MaxValue);
                    Debug.Assert(entry.Length           <= int.MaxValue);

                    writer.Write(entry.Path);
                    writer.Write((int)entry.CompressedLength);
                    writer.Write((int)entry.Length);
                }

                foreach (var entry in tmod.Entries)
                {
                    Debug.Assert(entry.Data is not null, $"{entry.Path} has null data!");

                    entry.Data.Write(writer);
                }
            }

            if (isLegacy)
            {
                Debug.Assert(writer.BaseStream is MemoryStream, "BaseStream of writer was somehow not MemoryStream!");

                var compressed = (writer.BaseStream as MemoryStream)!.GetBuffer();
                writer.Dispose();
                writer = new BinaryWriter(stream);
                writer.Write(compressed);
            }

            stream.Position = hashEndPos;
            {
                var hash = SHA1.Create().ComputeHash(stream);
                stream.Position = hashStartPos;
                {
                    writer.Write(hash);
                    writer.Write(new byte[SIGNATURE_LENGTH]);
                    writer.Write((int)(stream.Length - hashEndPos));
                }
            }
        }
        finally
        {
            writer.Dispose();
        }
    }
#endregion

#region Read
    /// <summary>
    ///     Reads a <c>.tmod</c> archive from a file.
    /// </summary>
    /// <param name="path">The path of the file to read.</param>
    /// <returns>
    ///     An <see cref="ITmodFile"/> instance containing the read data.
    /// </returns>
    public static ITmodFile Read(string path)
    {
        using var fs = File.OpenRead(path);
        return Read(fs);
    }

    /// <summary>
    ///     Reads a <c>.tmod</c> archive from a byte array.
    /// </summary>
    /// <param name="bytes">The byte array to read from.</param>
    /// <returns>
    ///     An <see cref="ITmodFile"/> instance containing the read data.
    /// </returns>
    public static ITmodFile Read(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        return Read(ms);
    }

    /// <summary>
    ///     Reads a <c>.tmod</c> archive from a stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>
    ///     An <see cref="ITmodFile"/> instance containing the read data.
    /// </returns>
    public static ITmodFile Read(Stream stream)
    {
        var reader = new BinaryReader(stream);

        try
        {
            if (reader.ReadUInt32() != TMOD_HEADER)
            {
                throw new InvalidDataException("Failed to read 'TMOD' header!");
            }

            var modLoaderVersion = reader.ReadString();

            // Jump ahead past hashes and signatures.  We could eventually
            // support checking the hash for validation, but it's of low
            // priority.  This never has and never will be a secure method of
            // integrity or validation, I don't know why the tModLoader
            // developers chose to include it in the first place.
            stream.Position += HASH_LENGTH
                             + SIGNATURE_LENGTH
                             + sizeof(uint);

            var isLegacy = Version.Parse(modLoaderVersion) < VERSION_0_11_0_0;
            if (isLegacy)
            {
                var ds = new DeflateStream(stream, CompressionMode.Decompress, true);
                reader = new BinaryReader(ds);
            }

            var name    = reader.ReadString();
            var version = reader.ReadString();

            var offset  = 0;
            var entries = new TmodFileEntry[reader.ReadInt32()];

            if (isLegacy)
            {
                for (var i = 0; i < entries.Length; i++)
                {
                    var entryPath = reader.ReadString();
                    var entrySize = reader.ReadInt32();
                    var entryData = reader.ReadBytes(entrySize);

                    // The data comes decompressed by the stream reader.
                    var data = DataViewFactory.ByteArray.Create(entryData, entrySize);

                    entries[i] = new TmodFileEntry(entryPath, offset, entrySize, entrySize, data);
                }
            }
            else
            {
                // The first block of data is the file paths and their lengths.
                for (var i = 0; i < entries.Length; i++)
                {
                    var entryPath             = reader.ReadString();
                    var entryLength           = reader.ReadInt32();
                    var entryCompressedLength = reader.ReadInt32();

                    entries[i] =  new TmodFileEntry(entryPath, offset, entryLength, entryCompressedLength, null);
                    offset     += entryCompressedLength;
                }

                if (stream.Position >= int.MaxValue)
                {
                    throw new InvalidDataException($"Stream position exceeded maximum expected value ({int.MaxValue})!");
                }

                var fileStartPos = (int)stream.Position;

                // The second block of data is the actual compressed data.
                for (var i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i];

                    Debug.Assert(entry.Length           <= int.MaxValue);
                    Debug.Assert(entry.CompressedLength <= int.MaxValue);

                    var isCompressed = entry.Length != entry.CompressedLength;
                    var data = isCompressed
                        ? DataViewFactory.ByteArray.Deflate.CreateCompressed(reader.ReadBytes((int)entry.CompressedLength), (int)entry.Length)
                        : DataViewFactory.ByteArray.Create(reader.ReadBytes((int)entry.CompressedLength), (int)entry.Length);

                    entries[i] = entries[i] with
                    {
                        Offset = entry.Offset + fileStartPos,
                        Data = data,
                    };
                }
            }

            return new TmodFile(modLoaderVersion, name, version, entries.ToDictionary(x => x.Path, x => x));
        }
        finally
        {
            reader.Dispose();
        }
    }
#endregion
}