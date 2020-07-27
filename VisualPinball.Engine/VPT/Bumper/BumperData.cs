#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Bumper
{
	[Serializable]
	public class BumperData : ItemData
	{
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", IsWideString = true, Pos = 17)]
		public string Name;

		[BiffVertex("VCEN", Pos = 1)]
		public Vertex2D Center;

		[BiffFloat("RADI", Pos = 2)]
		public float Radius = 45f;

		[MaterialReference]
		[BiffString("MATR", Pos = 12)]
		public string CapMaterial;

		[MaterialReference]
		[BiffString("RIMA", Pos = 15)]
		public string RingMaterial;

		[MaterialReference]
		[BiffString("BAMA", Pos = 13)]
		public string BaseMaterial;

		[MaterialReference]
		[BiffString("SKMA", Pos = 14)]
		public string SocketMaterial;

		[BiffFloat("THRS", Pos = 5)]
		public float Threshold = 1.0f;

		[BiffFloat("FORC", Pos = 6)]
		public float Force;

		[BiffFloat("BSCT", Pos = 7)]
		public float Scatter;

		[BiffFloat("HISC", Pos = 8)]
		public float HeightScale = 90.0f;

		[BiffFloat("RISP", Pos = 9)]
		public float RingSpeed = 0.5f;

		[BiffFloat("ORIN", Pos = 10)]
		public float Orientation = 0.0f;

		[BiffFloat("RDLI", Pos = 11)]
		public float RingDropOffset = 0.0f;

		[BiffString("SURF", Pos = 16)]
		public string Surface;

		[BiffBool("BVIS", SkipWrite = true)]
		[BiffBool("CAVI", Pos = 18)]
		public bool IsCapVisible = true;

		[BiffBool("BVIS", SkipWrite = true)]
		[BiffBool("BSVS", Pos = 19)]
		public bool IsBaseVisible = true;

		[BiffBool("BSVS", SkipWrite = true)]
		[BiffBool("BVIS", SkipWrite = true)]
		[BiffBool("RIVS", Pos = 20)]
		public bool IsRingVisible = true;

		[BiffBool("BSVS", SkipWrite = true)]
		[BiffBool("BVIS", SkipWrite = true)]
		[BiffBool("SKVS", Pos = 21)]
		public bool IsSocketVisible = true;

		[BiffBool("HAHE", Pos = 22)]
		public bool HitEvent = true;

		[BiffBool("COLI", Pos = 23)]
		public bool IsCollidable = true;

		[BiffBool("REEN", Pos = 24)]
		public bool IsReflectionEnabled = true;

		[BiffBool("TMON", Pos = 3)]
		public bool IsTimerEnabled;

		[BiffInt("TMIN", Pos = 4)]
		public int TimerInterval;

		#region BIFF

		static BumperData()
		{
			Init(typeof(BumperData), Attributes);
		}

		public BumperData(string storageName) : base(storageName)
		{
		}

		public BumperData(BinaryReader reader, string storageName) : this(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write((int)ItemType.Bumper);
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();
		private static readonly List<BiffAttribute> IgnoredTags= new List<BiffAttribute>();

		#endregion
	}
}
