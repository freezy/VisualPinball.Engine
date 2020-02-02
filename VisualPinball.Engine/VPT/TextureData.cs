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

namespace VisualPinball.Engine.VPT
{
	public class TextureData : ItemData
	{
		[BiffString("NAME", HasExplicitLength = true)]
		public override string Name { get; set; }

		[BiffString("INME")]
		public string InternalName;

		[BiffString("PATH")]
		public string Path;

		[BiffInt("WDTH")]
		public int Width;

		[BiffInt("HGHT")]
		public int Height;

		[BiffFloat("ALTV")]
		public float AlphaTestValue;

		[BiffBinary("JPEG")]
		public BinaryData Binary;

		[BiffBits("BITS")]
		public Bitmap Bitmap; // originally "PdsBuffer";

		public TextureData(Resource res) : base(res.Name)
		{
			Name = res.Name;
			Binary = new BinaryData(res);
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

		public override void Write(BinaryWriter writer)
		{
			Write(writer, Attributes);
			WriteEnd(writer);
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

		public override void Write<TItem>(TItem obj, BinaryWriter writer)
		{
			throw new System.NotImplementedException();
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

		public override void Write<TItem>(TItem obj, BinaryWriter writer)
		{
			throw new NotImplementedException();
		}
	}
}
