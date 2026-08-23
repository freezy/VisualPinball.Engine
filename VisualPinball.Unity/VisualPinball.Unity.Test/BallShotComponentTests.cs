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

namespace VisualPinball.Unity.Test
{
	public class BallShotComponentTests
	{
		[Test]
		public void ShouldAlignDirectionGizmoWithBallCenters()
		{
			var root = new GameObject("Table");
			try {
				root.transform.SetPositionAndRotation(new Vector3(2f, -1f, 3f),
					Quaternion.Euler(12f, 28f, -7f));
				root.transform.localScale = new Vector3(1.2f, 0.8f, 1.4f);

				var current = CreateChild(root.transform, "Current");
				var start = CreateChild(current, "StartGizmo");
				CreateChild(current, "EndGizmo");
				var direction = CreateChild(current, "DirectionGizmo");
				var component = root.AddComponent<BallShotComponent>();
				start.localPosition = new Vector3(-0.18f, 0.025f, 0.32f);
				var end = new Vector3(0.41f, 0.025f, -0.27f);

				component.SetShotEnd("Current", end);

				var halfAxis = direction.localRotation * Vector3.up * direction.localScale.y;
				Assert.That(Vector3.Distance(direction.localPosition - halfAxis, start.localPosition),
					Is.LessThan(1e-6f));
				Assert.That(Vector3.Distance(direction.localPosition + halfAxis, end),
					Is.LessThan(1e-6f));
			}
			finally {
				Object.DestroyImmediate(root);
			}
		}

		private static Transform CreateChild(Transform parent, string name)
		{
			var child = new GameObject(name).transform;
			child.SetParent(parent, false);
			return child;
		}
	}
}
