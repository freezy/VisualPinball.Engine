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
using VisualPinball.Resources;

namespace VisualPinball.Engine.VPT
{
	[Serializable]
	public class BinaryData : ItemData, IImageData
	{
		public byte[] Bytes => Data;
		public byte[] FileContent => Data;

		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", HasExplicitLength = true, Pos = 1)]
		public string Name;

		[BiffString("INME", Pos = 2)]
		public string InternalName;

		[BiffString("PATH", Pos = 3)]
		public string Path;

		[BiffInt("SIZE", Pos = 4)]
		public int Size;

		[BiffByte("DATA", Pos = 5)]
		public byte[] Data;

		public BinaryData(string name) : base(name)
		{
			Data = new byte[0];
		}

		public BinaryData(Resource res) : base(res.Name)
		{
			Data = res.Data;
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
		static BinaryData()
		{
			Init(typeof(BinaryData), Attributes);
		}

		public BinaryData(BinaryReader reader, string storageName) : base(storageName)
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
}
