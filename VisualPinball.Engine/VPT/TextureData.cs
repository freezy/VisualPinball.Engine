#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Resources;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT
{
	[Serializable]
	public class TextureData : ItemData
	{
		public override string GetName() => Name;

		[BiffString("NAME", HasExplicitLength = true, Pos = 1)]
		public string Name { get; set; }

		[BiffString("INME", Pos = 2)]
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

		public TextureData(Resource res) : base(res.Name)
		{
			Name = res.Name;
			Binary = new BinaryData(res);
		}

		protected override bool SkipWrite(BiffAttribute attr)
		{
			switch (attr.Name) {
				case "LOCK":
				case "LAYR":
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
			Write(writer, Attributes, hashWriter);
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
