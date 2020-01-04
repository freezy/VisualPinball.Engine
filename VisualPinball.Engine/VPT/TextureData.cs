using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;

namespace VisualPinball.Engine.VPT
{
	public class TextureData : ItemData
	{
		//[BiffString("NAME", IsWideString = false, HasExplicitLength = true)]
		//public new string Name;

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
		public byte[] PdsBuffer;

		static TextureData()
		{
			Init(typeof(TextureData), Attributes);
		}

		public TextureData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		private static readonly Dictionary<string, BiffAttribute> Attributes = new Dictionary<string, BiffAttribute>();

	}

	public class BiffBinaryAttribute : BiffAttribute
	{
		public BiffBinaryAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			SetValue(obj, new BinaryData(reader, "none"));
		}
	}

	public class BiffBitsAttribute : BiffAttribute
	{
		public BiffBitsAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			SetValue(obj, new BinaryData(reader, "none"));
		}
	}
}
