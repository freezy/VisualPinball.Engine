// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Linq;
using UnityEngine;

namespace VisualPinball.Unity
{
	public struct InsertLightPackable
	{
		public float Intensity;
		public PackableColor Color;
		public InsertLightBulbPackable[] ExtraBulbs;
		public string[] BulbNames;
		public bool TintWithLamp;
		public float LampTint;
		public float SpillLumens;
		public float SpillRange;

		public static byte[] Pack(InsertLightComponent light) => PackageApi.Packer.Pack(new InsertLightPackable {
			Intensity = light.Intensity,
			Color = light.Color,
			ExtraBulbs = light.ExtraBulbs?.Select(bulb => new InsertLightBulbPackable {
				Intensity = bulb?.Intensity ?? 0f,
				Color = bulb != null ? bulb.Color : UnityEngine.Color.white,
			}).ToArray(),
			BulbNames = light.BulbNames,
			TintWithLamp = light.TintWithLamp,
			LampTint = light.LampTint,
			SpillLumens = light.SpillLumens,
			SpillRange = light.SpillRange,
		});

		public static void Unpack(byte[] bytes, InsertLightComponent light)
		{
			var data = PackageApi.Packer.Unpack<InsertLightPackable>(bytes);
			light.Intensity = data.Intensity;
			light.Color = data.Color;
			light.ExtraBulbs = data.ExtraBulbs?.Select(bulb => new InsertLightComponent.Bulb {
				Intensity = bulb.Intensity,
				Color = bulb.Color,
			}).ToArray() ?? Array.Empty<InsertLightComponent.Bulb>();
			light.BulbNames = data.BulbNames ?? new string[InsertLightComponent.MaxBulbs];
			light.TintWithLamp = data.TintWithLamp;
			light.LampTint = data.LampTint;
			light.SpillLumens = data.SpillLumens;
			light.SpillRange = data.SpillRange;
		}

		public static byte[] PackReferences(InsertLightComponent light, PackagedRefs refs)
			=> PackageApi.Packer.Pack(new InsertLightReferencesPackable {
				SpillLightNodeId = refs.GetNodeId(light.SpillLight ? light.SpillLight.transform : null),
			});

		public static void UnpackReferences(byte[] bytes, InsertLightComponent light, PackagedRefs refs)
		{
			var data = PackageApi.Packer.Unpack<InsertLightReferencesPackable>(bytes);
			var node = refs.GetNode(data.SpillLightNodeId);
			light.SpillLight = node ? node.GetComponent<Light>() : null;
		}
	}

	public struct InsertLightBulbPackable
	{
		public float Intensity;
		public PackableColor Color;
	}

	public struct InsertLightReferencesPackable
	{
		public string SpillLightNodeId;
	}
}
