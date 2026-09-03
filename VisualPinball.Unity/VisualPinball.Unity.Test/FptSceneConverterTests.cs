// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

using System.Collections.Generic;

using NUnit.Framework;

using UnityEngine;

using VisualPinball.Engine.IO.FuturePinball;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Editor;
using VisualPinball.Unity.Playfield;

using EngineMesh = VisualPinball.Engine.VPT.Mesh;
using UnityMaterial = UnityEngine.Material;
using UnityMesh = UnityEngine.Mesh;
using UnityTexture = UnityEngine.Texture;

namespace VisualPinball.Unity.Test
{
	public class FptSceneConverterTests
	{
		[Test]
		public void ShouldAttachAndConfigureSpinningDiskFromSourceModel()
		{
			var modelInstances = new GameObject("Model Instances");
			var mechanism = new GameObject("Spinning Disk");
			var sourceModel = new GameObject("Source Model");
			sourceModel.transform.SetParent(modelInstances.transform, false);
			sourceModel.transform.localPosition = Vector3.one;
			sourceModel.transform.localRotation = Quaternion.Euler(10f, 20f, 30f);
			var visual = FptSceneConverter.CreateSpinningDiskVisualRoot(mechanism.AddComponent<TurntableComponent>(), sourceModel);
			var meshObject = new GameObject("Mesh");
			var mesh = new UnityMesh { vertices = new[] { new Vector3(0.03f, 0f, 0.04f) } };
			meshObject.transform.SetParent(visual.transform, false);
			meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;
			var component = mechanism.GetComponent<TurntableComponent>();

			try {
				var configured = FptSceneConverter.ConfigureSpinningDiskVisual(component, visual);

				Assert.That(configured, Is.True);
				Assert.That(sourceModel.transform.parent, Is.EqualTo(mechanism.transform));
				Assert.That(sourceModel.transform.localPosition, Is.EqualTo(Vector3.zero));
				Assert.That(sourceModel.transform.localRotation, Is.EqualTo(Quaternion.identity));
				Assert.That(visual.transform.parent, Is.EqualTo(sourceModel.transform));
				Assert.That(component.RotationTarget, Is.EqualTo(visual.transform));
				Assert.That(component.Radius, Is.EqualTo(50f).Within(0.001f));
			} finally {
				Object.DestroyImmediate(mechanism);
				Object.DestroyImmediate(modelInstances);
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void ShouldRetainDefaultRadiusForSpinningDiskWithoutUsableMesh()
		{
			var mechanism = new GameObject("Spinning Disk");
			var visual = new GameObject("Rotating Visual");
			var component = mechanism.AddComponent<TurntableComponent>();

			try {
				var configured = FptSceneConverter.ConfigureSpinningDiskVisual(component, visual);

				Assert.That(configured, Is.False);
				Assert.That(component.RotationTarget, Is.EqualTo(visual.transform));
				Assert.That(component.Radius, Is.EqualTo(60f));
			} finally {
				Object.DestroyImmediate(mechanism);
				Object.DestroyImmediate(visual);
			}
		}

		[Test]
		public void ShouldDriveNativeFlipperWithSourceVisual()
		{
			var modelInstances = new GameObject("Model Instances");
			modelInstances.transform.rotation = Quaternion.Euler(5f, 17f, -9f);
			var nativeParent = new GameObject("Native Parent");
			nativeParent.transform.rotation = Quaternion.Euler(11f, -31f, 7f);
			var nativeFlipper = new GameObject("Native Flipper");
			nativeFlipper.transform.SetParent(nativeParent.transform, false);
			var proceduralVisual = new GameObject("Procedural Visual");
			proceduralVisual.transform.SetParent(nativeFlipper.transform, false);
			var renderer = proceduralVisual.AddComponent<MeshRenderer>();
			var component = nativeFlipper.AddComponent<FlipperComponent>();
			component.StartAngle = 122f;
			var sourceModel = new GameObject("Source Model");
			sourceModel.transform.SetParent(modelInstances.transform, false);
			var sourceRotation = FptSceneConverter.FlipperSourceRotation(component.StartAngle);
			var sourceWorldRotation = modelInstances.transform.rotation * sourceRotation;
			var relativeRotation = Quaternion.Inverse(nativeFlipper.transform.rotation) * sourceWorldRotation;

			try {
				var visual = FptSceneConverter.CreateFlipperVisualRoot(component, sourceModel, sourceRotation);

				Assert.That(visual, Is.SameAs(sourceModel));
				Assert.That(sourceModel.transform.parent, Is.EqualTo(nativeFlipper.transform));
				Assert.That(sourceModel.transform.localPosition, Is.EqualTo(Vector3.zero));
				Assert.That(Quaternion.Angle(sourceModel.transform.rotation, sourceWorldRotation), Is.LessThan(0.001f));
				Assert.That(renderer.enabled, Is.False);
				Assert.That(component.InstantiateAsPrefab, Is.True);

				component.StartAngle = 70f;
				var expectedRotation = nativeFlipper.transform.rotation * relativeRotation;
				Assert.That(Quaternion.Angle(sourceModel.transform.rotation, expectedRotation), Is.LessThan(0.001f));
			} finally {
				Object.DestroyImmediate(nativeParent);
				Object.DestroyImmediate(modelInstances);
			}
		}

		[TestCase(122f, -212f)]
		[TestCase(-122f, -328f)]
		public void ShouldOrientFlipperSourceModelFromStartAngle(float startAngle, float expectedYaw)
		{
			var rotation = FptSceneConverter.FlipperSourceRotation(startAngle);

			Assert.That(Quaternion.Angle(rotation, Quaternion.Euler(0f, expectedYaw, 0f)), Is.LessThan(0.001f));
		}

		[Test]
		public void ShouldAttachHiddenVpePrimitiveCollider()
		{
			var gameObject = new GameObject("VPE Collider");
			var mesh = new UnityMesh();

			try {
				FptSceneConverter.AddVpePrimitiveCollider(gameObject, mesh, true, false);

				Assert.That(gameObject.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
				Assert.That(gameObject.GetComponent<PrimitiveComponent>(), Is.Not.Null);
				Assert.That(gameObject.GetComponent<PrimitiveMeshComponent>().enabled, Is.False);
				Assert.That(gameObject.GetComponent<PrimitiveColliderComponent>().HitEvent, Is.True);
				Assert.That(gameObject.GetComponent<PrimitiveColliderComponent>().enabled, Is.True);
			} finally {
				Object.DestroyImmediate(gameObject);
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void ShouldAttachRenderablePlayfieldComponents()
		{
			var gameObject = new GameObject("Playfield");
			var texture = new Texture2D(2, 2) { name = FuturePinballNativeItemConverter.PlayfieldImage };
			var data = new TableData {
				Name = "Playfield",
				Image = FuturePinballNativeItemConverter.PlayfieldImage,
				Right = FuturePinballCoordinateConverter.ToVpx(514f),
				Bottom = FuturePinballCoordinateConverter.ToVpx(1168f),
				AngleTiltMin = 6.5f
			};
			var provider = new TestMaterialProvider(texture);
			UnityMesh generatedMesh = null;
			UnityMaterial generatedMaterial = null;

			try {
				var component = FptSceneConverter.AddPlayfieldComponents(gameObject, data, true);
				component.SetReferencedData(
					data,
					FptSceneConverter.CreateNativeTable(data),
					provider,
					provider,
					new Dictionary<string, IMainComponent>()
				);

				Assert.That(component.RenderSlope, Is.EqualTo(6.5f));
				Assert.That(gameObject.GetComponent<PlayfieldColliderComponent>(), Is.Not.Null);
				Assert.That(gameObject.GetComponent<PlayfieldMeshComponent>(), Is.Not.Null);
				var meshFilter = gameObject.GetComponent<MeshFilter>();
				var meshRenderer = gameObject.GetComponent<MeshRenderer>();
				Assert.That(meshFilter, Is.Not.Null);
				Assert.That(meshFilter.sharedMesh, Is.Not.Null);
				Assert.That(meshFilter.sharedMesh.vertexCount, Is.EqualTo(4));
				Assert.That(meshRenderer, Is.Not.Null);
				Assert.That(meshRenderer.sharedMaterial, Is.Not.Null);
				Assert.That(provider.RequestedTextureName, Is.EqualTo(FuturePinballNativeItemConverter.PlayfieldImage));
				Assert.That(new[] { "_BaseColorMap", "_BaseMap", "_MainTex" }, Has.Some.Matches<string>(
					property => meshRenderer.sharedMaterial.HasProperty(property)
						&& meshRenderer.sharedMaterial.GetTexture(property) == texture
				));
				generatedMesh = meshFilter.sharedMesh;
				generatedMaterial = meshRenderer.sharedMaterial;
			} finally {
				Object.DestroyImmediate(gameObject);
				if (generatedMesh) Object.DestroyImmediate(generatedMesh);
				if (generatedMaterial) Object.DestroyImmediate(generatedMaterial);
				Object.DestroyImmediate(texture);
			}
		}

		[Test]
		public void ShouldDisableOnlyRoomScaleOrnamentModels()
		{
			var table = new TableData {
				Right = FuturePinballCoordinateConverter.ToVpx(514f),
				Bottom = FuturePinballCoordinateConverter.ToVpx(1168f)
			};
			var cabinetRail = new EngineMesh(new[] {
				new[] { -0.012f, -0.053f, -0.598f },
				new[] { 0.012f, 0.053f, 0.598f }
			}, new[] { 0, 1 });
			var arcadeWall = new EngineMesh(new[] {
				new[] { -3.042f, -1.939f, -1.590f },
				new[] { 3.042f, 0.930f, 0.524f }
			}, new[] { 0, 1 });

			Assert.That(FptSceneConverter.IsEnvironmentScaleOrnamentModel(new[] { cabinetRail }, table, out _), Is.False);
			Assert.That(FptSceneConverter.IsEnvironmentScaleOrnamentModel(new[] { arcadeWall }, table, out var size), Is.True);
			Assert.That(size.x, Is.EqualTo(6.084f).Within(0.001f));
		}

		[Test]
		public void ShouldExposeImportedTexturesToNativeMeshes()
		{
			var data = new TableData { Image = FuturePinballNativeItemConverter.PlayfieldImage };
			var table = FptSceneConverter.CreateNativeTable(data, new[] { "flipper-white-black" });

			Assert.That(table.GetTexture(FuturePinballNativeItemConverter.PlayfieldImage), Is.Not.Null);
			Assert.That(table.GetTexture("FLIPPER-WHITE-BLACK"), Is.Not.Null);
			Assert.That(table.GetTexture("missing"), Is.Null);
		}

		private sealed class TestMaterialProvider : IMaterialProvider, ITextureProvider
		{
			private readonly UnityTexture _texture;

			public string RequestedTextureName { get; private set; }

			public TestMaterialProvider(UnityTexture texture)
			{
				_texture = texture;
			}

			public bool HasMaterial(PbrMaterial material) => false;

			public void SaveMaterial(PbrMaterial vpxMaterial, UnityMaterial material) { }

			public UnityMaterial GetMaterial(PbrMaterial material) => null;

			public PhysicsMaterialAsset GetPhysicsMaterial(string name) => null;

			public UnityMaterial MergeMaterials(string vpxMaterial, UnityMaterial textureMaterial) => textureMaterial;

			public UnityTexture GetTexture(string name)
			{
				RequestedTextureName = name;
				return _texture;
			}
		}
	}
}
