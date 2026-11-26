// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

namespace VisualPinball.Plugins
{
	/// <summary>Key of the cabinet-trigger capability.</summary>
	public static class CabinetTriggerCapability
	{
		/// <summary>
		/// The running game's <see cref="IPluginCabinetTriggers"/>. An output plugin (DOF) publishes it while it drives a
		/// game's cabinet outputs and withdraws it when the game stops; tables, PUP packs and other plugins drive it.
		/// </summary>
		public const string Id = "vpe.cabinet-triggers.v1";
	}

	/// <summary>
	/// Cabinet triggers that are not controller outputs: numbered events (<c>E</c> in DOF configurations) and named
	/// elements (<c>$name</c>), which VPX tables generate from their scripts.
	/// </summary>
	/// <remarks>
	/// Published under <see cref="CabinetTriggerCapability.Id"/> by the plugin that owns the cabinet outputs, so a
	/// producer such as a table component or the PinUP plugin does not depend on that plugin. A producer only sets values:
	/// it cannot start, restart or finish the output session, which follows the game. Look the capability up with
	/// <see cref="PluginCapabilities.Find{T}"/> on the owner thread when it is needed, since the providing plugin may
	/// prepare after the producer.
	/// <para>
	/// Implementations accept calls from any thread, return without blocking and keep the calls' order. Values follow
	/// DOF's semantics: 0 is off, anything else on.
	/// </para>
	/// <example>
	/// <code>
	/// var triggers = game.Capabilities.Find&lt;IPluginCabinetTriggers&gt;(CabinetTriggerCapability.Id);
	/// triggers?.PulseEvent(101);         // E101, on and off again
	/// triggers?.SetNamed("Shaker", 1);   // $Shaker on
	/// </code>
	/// </example>
	/// </remarks>
	public interface IPluginCabinetTriggers
	{
		/// <summary>Sets a numbered event (<c>E</c> in DOF configurations) to a value; 0 turns it off.</summary>
		/// <param name="number">The event's number, as the table's configuration defines it.</param>
		/// <param name="value">The new value.</param>
		void SetEvent(int number, int value);
		/// <summary>
		/// Turns a numbered event on and off again, as VPX's <c>DOFPulse</c> does: 1 and then 0, back to back. Effects that
		/// last are timed by the configuration.
		/// </summary>
		/// <param name="number">The event's number.</param>
		void PulseEvent(int number);
		/// <summary>Sets a named element (<c>$name</c> in DOF configurations) to a value; 0 turns it off.</summary>
		/// <param name="name">The element's name, without the dollar sign. Empty names are ignored.</param>
		/// <param name="value">The new value.</param>
		void SetNamed(string name, int value);
		/// <summary>Turns a named element on and off again: 1 and then 0, back to back.</summary>
		/// <param name="name">The element's name, without the dollar sign. Empty names are ignored.</param>
		void PulseNamed(string name);
	}
}
