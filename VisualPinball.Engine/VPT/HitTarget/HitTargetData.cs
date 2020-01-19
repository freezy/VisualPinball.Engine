#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
#endregion

using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.HitTarget
{
	public class HitTargetData : ItemData
	{
		[BiffString("NAME", IsWideString = true)]
		public override string Name { get; set; }

		[BiffFloat("PIDB")]
		public float DepthBias;

		[BiffFloat("DILB")]
		public float DisableLightingBelow;

		[BiffFloat("DILI", QuantizedUnsignedBits = 8)]
		public float DisableLightingTop;

		[BiffFloat("DRSP")]
		public float DropSpeed =  0.5f;

		[BiffBool("REEN")]
		public bool IsReflectionEnabled = true;

		[BiffInt("RADE")]
		public int RaiseDelay = 100;

		[BiffFloat("ELAS")]
		public float Elasticity;

		[BiffFloat("ELFO")]
		public float ElasticityFalloff;

		[BiffFloat("RFCT")]
		public float Friction;

		[BiffBool("CLDR")]
		public bool IsCollidable = true;

		[BiffBool("ISDR")]
		public bool IsDropped = false;

		[BiffBool("TVIS")]
		public bool IsVisible = true;

		[BiffBool("LEMO")]
		public bool IsLegacy = false;

		[BiffBool("OVPH")]
		public bool OverwritePhysics = false;

		[BiffFloat("ROTZ")]
		public float RotZ = 0f;

		[BiffFloat("RSCT")]
		public float Scatter;

		[BiffString("IMAG")]
		public string Image;

		[BiffString("MATR")]
		public string Material;

		[BiffString("MAPH")]
		public string PhysicsMaterial;

		[BiffInt("TRTY")]
		public int TargetType = VisualPinball.Engine.VPT.TargetType.DropTargetSimple;

		[BiffFloat("THRS")]
		public float Threshold = 2.0f;

		[BiffBool("HTEV")]
		public bool UseHitEvent = true;

		[BiffVertex("VPOS")]
		public Vertex3D Position = new Vertex3D();

		[BiffVertex("VSIZ")]
		public Vertex3D Size = new Vertex3D(32, 32, 32);

		public bool IsDropTarget =>
			   TargetType == VisualPinball.Engine.VPT.TargetType.DropTargetBeveled
			|| TargetType == VisualPinball.Engine.VPT.TargetType.DropTargetFlatSimple
			|| TargetType == VisualPinball.Engine.VPT.TargetType.DropTargetSimple;

		#region Biff

		static HitTargetData()
		{
			Init(typeof(HitTargetData), Attributes);
		}

		public HitTargetData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
