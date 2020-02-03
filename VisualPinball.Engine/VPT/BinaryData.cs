#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
#endregion

using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Resources;

namespace VisualPinball.Engine.VPT
{
	public class BinaryData : ItemData, IImageData
	{
		public byte[] Bytes => Data;
		public byte[] FileContent => Data;

		[BiffString("NAME", HasExplicitLength = true, Pos = 1)]
		public override string Name { get; set; }

		[BiffString("INME", Pos = 2)]
		public string InternalName;

		[BiffString("PATH", Pos = 3)]
		public string Path;

		[BiffInt("SIZE", Pos = 4)]
		public int Size;

		[BiffByte("DATA", Pos = 5)]
		public byte[] Data;

		public BinaryData(Resource res) : base(res.Name)
		{
			Data = res.Data;
		}

		#region BIFF
		static BinaryData()
		{
			Init(typeof(BinaryData), Attributes);
		}

		public BinaryData(BinaryReader reader, string storageName) : base(storageName)
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
}
