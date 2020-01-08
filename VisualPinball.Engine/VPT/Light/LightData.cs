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

namespace VisualPinball.Engine.VPT.Light
{
	public class LightData : ItemData
	{
		[BiffString("NAME", IsWideString = true)]
		public override string Name { get; set; }

		[BiffVertex("VCEN")]
		public Vertex2D Center;

		[BiffFloat("RADI")]
		public float Falloff = 50f;

		[BiffFloat("FAPO")]
		public float FalloffPower = 2f;

		[BiffInt("STAT")]
		public int State = LightStatus.LightStateOff;

		[BiffColor("COLR")]
		public Color Color = new Color(0xffff00, ColorFormat.Argb);

		[BiffColor("COL2")]
		public Color Color2 = new Color(0xffffff, ColorFormat.Argb);

		[BiffString("IMG1")]
		public string OffImage;

		[BiffBool("SHAP")]
		public bool IsRoundLight = false;

		[BiffString("BPAT")]
		public string BlinkPattern = "0";

		[BiffInt("BINT")]
		public int BlinkInterval = 125;

		[BiffFloat("BWTH")]
		public float Intensity = 1f;

		[BiffFloat("TRMS")]
		public float TransmissionScale = 0f;

		[BiffString("SURF")]
		public string Surface;

		[BiffBool("BGLS")]
		public bool IsBackglass = false;

		[BiffFloat("LIDB")]
		public float DepthBias;

		[BiffFloat("FASP")]
		public float FadeSpeedUp = 0.2f;

		[BiffFloat("FASD")]
		public float FadeSpeedDown = 0.2f;

		[BiffBool("BULT")]
		public bool IsBulbLight = false;

		[BiffBool("IMMO")]
		public bool IsImageMode = false;

		[BiffBool("SHBM")]
		public bool ShowBulbMesh = false;

		[BiffBool("STBM")]
		public bool HasStaticBulbMesh = false;

		[BiffBool("SHRB")]
		public bool ShowReflectionOnBall = true;

		[BiffFloat("BMSC")]
		public float MeshRadius = 20f;

		[BiffFloat("BMVA")]
		public float BulbModulateVsAdd = 0.9f;

		[BiffFloat("BHHI")]
		public float BulbHaloHeight = 28f;

		[BiffDragPoint("DPNT")]
		public DragPoint[] DragPoints;

		static LightData()
		{
			Init(typeof(LightData), Attributes);
		}

		public LightData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();
	}
}
