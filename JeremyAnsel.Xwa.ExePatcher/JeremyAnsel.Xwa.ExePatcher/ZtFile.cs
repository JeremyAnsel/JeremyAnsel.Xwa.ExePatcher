using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace JeremyAnsel.Xwa.ExePatcher
{
    public sealed class ZtFile
    {
        private const string UnknownTargetName = "UNKNOWNF.ILE";

        private string targetName = UnknownTargetName;

        private string comment;

        public string FileName { get; private set; }

        public string Name
        {
            get
            {
                return Path.GetFileNameWithoutExtension(this.FileName);
            }
        }

        public string TargetName
        {
            get
            {
                return this.targetName;
            }

            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    this.targetName = UnknownTargetName;
                }
                else
                {
                    var invalidChars = Path.GetInvalidFileNameChars();

                    value = new string(value
                        .ToUpperInvariant()
                        .Where(c => !invalidChars.Contains(c) && !char.IsWhiteSpace(c) && !char.IsPunctuation(c))
                        .ToArray());

                    value = Encoding.ASCII.GetString(Encoding.ASCII.GetBytes(value));

                    string ext = Path.GetExtension(value);

                    if (ext.Length > 3)
                    {
                        ext = ext.Substring(0, 3);
                    }

                    string name = Path.GetFileNameWithoutExtension(value);

                    if (name.Length > 9)
                    {
                        name = string.Concat(name.Substring(0, 7), "~1");
                    }

                    if (string.IsNullOrEmpty(name))
                    {
                        this.targetName = UnknownTargetName;
                    }
                    else if (string.IsNullOrEmpty(ext))
                    {
                        this.targetName = name;
                    }
                    else
                    {
                        this.targetName = string.Concat(name, ".", ext);
                    }
                }
            }
        }

        public int TargetMinimumLength
        {
            get
            {
                if (this.Patches.Count == 0)
                {
                    return 0;
                }

                return this.Patches.Max(t => t.Key + t.Value.Length);
            }
        }

        public SortedList<int, byte[]> Patches { get; } = new SortedList<int, byte[]>();

        public int PatchesCount
        {
            get { return this.Patches.Count; }
        }

        public string Comment
        {
            get
            {
                return this.comment;
            }

            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    this.comment = null;
                }
                else
                {
                    this.comment = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(value));
                }
            }
        }

        public static ZtFile Create(string unmodifiedFile, string modifiedFile)
        {
            if (unmodifiedFile == null)
            {
                throw new ArgumentNullException(nameof(unmodifiedFile));
            }

            if (modifiedFile == null)
            {
                throw new ArgumentNullException(nameof(modifiedFile));
            }

            byte[] unmodifiedBytes = File.ReadAllBytes(unmodifiedFile);
            byte[] modifiedBytes = File.ReadAllBytes(modifiedFile);

            return Create(unmodifiedBytes, modifiedBytes);
        }

        public static ZtFile Create(byte[] unmodifiedBytes, byte[] modifiedBytes)
        {
            if (unmodifiedBytes == null)
            {
                throw new ArgumentNullException(nameof(unmodifiedBytes));
            }

            if (modifiedBytes == null)
            {
                throw new ArgumentNullException(nameof(modifiedBytes));
            }

            if (unmodifiedBytes.Length != modifiedBytes.Length)
            {
                throw new InvalidOperationException();
            }

            ZtFile zt = new();

            for (int offset = 0; offset < unmodifiedBytes.Length; offset++)
            {
                int start = offset;
                int count = 0;

                while (offset < unmodifiedBytes.Length && unmodifiedBytes[offset] != modifiedBytes[offset])
                {
                    offset++;
                    count++;
                }

                if (count != 0)
                {
                    byte[] bytes = new byte[count];
                    Array.Copy(modifiedBytes, start, bytes, 0, count);

                    zt.Add(start, bytes);
                }
            }

            return zt;
        }

        public static ZtFile FromFile(string fileName)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            using FileStream file = File.Open(fileName, FileMode.Open, FileAccess.Read);

            ZtFile zt = FromStream(file);

            zt.FileName = fileName;

            return zt;
        }

        public static ZtFile FromStream(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var zt = new ZtFile();

            using BinaryReader file = new(stream, new UTF8Encoding(), true);

            zt.TargetName = Encoding.ASCII.GetString(file.ReadBytes(13)).TrimEnd('\0');

            int count = file.ReadUInt16();

            for (int i = 0; i < count; i++)
            {
                int offset = file.ReadInt32();
                byte length = file.ReadByte();
                byte[] bytes = file.ReadBytes(length);

                zt.Patches.Add(offset, bytes);
            }

            if (file.BaseStream.Position != file.BaseStream.Length)
            {
                byte[] commentBytes = file.ReadBytes((int)file.BaseStream.Length - (int)file.BaseStream.Position);

                zt.Comment = Encoding.UTF8.GetString(commentBytes);
            }

            zt.Compact();

            return zt;
        }

        public void Save(string fileName)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            using FileStream file = File.Open(fileName, FileMode.Create, FileAccess.Write);

            Save(file);

            this.FileName = fileName;
        }

        public void Save(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            this.Compact();
            this.CompactAdd();

            using BinaryWriter file = new(stream, new UTF8Encoding(false, true), true);

            file.Write(Encoding.ASCII.GetBytes(this.TargetName.PadRight(13, '\0')));
            file.Write((ushort)this.Patches.Count);

            foreach (var patch in this.Patches)
            {
                file.Write(patch.Key);
                file.Write((byte)patch.Value.Length);
                file.Write(patch.Value);
            }

            if (!string.IsNullOrWhiteSpace(this.Comment))
            {
                file.Write(Encoding.UTF8.GetBytes(this.Comment));
            }
        }

        public void Apply(string fileName)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            byte[] bytes = File.ReadAllBytes(fileName);

            Apply(bytes);

            File.WriteAllBytes(fileName, bytes);
        }

        public void Apply(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length < this.TargetMinimumLength)
            {
                throw new ArgumentOutOfRangeException(nameof(bytes));
            }

            if (bytes.Length == 0)
            {
                return;
            }

            foreach (var patch in this.Patches)
            {
                for (int i = 0; i < patch.Value.Length; i++)
                {
                    bytes[patch.Key + i] = patch.Value[i];
                }
            }
        }

        private void Add(int offset, byte[] patch)
        {
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (patch == null)
            {
                throw new ArgumentNullException(nameof(patch));
            }

            for (int i = 0; i < patch.Length; i += 127)
            {
                if (this.Patches.Count >= short.MaxValue)
                {
                    throw new InvalidDataException();
                }

                int length = Math.Min(patch.Length - i, 127);

                byte[] bytes = new byte[length];
                Array.Copy(patch, i, bytes, 0, length);

                this.Patches[offset + i] = bytes;
            }
        }

        private void CompactAdd()
        {
            var items = new List<Tuple<int, byte[]>>();

            for (int i = this.Patches.Count - 1; i >= 0; i--)
            {
                var patch = this.Patches.ElementAt(i);

                if (patch.Value.Length > 127)
                {
                    items.Add(Tuple.Create(patch.Key, patch.Value));
                    this.Patches.RemoveAt(i);
                }
            }

            foreach (var item in items)
            {
                this.Add(item.Item1, item.Item2);
            }
        }

        private void Compact()
        {
            var items = new List<Tuple<int, int>>();

            int start = 0;
            int count = 0;

            for (int i = 1; i < this.Patches.Count; i++)
            {
                var p0 = this.Patches.ElementAt(i - 1);
                var p1 = this.Patches.ElementAt(i);

                if (!(count == 0 && p0.Value.Length == 127) && (p0.Key + p0.Value.Length == p1.Key))
                {
                    count++;
                    continue;
                }

                if (count != 0)
                {
                    items.Add(new Tuple<int, int>(this.Patches.ElementAt(start).Key, count));
                }

                start = i;
                count = 0;
            }

            if (count != 0)
            {
                items.Add(new Tuple<int, int>(this.Patches.ElementAt(start).Key, count));
            }

            foreach (var item in items)
            {
                int index = this.Patches.IndexOfKey(item.Item1);

                int offset = this.Patches.ElementAt(index).Key;

                byte[] bytes = this.Patches
                    .Skip(index)
                    .Take(item.Item2 + 1)
                    .SelectMany(t => t.Value)
                    .ToArray();

                for (int i = index + item.Item2; i >= index; i--)
                {
                    this.Patches.RemoveAt(i);
                }

                this.Add(offset, bytes);
            }
        }
    }
}
