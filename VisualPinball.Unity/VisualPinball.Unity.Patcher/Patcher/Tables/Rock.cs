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
using UnityEngine;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Patcher
{
	[MetaMatch(TableName = "Rock", AuthorName = "jpsalas")]
	public class Rock : TablePatcher
	{
		#region Global

		public override void PostPatch(GameObject tableGo)
		{
			var playfieldGo = Playfield(tableGo);
			playfieldGo.isStatic = true;

			RemoveDisplayLights(playfieldGo);

			SetupFlippers(playfieldGo);
			SetupDropTargetBanks(tableGo, playfieldGo);
			SetupTrough(tableGo, playfieldGo);
			SetupPinMame(tableGo, playfieldGo);
			SetupDisplays(tableGo);
		}

		private static void RemoveDisplayLights(GameObject playfieldGo)
		{
			var regex = new Regex("^[a-c][0-9a-f][0-9a-f]$");
			foreach (var child in playfieldGo.transform.Find("Lights").gameObject.transform.Cast<Transform>().ToList()) {
				var go = child.gameObject;
				if (regex.Match(go.name).Success) {
					Object.DestroyImmediate(go);
				}
			}
		}

		private static void SetupFlippers(GameObject playfieldGo)
		{
			var flipper = playfieldGo.transform.Find("Flippers/LeftFlipper1").gameObject;
			flipper.name = "LowerLeftFlipper";

			flipper = playfieldGo.transform.Find("Flippers/RightFlipper1").gameObject;
			flipper.name = "LowerRightFlipper";

			flipper = playfieldGo.transform.Find("Flippers/LeftFlipper5").gameObject;
			flipper.name = "UpperLeftFlipper";

			flipper = playfieldGo.transform.Find("Flippers/RightFlipper5").gameObject;
			flipper.name = "UpperRightFlipper";
		}

		private static void SetupDropTargetBanks(GameObject tableGo, GameObject playfieldGo)
		{
			CreateDropTargetBank(tableGo, playfieldGo, "4PosBankUpper",
				new string[] { "sw40", "sw50", "sw60", "sw70" });

			CreateDropTargetBank(tableGo, playfieldGo, "5PosBankLower",
				new string[] { "sw51", "sw61", "sw71", "sw62", "sw72" });
		}

		private static void SetupTrough(GameObject tableGo, GameObject playfieldGo)
		{
			var troughComponent = CreateTrough(tableGo, playfieldGo);
			troughComponent.Type = TroughType.ClassicSingleBall;
			troughComponent.BallCount = 1;
			troughComponent.SwitchCount = 1;
			troughComponent.KickTime = 100;
			troughComponent.RollTime = 300;
		}

		private static void SetupPinMame(GameObject tableGo, GameObject playfieldGo)
		{
#if !NO_PINMAME
			var tableComponent = tableGo.GetComponent<TableComponent>();

			// GLE
			Object.DestroyImmediate(tableGo.GetComponent<DefaultGamelogicEngine>());
			var pinmameGle = tableGo.AddComponent<Engine.PinMAME.PinMameGamelogicEngine>();
			pinmameGle.Game = new Engine.PinMAME.Games.Rock();
			pinmameGle.romId = "rock";
			pinmameGle.DisableMechs = true;
			pinmameGle.SolenoidDelay = 1000;
			tableComponent.RepopulateHardware(pinmameGle);
			TableSelector.Instance.TableUpdated();
#endif
		}

		private static void SetupDisplays(GameObject tableGo)
		{
			const float scale = 0.5f;
			var cabinetGo = tableGo.transform.Find("Cabinet").gameObject;
			var go = new GameObject {
				name = "Segment Display [0]",
				transform = {
					localEulerAngles = new Vector3(0, 0, 0),
					localPosition = new Vector3(0, 0.31f, 1.1f),
					localScale = new Vector3(scale, scale, scale)
				}
			};
			go.transform.SetParent(cabinetGo.transform, false);

			var segment = go.AddComponent<SegmentDisplayComponent>();
			segment.Id = "display0";
			segment.SegmentTypeName = "Seg16";
			segment.NumSegments = 14;
			segment.SeparatorType = 2;
			segment.NumChars = 20;
			segment.LitColor = Color.green;
			segment.Emission = 3;

			go = new GameObject {
				name = "Segment Display [1]",
				transform = {
					localEulerAngles = new Vector3(0, 0, 0),
					localPosition = new Vector3(0, 0.21f, 1.1f),
					localScale = new Vector3(scale, scale, scale)
				}
			};
			go.transform.SetParent(cabinetGo.transform, false);

			segment = go.AddComponent<SegmentDisplayComponent>();
			segment.Id = "display1";
			segment.SegmentTypeName = "Seg16";
			segment.NumSegments = 14;
			segment.SeparatorType = 2;
			segment.NumChars = 20;
			segment.LitColor = Color.green;
			segment.Emission = 3;
		}

		#endregion

		[NameMatch("LeftFlipper2", Ref = "Playfield/Flippers/LeftFlipper1")]
		[NameMatch("LeftFlipper3", Ref = "Playfield/Flippers/LeftFlipper1")]
		[NameMatch("LeftFlipper4", Ref = "Playfield/Flippers/LeftFlipper1")]
		[NameMatch("RightFlipper2", Ref = "Playfield/Flippers/RightFlipper1")]
		[NameMatch("RightFlipper3", Ref = "Playfield/Flippers/RightFlipper1")]
		[NameMatch("RightFlipper4", Ref = "Playfield/Flippers/RightFlipper1")]
		public void ReparentFlipper(FlipperComponent flipper, GameObject go, ref GameObject parent)
		{
			PatcherUtil.Reparent(go, parent);
			PatcherUtil.Hide(go.GetComponentInChildren<FlipperRubberMeshComponent>().gameObject);

			flipper.Position.x = 0;
			flipper.Position.y = 0;
			flipper._startAngle = 0;
		}

		[NameMatch("sw40")]
		[NameMatch("sw50")]
		[NameMatch("sw60")]
		[NameMatch("sw70")]
		public void FixUpperDropTargetTexture(GameObject go)
		{
			var renderer = go.GetComponent<Renderer>();
			var material = new UnityEngine.Material(renderer.sharedMaterial);
			material.mainTexture = TextureProvider.GetTexture("DropTargetSimple_rock-red");
			material.color = Color.white;
			renderer.sharedMaterial = material;
		}

		[NameMatch("sw51")]
		[NameMatch("sw61")]
		[NameMatch("sw71")]
		[NameMatch("sw62")]
		[NameMatch("sw72")]
		public void FixLowerDropTargetTexture(GameObject go)
		{
			var renderer = go.GetComponent<Renderer>();
			var material = new UnityEngine.Material(renderer.sharedMaterial);
			material.mainTexture = TextureProvider.GetTexture("DropTargetSimple_rock-black");
			material.color = Color.white;
			renderer.sharedMaterial = material;
		}

		[NameMatch("sw52")]
		public void FixTargetTexture(GameObject go)
		{
			var renderer = go.GetComponent<Renderer>();
			var material = new UnityEngine.Material(renderer.sharedMaterial);
			material.mainTexture = TextureProvider.GetTexture("target1-rock");
			material.color = Color.white;
			renderer.sharedMaterial = material;
		}

		[NameMatch("sw45")]
		[NameMatch("sw75")]
		public void FixSpinnerTexture(GameObject go)
		{
			var plate = go.transform.Find("Plate").gameObject;
			var renderer = plate.GetComponent<Renderer>();
			var material = new UnityEngine.Material(renderer.sharedMaterial);
			material.mainTexture = TextureProvider.GetTexture("spinner_gottlieb");
			material.color = Color.white;
			renderer.sharedMaterial = material;
		}

		[NameMatch("Gate4", Ref = "Playfield/Spinners/sw45")]
		[NameMatch("Gate5", Ref = "Playfield/Spinners/sw45")]
		public void FixGateBracketMaterial(GameObject go, ref GameObject spinnerGo)
		{
			var gateBracket = go.transform.Find("Bracket").gameObject;
			var spinnerBracket = spinnerGo.transform.Find("Bracket").gameObject;

			// Use spinner bracket material set gate bracket material not found
			gateBracket.GetComponent<Renderer>().sharedMaterial = spinnerBracket.GetComponent<Renderer>().sharedMaterial;
		}
	}
}
