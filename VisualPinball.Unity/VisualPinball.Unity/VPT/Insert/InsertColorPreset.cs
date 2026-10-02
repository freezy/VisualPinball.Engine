// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using UnityEngine;
using UnityEngine.Serialization;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The colors to use when making a new insert body and cover material. Materials
	/// linked to the preset keep following it when it changes.
	/// </summary>
	[CreateAssetMenu(fileName = "Insert Color Preset", menuName = "Visual Pinball/Insert Color Preset")]
	public sealed class InsertColorPreset : ScriptableObject
	{
		[Tooltip("Color name used in material names and lookup.")]
		public string ColorName;

		[Tooltip("Order of the color button in the insert inspector.")]
		public int Order;

		[Tooltip("Color of the button in the insert inspector.")]
		public Color Swatch = Color.white;

		[FormerlySerializedAs("BodyColor")]
		[Tooltip("The hybrid body's Tint: the color of the plastic.")]
		public Color Tint = Color.white;

		[Tooltip("The hybrid body's Cavity Color.")]
		public Color CavityColor = Color.white;

		[ColorUsage(true, true)]
		[Tooltip("The hybrid body's HDR lamp emission color.")]
		public Color LampEmission = Color.white;

		[Min(0f)]
		[Tooltip("The hybrid body's Max Brightness. Colors differ in how bright they look through the plastic, so each has its own.")]
		public float MaxBrightness = 1f;

		[FormerlySerializedAs("CoverColor")]
		[Tooltip("The cover material's Haze Color.")]
		public Color HazeColor = Color.white;
	}
}
