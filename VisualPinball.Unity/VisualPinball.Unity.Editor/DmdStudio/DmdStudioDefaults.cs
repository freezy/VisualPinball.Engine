// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using UnityEditor;

namespace VisualPinball.Unity.Editor
{
	public static class DmdStudioDefaults
	{
		public static bool EnsureSampleStates(DmdProjectAsset project)
		{
			if (project == null) {
				throw new ArgumentNullException(nameof(project));
			}
			project.SampleStates ??= new List<DmdSampleState>();
			var changed = false;
			for (var players = 1; players <= 4; players++) {
				changed |= AddIfMissing(project, State($"{players} Player{(players == 1 ? string.Empty : "s")}",
					DmdParamValue.From("player", 1L), DmdParamValue.From("players", (long)players),
					DmdParamValue.From("score", 1234560L)));
			}
			changed |= AddIfMissing(project, State("Huge Score",
				DmdParamValue.From("score", 9_999_999_990L)));
			changed |= AddIfMissing(project, State("Expired Timer", DmdParamValue.From("timer", 0L)));
			changed |= AddIfMissing(project, State("Missing Text"));
			changed |= AddIfMissing(project, State("Empty Text", DmdParamValue.From("text", string.Empty)));
			if (changed) {
				EditorUtility.SetDirty(project);
			}
			return changed;
		}

		public static DmdParams ToParams(DmdSampleState state)
		{
			var parameters = new DmdParams();
			if (state?.Values == null) {
				return parameters;
			}
			foreach (var value in state.Values) {
				if (string.IsNullOrWhiteSpace(value.Name)) {
					continue;
				}
				switch (value.Type) {
					case DmdParamType.Integer:
						parameters.Set(value.Name, value.IntValue);
						break;
					case DmdParamType.Float:
						parameters.Set(value.Name, value.FloatValue);
						break;
					case DmdParamType.String:
						parameters.Set(value.Name, value.StringValue);
						break;
					case DmdParamType.Boolean:
						parameters.Set(value.Name, value.BoolValue);
						break;
				}
			}
			return parameters;
		}

		private static bool AddIfMissing(DmdProjectAsset project, DmdSampleState state)
		{
			foreach (var existing in project.SampleStates) {
				if (existing != null && string.Equals(existing.Name, state.Name, StringComparison.Ordinal)) {
					return false;
				}
			}
			project.SampleStates.Add(state);
			return true;
		}

		private static DmdSampleState State(string name, params DmdParamValue[] values)
		{
			return new DmdSampleState {
				Name = name,
				Values = new List<DmdParamValue>(values)
			};
		}
	}
}
