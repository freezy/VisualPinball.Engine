// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Unity.Editor;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class RubberGuideTests
	{
		[Test]
		public void ShouldValueCompareAndRepairSlotIds()
		{
			var go = new GameObject("Guide");
			try {
				var guide = go.AddComponent<RubberGuideComponent>();
				var id = new SerializedGuid(10, 20);
				guide.Slots = new[] {
					CreateSlot(id, "First", 12f),
					CreateSlot(id, "Duplicate", 13f),
					CreateSlot(default, "Empty", 14f),
				};

				Assert.That(guide.ValidateSlots(), Has.Count.EqualTo(2));
				Assert.That(guide.RepairDuplicateIds(), Is.EqualTo(2));
				Assert.That(guide.ValidateSlots(), Is.Empty);
				Assert.That(guide.Slots[0].Id, Is.EqualTo(id));
				Assert.That(guide.Slots.Select(slot => slot.Id).Distinct().Count(), Is.EqualTo(3));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldRoundTripGuideSlots()
		{
			var sourceObject = new GameObject("Source Guide");
			var targetObject = new GameObject("Target Guide");
			try {
				var source = sourceObject.AddComponent<RubberGuideComponent>();
				source.Slots = new[] {
					new RubberGuideSlot {
						Id = new SerializedGuid(1, 2),
						DisplayName = "Upper",
						LocalHeight = 0.12f,
						Profile = new RubberGuideProfile {
							Type = RubberGuideProfileType.Circle,
							LocalCenter = new float2(0.1f, -0.2f),
							Radius = 0.04f,
							ConvexHull = new[] { new float2(1f, 2f) },
						},
						RubberSupportFriction = 0.25f,
					},
				};

				var target = targetObject.AddComponent<RubberGuideComponent>();
				target.Unpack(source.Pack());

				Assert.That(target.Slots, Has.Length.EqualTo(1));
				Assert.That(target.Slots[0].Id, Is.EqualTo(source.Slots[0].Id));
				Assert.That(target.Slots[0].DisplayName, Is.EqualTo("Upper"));
				Assert.That(target.Slots[0].Profile.LocalCenter, Is.EqualTo(new float2(0.1f, -0.2f)));
				Assert.That(target.Slots[0].Profile.ConvexHull, Is.EqualTo(new[] { new float2(1f, 2f) }));
			} finally {
				Object.DestroyImmediate(sourceObject);
				Object.DestroyImmediate(targetObject);
			}
		}

		[Test]
		public void ShouldDefaultOldRubberPayloadsToSplineAndLegacy()
		{
			var go = new GameObject("Rubber");
			try {
				var rubber = go.AddComponent<RubberComponent>();
				var oldBytes = PackageApi.Packer.Pack(new LegacyRubberPackable {
					Thickness = 9,
					DragPoints = new[] { DragPointPackable.From(new DragPointData(10f, 20f)) },
				});

				rubber.Unpack(oldBytes);

				Assert.That(rubber.PathSource, Is.EqualTo(RubberPathSource.Spline));
				Assert.That(rubber.GuideBindings, Is.Empty);
				Assert.That(rubber.BakedPath, Is.Empty);
				Assert.That(rubber.RestLength, Is.Zero);
				Assert.That(rubber.Thickness, Is.EqualTo(9));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldRoundTripGuidedRubberScalarData()
		{
			var sourceObject = new GameObject("Source Rubber");
			var targetObject = new GameObject("Target Rubber");
			var guideObject = new GameObject("Guide");
			try {
				var slot = CreateSlot(new SerializedGuid(4, 5), "Slot", 0.05f);
				var guide = guideObject.AddComponent<RubberGuideComponent>();
				guide.Slots = new[] { slot };

				var source = sourceObject.AddComponent<RubberComponent>();
				source.DragPoints = SquareDragPoints();
				source.SetGuideBindings(new[] { new RubberGuideBinding(guide, slot.Id) });
				source.RestLength = 250f;
				var hash = new Hash128(1, 2, 3, 4);
				var path = new[] {
					new RubberPathElement {
						Type = RubberPathElementType.SupportedArc,
						Start = new float2(1f, 2f),
						End = new float2(3f, 4f),
						Center = new float2(2f, 3f),
						Radius = 10f,
						SweepAngleRad = math.PI,
						StartBindingIndex = 0,
						EndBindingIndex = 0,
						Length = 31.4f,
					},
				};
				var frame = Matrix4x4.Translate(new Vector3(1f, 2f, 3f));
				source.ApplyGuidedBake(path, null, frame, hash, 7);

				var target = targetObject.AddComponent<RubberComponent>();
				target.Unpack(source.Pack());

				Assert.That(target.PathSource, Is.EqualTo(RubberPathSource.Guides));
				Assert.That(target.GuideBindings.Count, Is.EqualTo(1));
				Assert.That(target.GuideBindings[0].Guide, Is.Null);
				Assert.That(target.GuideBindings[0].SlotId, Is.EqualTo(slot.Id));
				Assert.That(target.BakedPath.Count, Is.EqualTo(1));
				Assert.That(target.BakedPath[0].SweepAngleRad, Is.EqualTo(math.PI));
				Assert.That(target.BakeVersion, Is.EqualTo(7));
				Assert.That(target.BakeInputHash, Is.EqualTo(hash));
				Assert.That(target.BakeFrameToLocal, Is.EqualTo(frame));
				Assert.That(target.RestLength, Is.EqualTo(250f));
			} finally {
				Object.DestroyImmediate(sourceObject);
				Object.DestroyImmediate(targetObject);
				Object.DestroyImmediate(guideObject);
			}
		}

		[Test]
		public void ShouldRoundTripEveryMaterialCurveField()
		{
			var source = ScriptableObject.CreateInstance<RubberPhysicsMaterialAsset>();
			var target = ScriptableObject.CreateInstance<RubberPhysicsMaterialAsset>();
			try {
				var key = new Keyframe(1.25f, 3.5f, -2f, 4f, 0.15f, 0.35f) {
					weightedMode = WeightedMode.Both,
				};
				source.NominalStressMpaByStretchRatio = new AnimationCurve(key) {
					preWrapMode = WrapMode.PingPong,
					postWrapMode = WrapMode.Loop,
				};
				var packer = new RubberPhysicsMaterialPacker();
				var meta = packer.Pack(42, source, null);
				var bytes = MetaPackable.PackMeta(meta);
				packer.Unpack(bytes, target, null);

				var actual = target.NominalStressMpaByStretchRatio;
				Assert.That(actual.preWrapMode, Is.EqualTo(WrapMode.PingPong));
				Assert.That(actual.postWrapMode, Is.EqualTo(WrapMode.Loop));
				Assert.That(actual.keys, Has.Length.EqualTo(1));
				Assert.That(actual.keys[0].time, Is.EqualTo(key.time));
				Assert.That(actual.keys[0].value, Is.EqualTo(key.value));
				Assert.That(actual.keys[0].inTangent, Is.EqualTo(key.inTangent));
				Assert.That(actual.keys[0].outTangent, Is.EqualTo(key.outTangent));
				Assert.That(actual.keys[0].inWeight, Is.EqualTo(key.inWeight));
				Assert.That(actual.keys[0].outWeight, Is.EqualTo(key.outWeight));
				Assert.That(actual.keys[0].weightedMode, Is.EqualTo(key.weightedMode));
			} finally {
				Object.DestroyImmediate(source);
				Object.DestroyImmediate(target);
			}
		}

		[UnityTest]
		public IEnumerator ShouldRoundTripGuideAndMaterialReferencesThroughPackage()
		{
			var package = Path.Combine(Path.GetTempPath(), $"vpe-rubber-guide-{Guid.NewGuid():N}.vpe");
			GameObject source = null;
			GameObject imported = null;
			RubberPhysicsMaterialAsset material = null;
			try {
				source = new GameObject("Table");
				source.AddComponent<TableComponent>();

				var guideObject = new GameObject("Guide");
				guideObject.transform.SetParent(source.transform, false);
				var guide = guideObject.AddComponent<RubberGuideComponent>();
				var slot = CreateSlot(new SerializedGuid(50, 60), "Groove", 0.05f);
				guide.Slots = new[] { slot };

				var rubberObject = new GameObject("Rubber");
				rubberObject.transform.SetParent(source.transform, false);
				var rubber = rubberObject.AddComponent<RubberComponent>();
				rubber.DragPoints = SquareDragPoints();
				rubber.SetGuideBindings(new[] { new RubberGuideBinding(guide, slot.Id) });
				rubber.ApplyGuidedBake(new[] {
					new RubberPathElement {
						Type = RubberPathElementType.SupportedArc,
						Start = new float2(10f, 0f),
						End = new float2(10f, 0f),
						Center = float2.zero,
						Radius = 10f,
						SweepAngleRad = 2f * math.PI,
						StartBindingIndex = 0,
						EndBindingIndex = 0,
						Length = 20f * math.PI,
					},
				}, null, Matrix4x4.identity, new Hash128(9, 8, 7, 6), 1);

				var collider = rubberObject.AddComponent<RubberColliderComponent>();
				collider.Mode = RubberColliderMode.Physical;
				material = ScriptableObject.CreateInstance<RubberPhysicsMaterialAsset>();
				material.name = "Natural Rubber";
				material.DensityKgPerCubicMeter = 1075f;
				material.NominalStressMpaByStretchRatio = new AnimationCurve(
					new Keyframe(1f, 0f), new Keyframe(1.5f, 1.2f));
				collider.RubberPhysicsMaterial = material;

				new PackageWriter(source).WritePackageSync(package);
				var task = new RuntimePackageReader(package).ImportIntoScene();
				while (!task.IsCompleted) {
					yield return null;
				}
				if (task.IsFaulted) {
					throw task.Exception!.GetBaseException();
				}
				imported = task.Result;

				var importedRubber = imported.GetComponentInChildren<RubberComponent>();
				var importedGuide = imported.GetComponentInChildren<RubberGuideComponent>();
				var importedCollider = importedRubber.GetComponent<RubberColliderComponent>();
				Assert.That(importedRubber.GuideBindings[0].Guide, Is.SameAs(importedGuide));
				Assert.That(importedRubber.HasValidGuidedPath, Is.True);
				Assert.That(importedCollider.Mode, Is.EqualTo(RubberColliderMode.Physical));
				Assert.That(importedCollider.RubberPhysicsMaterial, Is.Not.Null);
				Assert.That(importedCollider.RubberPhysicsMaterial.DensityKgPerCubicMeter, Is.EqualTo(1075f));
				Assert.That(importedCollider.RubberPhysicsMaterial.NominalStressMpaByStretchRatio.keys,
					Has.Length.EqualTo(2));
			} finally {
				if (source) Object.DestroyImmediate(source);
				if (imported) Object.DestroyImmediate(imported);
				if (material) Object.DestroyImmediate(material);
				File.Delete(package);
			}
		}

		private static RubberGuideSlot CreateSlot(SerializedGuid id, string name, float radius)
		{
			return new RubberGuideSlot {
				Id = id,
				DisplayName = name,
				Profile = RubberGuideProfile.Circle(radius),
			};
		}

		private static DragPointData[] SquareDragPoints()
		{
			return new[] {
				new DragPointData(-10f, -10f),
				new DragPointData(-10f, 10f),
				new DragPointData(10f, 10f),
				new DragPointData(10f, -10f),
			};
		}

		private struct LegacyRubberPackable
		{
			public int Thickness;
			public DragPointPackable[] DragPoints;
		}
	}
}
