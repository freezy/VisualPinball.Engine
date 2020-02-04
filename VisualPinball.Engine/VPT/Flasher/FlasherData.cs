#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ConvertToConstant.Global
#endregion

using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Flasher
{
	public class FlasherData : ItemData
	{
		[BiffString("NAME", IsWideString = true)]
		public override string Name { get; set; }

		[BiffFloat("FHEI")]
		public float Height = 50.0f;

		[BiffFloat("FLAX")] public float PosX { set => Center.X = value; }
		[BiffFloat("FLAY")] public float PosY { set => Center.Y = value; }

		public Vertex2D Center = new Vertex2D();

		[BiffFloat("FROX")]
		public float RotX = 0.0f;

		[BiffFloat("FROY")]
		public float RotY = 0.0f;

		[BiffFloat("FROZ")]
		public float RotZ = 0.0f;

		[BiffColor("COLR")]
		public Color Color = new Color(0xfffffff, ColorFormat.Bgr);

		[BiffString("IMAG")]
		public string ImageA;

		[BiffString("IMAB")]
		public string ImageB;

		[BiffInt("FALP", Min = 0)]
		public int Alpha = 100;

		[BiffFloat("MOVA")]
		public float ModulateVsAdd = 0.9f;

		[BiffBool("FVIS")]
		public bool IsVisible = true;

		[BiffBool("ADDB")]
		public bool AddBlend = false;

		[BiffBool("IDMD")]
		public bool IsDMD = false;

		[BiffBool("DSPT")]
		public bool DisplayTexture = false;

		[BiffFloat("FLDB")]
		public float DepthBias = 0.0f;

		[BiffInt("ALGN")]
		public int ImageAlignment = VisualPinball.Engine.VPT.ImageAlignment.ImageAlignTopLeft;

		[BiffInt("FILT")]
		public int Filter = Filters.Filter_Overlay;

		[BiffInt("FIAM")]
		public int FilterAmount = 100;

		#region BIFF

		static FlasherData()
		{
			Init(typeof(FlasherData), Attributes);
		}

		public FlasherData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write(ItemType.Flasher);
			Write(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
