// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	[Serializable]
	public struct RubberSpanBinding
	{
		public RubberGuideBinding StartSupport;
		public RubberGuideBinding EndSupport;

		public RubberSpanBinding(RubberGuideBinding startSupport,
			RubberGuideBinding endSupport)
		{
			StartSupport = startSupport;
			EndSupport = endSupport;
		}
	}

	public readonly struct ResolvedRubberSpan
	{
		public readonly int PathElementIndex;
		public readonly bool IsReversed;
		public readonly RubberPathElement Element;

		public ResolvedRubberSpan(int pathElementIndex, bool isReversed,
			RubberPathElement element)
		{
			PathElementIndex = pathElementIndex;
			IsReversed = isReversed;
			Element = element;
		}

		public float ToPathCoordinate(float authoredCoordinate01)
			=> IsReversed ? 1f - authoredCoordinate01 : authoredCoordinate01;
	}

	public static class RubberSpanResolver
	{
		private const float MinimumSpanLength = 1e-4f;

		public static bool TryResolve(RubberComponent rubber, RubberSpanBinding span,
			out ResolvedRubberSpan resolved, out string error)
		{
			resolved = default;
			if (!rubber) {
				error = "Select a guided rubber.";
				return false;
			}
			if (rubber.PathSource != RubberPathSource.Guides) {
				error = $"Rubber '{rubber.name}' is not guide-driven.";
				return false;
			}
			var collider = rubber.GetComponent<RubberColliderComponent>();
			if (!collider || collider.Mode != RubberColliderMode.Physical) {
				error = $"Rubber '{rubber.name}' must use the Physical collider mode.";
				return false;
			}
			if (!rubber.HasValidGuidedPath) {
				error = $"Rubber '{rubber.name}' needs a current valid autofit bake.";
				return false;
			}

			var startIndex = FindBinding(rubber, span.StartSupport);
			if (startIndex < 0) {
				error = "The start support no longer belongs to the rubber bake.";
				return false;
			}
			var endIndex = FindBinding(rubber, span.EndSupport);
			if (endIndex < 0) {
				error = "The end support no longer belongs to the rubber bake.";
				return false;
			}
			if (startIndex == endIndex) {
				error = "A slingshot span needs two different endpoint supports.";
				return false;
			}

			var matchCount = 0;
			for (var i = 0; i < rubber.BakedPath.Count; i++) {
				var element = rubber.BakedPath[i];
				if (element.Type != RubberPathElementType.FreeSpan) {
					continue;
				}
				var forward = element.StartBindingIndex == startIndex
					&& element.EndBindingIndex == endIndex;
				var reversed = element.StartBindingIndex == endIndex
					&& element.EndBindingIndex == startIndex;
				if (!forward && !reversed) {
					continue;
				}
				if (element.Length <= MinimumSpanLength
					|| math.distancesq(element.Start, element.End)
						<= MinimumSpanLength * MinimumSpanLength) {
					error = "The bound free span is degenerate.";
					return false;
				}
				resolved = new ResolvedRubberSpan(i, reversed, element);
				matchCount++;
			}

			if (matchCount == 1) {
				error = null;
				return true;
			}
			error = matchCount == 0
				? "The endpoint supports do not identify a current free span."
				: "The endpoint supports identify more than one free span.";
			resolved = default;
			return false;
		}

		private static int FindBinding(RubberComponent rubber, RubberGuideBinding endpoint)
		{
			if (!endpoint.Guide || endpoint.SlotId.IsEmpty
				|| !endpoint.Guide.TryGetSlot(endpoint.SlotId, out _)) {
				return -1;
			}
			for (var i = 0; i < rubber.GuideBindings.Count; i++) {
				var candidate = rubber.GuideBindings[i];
				if (candidate.Guide == endpoint.Guide && candidate.SlotId == endpoint.SlotId) {
					return i;
				}
			}
			return -1;
		}
	}
}
