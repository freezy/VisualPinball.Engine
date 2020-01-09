#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
#endregion

using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Gate
{
	public class GateData : ItemData
	{
		[BiffString("NAME", IsWideString = true)]
		public override string Name { get; set; }

		[BiffFloat("GAMA")]
		public float AngleMax = MathF.PI / 2.0f;

		[BiffFloat("GAMI")]
		public float AngleMin = 0f;

		[BiffFloat("AFRC")]
		public float Damping = 0.985f;

		[BiffFloat("ELAS")]
		public float Elasticity = 0.3f;

		[BiffFloat("GFRC")]
		public float Friction = 0.02f;

		[BiffFloat("GATY")]
		public float GateType = VisualPinball.Engine.VPT.GateType.GateWireW;

		[BiffFloat("GGFC")]
		public float GravityFactor = 0.25f;

		[BiffFloat("HGTH")]
		public float Height = 50f;

		[BiffBool("GCOL")]
		public bool IsCollidable = true;

		[BiffBool("REEN")]
		public bool IsReflectionEnabled = true;

		[BiffBool("GVSB")]
		public bool IsVisible = true;

		[BiffFloat("LGTH")]
		public float Length = 100f;

		[BiffFloat("ROTA")]
		public float Rotation = -90f;

		[BiffBool("GSUP")]
		public bool ShowBracket = true;

		[BiffString("MATR")]
		public string Material;

		[BiffString("SURF")]
		public string Surface;

		[BiffBool("TWWA")]
		public bool TwoWay = false;

		[BiffVertex("VCEN")]
		public Vertex2D Center;

		static GateData()
		{
			Init(typeof(GateData), Attributes);
		}

		public GateData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();
	}
}
