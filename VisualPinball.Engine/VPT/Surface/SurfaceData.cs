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

namespace VisualPinball.Engine.VPT.Surface
{
	[Serializable]
	public class SurfaceData : ItemData, IPhysicalData
	{
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", IsWideString = true, Pos = 16)]
		public string Name;

		[BiffBool("HTEV", Pos = 1)]
		public bool HitEvent = false;

		[BiffBool("DROP", Pos = 2)]
		public bool IsDroppable = false;

		[BiffBool("FLIP", Pos = 3)]
		public bool IsFlipbook = false;

		[BiffBool("ISBS", Pos = 4)]
		public bool IsBottomSolid = false;

		[BiffBool("CLDW", Pos = 5)]
		public bool IsCollidable = true;

		[BiffFloat("THRS", Pos = 8)]
		public float Threshold = 2.0f;

		[BiffString("IMAG", Pos = 9)]
		public string Image;

		[BiffString("SIMG", Pos = 10)]
		public string SideImage;

		[MaterialReference]
		[BiffString("SIMA", Pos = 11)]
		public string SideMaterial;

		[MaterialReference]
		[BiffString("TOMA", Pos = 12)]
		public string TopMaterial;

		[MaterialReference]
		[BiffString("MAPH", Pos = 29)]
		public string PhysicsMaterial;

		[MaterialReference]
		[BiffString("SLMA", Pos = 13)]
		public string SlingShotMaterial;

		[BiffFloat("HTBT", Pos = 14)]
		public float HeightBottom = 0f;

		[BiffFloat("HTTP", Pos = 15)]
		public float HeightTop = 50f;

		[BiffBool("INNR", SkipWrite = true)]
		public bool Inner = true;

		[BiffBool("DSPT", Pos = 17)]
		public bool DisplayTexture = false;

		[BiffFloat("SLGF", Pos = 18)]
		public float SlingshotForce = 80f;

		[BiffFloat("SLTH", Pos = 19)]
		public float SlingshotThreshold = 0f;

		[BiffBool("SLGA", Pos = 24)]
		public bool SlingshotAnimation = true;

		[BiffFloat("ELAS", Pos = 20)]
		public float Elasticity;

		[BiffFloat("WFCT", Pos = 21)]
		public float Friction;

		[BiffFloat("WSCT", Pos = 22)]
		public float Scatter;

		[BiffBool("VSBL", Pos = 23)]
		public bool IsTopBottomVisible = true;

		[BiffBool("OVPH", Pos = 30)]
		public bool OverwritePhysics = true;

		[BiffFloat("DILI", QuantizedUnsignedBits = 8, Pos = 26)]
		public float DisableLightingTop;

		[BiffFloat("DILB", Pos = 27)]
		public float DisableLightingBelow;

		[BiffBool("SVBL", Pos = 25)]
		public bool IsSideVisible = true;

		[BiffBool("REEN", Pos = 28)]
		public bool IsReflectionEnabled = true;

		[BiffDragPoint("DPNT", TagAll = true, Pos = 2000)]
		public DragPointData[] DragPoints;

		[BiffBool("TMON", Pos = 6)]
		public bool IsTimerEnabled;

		[BiffInt("TMIN", Pos = 7)]
		public int TimerInterval;

		[BiffTag("PNTS", Pos = 1999)]
		public bool Points;

		// IPhysicalData
		public float GetElasticity() => Elasticity;
		public float GetElasticityFalloff() => 0;
		public float GetFriction() => Friction;
		public float GetScatter() => Scatter;
		public bool GetOverwritePhysics() => OverwritePhysics;
		public bool GetIsCollidable() => IsCollidable;
		public string GetPhysicsMaterial() => PhysicsMaterial;

		// non-persisted
		public bool IsDisabled;

		#region BIFF

		static SurfaceData()
		{
			Init(typeof(SurfaceData), Attributes);
		}

		public SurfaceData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write((int)ItemType.Surface);
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
