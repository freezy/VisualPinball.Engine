// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using UnityEngine;

namespace VisualPinball.Unity
{
	public struct SpringHingePackable
	{
		private const int CurrentVersion = 1;

		public int Version;
		public PackableFloat3 HingeAxis;
		public PackableFloat3 CentreOfMass;
		public float ToyMass;
		public bool OverrideInertia;
		public float ManualInertia;
		public PackableFloat3 MassBoxHalfExtents;
		public float SpringStiffness;
		public float SpringDamping;
		public float EquilibriumAngle;
		public float MinimumAngle;
		public float MaximumAngle;
		public float InitialAngle;
		public bool EnableAngleSwitch;
		public float SwitchCloseAngle;
		public float SwitchOpenAngle;

		public static byte[] Pack(SpringHingeComponent comp)
		{
			return PackageApi.Packer.Pack(new SpringHingePackable {
				Version = CurrentVersion,
				HingeAxis = comp.HingeAxis,
				CentreOfMass = comp.CentreOfMass,
				ToyMass = comp.ToyMass,
				OverrideInertia = comp.OverrideInertia,
				ManualInertia = comp.ManualInertia,
				MassBoxHalfExtents = comp.MassBoxHalfExtents,
				SpringStiffness = comp.SpringStiffness,
				SpringDamping = comp.SpringDamping,
				EquilibriumAngle = comp.EquilibriumAngle,
				MinimumAngle = comp.MinimumAngle,
				MaximumAngle = comp.MaximumAngle,
				InitialAngle = comp.InitialAngle,
				EnableAngleSwitch = comp.EnableAngleSwitch,
				SwitchCloseAngle = comp.SwitchCloseAngle,
				SwitchOpenAngle = comp.SwitchOpenAngle
			});
		}

		public static void Unpack(byte[] bytes, SpringHingeComponent comp)
		{
			var data = PackageApi.Packer.Unpack<SpringHingePackable>(bytes);
			comp.HingeAxis = data.HingeAxis;
			comp.CentreOfMass = data.CentreOfMass;
			comp.ToyMass = data.ToyMass;
			comp.OverrideInertia = data.OverrideInertia;
			comp.ManualInertia = data.ManualInertia;
			comp.MassBoxHalfExtents = data.MassBoxHalfExtents;
			comp.SpringStiffness = data.SpringStiffness;
			comp.SpringDamping = data.SpringDamping;
			comp.EquilibriumAngle = data.EquilibriumAngle;
			comp.MinimumAngle = data.MinimumAngle;
			comp.MaximumAngle = data.MaximumAngle;
			comp.InitialAngle = data.InitialAngle;
			comp.EnableAngleSwitch = data.EnableAngleSwitch;
			comp.SwitchCloseAngle = data.SwitchCloseAngle;
			comp.SwitchOpenAngle = data.SwitchOpenAngle;
		}
	}

	public struct SpringHingeColliderPackable
	{
		private const int CurrentVersion = 2;

		public int Version;
		public PackableFloat3 LocalCentre;
		public PackableFloat3 LocalRotation;
		public PackableFloat3 HalfExtents;
		public bool ShowColliderMesh;
		public float Elasticity;
		public float ElasticityFalloff;
		public float Friction;
		public bool HitEvent;
		public float HitThreshold;
		public bool OverwritePhysics;

		public static byte[] Pack(SpringHingeColliderComponent comp)
		{
			return PackageApi.Packer.Pack(new SpringHingeColliderPackable {
				Version = CurrentVersion,
				LocalCentre = comp.LocalCentre,
				LocalRotation = comp.LocalRotation,
				HalfExtents = comp.HalfExtents,
				ShowColliderMesh = comp.ShowColliderMesh,
				Elasticity = comp.Elasticity,
				ElasticityFalloff = comp.ElasticityFalloff,
				Friction = comp.Friction,
				HitEvent = comp.HitEvent,
				HitThreshold = comp.HitThreshold,
				OverwritePhysics = comp.OverwritePhysics
			});
		}

		public static void Unpack(byte[] bytes, SpringHingeColliderComponent comp)
		{
			var data = PackageApi.Packer.Unpack<SpringHingeColliderPackable>(bytes);
			comp.LocalCentre = data.LocalCentre;
			comp.LocalRotation = data.LocalRotation;
			comp.HalfExtents = data.HalfExtents;
			comp.ShowColliderMesh = data.ShowColliderMesh;
			comp.Elasticity = data.Elasticity;
			comp.ElasticityFalloff = data.ElasticityFalloff;
			comp.Friction = data.Friction;
			comp.HitEvent = data.HitEvent;
			comp.HitThreshold = data.HitThreshold;
			comp.OverwritePhysics = data.OverwritePhysics;
		}
	}

	public struct SpringHingeColliderReferencesPackable
	{
		public PhysicalMaterialPackable PhysicalMaterial;

		public static byte[] PackReferences(SpringHingeColliderComponent comp, PackagedFiles files)
		{
			return PackageApi.Packer.Pack(new SpringHingeColliderReferencesPackable {
				PhysicalMaterial = new PhysicalMaterialPackable {
					Elasticity = comp.Elasticity,
					ElasticityFalloff = comp.ElasticityFalloff,
					Friction = comp.Friction,
					Scatter = 0f,
					Overwrite = comp.OverwritePhysics,
					AssetRef = files.AddAsset(comp.PhysicsMaterial)
				}
			});
		}

		public static void Unpack(byte[] bytes, SpringHingeColliderComponent comp,
			PackagedFiles files)
		{
			var data = PackageApi.Packer.Unpack<SpringHingeColliderReferencesPackable>(bytes);
			var material = data.PhysicalMaterial;
			comp.Elasticity = material.Elasticity;
			comp.ElasticityFalloff = material.ElasticityFalloff;
			comp.Friction = material.Friction;
			comp.OverwritePhysics = material.Overwrite;
			comp.PhysicsMaterial = files.GetAsset<PhysicsMaterialAsset>(material.AssetRef);
		}
	}

	public struct SpringHingeAnimationPackable
	{
		private const int CurrentVersion = 1;

		public int Version;
		public PackableFloat3 RotationAxis;

		public static byte[] Pack(SpringHingeAnimationComponent comp)
		{
			return PackageApi.Packer.Pack(new SpringHingeAnimationPackable {
				Version = CurrentVersion,
				RotationAxis = comp.RotationAxis
			});
		}

		public static void Unpack(byte[] bytes, SpringHingeAnimationComponent comp)
		{
			var data = PackageApi.Packer.Unpack<SpringHingeAnimationPackable>(bytes);
			comp.RotationAxis = data.RotationAxis;
		}
	}

	public struct SpringHingeAnimationReferencesPackable
	{
		public ReferencePackable EmitterRef;

		public static byte[] Pack(SpringHingeAnimationComponent comp, PackagedRefs refs)
		{
			var emitterRef = new ReferencePackable(null, null);
			if (comp._emitter != null) {
				if (refs.HasType(comp._emitter.GetType())) {
					emitterRef = refs.PackReference(comp._emitter);
				} else {
					Debug.LogWarning($"Cannot package spring-hinge animation emitter {comp._emitter.GetType().FullName} on '{comp.name}' because it has no PackAs attribute; writing a null reference.", comp);
				}
			}
			return PackageApi.Packer.Pack(new SpringHingeAnimationReferencesPackable {
				EmitterRef = emitterRef
			});
		}

		public static void Unpack(byte[] bytes, SpringHingeAnimationComponent comp, PackagedRefs refs)
		{
			var data = PackageApi.Packer.Unpack<SpringHingeAnimationReferencesPackable>(bytes);
			comp._emitter = refs.Resolve<MonoBehaviour, IAnimationValueEmitter<float>>(data.EmitterRef);
		}
	}
}
