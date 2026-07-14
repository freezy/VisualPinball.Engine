// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	public struct RubberPackable
	{
		public int Thickness;
		public IEnumerable<DragPointPackable> DragPoints;
		public int PathSource;
		public RubberGuideBindingPackable[] GuideBindings;
		public RubberPathElementPackable[] BakedPath;
		public uint BakeVersion;
		public string BakeInputHash;
		public PackableMatrix4x4 BakeFrameToLocal;
		public float RestLength;

		public static byte[] Pack(RubberComponent comp)
		{
			return PackageApi.Packer.Pack(new RubberPackable {
				Thickness = comp.Thickness,
				DragPoints = comp.DragPoints.Select(DragPointPackable.From),
				PathSource = (int)comp.PathSource,
				GuideBindings = comp.GuideBindings.Select(RubberGuideBindingPackable.From).ToArray(),
				BakedPath = comp.BakedPath.Select(RubberPathElementPackable.From).ToArray(),
				BakeVersion = comp.BakeVersion,
				BakeInputHash = comp.BakeInputHash.isValid ? comp.BakeInputHash.ToString() : null,
				BakeFrameToLocal = comp.BakeFrameToLocal,
				RestLength = comp.RestLength,
			});
		}

		public static void Unpack(byte[] bytes, RubberComponent comp)
		{
			var data = PackageApi.Packer.Unpack<RubberPackable>(bytes);
			comp._thickness = data.Thickness;
			comp.DragPoints = data.DragPoints?.Select(c => c.ToDragPoint()).ToArray()
				?? Array.Empty<VisualPinball.Engine.Math.DragPointData>();

			var hash = default(Hash128);
			if (!string.IsNullOrEmpty(data.BakeInputHash)) {
				if (data.BakeInputHash.Length != 32
					|| data.BakeInputHash.Any(character => !Uri.IsHexDigit(character))) {
					throw new InvalidDataException($"Invalid rubber bake hash '{data.BakeInputHash}'.");
				}
				try {
					hash = Hash128.Parse(data.BakeInputHash);
				} catch (Exception exception) when (exception is ArgumentException
					or FormatException) {
					throw new InvalidDataException($"Invalid rubber bake hash '{data.BakeInputHash}'.", exception);
				}
			}
			var pathSource = Enum.IsDefined(typeof(RubberPathSource), data.PathSource)
				? (RubberPathSource)data.PathSource
				: RubberPathSource.Spline;
			comp.RestorePackedState(
				pathSource,
				data.GuideBindings?.Select(binding => binding.ToBinding()).ToArray()
					?? Array.Empty<RubberGuideBinding>(),
				data.BakedPath?.Select(element => element.ToElement()).ToArray()
					?? Array.Empty<RubberPathElement>(),
				data.BakeVersion,
				hash,
				data.BakeFrameToLocal.IsZero ? Matrix4x4.identity : data.BakeFrameToLocal,
				data.RestLength
			);
		}
	}

	public struct RubberReferencesPackable
	{
		public ReferencePackable[] Guides;

		public static byte[] Pack(RubberComponent comp, PackagedRefs refs)
		{
			return PackageApi.Packer.Pack(new RubberReferencesPackable {
				Guides = comp.GuideBindings.Select(binding => refs.PackReference(binding.Guide)).ToArray(),
			});
		}

		public static void Unpack(byte[] bytes, RubberComponent comp, PackagedRefs refs)
		{
			if (bytes == null || bytes.Length == 0) {
				return;
			}
			var data = PackageApi.Packer.Unpack<RubberReferencesPackable>(bytes);
			var packedGuides = data.Guides ?? Array.Empty<ReferencePackable>();
			if (packedGuides.Length != comp.GuideBindings.Count) {
				Debug.LogError($"Rubber '{comp.name}' has {comp.GuideBindings.Count} bindings but {packedGuides.Length} guide references.");
				return;
			}
			comp.RestoreGuideReferences(packedGuides.Select(refs.Resolve<RubberGuideComponent>).ToArray());
		}
	}

	public struct RubberGuideBindingPackable
	{
		public ulong SlotIdA;
		public ulong SlotIdB;

		public static RubberGuideBindingPackable From(RubberGuideBinding binding)
			=> new() { SlotIdA = binding.SlotId.A, SlotIdB = binding.SlotId.B };

		public RubberGuideBinding ToBinding()
			=> new(null, new SerializedGuid(SlotIdA, SlotIdB));
	}

	public struct RubberPathElementPackable
	{
		public int Type;
		public PackableFloat2 Start;
		public PackableFloat2 End;
		public PackableFloat2 Center;
		public float Radius;
		public float StartAngleRad;
		public float SweepAngleRad;
		public int StartBindingIndex;
		public int EndBindingIndex;
		public float StartDistance;
		public float Length;

		public static RubberPathElementPackable From(RubberPathElement element)
		{
			return new RubberPathElementPackable {
				Type = (int)element.Type,
				Start = element.Start,
				End = element.End,
				Center = element.Center,
				Radius = element.Radius,
				StartAngleRad = element.StartAngleRad,
				SweepAngleRad = element.SweepAngleRad,
				StartBindingIndex = element.StartBindingIndex,
				EndBindingIndex = element.EndBindingIndex,
				StartDistance = element.StartDistance,
				Length = element.Length,
			};
		}

		public RubberPathElement ToElement()
		{
			return new RubberPathElement {
				Type = (RubberPathElementType)Type,
				Start = Start,
				End = End,
				Center = Center,
				Radius = Radius,
				StartAngleRad = StartAngleRad,
				SweepAngleRad = SweepAngleRad,
				StartBindingIndex = StartBindingIndex,
				EndBindingIndex = EndBindingIndex,
				StartDistance = StartDistance,
				Length = Length,
			};
		}
	}

	public struct PackableMatrix4x4
	{
		public float M00, M01, M02, M03;
		public float M10, M11, M12, M13;
		public float M20, M21, M22, M23;
		public float M30, M31, M32, M33;

		public bool IsZero => M00 == 0f && M01 == 0f && M02 == 0f && M03 == 0f
			&& M10 == 0f && M11 == 0f && M12 == 0f && M13 == 0f
			&& M20 == 0f && M21 == 0f && M22 == 0f && M23 == 0f
			&& M30 == 0f && M31 == 0f && M32 == 0f && M33 == 0f;

		public static implicit operator PackableMatrix4x4(Matrix4x4 matrix)
		{
			return new PackableMatrix4x4 {
				M00 = matrix.m00, M01 = matrix.m01, M02 = matrix.m02, M03 = matrix.m03,
				M10 = matrix.m10, M11 = matrix.m11, M12 = matrix.m12, M13 = matrix.m13,
				M20 = matrix.m20, M21 = matrix.m21, M22 = matrix.m22, M23 = matrix.m23,
				M30 = matrix.m30, M31 = matrix.m31, M32 = matrix.m32, M33 = matrix.m33,
			};
		}

		public static implicit operator Matrix4x4(PackableMatrix4x4 matrix)
		{
			var result = new Matrix4x4();
			result.SetRow(0, new Vector4(matrix.M00, matrix.M01, matrix.M02, matrix.M03));
			result.SetRow(1, new Vector4(matrix.M10, matrix.M11, matrix.M12, matrix.M13));
			result.SetRow(2, new Vector4(matrix.M20, matrix.M21, matrix.M22, matrix.M23));
			result.SetRow(3, new Vector4(matrix.M30, matrix.M31, matrix.M32, matrix.M33));
			return result;
		}
	}

	public struct RubberColliderPackable
	{
		public bool IsMovable;
		public bool HitEvent;
		public float ZOffset;
		public int Mode;

		public static byte[] Pack(RubberColliderComponent comp)
		{
			return PackageApi.Packer.Pack(new RubberColliderPackable {
				IsMovable = comp._isKinematic,
				HitEvent = comp.HitEvent,
				ZOffset = comp.ZOffset,
				Mode = (int)comp.Mode,
			});
		}

		public static void Unpack(byte[] bytes, RubberColliderComponent comp)
		{
			var data = PackageApi.Packer.Unpack<RubberColliderPackable>(bytes);
			comp._isKinematic = data.IsMovable;
			comp.HitEvent = data.HitEvent;
			comp.ZOffset = data.ZOffset;
			comp.Mode = Enum.IsDefined(typeof(RubberColliderMode), data.Mode)
				? (RubberColliderMode)data.Mode
				: RubberColliderMode.Legacy;
		}
	}

	public struct RubberColliderReferencesPackable
	{
		public float Elasticity;
		public float ElasticityFalloff;
		public float Friction;
		public float Scatter;
		public bool Overwrite;
		public int AssetRef;
		public int RubberPhysicsMaterialRef;

		public static byte[] Pack(RubberColliderComponent comp, PackagedFiles files)
		{
			return PackageApi.Packer.Pack(new RubberColliderReferencesPackable {
				Elasticity = comp.PhysicsElasticity,
				ElasticityFalloff = comp.PhysicsElasticityFalloff,
				Friction = comp.PhysicsFriction,
				Scatter = comp.PhysicsScatter,
				Overwrite = comp.PhysicsOverwrite,
				AssetRef = files.AddAsset(comp.PhysicsMaterialReference),
				RubberPhysicsMaterialRef = files.AddAsset(comp.RubberPhysicsMaterial),
			});
		}

		public static void Unpack(byte[] bytes, RubberColliderComponent comp, PackagedFiles files)
		{
			if (bytes == null || bytes.Length == 0) {
				return;
			}
			var data = PackageApi.Packer.Unpack<RubberColliderReferencesPackable>(bytes);
			comp.PhysicsElasticity = data.Elasticity;
			comp.PhysicsElasticityFalloff = data.ElasticityFalloff;
			comp.PhysicsFriction = data.Friction;
			comp.PhysicsScatter = data.Scatter;
			comp.PhysicsOverwrite = data.Overwrite;
			comp.PhysicsMaterialReference = files.GetAsset<PhysicsMaterialAsset>(data.AssetRef);
			comp.RubberPhysicsMaterial = files.GetAsset<RubberPhysicsMaterialAsset>(data.RubberPhysicsMaterialRef);
		}
	}
}
