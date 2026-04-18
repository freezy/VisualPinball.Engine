// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

// ReSharper disable MemberCanBePrivate.Global

namespace VisualPinball.Unity
{
	public struct FlipperPackable
	{
		public float EndAngle;
		public bool IsEnabled;
		public bool IsDualWound;
		public float Height;
		public float BaseRadius;
		public float EndRadius;
		public float FlipperRadiusMin;
		public float FlipperRadiusMax;
		public float RubberThickness;
		public float RubberHeight;
		public float RubberWidth;

		public static byte[] Pack(FlipperComponent comp)
		{
			return PackageApi.Packer.Pack(new FlipperPackable {
				EndAngle = comp.EndAngle,
				IsEnabled = comp.IsEnabled,
				IsDualWound = comp.IsDualWound,
				Height = comp.Height,
				BaseRadius = comp.BaseRadius,
				EndRadius = comp.EndRadius,
				FlipperRadiusMin = comp.FlipperRadiusMin,
				FlipperRadiusMax = comp.FlipperRadiusMax,
				RubberThickness = comp.RubberThickness,
				RubberHeight = comp.RubberHeight,
				RubberWidth = comp.RubberWidth,
			});
		}

		public static void Unpack(byte[] bytes, FlipperComponent comp)
		{
			var data = PackageApi.Packer.Unpack<FlipperPackable>(bytes);
			comp.EndAngle = data.EndAngle;
			comp.IsEnabled = data.IsEnabled;
			comp.IsDualWound = data.IsDualWound;
			comp._height = data.Height;
			comp._baseRadius = data.BaseRadius;
			comp._endRadius = data.EndRadius;
			comp.FlipperRadiusMin = data.FlipperRadiusMin;
			comp.FlipperRadiusMax = data.FlipperRadiusMax;
			comp._rubberThickness = data.RubberThickness;
			comp._rubberHeight = data.RubberHeight;
			comp._rubberWidth = data.RubberWidth;
		}
	}

	public struct FlipperColliderPackable
	{
		public bool IsMovable;
		public float Mass;
		public float Strength;
		public float Return;
		public float RampUp;
		public float TorqueDamping;
		public float TorqueDampingAngle;
		public bool? UseFlipperTricksPhysics;
		public float? SOSRampUp;
		public float? SOSEM;
		public float? EOSReturn;
		public float? EOSTNew;
		public float? EOSANew;
		public float? EOSRampup;
		public float? Overshoot;
		public float? BumpOnRelease;
		public bool? UseFlipperLiveCatch;
		public float? LiveCatchDistanceMin;
		public float? LiveCatchDistanceMax;
		public float? LiveCatchMinimalBallSpeed;
		public float? LiveCatchFullTime;
		public float? LiveCatchPerfectTime;
		public float? LiveCatchMinmalBounceSpeedMultiplier;
		public float? LiveCatchInaccurateBounceSpeedMultiplier;
		public float? LiveCatchBaseDampenDistance;
		public float? LiveCatchBaseDampen;

		public static byte[] Pack(FlipperColliderComponent comp)
		{
			return PackageApi.Packer.Pack(new FlipperColliderPackable {
				IsMovable = comp._isKinematic,
				Mass = comp.Mass,
				Strength = comp.Strength,
				Return = comp.Return,
				RampUp = comp.RampUp,
				TorqueDamping = comp.TorqueDamping,
				TorqueDampingAngle = comp.TorqueDampingAngle,
				UseFlipperTricksPhysics = comp.useFlipperTricksPhysics,
				SOSRampUp = comp.SOSRampUp,
				SOSEM = comp.SOSEM,
				EOSReturn = comp.EOSReturn,
				EOSTNew = comp.EOSTNew,
				EOSANew = comp.EOSANew,
				EOSRampup = comp.EOSRampup,
				Overshoot = comp.Overshoot,
				BumpOnRelease = comp.BumpOnRelease,
				UseFlipperLiveCatch = comp.useFlipperLiveCatch,
				LiveCatchDistanceMin = comp.LiveCatchDistanceMin,
				LiveCatchDistanceMax = comp.LiveCatchDistanceMax,
				LiveCatchMinimalBallSpeed = comp.LiveCatchMinimalBallSpeed,
				LiveCatchFullTime = comp.LiveCatchFullTime,
				LiveCatchPerfectTime = comp.LiveCatchPerfectTime,
				LiveCatchMinmalBounceSpeedMultiplier = comp.LiveCatchMinmalBounceSpeedMultiplier,
				LiveCatchInaccurateBounceSpeedMultiplier = comp.LiveCatchInaccurateBounceSpeedMultiplier,
				LiveCatchBaseDampenDistance = comp.LiveCatchBaseDampenDistance,
				LiveCatchBaseDampen = comp.LiveCatchBaseDampen,
			});
		}

		public static void Unpack(byte[] bytes, FlipperColliderComponent comp)
		{
			var data = PackageApi.Packer.Unpack<FlipperColliderPackable>(bytes);
			comp._isKinematic = data.IsMovable;
			comp.Mass = data.Mass;
			comp.Strength = data.Strength;
			comp.Return = data.Return;
			comp.RampUp = data.RampUp;
			comp.TorqueDamping = data.TorqueDamping;
			comp.TorqueDampingAngle = data.TorqueDampingAngle;
			comp.useFlipperTricksPhysics = data.UseFlipperTricksPhysics ?? comp.useFlipperTricksPhysics;
			comp.SOSRampUp = data.SOSRampUp ?? comp.SOSRampUp;
			comp.SOSEM = data.SOSEM ?? comp.SOSEM;
			comp.EOSReturn = data.EOSReturn ?? comp.EOSReturn;
			comp.EOSTNew = data.EOSTNew ?? comp.EOSTNew;
			comp.EOSANew = data.EOSANew ?? comp.EOSANew;
			comp.EOSRampup = data.EOSRampup ?? comp.EOSRampup;
			comp.Overshoot = data.Overshoot ?? comp.Overshoot;
			comp.BumpOnRelease = data.BumpOnRelease ?? comp.BumpOnRelease;
			comp.useFlipperLiveCatch = data.UseFlipperLiveCatch ?? comp.useFlipperLiveCatch;
			comp.LiveCatchDistanceMin = data.LiveCatchDistanceMin ?? comp.LiveCatchDistanceMin;
			comp.LiveCatchDistanceMax = data.LiveCatchDistanceMax ?? comp.LiveCatchDistanceMax;
			comp.LiveCatchMinimalBallSpeed = data.LiveCatchMinimalBallSpeed ?? comp.LiveCatchMinimalBallSpeed;
			comp.LiveCatchFullTime = data.LiveCatchFullTime ?? comp.LiveCatchFullTime;
			comp.LiveCatchPerfectTime = data.LiveCatchPerfectTime ?? comp.LiveCatchPerfectTime;
			comp.LiveCatchMinmalBounceSpeedMultiplier = data.LiveCatchMinmalBounceSpeedMultiplier ?? comp.LiveCatchMinmalBounceSpeedMultiplier;
			comp.LiveCatchInaccurateBounceSpeedMultiplier = data.LiveCatchInaccurateBounceSpeedMultiplier ?? comp.LiveCatchInaccurateBounceSpeedMultiplier;
			comp.LiveCatchBaseDampenDistance = data.LiveCatchBaseDampenDistance ?? comp.LiveCatchBaseDampenDistance;
			comp.LiveCatchBaseDampen = data.LiveCatchBaseDampen ?? comp.LiveCatchBaseDampen;
		}
	}

	public struct FlipperColliderReferencesPackable
	{
		public PhysicalMaterialPackable PhysicalMaterial;
		public int FlipperCorrectionRef;

		public static byte[] PackReferences(FlipperColliderComponent comp, PackagedFiles files)
		{
			return PackageApi.Packer.Pack(new FlipperColliderReferencesPackable {
				PhysicalMaterial = new PhysicalMaterialPackable {
					Elasticity = comp.Elasticity,
					ElasticityFalloff = comp.ElasticityFalloff,
					Friction = comp.Friction,
					Scatter = comp.Scatter,
					Overwrite = true,
					AssetRef = files.AddAsset(comp.PhysicsMaterial),
				},
				FlipperCorrectionRef = files.AddAsset(comp.FlipperCorrection)
			});
		}

		public static void Unpack(byte[] bytes, FlipperColliderComponent comp, PackagedFiles files)
		{
			var data = PackageApi.Packer.Unpack<FlipperColliderReferencesPackable>(bytes);
			comp.Elasticity = data.PhysicalMaterial.Elasticity;
			comp.ElasticityFalloff = data.PhysicalMaterial.ElasticityFalloff;
			comp.Friction = data.PhysicalMaterial.Friction;
			comp.Scatter = data.PhysicalMaterial.Scatter;
			comp.PhysicsMaterial = files.GetAsset<PhysicsMaterialAsset>(data.PhysicalMaterial.AssetRef);
			comp.FlipperCorrection = files.GetAsset<FlipperCorrectionAsset>(data.FlipperCorrectionRef);
		}
	}
}
