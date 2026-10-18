// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VisualPinball.Unity.Editor;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class MechanismAnimationPackagingTests
	{
		[UnityTest]
		public IEnumerator RuntimePackageRestoresDropAnimationAndEventRotations()
		{
			var packagePath = Path.Combine(Path.GetTempPath(), $"vpe-mechanism-animation-{Guid.NewGuid():N}.vpe");
			var source = new GameObject("Table");
			GameObject imported = null;
			try {
				source.AddComponent<TableComponent>();
				// A static root writes item data that sets the root active on import.
				source.isStatic = true;
				source.transform.rotation = Quaternion.Euler(8f, 12f, 3f);
				var cannon = new GameObject("Cannon");
				cannon.transform.SetParent(source.transform, false);
				var emitter = cannon.AddComponent<MechanismAnimationPackagingEmitter>();
				var rotation = cannon.AddComponent<EventRotateComponent>();
				rotation.EventName = "Mech Position";
				rotation.Axis = Vector3.up;
				rotation.Factor = -1f / 3f;
				rotation.Offset = 5f;
				cannon.transform.localRotation = Quaternion.Euler(0f, 17f, 0f);

				var globalCannon = new GameObject("Global Cannon");
				globalCannon.transform.SetParent(source.transform, false);
				globalCannon.AddComponent<MechanismAnimationPackagingEmitter>();
				var globalRotation = globalCannon.AddComponent<EventRotateComponent>();
				globalRotation.EventName = "Mech Position";
				globalRotation.Axis = Vector3.right;
				globalRotation.RotateGlobally = true;
				globalRotation.Factor = 0.5f;
				globalRotation.Offset = -7f;

				var target = new GameObject("Center Drop Target");
				target.transform.SetParent(source.transform, false);
				target.transform.localPosition = new Vector3(0f, 0.4f, 0f);
				var animation = target.AddComponent<DropTargetAnimationComponent>();
				// A sibling emitter cannot be recovered by searching the target's parents.
				animation._emitter = emitter;
				animation.RotationAngle = 4f;
				animation.RotationDuration = 0.12f;
				animation.DropDistance = 61f;
				animation.DropDelay = 0.08f;
				animation.DropDuration = 0.25f;
				animation.PullUpDuration = 0.45f;
				animation.RotationAnimationCurve = new AnimationCurve(new Keyframe(0f, 0f, 1f, 2f), new Keyframe(1f, 0f, 3f, 4f));
				animation.DropAnimationCurve = new AnimationCurve(new Keyframe(0f, 0f, 5f, 6f), new Keyframe(1f, 1f, 7f, 8f));
				animation.PullUpAnimationCurve = new AnimationCurve(new Keyframe(0f, 0f, 9f, 10f), new Keyframe(1f, 1f, 11f, 12f));
				animation.DropAnimationCurve.postWrapMode = WrapMode.ClampForever;

				new PackageWriter(source).WritePackageSync(packagePath);
				MechanismAnimationPackagingEmitter.UnpackedWhileActive = 0;
				var importTask = new RuntimePackageReader(packagePath).ImportIntoScene();
				while (!importTask.IsCompleted) { yield return null; }
				if (importTask.IsFaulted) { throw importTask.Exception.GetBaseException(); }
				imported = importTask.Result;
				// Restoring the root's item data must not wake the components unpacked after it.
				Assert.That(MechanismAnimationPackagingEmitter.UnpackedWhileActive, Is.Zero,
					"components must be restored while the table is inactive");
				Assert.That(imported.activeSelf, Is.True);

				var restored = imported.GetComponentInChildren<DropTargetAnimationComponent>(true);
				Assert.That(restored, Is.Not.Null, "the drop target's visual animation must survive export");
				var restoredCannon = imported.transform.Find("Cannon");
				var restoredEmitter = restoredCannon.GetComponent<MechanismAnimationPackagingEmitter>();
				Assert.That(restored._emitter, Is.SameAs(restoredEmitter));
				Assert.That(restored.RotationAngle, Is.EqualTo(4f));
				Assert.That(restored.RotationDuration, Is.EqualTo(0.12f));
				Assert.That(restored.DropDistance, Is.EqualTo(61f));
				Assert.That(restored.DropDelay, Is.EqualTo(0.08f));
				Assert.That(restored.DropDuration, Is.EqualTo(0.25f));
				Assert.That(restored.PullUpDuration, Is.EqualTo(0.45f));
				Assert.That(restored.RotationAnimationCurve.keys[0].outTangent, Is.EqualTo(2f));
				Assert.That(restored.DropAnimationCurve.keys[1].inTangent, Is.EqualTo(7f));
				Assert.That(restored.DropAnimationCurve.postWrapMode, Is.EqualTo(WrapMode.ClampForever));
				Assert.That(restored.PullUpAnimationCurve.keys[0].outTangent, Is.EqualTo(10f));

				// EditMode does not run these MonoBehaviour callbacks automatically.
				InvokeCallback(restored, "Awake");
				InvokeCallback(restored, "OnEnable");
				InvokeCallback(restored, "Start");
				restored.RotationDuration = 0f;
				restored.DropDelay = 0f;
				restored.DropDuration = 0f;
				restored.PullUpDuration = 0f;
				var initialY = restored.transform.localPosition.y;
				restoredEmitter.UpdateAnimationValue(true);
				InvokeCallback(restored, "Update");
				Assert.That(restored.transform.localPosition.y, Is.EqualTo(initialY - Physics.ScaleToWorld(61f)).Within(0.00001f));
				restoredEmitter.UpdateAnimationValue(false);
				InvokeCallback(restored, "Update");
				Assert.That(restored.transform.localPosition.y, Is.EqualTo(initialY).Within(0.00001f));
				InvokeCallback(restored, "OnDisable");

				VerifyRotation(restoredCannon, Vector3.up, false, -1f / 3f, 5f);
				VerifyRotation(imported.transform.Find("Global Cannon"), Vector3.right, true, 0.5f, -7f);
			} finally {
				Object.DestroyImmediate(source);
				if (imported) { Object.DestroyImmediate(imported); }
				File.Delete(packagePath);
			}
		}

		[Test]
		public void LegacyDropAnimationKeepsItsPackageType()
		{
			var root = new GameObject("Table");
			try {
				var refs = new PackagedRefs(root.transform);
				Assert.That(refs.GetType("DropTargetAnimation"), Is.EqualTo(typeof(DropTargetAnimationComponentLegacy)));
				Assert.That(refs.GetType("DropTargetVisualAnimation"), Is.EqualTo(typeof(DropTargetAnimationComponent)));
			} finally {
				Object.DestroyImmediate(root);
			}
		}

		private static void VerifyRotation(Transform cannon, Vector3 axis, bool global, float factor, float offset)
		{
			var restored = cannon.GetComponent<EventRotateComponent>();
			Assert.That(restored, Is.Not.Null, "the event-driven rotation must survive export");
			Assert.That(restored.EventName, Is.EqualTo("Mech Position"));
			Assert.That(restored.Axis, Is.EqualTo(axis));
			Assert.That(restored.RotateGlobally, Is.EqualTo(global));
			Assert.That(restored.Factor, Is.EqualTo(factor));
			Assert.That(restored.Offset, Is.EqualTo(offset));
			InvokeCallback(restored, "Awake");
			InvokeCallback(restored, "Start");
			var initial = global ? cannon.rotation : cannon.localRotation;
			var emitter = cannon.GetComponent<MechanismAnimationPackagingEmitter>();
			emitter.EmitRotation("Other Event", Mathf.PI / 2f);
			Assert.That(Quaternion.Angle(global ? cannon.rotation : cannon.localRotation, initial), Is.LessThan(0.01f));
			emitter.EmitRotation("Mech Position", Mathf.PI / 2f);
			var expected = Quaternion.AngleAxis(90f * factor + offset, axis) * initial;
			Assert.That(Quaternion.Angle(global ? cannon.rotation : cannon.localRotation, expected), Is.LessThan(0.01f));
		}

		private static void InvokeCallback(MonoBehaviour component, string methodName)
			=> component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(component, null);
	}

	[PackAs("MechanismAnimationPackagingTestEmitter")]
	public class MechanismAnimationPackagingEmitter : MonoBehaviour, IAnimationValueEmitter<bool>, IPinballEventEmitter, IPackable
	{
		public event Action<bool> OnAnimationValueChanged;
		public event EventHandler<PinballEventArgs> OnPinballEvent;
		public PinballEvent[] Events => new[] { new PinballEvent("Mech Position", PinballEventUnit.Radians) };
		public void UpdateAnimationValue(bool value) => OnAnimationValueChanged?.Invoke(value);
		public void EmitRotation(string name, float value) => OnPinballEvent?.Invoke(this, new PinballEventArgs(name, value, PinballEventUnit.Radians));
		public static int UnpackedWhileActive;
		public byte[] Pack() => PackageApi.Packer.Empty;
		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => null;
		public void Unpack(byte[] bytes) { if (gameObject.activeInHierarchy) UnpackedWhileActive++; }
		public void UnpackReferences(byte[] bytes, Transform root, PackagedRefs refs, PackagedFiles files) { if (gameObject.activeInHierarchy) UnpackedWhileActive++; }
	}
}
