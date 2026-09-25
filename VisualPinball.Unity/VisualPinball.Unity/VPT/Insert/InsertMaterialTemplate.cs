// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The look of an insert's materials without their bake: every number,
	/// colour and vector the body's and the cover's materials show in their
	/// inspectors, saved to be put on another insert whose baked maps stay as
	/// they are. A clear insert and a coloured one differ in nothing else.
	/// </summary>
	/// <remarks>
	/// What a template leaves alone: textures, which the bake writes; the
	/// virtual lamps, which sit where each insert's own bulbs are; and every
	/// hidden property, which the bake or the Insert Light drives at runtime.
	/// </remarks>
	[CreateAssetMenu(fileName = "Insert Material Template", menuName = "Visual Pinball/Insert Material Template")]
	public sealed class InsertMaterialTemplate : ScriptableObject
	{
		public enum Kind
		{
			Float,
			Color,
			Vector,
		}

		[Serializable]
		public sealed class Entry
		{
			public string Name;
			public Kind Kind;
			public float Float;
			public Color Color;
			public Vector4 Vector;
		}

		[Tooltip("The shader the body's values were taken from.")]
		public string BodyShader;

		[Tooltip("The body material's values, the baked maps and the virtual lamps left out.")]
		public List<Entry> Body = new();

		[Tooltip("The shader the cover's values were taken from, or empty without a cover.")]
		public string CoverShader;

		[Tooltip("The clear cover material's values.")]
		public List<Entry> Cover = new();

		/// <summary>The properties a template never carries: the virtual lamps sit where each insert's own bulbs are.</summary>
		private static readonly HashSet<string> Excluded = new() { "_OpticalLamp", "_OpticalLampB", "_OpticalLampC", "_OpticalLampD" };

		/// <summary>Takes the look off the materials, the body's and the cover's if there is one.</summary>
		public void Capture(Material body, Material cover)
		{
			BodyShader = body != null && body.shader != null ? body.shader.name : string.Empty;
			Body = body != null ? Read(body) : new List<Entry>();
			CoverShader = cover != null && cover.shader != null ? cover.shader.name : string.Empty;
			Cover = cover != null ? Read(cover) : new List<Entry>();
		}

		/// <summary>
		/// Puts the look on the materials, each value on the property of that
		/// name where the material has one, and returns how many were set.
		/// </summary>
		public int Apply(Material body, Material cover)
		{
			var count = 0;
			if (body != null) {
				count += Write(body, Body);
			}
			if (cover != null) {
				count += Write(cover, Cover);
			}
			return count;
		}

		/// <summary>Whether the material's property at that index is part of its look.</summary>
		public static bool IsStyle(Shader shader, int index)
		{
			var type = shader.GetPropertyType(index);
			if (type == ShaderPropertyType.Texture) {
				return false;
			}
			if ((shader.GetPropertyFlags(index) & ShaderPropertyFlags.HideInInspector) != 0) {
				return false;
			}
			return !Excluded.Contains(shader.GetPropertyName(index));
		}

		private static List<Entry> Read(Material material)
		{
			var entries = new List<Entry>();
			var shader = material.shader;
			for (var i = 0; i < shader.GetPropertyCount(); i++) {
				if (!IsStyle(shader, i)) {
					continue;
				}
				var name = shader.GetPropertyName(i);
				switch (shader.GetPropertyType(i)) {
					case ShaderPropertyType.Float:
					case ShaderPropertyType.Range:
					case ShaderPropertyType.Int:
						entries.Add(new Entry { Name = name, Kind = Kind.Float, Float = material.GetFloat(name) });
						break;
					case ShaderPropertyType.Color:
						entries.Add(new Entry { Name = name, Kind = Kind.Color, Color = material.GetColor(name) });
						break;
					case ShaderPropertyType.Vector:
						entries.Add(new Entry { Name = name, Kind = Kind.Vector, Vector = material.GetVector(name) });
						break;
				}
			}
			return entries;
		}

		private static int Write(Material material, List<Entry> entries)
		{
			var count = 0;
			if (entries == null) {
				return 0;
			}
			foreach (var entry in entries) {
				if (entry == null || string.IsNullOrEmpty(entry.Name) || Excluded.Contains(entry.Name) || !material.HasProperty(entry.Name)) {
					continue;
				}
				var index = material.shader.FindPropertyIndex(entry.Name);
				if (index < 0 || !IsStyle(material.shader, index)) {
					continue;
				}
				switch (entry.Kind) {
					case Kind.Float:
						material.SetFloat(entry.Name, entry.Float);
						break;
					case Kind.Color:
						material.SetColor(entry.Name, entry.Color);
						break;
					case Kind.Vector:
						material.SetVector(entry.Name, entry.Vector);
						break;
				}
				count++;
			}
			return count;
		}
	}
}
