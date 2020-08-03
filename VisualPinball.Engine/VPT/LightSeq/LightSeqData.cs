#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.LightSeq
{
	[Serializable]
	public class LightSeqData : ItemData
	{
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", IsWideString = true, Pos = 8)]
		public string Name;

		[BiffString("COLC", IsWideString = true, Pos = 2)]
		public string Collection;

		[BiffVertex("VCEN", Index = 0, Pos = 1)]
		public Vertex2D V = new Vertex2D();

		[BiffFloat("CTRX", Pos = 3)] public float PosX { set => Center.X = value; get => Center.X; }
		[BiffFloat("CTRY", Pos = 4)] public float PosY { set => Center.Y = value; get => Center.Y; }
		public Vertex2D Center = new Vertex2D();

		[BiffInt("UPTM", Pos = 5)]
		public int UpdateInterval = 25;

		[BiffBool("BGLS", Pos = 9)]
		public bool Backglass = false;

		[BiffBool("TMON", Pos = 6)]
		public bool IsTimerEnabled;

		[BiffInt("TMIN", Pos = 7)]
		public int TimerInterval;

		protected override bool SkipWrite(BiffAttribute attr)
		{
			// this is probably a bug in VP...
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

		static LightSeqData()
		{
			Init(typeof(LightSeqData), Attributes);
		}

		public LightSeqData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write((int)ItemType.LightSeq);
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
