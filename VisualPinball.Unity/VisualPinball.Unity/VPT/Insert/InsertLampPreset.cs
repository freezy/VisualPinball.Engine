// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// What kind of bulb sits under an insert: its colour and how it fades.
	/// The plastic's colour is the color preset's; this is the lamp behind it,
	/// so a table swaps from incandescent to LED without touching a material.
	/// </summary>
	[CreateAssetMenu(fileName = "Insert Lamp Preset", menuName = "Visual Pinball/Insert Lamp Preset")]
	public sealed class InsertLampPreset : ScriptableObject
	{
		[Tooltip("Name shown in the Insert Light's Lamp dropdown, and what a light that follows this preset remembers it by.")]
		public string LampName;

		[Tooltip("Order in the dropdown; lower values come first.")]
		public int Order;

		[Tooltip("The light the bulb gives: the Insert Light's Color, which multiplies the material's Lamp Emission. Keep a little of every channel in it, so a bright lamp fades to white under the tonemapper instead of shifting hue.")]
		public Color Color = Color.white;

		[Min(0f)]
		[Tooltip("How much light the bulb gives, relative to a #44 filament: an LED kit is brighter. Scales the lit look and the spill alike.")]
		public float Output = 1f;

		[Min(0)]
		[Tooltip("Time in seconds the lamp takes to reach full intensity: a filament warms up, an LED is instant.")]
		public float FadeSpeedUp;

		[Min(0)]
		[Tooltip("Time in seconds the lamp takes to turn off: a filament cools down, an LED is instant.")]
		public float FadeSpeedDown;
	}
}
