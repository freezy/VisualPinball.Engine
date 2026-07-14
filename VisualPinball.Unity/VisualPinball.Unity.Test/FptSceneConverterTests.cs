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

using NUnit.Framework;

using UnityEngine;

using VisualPinball.Unity.Editor;

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
			var mesh = new Mesh { vertices = new[] { new Vector3(0.03f, 0f, 0.04f) } };
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
	}
}
