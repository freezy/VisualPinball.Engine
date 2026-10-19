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
	public class MeshComponentRendererStateTests
	{
		private GameObject _go;

		[SetUp]
		public void SetUp()
		{
			_go = new GameObject("Primitive");
		}

		[TearDown]
		public void TearDown()
		{
			Object.DestroyImmediate(_go);
		}

		[Test]
		public void ShouldKeepAnAuthoredDisabledRendererDisabledWhenEnabled()
		{
			var mr = _go.AddComponent<MeshRenderer>();
			mr.enabled = false;

			_go.AddComponent<PrimitiveMeshComponent>();   // OnEnable runs (ExecuteInEditMode)

			Assert.That(mr.enabled, Is.False);
		}

		[Test]
		public void ShouldRestoreTheRendererItHidWhenReEnabled()
		{
			var mr = _go.AddComponent<MeshRenderer>();
			var mesh = _go.AddComponent<PrimitiveMeshComponent>();

			mesh.enabled = false;
			Assert.That(mr.enabled, Is.False);

			mesh.enabled = true;
			Assert.That(mr.enabled, Is.True);
		}

		[Test]
		public void ShouldNotShowAnAuthoredDisabledRendererAfterAToggle()
		{
			var mr = _go.AddComponent<MeshRenderer>();
			mr.enabled = false;
			var mesh = _go.AddComponent<PrimitiveMeshComponent>();

			mesh.enabled = false;
			mesh.enabled = true;

			Assert.That(mr.enabled, Is.False);
		}

		[Test]
		public void ShouldShowAnItemSavedHiddenBeforeTheFlagExistedWhenEnabled()
		{
			// saved by an older version: component and renderer both off, _hidRenderer false
			var mr = _go.AddComponent<MeshRenderer>();
			var mesh = _go.AddComponent<PrimitiveMeshComponent>();
			mr.enabled = false;
			mesh.enabled = false;
			var clone = Object.Instantiate(_go);   // Awake runs on the deserialized copy
			try {
				var cloneMesh = clone.GetComponent<PrimitiveMeshComponent>();
				var cloneRenderer = clone.GetComponent<MeshRenderer>();

				cloneMesh.enabled = true;

				Assert.That(cloneRenderer.enabled, Is.True);
			}
			finally {
				Object.DestroyImmediate(clone);
			}
		}
	}
}
