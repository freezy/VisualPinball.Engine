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
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// What the physical slingshot wizard and the rubber's scene handles share: which
	/// spans of a rubber its slingshot can bind to, what the rubber still lacks, and how
	/// the slingshot is added or re-bound. Both entry points go through here so they can
	/// never disagree about a span.
	/// </summary>
	/// <remarks>
	/// A physical slingshot is a component of its rubber, and a rubber has at most one.
	/// </remarks>
	public static class RubberSlingshotAuthoring
	{
		/// <summary>Where a default actuator asset is created when the project has none.</summary>
		public const string DefaultActuatorAssetPath = "Assets/Slingshot Actuator.asset";

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

		/// <summary>
		/// A slingshot binds by guide pair, so the rubber needs a current guided path and
		/// the Physical collider that the span records come from.
		/// </summary>
		public static bool IsEligibleRubber(RubberComponent rubber)
			=> rubber && rubber.TryGetComponent<RubberColliderComponent>(out var collider)
				&& collider.Mode == RubberColliderMode.Physical
				&& (rubber.HasValidGuidedPath || RubberAutofit.EnsureCurrent(rubber, out _));

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
		/// The first thing the rubber still lacks before its slingshot can work in play
		/// mode, as one sentence, or null when the rubber is ready. Adding the slingshot
		/// is allowed either way; the slingshot inspector repeats this.
		/// </summary>
		public static string DescribeMissing(RubberComponent rubber)
		{
			if (!rubber) {
				return "Select a rubber.";
			}
			if (!IsEligibleRubber(rubber)) {
				return "Choose a current guide-driven Physical rubber.";
			}
			if (!(rubber.RestLength > 0f)) {
				return "Set the rubber's rest length.";
			}
			var collider = rubber.GetComponent<RubberColliderComponent>();
			if (!collider.DeformationEnabled) {
				return "Enable the compliant rebound on the rubber collider.";
			}
			collider.EnsureBakesCurrent();
			if (!RubberSpanReboundEligibility.TryValidate(collider, out var reason)) {
				return $"Compliant rebound: {reason}";
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
		/// Adds a physical slingshot to the rubber, bound to the span, with the runtime
		/// defaults written explicitly so it does not depend on the field initializers.
		/// A rubber that already has a slingshot keeps it and gets it bound to the span
		/// instead. Registered with undo.
		/// </summary>
		public static RubberSlingshotComponent Add(RubberComponent rubber,
			RubberSpanBinding span, SlingshotActuatorAsset actuator)
		{
			var existing = SlingshotOf(rubber);
			if (existing) {
				Bind(existing, span);
				return existing;
			}
			var component = Undo.AddComponent<RubberSlingshotComponent>(rubber.gameObject);
			component.Span = span;
			component.Actuator = actuator;
			component.SelfWired = RubberSlingshotComponent.DefaultSelfWired;
			component.WireDelayMs = RubberSlingshotComponent.DefaultWireDelayMs;
			component.SelfWirePulseMs = RubberSlingshotComponent.DefaultSelfWirePulseMs;
			component.KickSpeed = RubberSlingshotComponent.DefaultKickSpeed;
			component.ArmHalfWidth01 = RubberSlingshotComponent.DefaultArmHalfWidth01;
			component.KickWindowMs = RubberSlingshotComponent.DefaultKickWindowMs;
			component.MinimumSwitchPulseMs = RubberSlingshotComponent.DefaultMinimumSwitchPulseMs;
			EditorUtility.SetDirty(component);
			return component;
		}

		/// <summary>Binds the slingshot to another span of its rubber. Registered with undo.</summary>
		public static void Bind(RubberSlingshotComponent slingshot, RubberSpanBinding span)
		{
			Undo.RecordObject(slingshot, "Bind Slingshot Span");
			slingshot.Span = span;
			EditorUtility.SetDirty(slingshot);
			PrefabUtility.RecordPrefabInstancePropertyModifications(slingshot);
		}

		/// <summary>
		/// The first actuator asset in the project, in asset path order, or null.
		/// </summary>
		public static SlingshotActuatorAsset FindActuator()
		{
			var path = AssetDatabase.FindAssets($"t:{nameof(SlingshotActuatorAsset)}")
				.Select(AssetDatabase.GUIDToAssetPath)
				.OrderBy(assetPath => assetPath, System.StringComparer.Ordinal)
				.FirstOrDefault();
			return path == null ? null : AssetDatabase.LoadAssetAtPath<SlingshotActuatorAsset>(path);
		}

		/// <summary>
		/// The project's actuator asset, created with the asset's defaults at
		/// <see cref="DefaultActuatorAssetPath"/> when there is none. The runtime does
		/// not consult the actuator yet but refuses a slingshot without one, so authoring
		/// must not stop at a missing asset.
		/// </summary>
		public static SlingshotActuatorAsset FindOrCreateActuator()
		{
			var existing = FindActuator();
			if (existing) {
				return existing;
			}
			var asset = ScriptableObject.CreateInstance<SlingshotActuatorAsset>();
			var path = AssetDatabase.GenerateUniqueAssetPath(DefaultActuatorAssetPath);
			AssetDatabase.CreateAsset(asset, path);
			AssetDatabase.SaveAssets();
			Debug.Log($"Created the default slingshot actuator asset at {path}.", asset);
			return asset;
		}
	}
}
