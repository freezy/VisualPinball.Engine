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
	/// <summary>Optional host capabilities for cabinet output configuration.</summary>
	public static class CabinetDisplayCapability
	{
		/// <summary>An immutable <see cref="CabinetDisplaySnapshot"/>, published for the game before preparation.</summary>
		public const string Configuration = "vpe.cabinet-displays.v1";
	}

	/// <summary>Who places one plugin's physical output. Does not control its in-scene output.</summary>
	public enum CabinetOutputMode { Custom = 0, Player = 1, Off = 2 }
	/// <summary>Physical destination; device drivers remain responsible for device protocols.</summary>
	public enum CabinetEndpointKind { DesktopRegion = 0, Device = 1 }
	/// <summary>Availability of a configured destination, independent of its content source.</summary>
	public enum CabinetEndpointState { Unassigned = 0, Available = 1, Disabled = 2, Unavailable = 3 }

	/// <summary>Immutable resolved destination. Desktop coordinates are physical pixels, including negative origins.</summary>
	public sealed class CabinetDisplayTarget
	{
		public string EndpointId { get; }
		public string Role { get; }
		public CabinetEndpointKind Kind { get; }
		public CabinetEndpointState State { get; }
		public string Reason { get; }
		public int X { get; }
		public int Y { get; }
		public int Width { get; }
		public int Height { get; }
		/// <summary>Canonical provider-resolved lease key, never a guessed USB identifier.</summary>
		public string DeviceLeaseKey { get; }
		/// <summary>Adapter profile; null on targets made with the legacy public constructor.</summary>
		public CabinetDeviceProfile? DeviceProfile { get; }

		public CabinetDisplayTarget(string endpointId, string role, CabinetEndpointKind kind, CabinetEndpointState state,
			int x = 0, int y = 0, int width = 0, int height = 0, string reason = "", string deviceLeaseKey = "")
		{
			if (string.IsNullOrWhiteSpace(endpointId)) throw new ArgumentException("An endpoint needs an ID.", nameof(endpointId));
			if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("An output needs a role.", nameof(role));
			if (!Enum.IsDefined(typeof(CabinetEndpointKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
			if (!Enum.IsDefined(typeof(CabinetEndpointState), state)) throw new ArgumentOutOfRangeException(nameof(state));
			if (state == CabinetEndpointState.Available && kind == CabinetEndpointKind.DesktopRegion && (width < 1 || height < 1)) throw new ArgumentOutOfRangeException(nameof(width));
			if (state == CabinetEndpointState.Available && kind == CabinetEndpointKind.Device && string.IsNullOrWhiteSpace(deviceLeaseKey)) throw new ArgumentException("An available device needs a canonical lease key.", nameof(deviceLeaseKey));
			EndpointId = endpointId; Role = role; Kind = kind; State = state;
			X = x; Y = y; Width = width; Height = height; Reason = reason ?? ""; DeviceLeaseKey = deviceLeaseKey ?? "";
		}
		private CabinetDisplayTarget(string endpointId, string role, CabinetEndpointState state, CabinetDeviceProfile? profile, string reason)
			: this(endpointId, role, CabinetEndpointKind.Device, state, reason: reason, deviceLeaseKey: profile?.LeaseKey ?? "") { DeviceProfile = profile; }
		public static CabinetDisplayTarget Device(string endpointId, string role, CabinetEndpointState state, CabinetDeviceProfile? profile, string reason = "")
		{
			if (profile != null && profile.Role != role) throw new ArgumentException("The profile role must match the target.", nameof(profile));
			if (state == CabinetEndpointState.Available && (profile == null || !profile.Available)) throw new ArgumentException("An available device needs an available profile.", nameof(profile));
			return new CabinetDisplayTarget(endpointId, role, state, profile, reason);
		}
	}

	/// <summary>Explicit adoption of one plugin output; absence always means Custom.</summary>
	public sealed class CabinetOutputAssignment
	{
		public string PluginId { get; }
		public string Role { get; }
		public CabinetOutputMode Mode { get; }
		public CabinetEndpointKind Kind { get; }
		public CabinetOutputAssignment(string pluginId, string role, CabinetOutputMode mode)
			: this(pluginId, role, mode, CabinetEndpointKind.DesktopRegion) { }
		public CabinetOutputAssignment(string pluginId, string role, CabinetOutputMode mode, CabinetEndpointKind kind)
		{
			if (string.IsNullOrWhiteSpace(pluginId) || string.IsNullOrWhiteSpace(role)) throw new ArgumentException("An assignment needs a plugin and role.");
			if (!Enum.IsDefined(typeof(CabinetOutputMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
			if (!Enum.IsDefined(typeof(CabinetEndpointKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
			PluginId = pluginId; Role = role; Mode = mode; Kind = kind;
		}
	}

	/// <summary>
	/// Game-scoped resolved configuration. Player/Off never imply falling back to a plugin-owned window when a monitor
	/// disappears. Source production and in-scene visibility are independent of destination availability.
	/// </summary>
	public sealed class CabinetDisplaySnapshot
	{
		public long Revision { get; }
		public long TopologyGeneration { get; }
		public long Session { get; }
		public IReadOnlyList<CabinetDisplayTarget> Targets { get; }
		public IReadOnlyList<CabinetOutputAssignment> Assignments { get; }
		public CabinetDisplaySnapshot(long revision, long topologyGeneration, long session,
			IEnumerable<CabinetDisplayTarget> targets, IEnumerable<CabinetOutputAssignment> assignments)
		{
			Revision = revision; TopologyGeneration = topologyGeneration; Session = session;
			var targetArray = (targets ?? throw new ArgumentNullException(nameof(targets))).ToArray();
			var assignmentArray = (assignments ?? throw new ArgumentNullException(nameof(assignments))).ToArray();
			if (targetArray.Any(value => value == null) || assignmentArray.Any(value => value == null)) throw new ArgumentException("Snapshot entries cannot be null.");
			if (targetArray.GroupBy(value => (value.EndpointId, value.Role)).Any(group => group.Count() > 1)) throw new ArgumentException("Duplicate endpoint/role target.", nameof(targets));
			if (assignmentArray.GroupBy(value => (value.PluginId, value.Role, value.Kind)).Any(group => group.Count() > 1)) throw new ArgumentException("Duplicate plugin/role/kind assignment.", nameof(assignments));
			Targets = Array.AsReadOnly(targetArray); Assignments = Array.AsReadOnly(assignmentArray);
		}

		public CabinetOutputMode ModeFor(string pluginId, string role)
			=> ModeFor(pluginId, role, CabinetEndpointKind.DesktopRegion);
		public CabinetOutputMode ModeFor(string pluginId, string role, CabinetEndpointKind kind)
		{
			foreach (var assignment in Assignments) if (assignment.PluginId == pluginId && assignment.Role == role && assignment.Kind == kind) return assignment.Mode;
			return CabinetOutputMode.Custom;
		}

		/// <summary>All destinations for this role, including unavailable ones; a monitor and device may mirror it.</summary>
		public IReadOnlyList<CabinetDisplayTarget> TargetsFor(string role)
			=> Array.AsReadOnly(Targets.Where(target => target.Role == role).ToArray());
	}

	/// <summary>Optional factory declaration. Inspecting roles must not create a gameplay instance or open a device.</summary>
	public interface ICabinetDisplayPlugin
	{
		IReadOnlyList<string> CabinetRoles { get; }
	}

	/// <summary>Where an updated surface is eligible to be shown.</summary>
	[Flags]
	public enum CabinetSurfaceConsumers { None = 0, Scene = 1, ManagedDesktop = 2 }

	/// <summary>Optional metadata on an existing resource, not a second transport. Legacy surfaces remain scene-eligible.</summary>
	/// <remarks>Call PluginResourceRegistry.NotifyChanged for this publication whenever readiness or consumers changes.</remarks>
	public interface ICabinetSurface
	{
		CabinetSurfaceConsumers Consumers { get; }
		/// <summary>False before the first composition or after withdrawal; a black image can still be ready.</summary>
		bool IsReady { get; }
	}
}
