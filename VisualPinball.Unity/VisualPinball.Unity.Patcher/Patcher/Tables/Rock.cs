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

			SetupLamps(tableGo, playfieldGo);

			SetupFlippers(playfieldGo);
			SetupDropTargetBanks(tableGo, playfieldGo);
			SetupTrough(tableGo, playfieldGo);

			SetupCollisionSwitch(playfieldGo.transform.Find("Walls/sw41a").gameObject);
			SetupCollisionSwitch(playfieldGo.transform.Find("Walls/sw41b").gameObject);
			SetupCollisionSwitch(playfieldGo.transform.Find("Walls/sw41c").gameObject);
			SetupCollisionSwitch(playfieldGo.transform.Find("Walls/sw41d").gameObject);
			SetupCollisionSwitch(playfieldGo.transform.Find("Walls/sw41e").gameObject);
			SetupCollisionSwitch(playfieldGo.transform.Find("Walls/sw41f").gameObject);

			SetupLeftSlingshot(playfieldGo.transform.Find("Walls/LeftSlingshot").gameObject);
			SetupRightSlingshot(playfieldGo.transform.Find("Walls/RightSlingshot").gameObject);

			SetupPinMame(tableGo, playfieldGo);
			SetupDisplays(tableGo);
		}

		private static void SetupLamps(GameObject tableGo, GameObject playfieldGo)
		{
			var displayRegEx = new Regex("^[a-c][0-9a-f][0-9a-f]$");

			foreach (var child in playfieldGo.transform.Find("Lights").gameObject.transform.Cast<Transform>().ToList()) {
				var go = child.gameObject;

				if (displayRegEx.Match(go.name).Success) {
					Object.DestroyImmediate(go);
				}
				else {
					var lc = go.GetComponentInParent<LightComponent>();

					if (lc != null) {
						lc.FadeSpeedUp = .2f;
						lc.FadeSpeedDown = .1f;

						PrefabUtility.RecordPrefabInstancePropertyModifications(lc);
					}
				}
			}

			var lampGroups = CreateEmptyGameObject(playfieldGo, "Lamp Groups");

			AddLightGroup(tableGo, CreateEmptyGameObject(lampGroups, "LampGroupUpperLeft1"),
				"gi26", "gi28", "gi25", "gi23");

			AddLightGroup(tableGo, CreateEmptyGameObject(lampGroups, "LampGroupUpperLeft2"),
				"gi27", "gi24", "gi22");

			AddLightGroup(tableGo, CreateEmptyGameObject(lampGroups, "LampGroupUpperRight"),
				 "gi30", "gi31", "gi14", "gi29", "gi4", "gi2", "gi7", "gi9", "gi21",
				 "gi20", "gi19", "gi18", "gi17", "gi16", "gi15", "gi13", "gi12", "gi11");

			AddLightGroup(tableGo, CreateEmptyGameObject(lampGroups, "LampGroupLower"),
				 "gi6", "gi10");

			AddLightGroup(tableGo, CreateEmptyGameObject(lampGroups, "LampGroupAux"),
				"AL1a", "AL1b", "AL2a", "AL2b", "AL3a", "AL3b", "AL4a", "AL4b",
				"AL5a", "AL5b", "AL6a", "AL6b", "AL7a", "AL7b", "AL8a", "AL8b",
				"AL9a", "AL10a");
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

		private static void SetupCollisionSwitch(GameObject go)
		{
			go.AddComponent<CollisionSwitchComponent>();
		}

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
			var go = new GameObject
			{
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

			go = new GameObject
			{
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
		[NameMatch("gi5")]
		[NameMatch("gi1")]
		[NameMatch("gi3")]
		[NameMatch("gi8")]
		public void DisableLamps(GameObject go)
		{
			go.SetActive(false);
		}

		[NameMatch("gi14")]
		public void FixGI14(GameObject go)
		{
			go.GetComponent<LightComponent>().transform.localPosition = new Vector3(894.29f, 178.4f);
		}

		[NameMatch("gi2")]
		[NameMatch("gi4")]
		[NameMatch("gi6")]
		[NameMatch("gi7")]
		[NameMatch("gi9")]
		[NameMatch("gi10")]
		[NameMatch("gi11")]
		[NameMatch("gi12")]
		[NameMatch("gi13")]
		[NameMatch("gi14")]
		[NameMatch("gi15")]
		[NameMatch("gi16")]
		[NameMatch("gi17")]
		[NameMatch("gi18")]
		[NameMatch("gi19")]
		[NameMatch("gi20")]
		[NameMatch("gi21")]
		[NameMatch("gi23")]
		[NameMatch("gi25")]
		[NameMatch("gi26")]
		[NameMatch("gi28")]
		[NameMatch("gi29")]
		[NameMatch("gi30")]
		[NameMatch("gi31")]
		[NameMatch("gi22")]
		[NameMatch("gi24")]
		[NameMatch("gi27")]
		public void FixGIs(GameObject go)
		{
			LightTemperature(go, 2700f);
			LightIntensity(go, 120f);
		}

		[NameMatch("L3")]
		[NameMatch("L5")]
		[NameMatch("L6")]
		[NameMatch("L7")]
		[NameMatch("L8")]
		[NameMatch("L9")]
		[NameMatch("L10")]
		[NameMatch("L11")]
		[NameMatch("L12")]
		[NameMatch("L13a")]
		[NameMatch("L13b")]
		[NameMatch("L14")]
		[NameMatch("L15")]
		[NameMatch("L16")]
		[NameMatch("L17")]
		[NameMatch("L18")]
		[NameMatch("L19")]
		[NameMatch("L20")]
		[NameMatch("L21")]
		[NameMatch("L22")]
		[NameMatch("L23")]
		[NameMatch("L24")]
		[NameMatch("L25")]
		[NameMatch("L26")]
		[NameMatch("L27")]
		[NameMatch("L28")]
		[NameMatch("L29")]
		[NameMatch("L30")]
		[NameMatch("L31")]
		[NameMatch("L32")]
		[NameMatch("L33")]
		[NameMatch("L34")]
		[NameMatch("L35")]
		[NameMatch("L36")]
		[NameMatch("L37")]
		[NameMatch("L38")]
		[NameMatch("L39")]
		[NameMatch("L40")]
		[NameMatch("L41")]
		[NameMatch("L42")]
		[NameMatch("L43")]
		[NameMatch("L44")]
		[NameMatch("L45")]
		[NameMatch("L46")]
		[NameMatch("L47a")]
		[NameMatch("L47b")]
		[NameMatch("L51a")]
		[NameMatch("L51b")]
		public void FixLamps(GameObject go)
		{
			LightTemperature(go, 2700f);
			LightIntensity(go, 50f);
		}

		[NameMatch("AL1a")]
		[NameMatch("AL1b")]
		[NameMatch("AL2a")]
		[NameMatch("AL2b")]
		[NameMatch("AL3a")]
		[NameMatch("AL3b")]
		[NameMatch("AL4a")]
		[NameMatch("AL4b")]
		[NameMatch("AL5a")]
		[NameMatch("AL5b")]
		[NameMatch("AL6a")]
		[NameMatch("AL6b")]
		[NameMatch("AL7a")]
		[NameMatch("AL7b")]
		[NameMatch("AL8a")]
		[NameMatch("AL8b")]
		[NameMatch("AL9a")]
		[NameMatch("AL10a")]
		public void FixAuxilaryLamps(GameObject go)
		{
			LightTemperature(go, 5500f);
			LightColor(go, UnityEngine.Color.white);
			LightIntensity(go, 175f);
		}
	}
}
