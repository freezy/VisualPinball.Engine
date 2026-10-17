// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

namespace VisualPinball.Unity
{
	/// <summary>
	/// A part of VPE whose cost the profile build measures, see <see cref="VpeProfiler"/>.
	/// </summary>
	/// <remarks>
	/// Appending is fine; the names go into traces, so don't rename them lightly.
	/// </remarks>
	public enum ProfileSection : byte
	{
		/// <summary>Physics events from the simulation thread, dispatched to the components.</summary>
		PhysicsEventDrain,
		/// <summary>Kinematic colliders checked for movement.</summary>
		PhysicsKinematicScan,
		/// <summary>The simulation's state applied to the transforms.</summary>
		PhysicsApplyMovements,
		/// <summary>Unity's clock handed to the simulation thread.</summary>
		SimClockSync,
		/// <summary>Input forwarded to the simulation thread.</summary>
		SimInputFlush,
		/// <summary>Lamps, GI and coils from the simulation thread applied.</summary>
		SimApplyState,
		/// <summary>A gamelogic engine's per-frame work.</summary>
		GleUpdate,
		/// <summary>Lamp changes of the gamelogic engine applied.</summary>
		GleLamps,
		/// <summary>DMD, segment and LCD textures uploaded.</summary>
		DisplayUpload,
		/// <summary>Light components and inserts updated.</summary>
		LightsUpdate,
		/// <summary>Wires dispatched.</summary>
		WireUpdate,
		/// <summary>The player's per-frame update.</summary>
		PlayerOnUpdate,
		/// <summary>Sounds started.</summary>
		SoundStart,
		/// <summary>Input processed on the main thread.</summary>
		InputProcess,
		/// <summary>The HDRP auto focus.</summary>
		HdrpAutoFocus,
		/// <summary>Plugins' per-frame work.</summary>
		PluginsUpdate,
		/// <summary>Overlays and menus drawn.</summary>
		UiOverlay,
		/// <summary>The profile build's own per-frame work.</summary>
		PerfSelf,
	}

	/// <summary>
	/// Something that happened, kept with the frame it happened in, see
	/// <see cref="VpeProfiler.Mark"/>.
	/// </summary>
	public enum ProfileEvent : byte
	{
		/// <summary>The window got or lost focus (value 1 = focused).</summary>
		Focus,
		/// <summary>Resolution, fullscreen mode, vsync or target frame rate changed.</summary>
		DisplayModeChanged,
		/// <summary>A display was added or removed, or its refresh rate changed.</summary>
		DisplaysChanged,
		/// <summary>A table started loading.</summary>
		TableLoad,
		/// <summary>A table was unloaded.</summary>
		TableUnload,
		/// <summary>The game was paused (value 1) or resumed (0).</summary>
		Pause,
		/// <summary>The graphics quality changed.</summary>
		QualityChanged,
		/// <summary>The GPU memory went over its budget.</summary>
		VramOverBudget,
		/// <summary>The profile build's own work starts (value 1) or ends (0): those frames are no incident.</summary>
		SelfInduced,
		/// <summary>The first time a light of a kind changed state.</summary>
		FirstUseLight,
		/// <summary>The first time a flasher changed state.</summary>
		FirstUseFlasher,
		/// <summary>The first time a material or renderer changed state.</summary>
		FirstUseMaterial,
		/// <summary>The first multiball.</summary>
		FirstUseMultiball,
		/// <summary>The service menu opened for the first time.</summary>
		FirstUseServiceMenu,
		/// <summary>The first ball drained.</summary>
		FirstUseDrain,
		/// <summary>The user pressed the "that just stuttered" key.</summary>
		UserMarker,
	}

	/// <summary>
	/// What the profile build counts per frame, see <see cref="VpeProfiler.Count"/>.
	/// </summary>
	public enum ProfileCounter : byte
	{
		DisplayFrames,
		LampChanges,
		CoilEvents,
		KinematicMoves,
		AsyncContinuations,
		PinMameCallbacks,
		DmdFramesDropped,
		LogBatches,
		LogDropped,
	}
}
