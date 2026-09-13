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
		public void ShouldCreateAndRestoreDefaultSlot()
		{
			var go = new GameObject("Guide");
			try {
				var guide = go.AddComponent<RubberGuideComponent>();
				Assert.That(guide.Slots, Has.Length.EqualTo(1));
				Assert.That(guide.Slots[0].DisplayName, Is.EqualTo("Default"));
				Assert.That(guide.Slots[0].Id.IsEmpty, Is.False);
				Assert.That(guide.Slots[0].Profile.Type, Is.EqualTo(RubberGuideProfileType.Circle));
				Assert.That(guide.Slots[0].Profile.Radius,
					Is.EqualTo(RubberGuideComponent.DefaultSlotRadius));

				guide.Slots = Array.Empty<RubberGuideSlot>();
				Assert.That(guide.EnsureDefaultSlot(), Is.True);
				var restoredId = guide.Slots[0].Id;
				Assert.That(guide.EnsureDefaultSlot(), Is.False);
				Assert.That(guide.Slots, Has.Length.EqualTo(1));
				Assert.That(guide.Slots[0].Id, Is.EqualTo(restoredId));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldPropagateNamedSlotAndReuseZeroHeightPlaceholders()
		{
			var objects = Enumerable.Range(0, 5)
				.Select(index => new GameObject($"Guide {index}"))
				.ToArray();
			try {
				var guides = objects.Select(gameObject => gameObject.AddComponent<RubberGuideComponent>())
					.ToArray();
				guides[0].Slots = new[] {
					CreateSlot(new SerializedGuid(1, 1), "Upper", 0.011f, 0.02f),
					CreateSlot(new SerializedGuid(1, 2), "Lower", 0.011f, 0.01f),
				};
				guides[1].Slots = new[] {
					CreateSlot(new SerializedGuid(2, 1), "Upper", 0.012f, 0.04f),
				};
				var zeroNamedSlotId = new SerializedGuid(3, 1);
				guides[2].Slots = new[] {
					CreateSlot(zeroNamedSlotId, "Upper", 0.013f, 0f),
				};
				var placeholderId = new SerializedGuid(4, 1);
				guides[3].Slots = new[] {
					CreateSlot(placeholderId, "Default", 0.014f, 0f),
				};
				guides[4].Slots = new[] {
					CreateSlot(new SerializedGuid(5, 1), "Other", 0.015f, 0.03f),
				};

				Assert.That(RubberGuideCommands.GetPropagatableSlotNames(guides),
					Is.EqualTo(new[] { "Upper", "Lower", "Other" }));
				Assert.That(RubberGuideCommands.TryPropagateSlot(guides, "Upper",
					"Test Slot Propagation", out var bindings, out var error), Is.True, error);

				Assert.That(bindings, Has.Length.EqualTo(guides.Length));
				var expectedPropagatedHeights = new[] { 0.02f, 0.04f, 0.03f, 0.03f, 0.03f };
				for (var i = 0; i < guides.Length; i++) {
					Assert.That(guides[i].TryGetSlot(bindings[i].SlotId, out var slot), Is.True);
					Assert.That(slot.DisplayName, Is.EqualTo("Upper"));
					Assert.That(slot.LocalHeight,
						Is.EqualTo(expectedPropagatedHeights[i]).Within(1e-6f));
				}
				Assert.That(guides[2].Slots, Has.Length.EqualTo(1));
				Assert.That(guides[2].Slots[0].Id, Is.EqualTo(zeroNamedSlotId));
				Assert.That(guides[2].Slots[0].Profile.Radius, Is.EqualTo(0.013f));
				Assert.That(guides[3].Slots, Has.Length.EqualTo(1));
				Assert.That(guides[3].Slots[0].Id, Is.EqualTo(placeholderId));
				Assert.That(guides[3].Slots[0].Profile.Radius, Is.EqualTo(0.014f));
				Assert.That(guides[4].Slots, Has.Length.EqualTo(2));
				Assert.That(guides[4].Slots[0].DisplayName, Is.EqualTo("Other"));

				Assert.That(RubberGuideCommands.TrySetSlotRadius(guides, "Upper",
					0.02f, out error), Is.True, error);
				Assert.That(RubberGuideCommands.TrySetSlotSupportFriction(guides, "Upper",
					0.65f, out error), Is.True, error);
				for (var i = 0; i < guides.Length; i++) {
					var upper = guides[i].Slots.Single(slot => slot.DisplayName == "Upper"
						&& slot.LocalHeight != 0f);
					Assert.That(upper.LocalHeight,
						Is.EqualTo(expectedPropagatedHeights[i]).Within(1e-6f));
				}
				Assert.That(RubberGuideCommands.TrySetSlotLocalHeight(guides, "Upper",
					0.05f, out error), Is.True, error);
				Assert.That(RubberGuideCommands.TrySetSlotLocalCenter(guides, "Upper",
					new Vector2(0.004f, -0.006f), out error), Is.True, error);
				foreach (var guide in guides) {
					var upper = guide.Slots.Single(slot => slot.DisplayName == "Upper"
						&& slot.LocalHeight != 0f);
					Assert.That(upper.LocalHeight, Is.EqualTo(0.05f));
					Assert.That(upper.Profile.LocalCenter, Is.EqualTo(new float2(0.004f, -0.006f)));
					Assert.That(upper.Profile.Radius, Is.EqualTo(0.02f));
					Assert.That(upper.RubberSupportFriction, Is.EqualTo(0.65f));
				}
				Assert.That(guides[4].Slots[0].Profile.Radius, Is.EqualTo(0.015f));
				Assert.That(guides[4].Slots[0].RubberSupportFriction, Is.Zero);
			} finally {
				foreach (var gameObject in objects) {
					Object.DestroyImmediate(gameObject);
				}
			}
		}

		[Test]
		public void ShouldCreateGuidedRubberWithRenderedMesh()
		{
			var root = new GameObject("Table");
			var previousSelection = UnityEditor.Selection.activeObject;
			try {
				var guideAObject = new GameObject("Guide A");
				guideAObject.transform.SetParent(root.transform, false);
				var guideBObject = new GameObject("Guide B");
				guideBObject.transform.SetParent(root.transform, false);
				guideBObject.transform.localPosition = new Vector3(0.1f, 0f, 0f);
				var guideA = guideAObject.AddComponent<RubberGuideComponent>();
				var guideB = guideBObject.AddComponent<RubberGuideComponent>();
				guideA.Slots = new[] {
					CreateSlot(new SerializedGuid(1, 1), "Upper", 0.01f, 0.02f),
				};
				guideB.Slots = new[] {
					CreateSlot(new SerializedGuid(2, 1), "Upper", 0.012f, 0.02f),
				};

				RubberGuideCommands.CreateRubberAroundGuides(new[] { guideA, guideB }, "Upper");

				var rubber = root.GetComponentsInChildren<RubberComponent>().Single();
				var renderer = rubber.GetComponent<MeshRenderer>();
				var meshFilter = rubber.GetComponent<MeshFilter>();
				Assert.That(renderer, Is.Not.Null);
				Assert.That(renderer.enabled, Is.True);
				Assert.That(renderer.sharedMaterial, Is.Not.Null);
				Assert.That(meshFilter.sharedMesh, Is.Not.Null);
				Assert.That(meshFilter.sharedMesh.vertexCount, Is.GreaterThan(0));
			} finally {
				UnityEditor.Selection.activeObject = previousSelection;
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void ShouldApplySlotOptionsToOneGuideWithoutChangingItsHeight()
		{
			var gameObject = new GameObject("Guide");
			try {
				var guide = gameObject.AddComponent<RubberGuideComponent>();
				guide.Slots = new[] {
					CreateSlot(new SerializedGuid(1, 1), "Upper", 0.011f, 0.027f),
				};

				Assert.That(RubberGuideCommands.TrySetSlotRadius(new[] { guide }, "Upper",
					0.02f, out var error), Is.True, error);
				Assert.That(RubberGuideCommands.TrySetSlotSupportFriction(new[] { guide },
					"Upper", 0.4f, out error), Is.True, error);

				Assert.That(guide.Slots[0].LocalHeight, Is.EqualTo(0.027f));
				Assert.That(guide.Slots[0].Profile.Radius, Is.EqualTo(0.02f));
				Assert.That(guide.Slots[0].RubberSupportFriction, Is.EqualTo(0.4f));
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void ShouldRejectAZeroAverageWhenPropagatingSignedSlotHeights()
		{
			var objects = Enumerable.Range(0, 3)
				.Select(index => new GameObject($"Guide {index}"))
				.ToArray();
			try {
				var guides = objects.Select(gameObject => gameObject.AddComponent<RubberGuideComponent>())
					.ToArray();
				guides[0].Slots = new[] {
					CreateSlot(new SerializedGuid(1, 1), "Contact", 0.01f, 0.02f),
				};
				guides[1].Slots = new[] {
					CreateSlot(new SerializedGuid(2, 1), "Contact", 0.01f, -0.02f),
				};
				var placeholder = CreateSlot(new SerializedGuid(3, 1), "Default", 0.01f, 0f);
				guides[2].Slots = new[] { placeholder };

				Assert.That(RubberGuideCommands.TryPropagateSlot(guides, "Contact",
					"Test Zero Average", out _, out var error), Is.False);
				Assert.That(error, Does.Contain("zero average height"));
				Assert.That(guides[2].Slots[0].DisplayName, Is.EqualTo("Default"));
				Assert.That(guides[2].Slots[0].LocalHeight, Is.Zero);
			} finally {
				foreach (var gameObject in objects) {
					Object.DestroyImmediate(gameObject);
				}
			}
		}

		[UnityTest]
		public IEnumerator ContinuousGuideAndRadiusEditsShouldRebuildLive()
		{
			var root = new GameObject("Table");
			try {
				var guideAObject = new GameObject("Guide A");
				guideAObject.transform.SetParent(root.transform, false);
				var guideBObject = new GameObject("Guide B");
				guideBObject.transform.SetParent(root.transform, false);
				guideBObject.transform.localPosition = new Vector3(0.1f, 0f, 0f);
				var guideA = guideAObject.AddComponent<RubberGuideComponent>();
				var guideB = guideBObject.AddComponent<RubberGuideComponent>();
				guideA.Slots = new[] {
					CreateSlot(new SerializedGuid(1, 1), "Upper", 0.01f, 0.02f),
				};
				guideB.Slots = new[] {
					CreateSlot(new SerializedGuid(2, 1), "Upper", 0.012f, 0.02f),
				};
				var rubberObject = new GameObject("Rubber");
				rubberObject.transform.SetParent(root.transform, false);
				rubberObject.transform.localPosition = new Vector3(0f, 0.02f, 0f);
				var rubber = rubberObject.AddComponent<RubberComponent>();
				rubber.SetGuideBindings(new[] {
					new RubberGuideBinding(guideA, guideA.Slots[0].Id),
					new RubberGuideBinding(guideB, guideB.Slots[0].Id),
				});
				Assert.That(RubberAutofit.TryBake(rubber, out _, out var bakeError),
					Is.True, bakeError);
				var previousRadius = rubber.BakedPath
					.Where(element => element.Type == RubberPathElementType.SupportedArc)
					.Max(element => element.Radius);

				for (var edit = 0; edit < 4; edit++) {
					var previousHash = rubber.BakeInputHash;
					var radius = 0.014f + edit * 0.002f;
					Assert.That(RubberGuideCommands.TrySetSlotRadius(new[] { guideA, guideB },
						"Upper", radius, out var editError), Is.True, editError);
					for (var frame = 0; frame < 3 && rubber.BakeInputHash == previousHash; frame++) {
						yield return null;
					}
					Assert.That(rubber.BakeInputHash, Is.Not.EqualTo(previousHash),
						$"Edit {edit + 1} was not previewed while input remained active.");
					Assert.That(RubberAutofit.GetStatus(rubber).IsValid, Is.True);
				}

				Assert.That(rubber.BakedPath
					.Where(element => element.Type == RubberPathElementType.SupportedArc)
					.Max(element => element.Radius), Is.GreaterThan(previousRadius));

				for (var move = 0; move < 4; move++) {
					var previousHash = rubber.BakeInputHash;
					UnityEditor.Undo.RecordObject(guideBObject.transform, "Move Rubber Guide");
					guideBObject.transform.localPosition += new Vector3(0.005f, 0f, 0f);
					for (var frame = 0; frame < 3 && rubber.BakeInputHash == previousHash; frame++) {
						yield return null;
					}
					Assert.That(rubber.BakeInputHash, Is.Not.EqualTo(previousHash),
						$"Move {move + 1} was not previewed while the guide remained active.");
					Assert.That(RubberAutofit.GetStatus(rubber).IsValid, Is.True);
				}

				for (var edit = 0; edit < 4; edit++) {
					var previousHash = rubber.BakeInputHash;
					var serializedRubber = new UnityEditor.SerializedObject(rubber);
					serializedRubber.Update();
					serializedRubber.FindProperty("_roundness").floatValue
						= 0.2f + edit * 0.2f;
					serializedRubber.ApplyModifiedProperties();
					for (var frame = 0; frame < 3 && rubber.BakeInputHash == previousHash; frame++) {
						yield return null;
					}
					Assert.That(rubber.BakeInputHash, Is.Not.EqualTo(previousHash),
						$"Roundness edit {edit + 1} was not previewed while input remained active.");
					Assert.That(rubber.BakedPath
						.Where(element => element.Type == RubberPathElementType.FreeSpan),
						Has.Some.Matches<RubberPathElement>(element =>
							!RubberPathMath.IsStraightFreeSpan(element)));
					Assert.That(RubberAutofit.GetStatus(rubber).IsValid, Is.True);
				}

				for (var move = 0; move < 4; move++) {
					var previousHash = rubber.BakeInputHash;
					UnityEditor.Undo.RecordObject(rubberObject.transform, "Move Guided Rubber");
					rubberObject.transform.localPosition += new Vector3(0.003f, 0f, 0f);
					for (var frame = 0; frame < 3 && rubber.BakeInputHash == previousHash; frame++) {
						yield return null;
					}
					Assert.That(rubber.BakeInputHash, Is.Not.EqualTo(previousHash),
						$"Rubber move {move + 1} was not previewed while the transform remained active.");
					Assert.That(RubberAutofit.GetStatus(rubber).IsValid, Is.True);
				}
			} finally {
				Object.DestroyImmediate(root);
			}
		}

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
		public void ShouldMigrateUnsupportedGuideProfilesToCircles()
		{
			var gameObject = new GameObject("Legacy Guide");
			try {
				var guide = gameObject.AddComponent<RubberGuideComponent>();
				guide.Slots = new[] {
					new RubberGuideSlot {
						Id = new SerializedGuid(1, 2),
						DisplayName = "Legacy",
						Profile = new RubberGuideProfile {
							Type = RubberGuideProfileType.Convex2D,
							LocalCenter = new float2(1f, 1f),
							ConvexHull = new[] {
								new float2(1f, 4f),
								new float2(-1f, 1f),
							},
						},
					},
				};

				Assert.That(guide.Slots[0].Profile.Type,
					Is.EqualTo(RubberGuideProfileType.Circle));
				Assert.That(guide.Slots[0].Profile.Radius, Is.EqualTo(3f).Within(1e-6f));
				Assert.That(guide.Slots[0].Profile.ConvexHull, Is.Empty);
			} finally {
				Object.DestroyImmediate(gameObject);
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
				Assert.That(rubber.Roundness, Is.Zero);
				Assert.That(rubber.Thickness, Is.EqualTo(9));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldRoundTripGuideDistanceElasticityColliderSettings()
		{
			var sourceObject = new GameObject("Source Rubber Collider");
			var targetObject = new GameObject("Target Rubber Collider");
			try {
				sourceObject.AddComponent<RubberComponent>();
				var source = sourceObject.AddComponent<RubberColliderComponent>();
				source.GuideDistanceElasticityEnabled = true;
				source.GuideElasticityScale = 0.35f;
				source.GuideInfluenceLength = 75f;
				source.MaximumResponseSegmentLength = 12f;

				targetObject.AddComponent<RubberComponent>();
				var target = targetObject.AddComponent<RubberColliderComponent>();
				target.Unpack(source.Pack());

				Assert.That(target.GuideDistanceElasticityEnabled, Is.True);
				Assert.That(target.GuideElasticityScale, Is.EqualTo(0.35f));
				Assert.That(target.GuideInfluenceLength, Is.EqualTo(75f));
				Assert.That(target.MaximumResponseSegmentLength, Is.EqualTo(12f));
			} finally {
				Object.DestroyImmediate(sourceObject);
				Object.DestroyImmediate(targetObject);
			}
		}

		[Test]
		public void ShouldDefaultOldRubberColliderPayloadsToDisabledDistanceElasticity()
		{
			var gameObject = new GameObject("Legacy Rubber Collider");
			try {
				gameObject.AddComponent<RubberComponent>();
				var collider = gameObject.AddComponent<RubberColliderComponent>();
				collider.GuideDistanceElasticityEnabled = true;
				collider.GuideElasticityScale = 0f;
				collider.GuideInfluenceLength = 1f;
				collider.MaximumResponseSegmentLength = 1f;
				var oldBytes = PackageApi.Packer.Pack(new LegacyRubberColliderPackable {
					IsMovable = true,
					HitEvent = true,
					ZOffset = 3f,
					Mode = (int)RubberColliderMode.Physical,
				});

				collider.Unpack(oldBytes);

				Assert.That(collider.IsKinematic, Is.True);
				Assert.That(collider.HitEvent, Is.True);
				Assert.That(collider.ZOffset, Is.EqualTo(3f));
				Assert.That(collider.Mode, Is.EqualTo(RubberColliderMode.Physical));
				Assert.That(collider.GuideDistanceElasticityEnabled, Is.False);
				Assert.That(collider.GuideElasticityScale,
					Is.EqualTo(RubberColliderComponent.DefaultGuideElasticityScale));
				Assert.That(collider.GuideInfluenceLength,
					Is.EqualTo(RubberColliderComponent.DefaultGuideInfluenceLength));
				Assert.That(collider.MaximumResponseSegmentLength,
					Is.EqualTo(RubberColliderComponent.DefaultMaximumResponseSegmentLength));
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void ShouldMapVersionOneFreeSpanScaleToGuideScale()
		{
			var gameObject = new GameObject("Version 1 Rubber Collider");
			try {
				gameObject.AddComponent<RubberComponent>();
				var collider = gameObject.AddComponent<RubberColliderComponent>();
				var bytes = PackageApi.Packer.Pack(new RubberColliderPackable {
					Version = 1,
					GuideDistanceElasticityEnabled = true,
					FreeSpanElasticityScale = 0.35f,
					GuideInfluenceLength = 75f,
					MaximumResponseSegmentLength = 12f,
				});

				collider.Unpack(bytes);

				Assert.That(collider.GuideDistanceElasticityEnabled, Is.True);
				Assert.That(collider.GuideElasticityScale, Is.EqualTo(0.35f));
				Assert.That(collider.GuideInfluenceLength, Is.EqualTo(75f));
				Assert.That(collider.MaximumResponseSegmentLength, Is.EqualTo(12f));
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void ShouldSanitizeMalformedGuideDistanceElasticityPayload()
		{
			var gameObject = new GameObject("Malformed Rubber Collider");
			try {
				gameObject.AddComponent<RubberComponent>();
				var collider = gameObject.AddComponent<RubberColliderComponent>();
				var bytes = PackageApi.Packer.Pack(new RubberColliderPackable {
					Version = 2,
					GuideDistanceElasticityEnabled = true,
					GuideElasticityScale = float.NaN,
					GuideInfluenceLength = float.NegativeInfinity,
					MaximumResponseSegmentLength = 0.01f,
				});

				collider.Unpack(bytes);

				Assert.That(collider.GuideDistanceElasticityEnabled, Is.True);
				Assert.That(collider.GuideElasticityScale,
					Is.EqualTo(RubberColliderComponent.DefaultGuideElasticityScale));
				Assert.That(collider.GuideInfluenceLength,
					Is.EqualTo(RubberColliderComponent.DefaultGuideInfluenceLength));
				Assert.That(collider.MaximumResponseSegmentLength,
					Is.EqualTo(RubberColliderComponent.MinimumResponseLength));
			} finally {
				Object.DestroyImmediate(gameObject);
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
				source.Roundness = 0.65f;
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
					new RubberPathElement {
						Type = RubberPathElementType.FreeSpan,
						Start = new float2(3f, 4f),
						End = new float2(13f, 4f),
						Control1 = new float2(5f, 8f),
						Control2 = new float2(11f, 8f),
						StartBindingIndex = 0,
						EndBindingIndex = 1,
						StartDistance = 31.4f,
						Length = 12f,
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
				Assert.That(target.BakedPath.Count, Is.EqualTo(2));
				Assert.That(target.BakedPath[0].SweepAngleRad, Is.EqualTo(math.PI));
				Assert.That(target.BakedPath[1].Start, Is.EqualTo(new float2(3f, 4f)));
				Assert.That(target.BakedPath[1].End, Is.EqualTo(new float2(13f, 4f)));
				Assert.That(target.BakedPath[1].Control1, Is.EqualTo(new float2(5f, 8f)));
				Assert.That(target.BakedPath[1].Control2, Is.EqualTo(new float2(11f, 8f)));
				Assert.That(target.BakeVersion, Is.EqualTo(7));
				Assert.That(target.BakeInputHash, Is.EqualTo(hash));
				Assert.That(target.BakeFrameToLocal, Is.EqualTo(frame));
				Assert.That(target.RestLength, Is.EqualTo(250f));
				Assert.That(target.Roundness, Is.EqualTo(0.65f));
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
				Assert.That(RubberAutofit.TryBake(rubber, out _, out var bakeError),
					Is.True, bakeError);

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

		private static RubberGuideSlot CreateSlot(SerializedGuid id, string name, float radius,
			float localHeight = 0f)
		{
			return new RubberGuideSlot {
				Id = id,
				DisplayName = name,
				LocalHeight = localHeight,
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

		private struct LegacyRubberColliderPackable
		{
			public bool IsMovable;
			public bool HitEvent;
			public float ZOffset;
			public int Mode;
		}
	}
}
