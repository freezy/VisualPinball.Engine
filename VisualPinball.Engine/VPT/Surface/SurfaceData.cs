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

namespace VisualPinball.Engine.VPT.Surface
{
	public class SurfaceData : ItemData
	{
		[BiffString("NAME", IsWideString = true)]
		public override string Name { get; set; }

		[BiffBool("HTEV")]
		public bool HitEvent = false;

		[BiffBool("DROP")]
		public bool IsDroppable = false;

		[BiffBool("FLIP")]
		public bool IsFlipbook = false;

		[BiffBool("ISBS")]
		public bool IsBottomSolid = false;

		[BiffBool("CLDW")]
		public bool IsCollidable = true;

		[BiffFloat("THRS")]
		public float Threshold = 2.0f;

		[BiffString("IMAG")]
		public string Image;

		[BiffString("SIMG")]
		public string SideImage;

		[BiffString("SIMA")]
		public string SideMaterial;

		[BiffString("TOMA")]
		public string TopMaterial;

		[BiffString("MAPH")]
		public string PhysicsMaterial;

		[BiffString("SLMA")]
		public string SlingShotMaterial;

		[BiffFloat("HTBT")]
		public float HeightBottom = 0f;

		[BiffFloat("HTTP")]
		public float HeightTop = 50f;

		[BiffBool("INNR")]
		public bool Inner = true;

		[BiffBool("DSPT")]
		public bool DisplayTexture = false;

		[BiffFloat("SLGF")]
		public float SlingshotForce = 80f;

		[BiffFloat("SLTH")]
		public float SlingshotThreshold = 0f;

		[BiffBool("SLGA")]
		public bool SlingshotAnimation = true;

		[BiffFloat("ELAS")]
		public float Elasticity;

		[BiffFloat("WFCT")]
		public float Friction;

		[BiffFloat("WSCT")]
		public float Scatter;

		[BiffBool("VSBL")]
		public bool IsTopBottomVisible = true;

		[BiffBool("OVPH")]
		public bool OverwritePhysics = true;

		[BiffFloat("DILI", QuantizedUnsignedBits = 8)]
		public float DisableLightingTop;

		[BiffFloat("DILB")]
		public float DisableLightingBelow;

		[BiffBool("SVBL")]
		public bool IsSideVisible = true;

		[BiffBool("REEN")]
		public bool IsReflectionEnabled = true;

		[BiffDragPoint("DPNT")]
		public DragPoint[] DragPoints;

		#region BIFF

		static SurfaceData()
		{
			Init(typeof(SurfaceData), Attributes);
		}

		public SurfaceData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer)
		{
			writer.Write(ItemType.Surface);
			Write(writer, Attributes);
			WriteEnd(writer);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
