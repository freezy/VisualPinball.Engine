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

using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace VisualPinball.Unity.Test
{
	public class PhysicsMaterialTests
	{
		private const string PreRollingResistancePhysicalMaterialJson =
			"{\"Elasticity\":0.3,\"ElasticityFalloff\":0.1,\"Friction\":0.2," +
			"\"Scatter\":1.5,\"Overwrite\":true,\"AssetRef\":0}";

		[Test]
		public void PhysicsMaterialDataEqualityIncludesRollingResistance()
		{
			var first = new PhysicsMaterialData { RollingResistance = 0.01f };
			var second = new PhysicsMaterialData { RollingResistance = 0.02f };

			Assert.That(first, Is.Not.EqualTo(second));
			Assert.That(first.Equals((object)second), Is.False);

			second.RollingResistance = first.RollingResistance;
			Assert.That(first, Is.EqualTo(second));
			Assert.That(first.Equals((object)second), Is.True);
			Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
		}

		[TestCase(-0.01f)]
		[TestCase(float.NaN)]
		[TestCase(float.PositiveInfinity)]
		[TestCase(float.NegativeInfinity)]
		public void InvalidRollingResistanceSanitizesToZero(float value)
		{
			Assert.That(PhysicsMaterialData.SanitizeRollingResistance(value), Is.Zero);
		}

		[Test]
		public void ValidRollingResistanceIsPreserved()
		{
			Assert.That(PhysicsMaterialData.SanitizeRollingResistance(0.02f), Is.EqualTo(0.02f));
		}

		[Test]
		public void ColliderMaterialSelectionUsesAssetOrOverride()
		{
			var gameObject = new GameObject("Rolling resistance material selection");
			var asset = ScriptableObject.CreateInstance<PhysicsMaterialAsset>();
			try {
				var collider = gameObject.AddComponent<RampColliderComponent>();
				asset.RollingResistance = 0.01f;
				collider.PhysicsMaterial = asset;
				collider.RollingResistance = 0.02f;

				collider.OverwritePhysics = false;
				Assert.That(collider.GetPhysicsMaterialData().RollingResistance, Is.EqualTo(0.01f));

				collider.OverwritePhysics = true;
				Assert.That(collider.GetPhysicsMaterialData().RollingResistance, Is.EqualTo(0.02f));
			} finally {
				Object.DestroyImmediate(asset);
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void OldPhysicalMaterialPackageDefaultsToZero()
		{
			var bytes = Encoding.UTF8.GetBytes(PreRollingResistancePhysicalMaterialJson);

			var material = PackageApi.Packer.Unpack<PhysicalMaterialPackable>(bytes);

			Assert.That(material.RollingResistance, Is.Zero);
		}

		[Test]
		public void PhysicalMaterialPackageRoundTripsRollingResistance()
		{
			var expected = new PhysicalMaterialPackable {
				Elasticity = 0.3f,
				ElasticityFalloff = 0.1f,
				Friction = 0.2f,
				RollingResistance = 0.015f,
				Scatter = 1.5f,
				Overwrite = true,
				AssetRef = 2
			};

			var bytes = PackageApi.Packer.Pack(expected);
			var actual = PackageApi.Packer.Unpack<PhysicalMaterialPackable>(bytes);

			Assert.That(actual.RollingResistance, Is.EqualTo(expected.RollingResistance));
		}
	}
}
