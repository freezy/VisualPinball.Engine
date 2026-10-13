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
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// The scatter angle of a physics material is in degrees, like in VPX and like
	/// the per-collider scatter values. The physics engine wants radians.
	/// </summary>
	public class PhysicsMaterialScatterTests
	{
		private const float ScatterDegrees = 15f;

		[Test]
		public void ShouldConvertMaterialScatterLikeTheOverwrittenValue()
		{
			var go = new GameObject("Physics Material Scatter Test");
			var material = ScriptableObject.CreateInstance<PhysicsMaterialAsset>();
			try {
				go.AddComponent<SurfaceComponent>();
				var collider = go.AddComponent<SurfaceColliderComponent>();
				material.ScatterAngle = ScatterDegrees;
				collider.PhysicsMaterial = material;
				collider.Scatter = ScatterDegrees;

				collider.OverwritePhysics = true;
				var overwritten = collider.GetPhysicsMaterialData();
				collider.OverwritePhysics = false;
				var fromMaterial = collider.GetPhysicsMaterialData();

				Assert.That(overwritten.ScatterAngleRad, Is.EqualTo(math.radians(ScatterDegrees)).Within(1e-6f));
				Assert.That(fromMaterial.ScatterAngleRad, Is.EqualTo(overwritten.ScatterAngleRad).Within(1e-6f));
			} finally {
				Object.DestroyImmediate(material);
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldConvertMaterialScatterOfGeneratedMagnetCollider()
		{
			var go = new GameObject("Magnet Physics Material Scatter Test");
			var material = ScriptableObject.CreateInstance<PhysicsMaterialAsset>();
			var transforms = new NativeParallelHashMap<int, float4x4>(0, Allocator.Temp);
			var colliders = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				var component = go.AddComponent<MagnetComponent>();
				component.MagnetType = MagnetType.Cylindrical;
				component.CylinderRadius = 25f;
				component.CylinderHeight = 50f;
				component.GenerateCylinderCollider = true;
				material.ScatterAngle = ScatterDegrees;
				var collidable = (ICollidableComponent)component;
				collidable.PhysicsMaterialReference = material;
				collidable.PhysicsOverwrite = false;

				collidable.GetColliders(null, null, ref colliders, float4x4.identity, 0f);

				Assert.That(colliders.Count, Is.EqualTo(1));
				var collider = (CircleCollider)colliders[0];
				Assert.That(collider.Header.Material.ScatterAngleRad, Is.EqualTo(math.radians(ScatterDegrees)).Within(1e-6f));
			} finally {
				colliders.Dispose();
				transforms.Dispose();
				Object.DestroyImmediate(material);
				Object.DestroyImmediate(go);
			}
		}
	}
}
