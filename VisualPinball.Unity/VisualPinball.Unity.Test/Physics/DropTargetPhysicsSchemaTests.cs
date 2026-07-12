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
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Test
{
	public class DropTargetPhysicsSchemaTests
	{
		[Test]
		public void ColliderHeaderPreservesSemanticRole()
		{
			var header = new ColliderHeader();
			header.Init(new ColliderInfo {
				ItemId = 42,
				ItemType = ItemType.HitTarget,
				Role = ColliderRole.DropTargetPhysicalFace,
			}, ColliderType.Triangle);

			Assert.That(header.Role, Is.EqualTo(ColliderRole.DropTargetPhysicalFace));
			Assert.That(header.ColliderInfo.Role, Is.EqualTo(ColliderRole.DropTargetPhysicalFace));
		}

		[Test]
		public void PreSchemaPackageDefaultsToLegacyWithoutZeroingAdvancedDefaults()
		{
			var legacyBytes = Encoding.UTF8.GetBytes("{\"IsMovable\":true,\"Threshold\":2.0,\"UseHitEvent\":true}");
			var data = PackageApi.Packer.Unpack<DropTargetColliderPackable>(legacyBytes);

			DropTargetColliderPackable.ApplySchemaDefaults(ref data);

			Assert.That(data.IsMovable, Is.True);
			Assert.That(data.Threshold, Is.EqualTo(2f));
			Assert.That(data.UseHitEvent, Is.True);
			Assert.That(data.PhysicsMode, Is.EqualTo(DropTargetPhysicsMode.Legacy));
			Assert.That(data.MechanicalOverrides.EffectiveFaceMass, Is.EqualTo(0.2f));
			Assert.That(data.MechanicalOverrides.DropTravel, Is.EqualTo(52f));
			Assert.That(data.RothConfig.TargetMass, Is.EqualTo(0.2f));
			Assert.That(data.RothConfig.EnableBrick, Is.False);
		}

		[Test]
		public void CurrentSchemaRoundTripsModeAndConfigurations()
		{
			var expected = new DropTargetColliderPackable {
				PhysicsSchemaVersion = DropTargetColliderPackable.CurrentPhysicsSchemaVersion,
				PhysicsMode = DropTargetPhysicsMode.Mechanical,
				OverrideMechanicalProfile = true,
				MechanicalOverrides = DropTargetMechanicalConfig.Default,
				RothConfig = DropTargetRothConfig.Default,
			};
			expected.MechanicalOverrides.RearStopTravel = 7f;
			expected.RothConfig.EnableBacksideDrop = true;

			var bytes = PackageApi.Packer.Pack(expected);
			var actual = PackageApi.Packer.Unpack<DropTargetColliderPackable>(bytes);
			DropTargetColliderPackable.ApplySchemaDefaults(ref actual);

			Assert.That(actual.PhysicsMode, Is.EqualTo(DropTargetPhysicsMode.Mechanical));
			Assert.That(actual.OverrideMechanicalProfile, Is.True);
			Assert.That(actual.MechanicalOverrides.RearStopTravel, Is.EqualTo(7f));
			Assert.That(actual.RothConfig.EnableBacksideDrop, Is.True);
		}

		[Test]
		public void DefaultModeRemainsLegacy()
		{
			Assert.That(default(DropTargetPhysicsMode), Is.EqualTo(DropTargetPhysicsMode.Legacy));
			Assert.That(default(ColliderRole), Is.EqualTo(ColliderRole.None));
		}
	}
}
