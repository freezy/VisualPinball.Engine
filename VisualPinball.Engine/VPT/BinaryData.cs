using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;

namespace VisualPinball.Engine.VPT
{
	public class BinaryData : ItemData
	{
		[BiffString("NAME", IsWideString = true)]
		public override string Name { get; set; }

		[BiffString("INME")]
		public string InternalName;

		[BiffString("PATH")]
		public string Path;

		[BiffInt("SIZE")]
		public int Size;

		[BiffByte("DATA")]
		public byte[] Data;

		static BinaryData()
		{
			Init(typeof(BinaryData), Attributes);
		}

		public BinaryData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		private static readonly Dictionary<string, BiffAttribute> Attributes = new Dictionary<string, BiffAttribute>();
	}
}
