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

using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using VisualPinball.Unity.Playfield;

namespace VisualPinball.Unity.Test
{
	public class PlayfieldMeshPackagingTests
	{
		[Test]
		public void ShouldBePackedSoACustomMeshSurvivesExport()
		{
			Assert.That(typeof(PlayfieldMeshComponent).GetCustomAttribute<PackAsAttribute>(), Is.Not.Null);
			Assert.That(typeof(IPackable).IsAssignableFrom(typeof(PlayfieldMeshComponent)), Is.True);
		}

		[Test]
		public void AutoGenerateRoundTrips()
		{
			var gameObject = new GameObject("Playfield");
			try {
				var mesh = gameObject.AddComponent<PlayfieldMeshComponent>();
				mesh.AutoGenerate = false;
				var bytes = mesh.Pack();

				mesh.AutoGenerate = true;
				mesh.Unpack(bytes);

				Assert.That(mesh.AutoGenerate, Is.False);
			}
			finally {
				Object.DestroyImmediate(gameObject);
			}
		}
	}
}
