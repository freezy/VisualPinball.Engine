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

namespace VisualPinball.Engine.VPT.Ramp
{
	public class RampData : ItemData
	{
		[BiffString("NAME", IsWideString = true)]
		public override string Name { get; set; }

		[BiffFloat("RADB")]
		public float DepthBias = 0f;

		[BiffDragPoint("DPNT")]
		public DragPoint[] DragPoints;

		[BiffFloat("ELAS")]
		public float Elasticity;

		[BiffFloat("RFCT")]
		public float Friction;

		[BiffBool("HTEV")]
		public bool HitEvent = false;

		[BiffFloat("HTBT")]
		public float HeightBottom = 0f;

		[BiffFloat("HTTP")]
		public float HeightTop = 50f;

		[BiffInt("ALGN")]
		public int ImageAlignment = RampImageAlignment.ImageModeWorld;

		[BiffBool("IMGW")]
		public bool ImageWalls = true;

		[BiffBool("CLDR")]
		public bool IsCollidable = true;

		[BiffBool("REEN")]
		public bool IsReflectionEnabled = true;

		[BiffBool("RVIS")]
		public bool IsVisible = true;

		[BiffFloat("WLHL")]
		public float LeftWallHeight = 62f;

		[BiffFloat("WVHL")]
		public float LeftWallHeightVisible = 30f;

		[BiffBool("OVPH")]
		public bool OverwritePhysics = true;

		[BiffInt("TYPE")]
		public int RampType = VisualPinball.Engine.VPT.RampType.RampTypeFlat;

		[BiffFloat("WLHR")]
		public float RightWallHeight = 62f;

		[BiffFloat("WVHR")]
		public float RightWallHeightVisible = 30f;

		[BiffFloat("RSCT")]
		public float Scatter;

		[BiffString("IMAG")]
		public string Image;

		[BiffString("MATR")]
		public string Material;

		[BiffString("MAPH")]
		public string PhysicsMaterial;

		[BiffFloat("THRS")]
		public float Threshold;

		[BiffFloat("WDBT")]
		public float WidthBottom = 75f;

		[BiffFloat("WDTP")]
		public float WidthTop = 60f;

		[BiffFloat("RADI")]
		public float WireDiameter = 8f;

		[BiffFloat("RADX")]
		public float WireDistanceX = 38f;

		[BiffFloat("RADY")]
		public float WireDistanceY = 88f;


		#region BIFF

		static RampData()
		{
			Init(typeof(RampData), Attributes);
		}

		public RampData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer)
		{
			writer.Write(ItemType.Ramp);
			Write(writer, Attributes);
			WriteEnd(writer);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
