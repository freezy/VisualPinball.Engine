// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

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
		public const uint CurrentBakeVersion = 1;
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
				rubber.Thickness * 0.5f, SolverEpsilonVpx);
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

			var previousBindings = rubber.GuideBindings.ToArray();
			var previousPath = rubber.BakedPath.ToArray();
			var previousVersion = rubber.BakeVersion;
			var previousHash = rubber.BakeInputHash;
			var previousFrame = rubber.BakeFrameToLocal;
			var previousRestLength = rubber.RestLength;
			rubber.SetGuideBindings(bindings);
			if (TryBake(rubber, out fit, out error)) {
				return true;
			}

			rubber.RestorePackedState(RubberPathSource.Spline, previousBindings,
				previousPath, previousVersion, previousHash, previousFrame,
				previousRestLength);
			return false;
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
			if (!RubberPathValidator.TryValidate(rubber.BakedPath,
				SolverEpsilonVpx, out var validationError)) {
				return new RubberBakeStatus(false, false, validationError);
			}
			return new RubberBakeStatus(true, false, "Guided bake is current.");
		}
	}
}
