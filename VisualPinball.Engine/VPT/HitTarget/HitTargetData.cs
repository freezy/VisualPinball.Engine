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

namespace VisualPinball.Engine.VPT.HitTarget
{
	[Serializable]
	public class HitTargetData : ItemData, IPhysicalData
	{
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", IsWideString = true, Pos = 6)]
		public string Name;

		[BiffFloat("PIDB", Pos = 20)]
		public float DepthBias;

		[BiffFloat("DILB", Pos = 18)]
		public float DisableLightingBelow;

		[BiffFloat("DILI", QuantizedUnsignedBits = 8, Pos = 17)]
		public float DisableLightingTop;

		[BiffFloat("DRSP", Pos = 22)]
		public float DropSpeed =  0.5f;

		[BiffBool("REEN", Pos = 19)]
		public bool IsReflectionEnabled = true;

		[BiffInt("RADE", Pos = 25)]
		public int RaiseDelay = 100;

		[BiffFloat("ELAS", Pos = 12)]
		public float Elasticity;

		[BiffFloat("ELFO", Pos = 13)]
		public float ElasticityFalloff;

		[BiffFloat("RFCT", Pos = 14)]
		public float Friction;

		[BiffBool("CLDR", Pos = 16)]
		public bool IsCollidable = true;

		[BiffBool("ISDR", Pos = 21)]
		public bool IsDropped = false;

		[BiffBool("TVIS", Pos = 8)]
		public bool IsVisible = true;

		[BiffBool("LEMO", Pos = 9)]
		public bool IsLegacy = false;

		[BiffBool("OVPH", Pos = 27)]
		public bool OverwritePhysics = false;

		[BiffFloat("ROTZ", Pos = 3)]
		public float RotZ = 0f;

		[BiffFloat("RSCT", Pos = 15)]
		public float Scatter;

		[TextureReference]
		[BiffString("IMAG", Pos = 4)]
		public string Image = string.Empty;

		[MaterialReference]
		[BiffString("MATR", Pos = 7)]
		public string Material = string.Empty;

		[MaterialReference]
		[BiffString("MAPH", Pos = 26)]
		public string PhysicsMaterial = string.Empty;

		[BiffInt("TRTY", Pos = 5)]
		public int TargetType = VisualPinball.Engine.VPT.TargetType.DropTargetSimple;

		[BiffFloat("THRS", Pos = 11)]
		public float Threshold = 2.0f;

		[BiffBool("HTEV", Pos = 10)]
		public bool UseHitEvent = true;

		[BiffVertex("VPOS", IsPadded = true, Pos = 1)]
		public Vertex3D Position = new Vertex3D();

		[BiffVertex("VSIZ", IsPadded = true, Pos = 2)]
		public Vertex3D Size = new Vertex3D(32, 32, 32);

		[BiffBool("TMON", Pos = 23)]
		public bool IsTimerEnabled;

		[BiffInt("TMIN", Pos = 24)]
		public int TimerInterval;

		public bool IsDropTarget =>
			   TargetType == VisualPinball.Engine.VPT.TargetType.DropTargetBeveled
			|| TargetType == VisualPinball.Engine.VPT.TargetType.DropTargetFlatSimple
			|| TargetType == VisualPinball.Engine.VPT.TargetType.DropTargetSimple;

		public HitTargetData(string name, float x, float y) : base(StoragePrefix.GameItem)
		{
			Name = name;
			Position = new Vertex3D(x, y, 0f);
		}

		#region BIFF

		static HitTargetData()
		{
			Init(typeof(HitTargetData), Attributes);
		}

		public HitTargetData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write((int)ItemType.HitTarget);
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion

		// IPhysicalData
		public float GetElasticity() => Elasticity;
		public float GetElasticityFalloff() => 0;
		public float GetFriction() => Friction;
		public float GetScatter() => Scatter;
		public bool GetOverwritePhysics() => OverwritePhysics;
		public bool GetIsCollidable() => IsCollidable;
		public string GetPhysicsMaterial() => PhysicsMaterial;
	}
}
