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
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Flipper
{
	[Serializable]
	public class FlipperData : ItemData
	{
		public override string GetName() => Name;

		[BiffString("NAME", IsWideString = true, Pos = 14)]
		public string Name;

		[BiffFloat("BASR", Pos = 2)]
		public float BaseRadius = 21.5f;

		[BiffFloat("ENDR", Pos = 3)]
		public float EndRadius = 13.0f;

		[BiffFloat("FRMN", Pos = 29)]
		public float FlipperRadiusMin;

		[BiffFloat("FLPR", Pos = 4)]
		public float FlipperRadiusMax = 130.0f;

		[BiffFloat("FLPR", SkipWrite = true)]
		public float FlipperRadius = 130.0f;

		[BiffFloat("ANGS", Pos = 6)]
		public float StartAngle = 121.0f;

		[BiffFloat("ANGE", Pos = 7)]
		public float EndAngle = 70.0f;

		[BiffFloat("FHGT", Pos = 30)]
		public float Height = 50.0f;

		[BiffVertex("VCEN", Pos = 1)]
		public Vertex2D Center;

		[BiffString("IMAG", Pos = 31)]
		public string Image;

		[BiffString("SURF", Pos = 12)]
		public string Surface;

		[BiffString("MATR", Pos = 13)]
		public string Material;

		[BiffString("RUMA", Pos = 15)]
		public string RubberMaterial;

		[BiffFloat("RTHF", Pos = 16.1)]
		public float RubberThickness = 7.0f;

		[BiffFloat("RHGF", Pos = 17.1)]
		public float RubberHeight = 19.0f;

		[BiffFloat("RWDF", Pos = 18.1)]
		public float RubberWidth = 24.0f;

		[BiffFloat("FORC", Pos = 9)]
		public float Mass;

		[BiffFloat("STRG", Pos = 19)]
		public float Strength;

		[BiffFloat("ELAS", Pos = 20)]
		public float Elasticity;

		[BiffFloat("ELFO", Pos = 21)]
		public float ElasticityFalloff;

		[BiffFloat("FRIC", Pos = 22)]
		public float Friction;

		[BiffFloat("FRTN", Pos = 5)]
		public float Return;

		[BiffFloat("RPUP", Pos = 23)]
		public float RampUp;

		[BiffFloat("TODA", Pos = 25)]
		public float TorqueDamping;

		[BiffFloat("TDAA", Pos = 26)]
		public float TorqueDampingAngle;

		[BiffFloat("SCTR", Pos = 24)]
		public float Scatter;

		[BiffInt("OVRP", Pos = 8)]
		public int OverridePhysics;

		[BiffBool("VSBL", Pos = 27)]
		public bool IsVisible = true;

		[BiffBool("ENBL", Pos = 28)]
		public bool IsEnabled = true;

		[BiffBool("REEN", Pos = 32)]
		public bool IsReflectionEnabled = true;

		[BiffBool("TMON", Pos = 10)]
		public bool IsTimerEnabled;

		[BiffInt("TMIN", Pos = 11)]
		public int TimerInterval;

		#region BIFF

		static FlipperData()
		{
			Init(typeof(FlipperData), Attributes);
		}

		public FlipperData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write(ItemType.Flipper);
			Write(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
