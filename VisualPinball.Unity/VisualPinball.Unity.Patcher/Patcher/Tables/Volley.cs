// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

// ReSharper disable StringLiteralTypo
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Patcher
{
	[MetaMatch(TableName = "Volley", TableVersion = "1.0")]
	public class Volley : TablePatcher
	{
		public override void PostPatch(GameObject tableGo)
		{
			var playfieldGo = Playfield(tableGo);
			playfieldGo.isStatic = true;

			SetupLights(tableGo, playfieldGo);

			SetupDropTargetBanks(tableGo, playfieldGo);
			SetupTrough(tableGo, playfieldGo);
		}

		private static void SetupLights(GameObject tableGo, GameObject playfieldGo)
		{
			/*foreach (var child in playfieldGo.transform.Find("Lights").gameObject.transform.Cast<Transform>().ToList()) {
				var go = child.gameObject;

				var lc = go.GetComponentInParent<LightComponent>();

				if (lc != null) {
					lc.FadeSpeedUp = .2f;
					lc.FadeSpeedDown = .1f;

					PrefabUtility.RecordPrefabInstancePropertyModifications(lc);
				}
			}*/

			var lightGroups = CreateEmptyGameObject(playfieldGo, "Light Groups");

			AddLightGroup(tableGo, CreateEmptyGameObject(lightGroups, "LightGroupGI"),
				"GILight1", "GILight2", "GILight3", "GILight4",
				"GILight6", "GILight7", "GILight9", "GILight20",
				"GILight21", "GILight22", "GILight23", "GILight24",
				"GILight25", "GILight26", "GILight27", "GILight28",
				"GILight29", "GILight30", "GILight31", "GILight32",
				"GILight33", "GILight34",
				"GILight5", "GILight8", "GILight10", "GILight11", "GILight12",
				"GILight13");
		}

		private static void SetupDropTargetBanks(GameObject tableGo, GameObject playfieldGo)
		{
			CreateDropTargetBank(tableGo, playfieldGo, "YellowDropTargetBank",
				new string[] { "Yellow1", "Yellow2", "Yellow3", "Yellow4", "Yellow5" });

			CreateDropTargetBank(tableGo, playfieldGo, "BlueDropTargetBank",
				new string[] { "Blue1", "Blue2", "Blue3", "Blue4", "Blue5" });

			CreateDropTargetBank(tableGo, playfieldGo, "GreenDropTargetBank",
				new string[] { "Green1", "Green2", "Green3", "Green4", "Green5" });
		}

		private static void SetupTrough(GameObject tableGo, GameObject playfieldGo)
		{
			/*var troughComponent = CreateTrough(tableGo, playfieldGo);
			troughComponent.Type = TroughType.ClassicSingleBall;
			troughComponent.BallCount = 1;
			troughComponent.SwitchCount = 1;
			troughComponent.KickTime = 100;
			troughComponent.RollTime = 300;*/
		}

		[NameMatch("Yellow1")]
		[NameMatch("Yellow2")]
		[NameMatch("Yellow3")]
		[NameMatch("Yellow4")]
		[NameMatch("Yellow5")]
		public void FixYellowDropTargetTexture(GameObject go)
		{
			var renderer = go.GetComponent<Renderer>();
			var material = new UnityEngine.Material(renderer.sharedMaterial);
			material.mainTexture = TextureProvider.GetTexture("DropTarget_Yellow");
			material.color = UnityEngine.Color.white;
			renderer.sharedMaterial = material;
		}

		[NameMatch("Blue1")]
		[NameMatch("Blue2")]
		[NameMatch("Blue3")]
		[NameMatch("Blue4")]
		[NameMatch("Blue5")]
		public void FixBlueDropTargetTexture(GameObject go)
		{
			var renderer = go.GetComponent<Renderer>();
			var material = new UnityEngine.Material(renderer.sharedMaterial);
			material.mainTexture = TextureProvider.GetTexture("DropTarget_Blue");
			material.color = UnityEngine.Color.white;
			renderer.sharedMaterial = material;
		}

		[NameMatch("Green1")]
		[NameMatch("Green2")]
		[NameMatch("Green3")]
		[NameMatch("Green4")]
		[NameMatch("Green5")]
		public void FixGreenDropTargetTexture(GameObject go)
		{
			var renderer = go.GetComponent<Renderer>();
			var material = new UnityEngine.Material(renderer.sharedMaterial);
			material.mainTexture = TextureProvider.GetTexture("DropTarget_Green");
			material.color = UnityEngine.Color.white;
			renderer.sharedMaterial = material;
		}

		[NameMatch("OperatorMenuBackdrop")]
		public void DisableOperatorMenuBackgroup(GameObject go)
		{
			go.SetActive(false);
		}

		[NameMatch("GILight1")]
		[NameMatch("GILight2")]
		[NameMatch("GILight3")]
		[NameMatch("GILight4")]
		[NameMatch("GILight5")]
		[NameMatch("GILight6")]
		[NameMatch("GILight7")]
		[NameMatch("GILight9")]
		[NameMatch("GILight10")]
		[NameMatch("GILight11")]
		[NameMatch("GILight12")]
		[NameMatch("GILight13")]
		[NameMatch("GILight20")]
		[NameMatch("GILight21")]
		[NameMatch("GILight22")]
		[NameMatch("GILight23")]
		[NameMatch("GILight24")]
		[NameMatch("GILight25")]
		[NameMatch("GILight26")]
		[NameMatch("GILight27")]
		[NameMatch("GILight28")]
		[NameMatch("GILight29")]
		[NameMatch("GILight30")]
		[NameMatch("GILight31")]
		[NameMatch("GILight32")]
		[NameMatch("GILight33")]
		[NameMatch("GILight34")]
		public void FixGIs(GameObject go)
		{
			//LightTemperature(go, 2700f);
			//LightIntensity(go, 120f);
		}
	}
}