// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	public readonly struct RubberBakeStatus
	{
		public readonly bool IsValid;
		public readonly bool IsStale;
		public readonly string Message;

		public RubberBakeStatus(bool isValid, bool isStale, string message)
		{
			IsValid = isValid;
			IsStale = isStale;
			Message = message;
		}
	}

	public static class RubberAutofit
	{
		public const uint CurrentBakeVersion = 4;
		public const float SolverEpsilonVpx = 1e-4f;
		public const float RenderToleranceVpx = 0.05f;

		public static bool TryBake(RubberComponent rubber, out RubberAutofitResult fit,
			out string error)
		{
			fit = null;
			error = null;
			var resolution = RubberGuideResolver.Resolve(rubber);
			if (!resolution.IsValid) {
				error = resolution.Error;
				return false;
			}

			fit = RubberCircleHullSolver.Solve(resolution.Circles,
				rubber.Thickness * 0.5f, SolverEpsilonVpx, rubber.Roundness);
			if (!fit.IsValid) {
				error = fit.Error;
				return false;
			}
			if (!RubberPathSplineBaker.TryCreateDragPoints(fit.Elements,
				resolution.Plane.BakeFrameToLocal, RenderToleranceVpx, out var dragPoints,
				out _, out error)) {
				return false;
			}

			rubber.ApplyGuidedBake(fit.Elements, dragPoints,
				resolution.Plane.BakeFrameToLocal, resolution.InputHash, CurrentBakeVersion);
			rubber.RebuildMeshes();
			return true;
		}

		public static bool TryConvertToGuides(RubberComponent rubber,
			IEnumerable<RubberGuideBinding> bindings, out RubberAutofitResult fit,
			out string error)
		{
			fit = null;
			if (!rubber) {
				error = "A rubber component is required.";
				return false;
			}
			if (rubber.PathSource != RubberPathSource.Spline) {
				error = "Only a manual spline rubber can use the transactional conversion workflow.";
				return false;
			}
			return TryApplyGuideBindings(rubber, bindings, out fit, out error);
		}

		public static bool TryReplaceGuideBindings(RubberComponent rubber,
			IEnumerable<RubberGuideBinding> bindings, out RubberAutofitResult fit,
			out string error)
		{
			fit = null;
			if (!rubber) {
				error = "A rubber component is required.";
				return false;
			}
			if (rubber.PathSource != RubberPathSource.Guides) {
				error = "Only a guided rubber can replace its guide bindings.";
				return false;
			}
			return TryApplyGuideBindings(rubber, bindings, out fit, out error);
		}

		public static bool TryReplaceOrRepairGuideBindings(RubberComponent rubber,
			IEnumerable<RubberGuideBinding> bindings, out bool baked,
			out RubberAutofitResult fit, out string error)
		{
			var replacements = bindings?.ToArray() ?? Array.Empty<RubberGuideBinding>();
			var brokenBindingCount = CountBrokenBindings(rubber?.GuideBindings);
			baked = TryReplaceGuideBindings(rubber, replacements, out fit, out error);
			if (baked) {
				return true;
			}
			if (!rubber || rubber.PathSource != RubberPathSource.Guides
				|| brokenBindingCount == 0
				|| CountBrokenBindings(replacements) >= brokenBindingCount) {
				return false;
			}

			// A branch or prefab swap can invalidate several slot IDs at once. Keep each
			// valid repair even while another stale binding still blocks the rebake.
			// SetGuideBindings deliberately leaves the previous exact and sampled paths
			// intact, but invalidates their version/hash until all bindings can bake.
			rubber.SetGuideBindings(replacements);
			return true;
		}

		private static bool TryApplyGuideBindings(RubberComponent rubber,
			IEnumerable<RubberGuideBinding> bindings, out RubberAutofitResult fit,
			out string error)
		{
			var previousSource = rubber.PathSource;
			var previousBindings = rubber.GuideBindings.ToArray();
			var previousPath = rubber.BakedPath.ToArray();
			var previousVersion = rubber.BakeVersion;
			var previousHash = rubber.BakeInputHash;
			var previousFrame = rubber.BakeFrameToLocal;
			var previousRestLength = rubber.RestLength;
			var previousRoundness = rubber.Roundness;
			rubber.SetGuideBindings(bindings);
			if (TryBake(rubber, out fit, out error)) {
				return true;
			}

			rubber.RestorePackedState(previousSource, previousBindings,
				previousPath, previousVersion, previousHash, previousFrame,
				previousRestLength, previousRoundness);
			return false;
		}

		private static int CountBrokenBindings(IEnumerable<RubberGuideBinding> bindings)
		{
			return bindings?.Count(binding => !binding.Guide || binding.SlotId.IsEmpty
				|| !binding.Guide.TryGetSlot(binding.SlotId, out _)) ?? 0;
		}

		/// <summary>
		/// Refits the guided path when its bake is stale or missing, so the bake stored
		/// on the rubber is a cache nobody has to refresh by hand. Returns true when the
		/// bake is valid afterwards. A rubber whose guides cannot be resolved keeps what
		/// it has and reports why.
		/// </summary>
		public static bool EnsureCurrent(RubberComponent rubber, out string error)
		{
			error = null;
			if (!rubber || rubber.PathSource != RubberPathSource.Guides) {
				error = "The rubber is not guide-driven.";
				return false;
			}
			var status = GetStatus(rubber);
			if (status.IsValid) {
				return true;
			}
			if (!status.IsStale && rubber.BakedPath.Count > 0) {
				error = status.Message;
				return false;
			}
			if (!TryBake(rubber, out _, out error)) {
				return false;
			}
#if UNITY_EDITOR
			if (!UnityEngine.Application.isPlaying) {
				UnityEditor.EditorUtility.SetDirty(rubber);
			}
#endif
			return true;
		}

		public static RubberBakeStatus GetStatus(RubberComponent rubber)
		{
			if (!rubber || rubber.PathSource != RubberPathSource.Guides) {
				return new RubberBakeStatus(false, false, "Spline path is authoritative.");
			}
			if (rubber.BakedPath.Count == 0) {
				return new RubberBakeStatus(false, false, "No guided path has been baked.");
			}
			if (rubber.BakeVersion != CurrentBakeVersion) {
				return new RubberBakeStatus(false, true,
					$"Bake version {rubber.BakeVersion} is stale; expected {CurrentBakeVersion}.");
			}
			var resolution = RubberGuideResolver.Resolve(rubber);
			if (!resolution.IsValid) {
				return new RubberBakeStatus(false, false, resolution.Error);
			}
			if (resolution.InputHash != rubber.BakeInputHash) {
				return new RubberBakeStatus(false, true, "Guide geometry changed after the last bake.");
			}
			if (!rubber.TryGetCachedPathValidation(out var pathIsValid,
				out var validationError)) {
				pathIsValid = RubberPathValidator.TryValidate(rubber.BakedPath,
					SolverEpsilonVpx, out validationError);
				rubber.CachePathValidation(pathIsValid, validationError);
			}
			if (!pathIsValid) {
				return new RubberBakeStatus(false, false, validationError);
			}
			return new RubberBakeStatus(true, false, "Guided bake is current.");
		}
	}
}
