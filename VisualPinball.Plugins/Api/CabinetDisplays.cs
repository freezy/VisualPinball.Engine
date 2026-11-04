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

	/// <summary>Shared cabinet destinations with Player-defined semantics. Plug-in-private screens are not cabinet roles.</summary>
	public enum CabinetOutputRole
	{
		Backglass = 1,
		Dmd = 2,
		/// <summary>The game’s LCD/HD display, independent of backglass artwork and cabinet video panels.</summary>
		GameDisplay = 3,
		Topper = 4,
		ScorePanel = 5,
	}

	/// <summary>Conversion at the generic resource/persistence boundary. The cabinet API itself uses the enum.</summary>
	public static class CabinetOutputRoles
	{
		public static bool IsDefined(CabinetOutputRole role) => Enum.IsDefined(typeof(CabinetOutputRole), role);
		public static string Id(this CabinetOutputRole role) => role switch {
			CabinetOutputRole.Backglass => SurfaceRoles.Backglass,
			CabinetOutputRole.Dmd => SurfaceRoles.Dmd,
			CabinetOutputRole.GameDisplay => SurfaceRoles.GameDisplay,
			CabinetOutputRole.Topper => SurfaceRoles.Topper,
			CabinetOutputRole.ScorePanel => SurfaceRoles.ScorePanel,
			_ => throw new ArgumentOutOfRangeException(nameof(role)),
		};
		public static bool TryParse(string id, out CabinetOutputRole role)
		{
			switch (id) {
				case SurfaceRoles.Backglass: role = CabinetOutputRole.Backglass; return true;
				case SurfaceRoles.Dmd: role = CabinetOutputRole.Dmd; return true;
				case SurfaceRoles.GameDisplay: role = CabinetOutputRole.GameDisplay; return true;
				case SurfaceRoles.Topper: role = CabinetOutputRole.Topper; return true;
				case SurfaceRoles.ScorePanel: role = CabinetOutputRole.ScorePanel; return true;
				default: role = default; return false;
			}
		}
		public static CabinetOutputRole Parse(string id)
			=> TryParse(id, out var role) ? role : throw new ArgumentException("Unknown cabinet output role: " + id, nameof(id));
	}

	/// <summary>Who places one plugin's physical output. Does not control its in-scene output.</summary>
	public enum CabinetOutputMode { Custom = 0, Player = 1, Off = 2 }
	/// <summary>Availability of a configured destination, independent of its content source.</summary>
	public enum CabinetEndpointState { Unassigned = 0, Available = 1, Disabled = 2, Unavailable = 3 }

	/// <summary>Immutable resolved destination: a region of the desktop, in physical pixels, including negative origins.</summary>
	/// <remarks>Physical DMD panels are not player destinations. Their plug-ins open them from their own settings.</remarks>
	public sealed class CabinetDisplayTarget
	{
		public string EndpointId { get; }
		public CabinetOutputRole Role { get; }
		public CabinetEndpointState State { get; }
		public string Reason { get; }
		public int X { get; }
		public int Y { get; }
		public int Width { get; }
		public int Height { get; }

		public CabinetDisplayTarget(string endpointId, CabinetOutputRole role, CabinetEndpointState state,
			int x = 0, int y = 0, int width = 0, int height = 0, string reason = "")
		{
			if (string.IsNullOrWhiteSpace(endpointId)) throw new ArgumentException("An endpoint needs an ID.", nameof(endpointId));
			if (!CabinetOutputRoles.IsDefined(role)) throw new ArgumentException("An output needs a role.", nameof(role));
			if (!Enum.IsDefined(typeof(CabinetEndpointState), state)) throw new ArgumentOutOfRangeException(nameof(state));
			if (state == CabinetEndpointState.Available && (width < 1 || height < 1)) throw new ArgumentOutOfRangeException(nameof(width));
			EndpointId = endpointId; Role = role; State = state;
			X = x; Y = y; Width = width; Height = height; Reason = reason ?? "";
		}
	}

	/// <summary>Explicit adoption of one plugin output; absence always means Custom.</summary>
	public sealed class CabinetOutputAssignment
	{
		public string PluginId { get; }
		public CabinetOutputRole Role { get; }
		public CabinetOutputMode Mode { get; }
		public CabinetOutputAssignment(string pluginId, CabinetOutputRole role, CabinetOutputMode mode)
		{
			if (string.IsNullOrWhiteSpace(pluginId) || !CabinetOutputRoles.IsDefined(role)) throw new ArgumentException("An assignment needs a plugin and role.");
			if (!Enum.IsDefined(typeof(CabinetOutputMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
			PluginId = pluginId; Role = role; Mode = mode;
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
			if (assignmentArray.GroupBy(value => (value.PluginId, value.Role)).Any(group => group.Count() > 1)) throw new ArgumentException("Duplicate plugin/role assignment.", nameof(assignments));
			Targets = Array.AsReadOnly(targetArray); Assignments = Array.AsReadOnly(assignmentArray);
		}

		public CabinetOutputMode ModeFor(string pluginId, CabinetOutputRole role)
		{
			foreach (var assignment in Assignments) if (assignment.PluginId == pluginId && assignment.Role == role) return assignment.Mode;
			return CabinetOutputMode.Custom;
		}

		/// <summary>All destinations for this role, including unavailable ones.</summary>
		public IReadOnlyList<CabinetDisplayTarget> TargetsFor(CabinetOutputRole role)
			=> Array.AsReadOnly(Targets.Where(target => target.Role == role).ToArray());
	}

	/// <summary>Optional factory declaration. Inspecting roles must not create a gameplay instance or open a device.</summary>
	public interface ICabinetDisplayPlugin
	{
		IReadOnlyList<CabinetOutputRole> CabinetRoles { get; }
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
