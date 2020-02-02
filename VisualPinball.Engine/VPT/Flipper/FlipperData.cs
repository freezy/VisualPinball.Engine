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

namespace VisualPinball.Engine.VPT.Flipper
{
	[Serializable]
	public class FlipperData : ItemData
	{
		[BiffString("NAME", IsWideString = true)]
		public override string Name { get; set; }

		[BiffFloat("BASR")]
		public float BaseRadius = 21.5f;

		[BiffFloat("ENDR")]
		public float EndRadius = 13.0f;

		[BiffFloat("FRMN")]
		public float FlipperRadiusMin;

		[BiffFloat("FLPR")]
		public float FlipperRadiusMax = 130.0f;

		[BiffFloat("FLPR")]
		public float FlipperRadius = 130.0f;

		[BiffFloat("ANGS")]
		public float StartAngle = 121.0f;

		[BiffFloat("ANGE")]
		public float EndAngle = 70.0f;

		[BiffFloat("FHGT")]
		public float Height = 50.0f;

		[BiffVertex("VCEN")]
		public Vertex2D Center;

		[BiffString("IMAG")]
		public string Image;

		[BiffString("SURF")]
		public string Surface;

		[BiffString("MATR")]
		public string Material;

		[BiffString("RUMA")]
		public string RubberMaterial;

		[BiffFloat("RTHF")]
		public float RubberThickness = 7.0f;

		[BiffFloat("RHGF")]
		public float RubberHeight = 19.0f;

		[BiffFloat("RWDF")]
		public float RubberWidth = 24.0f;

		[BiffFloat("FORC")]
		public float Mass;

		[BiffFloat("STRG")]
		public float Strength;

		[BiffFloat("ELAS")]
		public float Elasticity;

		[BiffFloat("ELFO")]
		public float ElasticityFalloff;

		[BiffFloat("FRIC")]
		public float Friction;

		[BiffFloat("FRTN")]
		public float Return;

		[BiffFloat("RPUP")]
		public float RampUp;

		[BiffFloat("TODA")]
		public float TorqueDamping;

		[BiffFloat("TDAA")]
		public float TorqueDampingAngle;

		[BiffFloat("SCTR")]
		public float Scatter;

		[BiffInt("OVRP")]
		public int OverridePhysics;

		[BiffBool("VSBL")]
		public bool IsVisible = true;

		[BiffBool("ENBL")]
		public bool IsEnabled = true;

		[BiffBool("REEN")]
		public bool IsReflectionEnabled = true;

		static FlipperData()
		{
			Init(typeof(FlipperData), Attributes);
		}

		public FlipperData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();
	}
}
