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
	[MetaMatch(TableName = "Rock", AuthorName = "jpsalas")]
	public class Rock : TablePatcher
	{
		#region Global

		public override void PostPatch(GameObject tableGo)
		{
			var playfieldGo = Playfield(tableGo);
			playfieldGo.isStatic = true;

			SetupLights(playfieldGo);
			
			SetupFlippers(playfieldGo);
			SetupDropTargetBanks(tableGo, playfieldGo);
			SetupTrough(tableGo, playfieldGo);
			SetupPinMame(tableGo, playfieldGo);
			SetupDisplays(tableGo);

			SetupLeftSlingshot(playfieldGo.transform.Find("Walls/LeftSlingshot").gameObject);
			SetupRightSlingshot(playfieldGo.transform.Find("Walls/RightSlingshot").gameObject);
		}

		private static void SetupLights(GameObject playfieldGo)
		{
			var displayRegEx = new Regex("^[a-c][0-9a-f][0-9a-f]$");
			var giRegEx = new Regex("^gi\\d+$");

			foreach (var child in playfieldGo.transform.Find("Lights").gameObject.transform.Cast<Transform>().ToList()) {
				var go = child.gameObject;

				if (!displayRegEx.Match(go.name).Success) {
					var lc = go.GetComponentInParent<LightComponent>();

					if (lc != null) {
						lc.FadeSpeedUp = .2f;
						lc.FadeSpeedDown = .1f;
					}

					foreach (var l in go.GetComponentsInChildren<Light>())
					{
						RenderPipeline.Current.LightConverter.SetIntensity(l, giRegEx.Match(go.name).Success ? 120 : 50);
						RenderPipeline.Current.LightConverter.SetTemperature(l, 2700);
					}
				}
				else {
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

			// create light groups
			var lightGroups = CreateEmptyGameObject(playfieldGo, "Light Groups");
			var lamp1 = CreateEmptyGameObject(lightGroups, "Lamp 1");
			var lamp12 = CreateEmptyGameObject(lightGroups, "Lamp 12");
			var lamp13 = CreateEmptyGameObject(lightGroups, "Lamp 13");

			var lamp1Group = AddLightGroup(tableGo, lamp1,
				"gi1", "gi3", "gi4", "gi5", "gi6", "gi8",
				"gi10", "gi11", "gi12", "gi13", "gi14", "gi15", "gi16", "gi17", "gi18", "gi19",
				"gi20", "gi21", "gi23", "gi25", "gi26", "gi28", "gi29",
				"gi30", "gi31");

			var lamp12Group = AddLightGroup(tableGo, lamp12,
				"gi22", "gi24", "gi27", "L12");

			var lamp13Group = AddLightGroup(tableGo, lamp13,
				"AL1a", "AL1b", "AL2a", "AL2b", "AL3a", "AL3b", "AL4a", "AL4b",
				"AL5a", "AL5b", "AL6a", "AL6b", "AL7a", "AL7b", "AL8a", "AL8b",
				"AL9a", "AL10a", "L13a", "L13b");

			tableComponent.MappingConfig.Lamps.First(lm => lm.Id == "01").Device = lamp1Group;
			tableComponent.MappingConfig.Lamps.First(lm => lm.Id == "12").Device = lamp12Group;
			tableComponent.MappingConfig.Lamps.First(lm => lm.Id == "13").Device = lamp13Group;
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
			segment.LitColor = new UnityEngine.Color(0, 0.87f, 0.87f);
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
			segment.LitColor = new UnityEngine.Color(0, 0.87f, 0.87f);
			segment.Emission = 3;
		}

		#endregion

		private static void SetupLeftSlingshot(GameObject go)
		{
			var playfieldGo = go.GetComponentInParent<PlayfieldComponent>().gameObject;
			var ssParentGo = GetOrCreateGameObject(playfieldGo, "Slingshots");

			var ssGo = PrefabUtility.InstantiatePrefab(SlingshotComponent.LoadPrefab(), ssParentGo.transform) as GameObject;
			var ss = ssGo!.GetComponent<SlingshotComponent>();

			ss.name = "Left Slingshot";
			ss.SlingshotSurface = go.GetComponent<SurfaceColliderComponent>();
			ss.RubberOff = playfieldGo.transform.Find("Rubbers/LeftSling1").GetComponent<RubberComponent>();
			ss.RubberOn = playfieldGo.transform.Find("Rubbers/LeftSling4").GetComponent<RubberComponent>();
			ss.CoilArm = playfieldGo.transform.Find("Primitives/Lemk").GetComponent<PrimitiveComponent>();
			ss.CoilArmAngle = 22f;

			EditorUtility.SetDirty(ssGo);
			PrefabUtility.RecordPrefabInstancePropertyModifications(ss);

			ss.RebuildMeshes();
		}

		private static void SetupRightSlingshot(GameObject go)
		{
			var playfieldGo = go.GetComponentInParent<PlayfieldComponent>().gameObject;
			var ssParentGo = GetOrCreateGameObject(playfieldGo, "Slingshots");

			var ssGo = PrefabUtility.InstantiatePrefab(SlingshotComponent.LoadPrefab(), ssParentGo.transform) as GameObject;
			var ss = ssGo!.GetComponent<SlingshotComponent>();

			ss.name = "Right Slingshot";
			ss.SlingshotSurface = go.GetComponent<SurfaceColliderComponent>();
			ss.RubberOff = playfieldGo.transform.Find("Rubbers/RightSling1").GetComponent<RubberComponent>();
			ss.RubberOn = playfieldGo.transform.Find("Rubbers/RightSling3").GetComponent<RubberComponent>();
			ss.CoilArm = playfieldGo.transform.Find("Primitives/Remk").GetComponent<PrimitiveComponent>();
			ss.CoilArmAngle = 22f;

			EditorUtility.SetDirty(ssGo);
			PrefabUtility.RecordPrefabInstancePropertyModifications(ss);

			ss.RebuildMeshes();
		}

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
			material.color = UnityEngine.Color.white;
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
			material.color = UnityEngine.Color.white;
			renderer.sharedMaterial = material;
		}

		[NameMatch("sw52")]
		public void FixTargetTexture(GameObject go)
		{
			var renderer = go.GetComponent<Renderer>();
			var material = new UnityEngine.Material(renderer.sharedMaterial);
			material.mainTexture = TextureProvider.GetTexture("target1-rock");
			material.color = UnityEngine.Color.white;
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
			material.color = UnityEngine.Color.white;
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

		[NameMatch("LeftSling2")]
		[NameMatch("LeftSling3")]
		[NameMatch("RightSling2")]
		[NameMatch("RightSling3")]
		public void DisableObsoleteSlingshotElements(GameObject go)
		{
			go.SetActive(false);
		}

		[NameMatch("LeftSling1")]
		public void AddLeftSlingshotDragPoints(RubberComponent rubberComponent)
		{
			var dp = rubberComponent.DragPoints.ToList();
			dp.Insert(13, new DragPointData(219.8f, 1342.7f));
			dp.Insert(14, new DragPointData(209.1f, 1305.8f));
			rubberComponent.DragPoints = dp.ToArray();
		}

		[NameMatch("RightSling1")]
		public void AddRightSlingshotDragPoints(RubberComponent rubberComponent)
		{
			var dp = rubberComponent.DragPoints.ToList();
			dp.Insert(8, new DragPointData(657.1f, 1308.5f));
			dp.Insert(9, new DragPointData(648.8f, 1338.4f));
			rubberComponent.DragPoints = dp.ToArray();
		}

		[NameMatch("L0")]
		[NameMatch("L1")]
		[NameMatch("L2")]
		[NameMatch("L4")]
		[NameMatch("gi2")]
		[NameMatch("gi7")]
		[NameMatch("gi9")]
		public void DisableUnusedLights(GameObject go)
		{
			go.SetActive(false);
		}
	}
}
