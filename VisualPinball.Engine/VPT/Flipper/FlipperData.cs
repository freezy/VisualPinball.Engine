#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Flipper
{
	[Serializable]
	[BiffIgnore("RWDT")]
	[BiffIgnore("RHGT")]
	[BiffIgnore("RTHK")]
	public class FlipperData : ItemData
	{
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

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

		[MaterialReference]
		[BiffString("MATR", Pos = 13)]
		public string Material;

		[MaterialReference]
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

		public float OverrideMass;
		public float OverrideStrength;
		public float OverrideElasticity;
		public float OverrideElasticityFalloff;
		public float OverrideFriction;
		public float OverrideReturnStrength;
		public float OverrideCoilRampUp;
		public float OverrideTorqueDamping;
		public float OverrideTorqueDampingAngle;
		public float OverrideScatterAngle;

		public float GetReturnRatio(TableData tableData) => DoOverridePhysics(tableData) ? OverrideReturnStrength : Return;
		public float GetStrength(TableData tableData) => DoOverridePhysics(tableData) ? OverrideStrength : Strength;
		public float GetTorqueDampingAngle(TableData tableData) => DoOverridePhysics(tableData) ? OverrideTorqueDampingAngle : TorqueDampingAngle;
		public float GetFlipperMass(TableData tableData) => DoOverridePhysics(tableData) ? OverrideMass : Mass;
		public float GetTorqueDamping(TableData tableData) => DoOverridePhysics(tableData) ? OverrideTorqueDamping : TorqueDamping;
		public float GetRampUpSpeed(TableData tableData) => DoOverridePhysics(tableData) ? OverrideCoilRampUp : RampUp;

		public void UpdatePhysicsSettings(Table.Table table)
		{
			if (DoOverridePhysics(table.Data)) {
				var registry = Registry.Instance;

				var idx = OverridePhysics != 0 ? OverridePhysics - 1 : table.Data.OverridePhysics - 1;

				OverrideMass = registry.Get<float>("Player", $"FlipperPhysicsMass${idx}", 1);
				if (OverrideMass < 0.0) {
					OverrideMass = Mass;
				}

				OverrideStrength = registry.Get<float>("Player", $"FlipperPhysicsStrength${idx}", 2200);
				if (OverrideStrength < 0.0) {
					OverrideStrength = Strength;
				}

				OverrideElasticity = registry.Get<float>("Player", $"FlipperPhysicsElasticity${idx}", 0.8f);
				if (OverrideElasticity < 0.0) {
					OverrideElasticity = Elasticity;
				}

				OverrideScatterAngle = registry.Get<float>("Player", $"FlipperPhysicsScatter${idx}", 0);
				if (OverrideScatterAngle < 0.0) {
					OverrideScatterAngle = Scatter;
				}

				OverrideReturnStrength = registry.Get<float>("Player", $"FlipperPhysicsReturnStrength${idx}", 0.058f);
				if (OverrideReturnStrength < 0.0) {
					OverrideReturnStrength = Return;
				}

				OverrideElasticityFalloff =
					registry.Get<float>("Player", $"FlipperPhysicsElasticityFalloff${idx}", 0.43f);
				if (OverrideElasticityFalloff < 0.0) {
					OverrideElasticityFalloff = ElasticityFalloff;
				}

				OverrideFriction = registry.Get<float>("Player", $"FlipperPhysicsFriction${idx}", 0.6f);
				if (OverrideFriction < 0.0) {
					OverrideFriction = Friction;
				}

				OverrideCoilRampUp = registry.Get<float>("Player", $"FlipperPhysicsCoilRampUp${idx}", 3.0f);
				if (OverrideCoilRampUp < 0.0) {
					OverrideCoilRampUp = RampUp;
				}

				OverrideTorqueDamping = registry.Get<float>("Player", $"FlipperPhysicsEOSTorque${idx}", 0.75f);
				if (OverrideTorqueDamping < 0.0) {
					OverrideTorqueDamping = TorqueDamping;
				}

				OverrideTorqueDampingAngle = registry.Get<float>("Player", $"FlipperPhysicsEOSTorqueAngle${idx}", 6.0f);
				if (OverrideTorqueDampingAngle < 0.0) {
					OverrideTorqueDampingAngle = TorqueDampingAngle;
				}
			}
		}

		#region BIFF

		static FlipperData()
		{
			Init(typeof(FlipperData), Attributes);
		}

		public FlipperData(string storageName) : base(storageName)
		{
		}

		public FlipperData(BinaryReader reader, string storageName) : this(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write((int)ItemType.Flipper);
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion

		public bool DoOverridePhysics(TableData tableData) => OverridePhysics != 0 || tableData.OverridePhysicsFlipper && tableData.OverridePhysics != 0;
	}
}
