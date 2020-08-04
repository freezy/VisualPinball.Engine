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

namespace VisualPinball.Engine.VPT.Gate
{
	[Serializable]
	public class GateData : ItemData
	{
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", IsWideString = true, Pos = 18)]
		public string Name;

		[BiffFloat("GAMA", Pos = 12)]
		public float AngleMax = MathF.PI / 2.0f;

		[BiffFloat("GAMI", Pos = 13)]
		public float AngleMin = 0f;

		[BiffFloat("AFRC", Pos = 15)]
		public float Damping = 0.985f;

		[BiffFloat("ELAS", Pos = 11)]
		public float Elasticity = 0.3f;

		[BiffFloat("GFRC", Pos = 14)]
		public float Friction = 0.02f;

		[BiffInt("GATY", Min = VisualPinball.Engine.VPT.GateType.GateWireW, Max = VisualPinball.Engine.VPT.GateType.GateLongPlate, Pos = 21)]
		public int GateType = VisualPinball.Engine.VPT.GateType.GateWireW;

		[BiffFloat("GGFC", Pos = 16)]
		public float GravityFactor = 0.25f;

		[BiffFloat("HGTH", Pos = 3)]
		public float Height = 50f;

		[BiffBool("GCOL", Pos = 8)]
		public bool IsCollidable = true;

		[BiffBool("REEN", Pos = 20)]
		public bool IsReflectionEnabled = true;

		[BiffBool("GVSB", Pos = 17)]
		public bool IsVisible = true;

		[BiffFloat("LGTH", Pos = 2)]
		public float Length = 100f;

		[BiffFloat("ROTA", Pos = 4)]
		public float Rotation = -90f;

		[BiffBool("GSUP", Pos = 7)]
		public bool ShowBracket = true;

		[MaterialReference]
		[BiffString("MATR", Pos = 5)]
		public string Material;

		[BiffString("SURF", Pos = 10)]
		public string Surface;

		[BiffBool("TWWA", Pos = 19)]
		public bool TwoWay = false;

		[BiffVertex("VCEN", Pos = 1)]
		public Vertex2D Center;

		[BiffBool("TMON", Pos = 6)]
		public bool IsTimerEnabled;

		[BiffInt("TMIN", Pos = 9)]
		public int TimerInterval;

		public GateData(string name, float x, float y) : base(StoragePrefix.GameItem)
		{
			Name = name;
			Center = new Vertex2D(x, y);
			Rotation = 180f;
		}

		#region BIFF

		static GateData()
		{
			Init(typeof(GateData), Attributes);
		}

		public GateData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write((int)ItemType.Gate);
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
