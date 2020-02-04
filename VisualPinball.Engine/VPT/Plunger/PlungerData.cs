#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
#endregion

using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Plunger
{
	public class PlungerData : ItemData
	{
		[BiffString("NAME", IsWideString = true)]
		public override string Name { get; set; }

		[BiffInt("TYPE")]
		public int Type = PlungerType.PlungerTypeModern;

		[BiffVertex("VCEN")]
		public Vertex2D Center;

		[BiffFloat("WDTH")]
		public float Width = 25f;

		[BiffFloat("HIGH")]
		public float Height = 20f;

		[BiffFloat("ZADJ")]
		public float ZAdjust;

		[BiffFloat("HPSL")]
		public float Stroke;

		[BiffFloat("SPDP")]
		public float SpeedPull = 0.5f;

		[BiffFloat("SPDF")]
		public float SpeedFire = 80f;

		[BiffFloat("MEST")]
		public float MechStrength = 85f;

		[BiffFloat("MPRK")]
		public float ParkPosition = 0.5f / 3.0f;

		[BiffFloat("PSCV")]
		public float ScatterVelocity = 0f;

		[BiffFloat("MOMX")]
		public float MomentumXfer = 1f;

		[BiffBool("MECH")]
		public bool MechPlunger = false;

		[BiffBool("APLG")]
		public bool AutoPlunger = false;

		[BiffInt("ANFR")]
		public int AnimFrames;

		[BiffString("MATR")]
		public string Material;

		[BiffString("IMAG")]
		public string Image;

		[BiffBool("VSBL")]
		public bool IsVisible = true;

		[BiffBool("REEN")]
		public bool IsReflectionEnabled = true;

		[BiffString("SURF")]
		public string Surface;

		[BiffString("TIPS")]
		public string TipShape = "0 .34; 2 .6; 3 .64; 5 .7; 7 .84; 8 .88; 9 .9; 11 .92; 14 .92; 39 .84";

		[BiffFloat("RODD")]
		public float RodDiam = 0.6f;

		[BiffFloat("RNGG")]
		public float RingGap = 2.0f;

		[BiffFloat("RNGD")]
		public float RingDiam = 0.94f;

		[BiffFloat("RNGW")]
		public float RingWidth = 3.0f;

		[BiffFloat("SPRD")]
		public float SpringDiam = 0.77f;

		[BiffFloat("SPRG")]
		public float SpringGauge = 1.38f;

		[BiffFloat("SPRL")]
		public float SpringLoops = 8.0f;

		[BiffFloat("SPRE")]
		public float SpringEndLoops = 2.5f;

		public Color Color = new Color(0x4c4c4cf, ColorFormat.Bgr);

		#region BIFF

		static PlungerData()
		{
			Init(typeof(PlungerData), Attributes);
		}

		public PlungerData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write(ItemType.Plunger);
			Write(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
