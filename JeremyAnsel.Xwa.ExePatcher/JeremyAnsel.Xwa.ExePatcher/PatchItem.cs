﻿using System;
using System.Globalization;
using System.Linq;

namespace JeremyAnsel.Xwa.ExePatcher
{
    public sealed class PatchItem
    {
        public string OffsetString
        {
            get { return this.Offset.ToString("X6", CultureInfo.InvariantCulture); }
            set { this.Offset = int.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture); }
        }

        public string OldValuesString
        {
            get { return PatchItem.ByteArrayToString(this.OldValues); }
            set { this.OldValues = PatchItem.StringToByteArray(value); }
        }

        public string NewValuesString
        {
            get { return PatchItem.ByteArrayToString(this.NewValues); }
            set { this.NewValues = PatchItem.StringToByteArray(value); }
        }

        public int Offset { get; set; }

        public byte[] OldValues { get; set; } = Array.Empty<byte>();

        public byte[] NewValues { get; set; } = Array.Empty<byte>();

        private static byte[] StringToByteArray(string s)
        {
            char[] data = s.ToCharArray();

            return Enumerable.Range(0, data.Length / 2)
                .Select(i => byte.Parse(new string(data, i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture))
                .ToArray();
        }

        private static string ByteArrayToString(byte[] array)
        {
            return string.Concat(array.Select(t => t.ToString("X2", CultureInfo.InvariantCulture)));
        }
    }
}
