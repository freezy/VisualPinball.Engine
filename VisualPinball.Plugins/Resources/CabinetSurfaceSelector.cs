// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;

namespace VisualPinball.Plugins
{
	/// <summary>Deterministic ranking shared by desktop and scene consumers; callers filter controller/session/role.</summary>
	public static class CabinetSurfaceSelector
	{
		public static PluginResourceDescriptor? Choose(IEnumerable<PluginResourceDescriptor> candidates,
			IReadOnlyList<string> providers, Func<PluginResourceDescriptor, bool> eligible)
		{
			if (candidates == null) throw new ArgumentNullException(nameof(candidates));
			if (providers == null) throw new ArgumentNullException(nameof(providers));
			if (eligible == null) throw new ArgumentNullException(nameof(eligible));
			PluginResourceDescriptor? best = null;
			int bestRank = int.MaxValue;
			foreach (var candidate in candidates) {
				if (!eligible(candidate)) continue;
				int rank = Rank(candidate.Provider, providers);
				int providerOrder = best == null ? -1 : StringComparer.Ordinal.Compare(candidate.Provider, best.Provider);
				if (best == null || rank < bestRank || (rank == bestRank && (providerOrder < 0 || (providerOrder == 0 && StringComparer.Ordinal.Compare(candidate.Id, best.Id) < 0)))) {
					best = candidate; bestRank = rank;
				}
			}
			return best;
		}

		public static bool Eligible(IPluginResourceSource source, CabinetSurfaceConsumers consumer)
			=> source is ICabinetSurface surface ? surface.IsReady && (surface.Consumers & consumer) != 0 : (consumer & CabinetSurfaceConsumers.Scene) != 0;

		private static int Rank(string provider, IReadOnlyList<string> order)
		{
			for (int i = 0; i < order.Count; i++) if (string.Equals(provider, order[i], StringComparison.Ordinal)) return i;
			return int.MaxValue;
		}
	}
}
