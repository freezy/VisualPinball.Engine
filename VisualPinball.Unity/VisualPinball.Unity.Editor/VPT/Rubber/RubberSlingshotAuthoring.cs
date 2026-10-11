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

using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// What the rubber slingshot wizard, the rubber's scene handles and the slingshot
	/// inspector share: which spans of a rubber its slingshot can bind to, how the
	/// slingshot is added or re-bound, its slingshot wall, and its arm. All entry points
	/// go through here so they can never disagree about a span.
	/// </summary>
	/// <remarks>
	/// A rubber slingshot is a component of its rubber, and a rubber has at most one.
	/// </remarks>
	[InitializeOnLoad]
	public static class RubberSlingshotAuthoring
	{
		static RubberSlingshotAuthoring()
		{
			RubberAutofit.Baked -= OnRubberBaked;
			RubberAutofit.Baked += OnRubberBaked;
		}

		[System.NonSerialized] private static bool _refittingWall;

		/// <summary>
		/// A rubber's path changed: its slingshot's wall follows, so a moved guide or a
		/// rounder span never leaves the wall behind the cord. The wall is derived from the
		/// path the way the bake is, so the refit records no undo of its own; the commands
		/// that rebake record the rubber's whole hierarchy, the wall included. Not while
		/// the game runs, where the colliders are already built.
		/// </summary>
		private static void OnRubberBaked(RubberComponent rubber)
		{
			if (_refittingWall || EditorApplication.isPlayingOrWillChangePlaymode) {
				return;
			}
			_refittingWall = true;
			try {
				RefitSlingshotWallAfterBake(rubber, false);
			} finally {
				_refittingWall = false;
			}
		}

		/// <summary>A free span the rubber's slingshot can be bound to, or already is.</summary>
		public readonly struct SpanCandidate
		{
			public readonly RubberSpanBinding Binding;
			public readonly int PathElementIndex;
			public readonly RubberPathElement Element;

			/// <summary>The rubber's slingshot when it is bound to this span, or null.</summary>
			public readonly RubberSlingshotComponent Existing;

			public SpanCandidate(RubberSpanBinding binding, int pathElementIndex,
				RubberPathElement element, RubberSlingshotComponent existing)
			{
				Binding = binding;
				PathElementIndex = pathElementIndex;
				Element = element;
				Existing = existing;
			}

			public bool IsBound => Existing;
		}

		/// <summary>A slingshot binds by guide pair, so the rubber needs a current guided path.</summary>
		public static bool IsEligibleRubber(RubberComponent rubber)
			=> rubber && (rubber.HasValidGuidedPath || RubberAutofit.EnsureCurrent(rubber, out _));

		/// <summary>The rubber's slingshot, or null.</summary>
		public static RubberSlingshotComponent SlingshotOf(RubberComponent rubber)
			=> rubber ? rubber.GetComponent<RubberSlingshotComponent>() : null;

		/// <summary>
		/// Every free span of the rubber whose guide pair identifies it unambiguously, in
		/// path order, marking the one the rubber's slingshot is bound to. A rubber on
		/// exactly two posts has two spans between the same guides and yields nothing.
		/// </summary>
		public static List<SpanCandidate> FindSpans(RubberComponent rubber)
		{
			var candidates = new List<SpanCandidate>();
			if (!IsEligibleRubber(rubber)) {
				return candidates;
			}
			var slingshot = SlingshotOf(rubber);
			var boundIndex = slingshot && slingshot.TryResolveSpan(out var resolved, out _)
				? resolved.PathElementIndex
				: -1;
			var freeSpans = rubber.BakedPath
				.Select((element, index) => (element, index))
				.Where(item => item.element.Type == RubberPathElementType.FreeSpan)
				.ToArray();
			foreach (var (span, pathIndex) in freeSpans) {
				var duplicateCount = freeSpans.Count(other =>
					(other.element.StartBindingIndex == span.StartBindingIndex
						&& other.element.EndBindingIndex == span.EndBindingIndex)
					|| (other.element.StartBindingIndex == span.EndBindingIndex
						&& other.element.EndBindingIndex == span.StartBindingIndex));
				if (duplicateCount != 1) {
					continue;
				}
				candidates.Add(new SpanCandidate(
					new RubberSpanBinding(
						rubber.GuideBindings[span.StartBindingIndex],
						rubber.GuideBindings[span.EndBindingIndex]),
					pathIndex, span, pathIndex == boundIndex ? slingshot : null));
			}
			return candidates;
		}

		/// <summary>
		/// The spans a new slingshot could be added on: all of <see cref="FindSpans"/>
		/// while the rubber has no slingshot, none once it has one.
		/// </summary>
		public static List<SpanCandidate> FindUnboundSpans(RubberComponent rubber)
			=> SlingshotOf(rubber) ? new List<SpanCandidate>() : FindSpans(rubber);

		/// <summary>
		/// What the rubber lacks before a slingshot can be added to it, as one sentence,
		/// or null when it is ready.
		/// </summary>
		public static string DescribeMissing(RubberComponent rubber)
		{
			if (!rubber) {
				return "Select a rubber.";
			}
			if (!IsEligibleRubber(rubber)) {
				return "Choose a rubber bound to its guides.";
			}
			return null;
		}

		public static string Label(RubberSpanBinding span)
			=> $"{Label(span.StartSupport)} -> {Label(span.EndSupport)}";

		private static string Label(RubberGuideBinding binding)
		{
			if (!binding.Guide || !binding.Guide.TryGetSlot(binding.SlotId, out var slot)) {
				return "Missing support";
			}
			return $"{binding.Guide.name}/{slot.DisplayName}";
		}

		public static bool SameBinding(RubberSpanBinding left, RubberSpanBinding right)
		{
			return (SameSupport(left.StartSupport, right.StartSupport)
					&& SameSupport(left.EndSupport, right.EndSupport))
				|| (SameSupport(left.StartSupport, right.EndSupport)
					&& SameSupport(left.EndSupport, right.StartSupport));
		}

		private static bool SameSupport(RubberGuideBinding left, RubberGuideBinding right)
			=> left.Guide == right.Guide && left.SlotId == right.SlotId;

		/// <summary>
		/// Adds a slingshot to the rubber, bound to the span, with its slingshot wall in
		/// front of the span and the defaults written explicitly so it does not depend on
		/// the field initializers. A rubber that already has a slingshot keeps it and gets
		/// it bound to the span instead. Registered with undo.
		/// </summary>
		public static RubberSlingshotComponent Add(RubberComponent rubber,
			RubberSpanBinding span)
		{
			var existing = SlingshotOf(rubber);
			if (existing) {
				Bind(existing, span);
				return existing;
			}
			EnsureDeformationRenderer(rubber);
			var component = Undo.AddComponent<RubberSlingshotComponent>(rubber.gameObject);
			component.Span = span;
			component.SelfWired = RubberSlingshotComponent.DefaultSelfWired;
			component.SelfWirePulseMs = RubberSlingshotComponent.DefaultSelfWirePulseMs;
			EditorUtility.SetDirty(component);
			if (!TryFitSlingshotWall(component, out var error)) {
				Debug.LogWarning($"Rubber slingshot '{rubber.name}' has no slingshot wall: {error}", component);
			}
			return component;
		}

		/// <summary>
		/// The renderer that animates the rubber's cord, added when the rubber lacks one.
		/// Registered with undo.
		/// </summary>
		public static RubberDeformationRenderer EnsureDeformationRenderer(RubberComponent rubber)
		{
			if (!rubber) {
				return null;
			}
			return rubber.TryGetComponent<RubberDeformationRenderer>(out var renderer)
				? renderer
				: Undo.AddComponent<RubberDeformationRenderer>(rubber.gameObject);
		}

		/// <summary>Arm stroke out, in seconds; a slingshot arm hits within one coil pulse.</summary>
		internal const float ArmActivationDuration = 0.012f;

		/// <summary>How long the arm stays out before it falls back, in seconds.</summary>
		internal const float ArmHoldDuration = 0.015f;

		/// <summary>Arm stroke back, in seconds.</summary>
		internal const float ArmReleaseDuration = 0.05f;

		/// <summary>
		/// Makes the arm swing with the slingshot's coil: the arm gets a One Shot Motion
		/// Controller, which the slingshot feeds its coil, and a Motion Transform that
		/// turns the arm about the hinge through its own origin, parallel to the span,
		/// by the angle that carries the arm's tip from its rest clearance to its full
		/// travel past the cord. Run it again after changing the arm's contact position,
		/// clearance or travel. Registered with undo.
		/// </summary>
		public static bool TrySetUpArm(RubberSlingshotComponent slingshot, Transform arm,
			out string error)
		{
			if (!slingshot || !arm) {
				error = "Choose the arm's game object.";
				return false;
			}
			if (!TryComputeArmSwing(slingshot, arm, out var rotationOffset, out error)) {
				return false;
			}
			var undoGroup = Undo.GetCurrentGroup();
			Undo.SetCurrentGroupName("Set Up Slingshot Arm");

			if (!arm.TryGetComponent<MotionControllerComponent>(out var controller)) {
				controller = Undo.AddComponent<MotionControllerComponent>(arm.gameObject);
			} else {
				Undo.RecordObject(controller, "Set Up Slingshot Arm");
			}
			controller.CoilMode = MotionCoilMode.OneShot;
			controller.InitialPosition = 0f;
			controller.ActivationDuration = ArmActivationDuration;
			controller.OneShotHoldDuration = ArmHoldDuration;
			controller.ReleaseDuration = ArmReleaseDuration;
			controller.ReleaseDelay = 0f;
			controller.ActivationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
			controller.ReleaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

			if (!arm.TryGetComponent<MotionTransformComponent>(out var follower)) {
				follower = Undo.AddComponent<MotionTransformComponent>(arm.gameObject);
			} else {
				Undo.RecordObject(follower, "Set Up Slingshot Arm");
			}
			follower._emitter = controller;
			follower.AnimatePosition = false;
			follower.AnimateRotation = true;
			follower.RotationOffset = rotationOffset;
			follower.InputMin = 0f;
			follower.InputMax = 1f;
			follower.Reverse = false;
			follower.ResponseCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

			Undo.RecordObject(slingshot, "Set Up Slingshot Arm");
			slingshot.MotionController = controller;

			foreach (var target in new Object[] { controller, follower, slingshot }) {
				EditorUtility.SetDirty(target);
				PrefabUtility.RecordPrefabInstancePropertyModifications(target);
			}
			Undo.CollapseUndoOperations(undoGroup);
			return true;
		}

		/// <summary>
		/// The local Euler rotation that swings the arm from rest to fired. The hinge runs
		/// through the arm's origin along the span; the angle is the one between the
		/// tip's rest point, the arm's rest clearance inside the cord, and its fired point,
		/// the arm's travel past the cord, seen along the hinge.
		/// </summary>
		internal static bool TryComputeArmSwing(RubberSlingshotComponent slingshot,
			Transform arm, out Vector3 rotationOffset, out string error)
		{
			rotationOffset = Vector3.zero;
			var rubber = slingshot.Rubber;
			if (!rubber) {
				error = "The slingshot has no rubber.";
				return false;
			}
			if (!slingshot.TryResolveSpan(out var resolved, out error)) {
				return false;
			}
			var contact01 = Mathf.Clamp01(slingshot.ArmContactPosition01);
			var contact = resolved.PointAt(contact01);
			var tangent = resolved.TangentAt(contact01);
			var inward = resolved.InwardAt(contact01);

			var hinge = ToWorld(rubber, contact + tangent) - ToWorld(rubber, contact);
			var tipRest = ToWorld(rubber, contact + inward * slingshot.ArmRestClearance);
			var tipFired = ToWorld(rubber,
				contact - inward * (slingshot.ArmTipTravel - slingshot.ArmRestClearance));
			if (hinge.sqrMagnitude < 1e-12f) {
				error = "The bound span has no direction at the arm's contact position.";
				return false;
			}
			hinge.Normalize();
			var restArm = Vector3.ProjectOnPlane(tipRest - arm.position, hinge);
			var firedArm = Vector3.ProjectOnPlane(tipFired - arm.position, hinge);
			// an arm whose origin lies on the cord has no lever to swing it by
			var travel = (tipFired - tipRest).magnitude;
			if (restArm.magnitude < travel || firedArm.magnitude < travel) {
				error = $"The origin of '{arm.name}' must be the arm's pivot, below the playfield, but it lies on the cord.";
				return false;
			}
			var angle = Vector3.SignedAngle(restArm, firedArm, hinge);
			var localHinge = Quaternion.Inverse(arm.rotation) * hinge;
			var euler = Quaternion.AngleAxis(angle, localHinge).eulerAngles;
			rotationOffset = new Vector3(Mathf.DeltaAngle(0f, euler.x),
				Mathf.DeltaAngle(0f, euler.y), Mathf.DeltaAngle(0f, euler.z));
			error = null;
			return true;
		}

		private static Vector3 ToWorld(RubberComponent rubber, float2 point)
			=> rubber.BakeFrameToLocal.MultiplyPoint3x4(new Vector3(point.x, point.y, 0f))
				.TranslateToWorld(rubber.transform);

		/// <summary>
		/// Binds the slingshot to another span of its rubber and moves its slingshot wall
		/// there. Registered with undo.
		/// </summary>
		public static void Bind(RubberSlingshotComponent slingshot, RubberSpanBinding span)
		{
			Undo.RecordObject(slingshot, "Bind Slingshot Span");
			slingshot.Span = span;
			EditorUtility.SetDirty(slingshot);
			PrefabUtility.RecordPrefabInstancePropertyModifications(slingshot);
			if (!TryFitSlingshotWall(slingshot, out var error)) {
				Debug.LogWarning($"Rubber slingshot '{slingshot.name}' has no slingshot wall: {error}", slingshot);
			}
		}

		/// <summary>
		/// What the authoring names a slingshot wall after its rubber. Table items are
		/// looked up by name, so the name is made unique across the table.
		/// </summary>
		public const string SlingshotWallSuffix = " Slingshot Wall";

		/// <summary>
		/// How far the wall's slingshot edge stands in front of the cord, in VPX units, so
		/// the ball meets the wall before the rubber's own collider.
		/// </summary>
		public const float SlingshotWallGap = 0.5f;

		/// <summary>How finely a rounded span is sampled for its bulge.</summary>
		private const int SlingshotWallSamples = 32;

		/// <summary>
		/// Visual Pinball's rubber physics, for the wall of a rubber whose collider has
		/// no physics of its own: no material, no elasticity.
		/// </summary>
		internal const float DefaultWallElasticity = 0.8f;
		internal const float DefaultWallElasticityFalloff = 0.3f;
		internal const float DefaultWallFriction = 0.6f;
		internal const float DefaultWallScatter = 5f;

		/// <summary>
		/// Puts the slingshot's wall in front of its span: an invisible Surface under the
		/// rubber whose front edge, one slingshot segment, runs along the cord's outer
		/// side and whose back edge lies inside the cord, from the playfield up to the top
		/// of the cord. A rounded span's front edge is pushed out to clear its bulge, so
		/// the ball always meets the wall before the cord. Its physics are the rubber's,
		/// or Visual Pinball's rubber defaults when the rubber's collider has none, and it
		/// fires the slingshot event that closes the slingshot's switch. A missing wall is
		/// created; the slingshot's wall is refitted in place. Registered with undo unless
		/// <paramref name="recordUndo"/> is off, for a refit that follows a rubber's own
		/// rebake, which records its undo itself or none at all.
		/// </summary>
		public static bool TryFitSlingshotWall(RubberSlingshotComponent slingshot,
			out string error, bool recordUndo = true)
		{
			var rubber = slingshot ? slingshot.Rubber : null;
			if (!rubber) {
				error = "The slingshot has no rubber.";
				return false;
			}
			if (!slingshot.TryResolveSpan(out var resolved, out error)) {
				return false;
			}
			var undoGroup = Undo.GetCurrentGroup();
			var wall = slingshot.SlingshotWall;
			if (!wall) {
				var wallObject = new GameObject(UniqueTableItemName(rubber, rubber.name + SlingshotWallSuffix));
				Undo.RegisterCreatedObjectUndo(wallObject, "Create Slingshot Wall");
				Undo.SetTransformParent(wallObject.transform, rubber.transform, "Create Slingshot Wall");
				wallObject.transform.localPosition = Vector3.zero;
				wallObject.transform.localRotation = Quaternion.identity;
				wallObject.transform.localScale = Vector3.one;
				wall = Undo.AddComponent<SurfaceComponent>(wallObject);
				Undo.AddComponent<SurfaceColliderComponent>(wallObject);
				Undo.RecordObject(slingshot, "Create Slingshot Wall");
				slingshot.SlingshotWall = wall;
			} else if (recordUndo) {
				var spline = wall.DragPointSpline;
				Undo.RecordObjects(new Object[] { wall, spline, spline.Container }, "Fit Slingshot Wall");
			}

			// the span in the wall's own frame, in VPX units. One straight slingshot
			// segment: a chain of them would leave passive joints between its segments.
			var cordRadius = rubber.Thickness * 0.5f;
			var start3 = WallPoint(rubber, wall, resolved.PointAt(0f));
			var end3 = WallPoint(rubber, wall, resolved.PointAt(1f));
			var start = new float2(start3.x, start3.y);
			var end = new float2(end3.x, end3.y);
			var chord = math.normalizesafe(end - start);
			var middle3 = WallPoint(rubber, wall, resolved.PointAt(0.5f));
			var inside3 = WallPoint(rubber, wall, resolved.PointAt(0.5f) + resolved.InwardAt(0.5f));
			var inward = new float2(inside3.x - middle3.x, inside3.y - middle3.y);
			// the inward normal of the chord itself, on the span's inner side
			var chordInward = new float2(-chord.y, chord.x);
			if (math.dot(chordInward, inward) < 0f) {
				chordInward = -chordInward;
			}
			if (math.lengthsq(chord) < 0.5f || math.lengthsq(inward) < 1e-12f) {
				error = "The span has no direction in the wall's plane.";
				return false;
			}
			// how far a rounded span bulges out past its chord
			var bulge = 0f;
			for (var i = 1; i < SlingshotWallSamples; i++) {
				var point = WallPoint(rubber, wall, resolved.PointAt(i / (float)SlingshotWallSamples));
				bulge = math.max(bulge, math.dot(start - new float2(point.x, point.y), chordInward));
			}
			var front = -chordInward * (bulge + cordRadius + SlingshotWallGap);
			var back = chordInward * cordRadius;
			var frontStart = start + front;
			var frontEnd = end + front;
			// the slingshot segment's normal is its direction turned left; it must face
			// the ball, out of the rubber
			var direction = frontEnd - frontStart;
			if (math.dot(new float2(-direction.y, direction.x), -chordInward) < 0f) {
				(frontStart, frontEnd) = (frontEnd, frontStart);
			}
			var backEnd = frontEnd - front + back;
			var backStart = frontStart - front + back;
			wall.DragPoints = new[] {
				new DragPointData(frontStart.x, frontStart.y) { IsSlingshot = true },
				new DragPointData(frontEnd.x, frontEnd.y),
				new DragPointData(backEnd.x, backEnd.y),
				new DragPointData(backStart.x, backStart.y),
			};
			wall.HeightBottom = PlayfieldHeight(wall, middle3.z - cordRadius * 4f);
			wall.HeightTop = middle3.z + cordRadius;

			var collider = wall.GetComponent<SurfaceColliderComponent>();
			if (collider) {
				if (recordUndo) {
					Undo.RecordObject(collider, "Fit Slingshot Wall");
				}
				collider.HitEvent = true;
				// the slingshot's kick is the wall's bounce of the boosted ball, so a wall
				// without elasticity swallows it
				var rubberCollider = rubber.GetComponent<RubberColliderComponent>();
				if (rubberCollider && (rubberCollider.PhysicsMaterial || rubberCollider.Elasticity > 0f)) {
					collider.OverwritePhysics = rubberCollider.OverwritePhysics;
					collider.Elasticity = rubberCollider.Elasticity;
					collider.ElasticityFalloff = rubberCollider.ElasticityFalloff;
					collider.Friction = rubberCollider.Friction;
					collider.Scatter = rubberCollider.Scatter;
					collider.PhysicsMaterial = rubberCollider.PhysicsMaterial;
				} else {
					collider.OverwritePhysics = true;
					collider.Elasticity = DefaultWallElasticity;
					collider.ElasticityFalloff = DefaultWallElasticityFalloff;
					collider.Friction = DefaultWallFriction;
					collider.Scatter = DefaultWallScatter;
					collider.PhysicsMaterial = null;
				}
				EditorUtility.SetDirty(collider);
				PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
			}
			var wallSpline = wall.DragPointSpline;
			foreach (var target in new Object[] { wall, wallSpline, wallSpline.Container, slingshot }) {
				EditorUtility.SetDirty(target);
				PrefabUtility.RecordPrefabInstancePropertyModifications(target);
			}
			Undo.CollapseUndoOperations(undoGroup);
			error = null;
			return true;
		}

		/// <summary>
		/// Refits the wall of the rubber's slingshot after the rubber's path changed, so a
		/// guide moved or a rounder span never leaves the wall behind the cord. A rubber
		/// without a slingshot, or a slingshot without a wall, is left alone.
		/// </summary>
		public static void RefitSlingshotWallAfterBake(RubberComponent rubber, bool recordUndo)
		{
			var slingshot = SlingshotOf(rubber);
			if (!slingshot || !slingshot.SlingshotWall) {
				return;
			}
			if (!TryFitSlingshotWall(slingshot, out var error, recordUndo)) {
				Debug.LogWarning($"Could not refit the slingshot wall of '{rubber.name}': {error}", slingshot);
			}
		}

		/// <summary>
		/// The name, or the name with a number appended, that no other item of the
		/// rubber's table carries.
		/// </summary>
		private static string UniqueTableItemName(RubberComponent rubber, string name)
		{
			var table = rubber.GetComponentInParent<TableComponent>();
			var root = table ? table.transform : rubber.transform.root;
			var taken = new HashSet<string>(root.GetComponentsInChildren<IMainComponent>(true)
				.OfType<MonoBehaviour>()
				.Select(component => component.name));
			if (!taken.Contains(name)) {
				return name;
			}
			for (var i = 2; ; i++) {
				var candidate = $"{name} {i}";
				if (!taken.Contains(candidate)) {
					return candidate;
				}
			}
		}

		/// <summary>A point of the rubber's bake plane in the wall's local frame, in VPX units.</summary>
		private static Vector3 WallPoint(RubberComponent rubber, SurfaceComponent wall, float2 point)
			=> ToWorld(rubber, point).TranslateToVpx(wall.transform);

		/// <summary>
		/// The playfield's height in the wall's local frame, in VPX units, so the wall
		/// stands on the playfield; the fallback without a playfield above it.
		/// </summary>
		private static float PlayfieldHeight(SurfaceComponent wall, float fallback)
		{
			var playfield = wall.GetComponentInParent<PlayfieldComponent>();
			if (!playfield) {
				return fallback;
			}
			var onPlayfield = playfield.transform.InverseTransformPoint(wall.transform.position);
			onPlayfield.y = 0f;
			return playfield.transform.TransformPoint(onPlayfield).TranslateToVpx(wall.transform).z;
		}
	}
}
