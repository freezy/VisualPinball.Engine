// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Resources;

namespace VisualPinball.Engine.VPT
{
	[Serializable]
	public class TextureData : ItemData
	{
		public bool HasBitmap => Bitmap != null && Bitmap.Data != null && Bitmap.Data.Length > 0;

		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", HasExplicitLength = true, Pos = 1)]
		public string Name = string.Empty;

		[BiffString("INME", Pos = 2, WasRemovedInVp107 = true)]
		public string InternalName;

		[BiffString("PATH", Pos = 3)]
		public string Path;

		[BiffInt("WDTH", Pos = 4)]
		public int Width;

		[BiffInt("HGHT", Pos = 5)]
		public int Height;

		[BiffFloat("ALTV", Pos = 7)]
		public float AlphaTestValue;

		[BiffBinary("JPEG", Pos = 6)]
		public BinaryData Binary;

		[BiffBits("BITS", Pos = 6)]
		public Bitmap Bitmap; // originally "PdsBuffer";

		public TextureData(string name) : base(StoragePrefix.Image)
		{
			Name = name;
			Binary = new BinaryData(name);
		}

		public TextureData(Resource res) : base(res.Name)
		{
			Name = res.Name;
			Binary = new BinaryData(res);
		}

		public void FreeBinaryData()
		{
			Binary?.FreeBinaryData();
			Bitmap?.FreeBinaryData();
		}

		protected override bool SkipWrite(BiffAttribute attr)
		{
			switch (attr.Name) {
				case "LOCK":
				case "LAYR":
				case "LANR":
				case "LVIS":
					return true;
			}
			return false;
		}

		#region BIFF

		static TextureData()
		{
			Init(typeof(TextureData), Attributes);
		}

		public TextureData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}

	public class BiffBinaryAttribute : BiffAttribute
	{
		public BiffBinaryAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			if (obj is TextureData textureData) {
				SetValue(obj, new BinaryData(reader, textureData.StorageName));
			}
		}

		public override void Write<TItem>(TItem obj, BinaryWriter writer, HashWriter hashWriter)
		{
			if (Type == typeof(BinaryData)) {
				if (obj is TextureData textureData) {
					if (textureData.HasBitmap) {
						return;
					}
				}
				if (!(GetValue(obj) is BinaryData data)) {
					return;
				}
				WriteStart(writer, 0, hashWriter);
				data.Write(writer, hashWriter);

			} else {
				throw new InvalidOperationException("Unknown type " + Type + " for [" + GetType().Name + "] on field \"" + Name + "\".");
			}
		}
	}

	public class BiffBitsAttribute : BiffAttribute
	{
		public BiffBitsAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			if (obj is TextureData textureData) {
				SetValue(obj, new Bitmap(reader, textureData.Width, textureData.Height));
			}
		}

		public override void Write<TItem>(TItem obj, BinaryWriter writer, HashWriter hashWriter)
		{
			if (Type == typeof(Bitmap)) {
				if (obj is TextureData textureData) {
					if (!textureData.HasBitmap) {
						return;
					}
				}
				if (!(GetValue(obj) is Bitmap bitmap)) {
					return;
				}
				WriteStart(writer, 0, hashWriter);
				bitmap.WriteCompressed(writer);

			} else {
				throw new InvalidOperationException("Unknown type " + Type + " for [" + GetType().Name + "] on field \"" + Name + "\".");
			}
		}
	}
}
