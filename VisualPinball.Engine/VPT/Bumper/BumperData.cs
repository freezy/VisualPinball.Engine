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

namespace VisualPinball.Engine.VPT.Bumper
{
	public class BumperData : ItemData
	{
		[BiffString("NAME", IsWideString = true)]
		public override string Name { get; set; }

		[BiffVertex("VCEN")]
		public Vertex2D Center;

		[BiffFloat("RADI")]
		public float Radius = 45f;

		[BiffString("MATR")]
		public string CapMaterial;

		[BiffString("RIMA")]
		public string RingMaterial;

		[BiffString("BAMA")]
		public string BaseMaterial;

		[BiffString("SKMA")]
		public string SkirtMaterial;

		[BiffFloat("THRS")]
		public float Threshold = 1.0f;

		[BiffFloat("FORC")]
		public float Force;

		[BiffFloat("BSCT")]
		public float Scatter;

		[BiffFloat("HISC")]
		public float HeightScale = 90.0f;

		[BiffFloat("RISP")]
		public float RingSpeed = 0.5f;

		[BiffFloat("ORIN")]
		public float Orientation = 0.0f;

		[BiffFloat("RDLI")]
		public float RingDropOffset = 0.0f;

		[BiffString("SURF")]
		public string Surface;

		[BiffBool("BVIS")]
		[BiffBool("CAVI")]
		public bool IsCapVisible = true;

		[BiffBool("BSVS")]
		[BiffBool("BVIS")]
		public bool IsBaseVisible = true;

		[BiffBool("BSVS")]
		[BiffBool("BVIS")]
		[BiffBool("RIVS")]
		public bool IsRingVisible = true;

		[BiffBool("BSVS")]
		[BiffBool("BVIS")]
		[BiffBool("SKVS")]
		public bool IsSkirtVisible = true;

		[BiffBool("HAHE")]
		public bool HitEvent = true;

		[BiffBool("COLI")]
		public bool IsCollidable = true;

		[BiffBool("REEN")]
		public bool IsReflectionEnabled = true;

		static BumperData()
		{
			Init(typeof(BumperData), Attributes);
		}

		public BumperData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();
	}
}
