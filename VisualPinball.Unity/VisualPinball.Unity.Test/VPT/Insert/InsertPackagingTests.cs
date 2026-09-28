// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace VisualPinball.Unity.Test
{
	public class InsertPackagingTests
	{
		[Test]
		public void ImportedInsertKeepsItsGlbMeshWhenEnabled()
		{
			var go = new GameObject("Imported Insert");
			var mesh = new Mesh { name = "GLB insert mesh" };
			try {
				go.AddComponent<MeshFilter>().sharedMesh = mesh;
				go.AddComponent<MeshRenderer>();
				InsertComponent insert;
				using (InsertComponent.DeferGenerationForPackageImport()) {
					insert = go.AddComponent<InsertComponent>();
					Assert.That(go.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
					insert.Unpack(insert.Pack());
				}

				go.SetActive(false);
				go.SetActive(true);
				insert.RebuildMesh();

				Assert.That(go.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
				Assert.That(mesh.name, Is.EqualTo("GLB insert mesh"));
			} finally {
				Object.DestroyImmediate(go);
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void LampMappingSelectsInsertLightOnSharedNode()
		{
			var go = new GameObject("Insert with Light");
			go.SetActive(false);
			try {
				go.AddComponent<LightComponent>();
				var insertLight = go.AddComponent<InsertLightComponent>();
				var refs = new PackagedRefs(go.transform);
				refs.SetNodeIdsForWrite(new Dictionary<Transform, string> { [go.transform] = "insert-node" });
				refs.SetNodeIdsForRead(new Dictionary<string, Transform> { ["insert-node"] = go.transform });
				var mapping = new LampMapping { Device = insertLight };

				mapping.SaveReference(refs);
				var restored = PackageApi.Packer.Unpack<LampMapping>(PackageApi.Packer.Pack(mapping));
				restored.RestoreReference(refs);

				Assert.That(restored.Device, Is.SameAs(insertLight));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void InsertLightStateAndSpillReferenceRoundTrip()
		{
			var go = new GameObject("Insert Lamp");
			go.SetActive(false);
			var spillGo = new GameObject("Spill");
			spillGo.transform.SetParent(go.transform);
			try {
				var insertLight = go.AddComponent<InsertLightComponent>();
				var spill = spillGo.AddComponent<Light>();
				insertLight.Intensity = 0.35f;
				insertLight.Color = Color.cyan;
				insertLight.ExtraBulbs[0].Intensity = 0.7f;
				insertLight.BulbNames[1] = "Middle";
				insertLight.TintWithLamp = true;
				insertLight.SpillLumens = 3.5f;
				insertLight.SpillLight = spill;

				var refs = new PackagedRefs(go.transform);
				refs.SetNodeIdsForWrite(new Dictionary<Transform, string> {
					[go.transform] = "insert-node",
					[spillGo.transform] = "spill-node",
				});
				refs.SetNodeIdsForRead(new Dictionary<string, Transform> {
					["insert-node"] = go.transform,
					["spill-node"] = spillGo.transform,
				});
				var data = insertLight.Pack();
				var referenceData = insertLight.PackReferences(go.transform, refs, null);
				insertLight.Intensity = 0f;
				insertLight.Color = Color.white;
				insertLight.ExtraBulbs[0].Intensity = 0f;
				insertLight.BulbNames[1] = null;
				insertLight.TintWithLamp = false;
				insertLight.SpillLumens = 0f;
				insertLight.SpillLight = null;

				insertLight.Unpack(data);
				insertLight.UnpackReferences(referenceData, go.transform, refs, null);

				Assert.That(insertLight.Intensity, Is.EqualTo(0.35f));
				Assert.That(insertLight.Color, Is.EqualTo(Color.cyan));
				Assert.That(insertLight.ExtraBulbs[0].Intensity, Is.EqualTo(0.7f));
				Assert.That(insertLight.BulbNames[1], Is.EqualTo("Middle"));
				Assert.That(insertLight.TintWithLamp, Is.True);
				Assert.That(insertLight.SpillLumens, Is.EqualTo(3.5f));
				Assert.That(insertLight.SpillLight, Is.SameAs(spill));
			} finally {
				Object.DestroyImmediate(go);
			}
		}
	}
}
