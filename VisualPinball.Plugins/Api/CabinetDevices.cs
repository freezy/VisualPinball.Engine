// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;

namespace VisualPinball.Plugins
{
	/// <summary>Adapter-owned device configuration. Enumerating a profile must never open hardware.</summary>
	public sealed class CabinetDeviceProfile
	{
		public string Id { get; }
		public string Provider { get; }
		public CabinetOutputRole Role { get; }
		public string Label { get; }
		public string LeaseKey { get; }
		/// <summary>Optional conservative reservation shared by auto and explicit choices of one driver family.</summary>
		public string FamilyLeaseKey { get; }
		/// <summary>Opaque adapter-owned payload, detached from mutable settings; never contains handles or live state.</summary>
		public string Configuration { get; }
		public bool Available { get; }
		public string Reason { get; }
		public CabinetDeviceProfile(string id, string provider, CabinetOutputRole role, string label, string leaseKey, string familyLeaseKey, string configuration, bool available = true, string reason = "")
		{
			if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(provider) || !CabinetOutputRoles.IsDefined(role)) throw new ArgumentException("A device profile needs an ID, provider and role.");
			if (available && string.IsNullOrWhiteSpace(leaseKey)) throw new ArgumentException("An available profile needs a canonical device lease key.");
			if ((configuration?.Length ?? 0) > 65536) throw new ArgumentException("Device configuration exceeds 65536 characters.");
			Id = id; Provider = provider; Role = role; Label = label ?? id; LeaseKey = leaseKey ?? ""; FamilyLeaseKey = familyLeaseKey ?? "";
			Configuration = configuration ?? ""; Available = available; Reason = reason ?? "";
		}
	}

	public sealed class CabinetDeviceProfileRequest
	{
		public PluginSettingsSnapshot Settings { get; }
		public IReadOnlyCollection<string> SavedKeys { get; }
		public string Platform { get; }
		public CabinetDeviceProfileRequest(PluginSettingsSnapshot settings, IEnumerable<string> savedKeys, string platform)
		{
			Settings = (settings ?? throw new ArgumentNullException(nameof(settings))).Copy();
			SavedKeys = Array.AsReadOnly((savedKeys ?? throw new ArgumentNullException(nameof(savedKeys))).ToArray()); Platform = platform ?? "";
		}
	}

	/// <summary>Optional factory API. Profiles describe configuration capability, not physical presence or a successful probe.</summary>
	public interface ICabinetDeviceProfileProvider
	{
		IReadOnlyList<CabinetDeviceProfile> GetDeviceProfiles(CabinetDeviceProfileRequest request);
	}
}
