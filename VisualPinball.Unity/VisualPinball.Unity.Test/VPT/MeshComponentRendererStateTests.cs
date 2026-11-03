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

using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.Primitive;
using Object = UnityEngine.Object;

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
			// saved by an older version: component and renderer both off, no _hidRenderer and no
			// _rendererStateVersion (both load as their defaults)
			var mr = _go.AddComponent<MeshRenderer>();
			var mesh = _go.AddComponent<PrimitiveMeshComponent>();
			mr.enabled = false;
			mesh.enabled = false;
			var state = new SerializedObject(mesh);
			state.FindProperty("_hidRenderer").boolValue = false;
			state.FindProperty("_rendererStateVersion").intValue = 0;
			state.ApplyModifiedPropertiesWithoutUndo();
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

		[Test]
		public void ShouldShowTheMeshAgainWhenTheDisableIsUndone()
		{
			var mr = _go.AddComponent<MeshRenderer>();
			var mesh = _go.AddComponent<PrimitiveMeshComponent>();

			Undo.IncrementCurrentGroup();
			Undo.RecordObject(mesh, "Disable Mesh");   // what the inspector checkbox records
			mesh.enabled = false;
			Assert.That(mr.enabled, Is.False);

			Undo.FlushUndoRecordObjects();

			Undo.PerformUndo();
			Assert.That(mesh.enabled, Is.True, "undo re-enables the component");
			Assert.That(mr.enabled, Is.True, "undo shows the mesh again");

			Undo.PerformRedo();
			Assert.That(mesh.enabled, Is.False, "redo disables the component");
			Assert.That(mr.enabled, Is.False, "redo hides the mesh");
		}

		[Test]
		public void ShouldLeaveTheRendererAloneWhenTheGameObjectIsDeactivated()
		{
			var mr = _go.AddComponent<MeshRenderer>();
			_go.AddComponent<PrimitiveMeshComponent>();

			_go.SetActive(false);
			Assert.That(mr.enabled, Is.True);

			_go.SetActive(true);
			Assert.That(mr.enabled, Is.True);
		}

		[Test]
		public void ShouldShowARendererTheOldCodeHidOnAnInactiveGameObjectWhenActivated()
		{
			// saved by an older version: GameObject inactive, component on, renderer turned off by
			// the old OnDisable, no _hidRenderer and no _rendererStateVersion
			var mr = _go.AddComponent<MeshRenderer>();
			var mesh = _go.AddComponent<PrimitiveMeshComponent>();
			_go.SetActive(false);
			mr.enabled = false;
			var state = new SerializedObject(mesh);
			state.FindProperty("_hidRenderer").boolValue = false;
			state.FindProperty("_rendererStateVersion").intValue = 0;
			state.ApplyModifiedPropertiesWithoutUndo();

			((IMeshRendererState)mesh).MigrateInactiveLegacyRenderer();   // what the editor does when the scene opens
			_go.SetActive(true);

			Assert.That(mr.enabled, Is.True);
		}

		[Test]
		public void ShouldKeepARendererTheAuthorHidFirstHiddenAfterADuplicate()
		{
			var mr = _go.AddComponent<MeshRenderer>();
			var mesh = _go.AddComponent<PrimitiveMeshComponent>();
			mr.enabled = false;    // the author hides the renderer
			mesh.enabled = false;  // then switches the component off
			var clone = Object.Instantiate(_go);   // Awake runs on the deserialized copy, as after a scene reload
			try {
				var cloneMesh = clone.GetComponent<PrimitiveMeshComponent>();
				var cloneRenderer = clone.GetComponent<MeshRenderer>();

				cloneMesh.enabled = true;

				Assert.That(cloneRenderer.enabled, Is.False);
			}
			finally {
				Object.DestroyImmediate(clone);
			}
		}

		[Test]
		public void ShouldKeepARendererTheAuthorHidOnAnInactiveGameObjectHidden()
		{
			var mr = _go.AddComponent<MeshRenderer>();
			var mesh = _go.AddComponent<PrimitiveMeshComponent>();
			mr.enabled = false;     // the author hides the renderer
			_go.SetActive(false);   // then deactivates the GameObject

			((IMeshRendererState)mesh).MigrateInactiveLegacyRenderer();   // what the editor does when the scene opens
			_go.SetActive(true);

			Assert.That(mr.enabled, Is.False);
		}

		[Test]
		public void ShouldExportAPrimitiveOnADeactivatedGameObjectAsHidden()
		{
			var primitive = _go.AddComponent<PrimitiveComponent>();
			var mr = _go.AddComponent<MeshRenderer>();
			_go.AddComponent<PrimitiveMeshComponent>();

			var shown = primitive.CopyDataTo(new PrimitiveData("Primitive", 0f, 0f), Array.Empty<string>(), Array.Empty<string>(), false);
			_go.SetActive(false);   // hidden through the GameObject checkbox: the renderer stays enabled
			var hidden = primitive.CopyDataTo(new PrimitiveData("Primitive", 0f, 0f), Array.Empty<string>(), Array.Empty<string>(), false);

			Assert.That(shown.IsVisible, Is.True);
			Assert.That(mr.enabled, Is.True);
			Assert.That(hidden.IsVisible, Is.False);
		}
	}
}
