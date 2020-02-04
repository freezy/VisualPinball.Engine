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
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Rubber
{
	public class RubberData : ItemData
	{
		[BiffString("NAME", IsWideString = true)]
		public override string Name { get; set; }

		[BiffFloat("HTTP")]
		public float Height = 25f;

		[BiffFloat("HTHI")]
		public float HitHeight = -1.0f;

		[BiffInt("WDTP")]
		public int Thickness = 8;

		[BiffBool("HTEV")]
		public bool HitEvent = false;

		[BiffString("MATR")]
		public string Material;

		[BiffString("IMAG")]
		public string Image;

		[BiffFloat("ELAS")]
		public float Elasticity;

		[BiffFloat("ELFO")]
		public float ElasticityFalloff;

		[BiffFloat("RFCT")]
		public float Friction;

		[BiffFloat("RSCT")]
		public float Scatter;

		[BiffBool("CLDR")]
		public bool IsCollidable = true;

		[BiffBool("RVIS")]
		public bool IsVisible = true;

		[BiffBool("REEN")]
		public bool IsReflectionEnabled = true;

		[BiffBool("ESTR")]
		public bool StaticRendering = true;

		[BiffBool("ESIE")]
		public bool ShowInEditor = true;

		[BiffFloat("ROTX")]
		public float RotX = 0f;

		[BiffFloat("ROTY")]
		public float RotY = 0f;

		[BiffFloat("ROTZ")]
		public float RotZ = 0f;

		[BiffString("MAPH")]
		public string PhysicsMaterial;

		[BiffBool("OVPH")]
		public bool OverwritePhysics = false;

		[BiffDragPoint("DPNT")]
		public DragPoint[] DragPoints;

		#region BIFF

		static RubberData()
		{
			Init(typeof(RubberData), Attributes);
		}

		public RubberData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write(ItemType.Rubber);
			Write(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
