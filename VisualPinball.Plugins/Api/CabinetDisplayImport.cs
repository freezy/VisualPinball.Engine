// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;
using System.Linq;

namespace VisualPinball.Plugins
{
	/// <summary>Read-only settings for a migration preview. Providers must not write files, create instances or open outputs.</summary>
	public sealed class CabinetDisplayImportRequest
	{
		public PluginSettingsSnapshot Settings { get; }
		public IReadOnlyCollection<string> SavedKeys { get; }
		public string Platform { get; }
		public string GameKey { get; }
		public CabinetDisplayImportRequest(PluginSettingsSnapshot settings, IEnumerable<string> savedKeys, string platform, string gameKey = "")
		{
			Settings = (settings ?? throw new ArgumentNullException(nameof(settings))).Copy();
			SavedKeys = Array.AsReadOnly((savedKeys ?? throw new ArgumentNullException(nameof(savedKeys))).ToArray());
			Platform = platform ?? ""; GameKey = gameKey ?? "";
		}
	}

	/// <summary>One adapter-described legacy output. Coordinates use physical virtual-desktop pixels, the same space as CabinetDisplayTarget; adapters must convert monitor-relative or DPI-scaled legacy values. No monitor identity is inferred here.</summary>
	public sealed class CabinetDisplayImportCandidate
	{
		public string Role { get; }
		public CabinetEndpointKind Kind { get; }
		public int X { get; }
		public int Y { get; }
		public int Width { get; }
		public int Height { get; }
		public string DeviceProfileId { get; }
		public string Note { get; }
		public bool Supported { get; }
		public CabinetDisplayImportCandidate(string role, int x, int y, int width, int height, string note = "", bool supported = true)
		{
			if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("An import candidate needs a role.");
			Role = role; Kind = CabinetEndpointKind.DesktopRegion; X = x; Y = y; Width = width; Height = height;
			DeviceProfileId = ""; Note = note ?? ""; Supported = supported && width > 0 && height > 0 && (long)x + width <= int.MaxValue && (long)y + height <= int.MaxValue;
		}
		public CabinetDisplayImportCandidate(string role, string deviceProfileId, string note = "", bool supported = true)
		{
			if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(deviceProfileId)) throw new ArgumentException("A device candidate needs a role and profile ID.");
			Role = role; Kind = CabinetEndpointKind.Device; DeviceProfileId = deviceProfileId; Note = note ?? ""; Supported = supported;
		}
	}

	/// <summary>Optional factory capability for a user-reviewed migration. Never mutates legacy or external configuration. Playfield placement is outside this API and remains in Video.</summary>
	public interface ICabinetDisplayImportProvider
	{
		IReadOnlyList<CabinetDisplayImportCandidate> GetDisplayImportCandidates(CabinetDisplayImportRequest request);
	}
}
