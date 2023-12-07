// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using Color = UnityEngine.Color;

namespace VisualPinball.Unity.Patcher
{
	[MetaMatch(TableName = "Terminator 2 - Judgment Day (Williams 1991)", AuthorName = "g5k")]
	public class Terminator2 : TablePatcher
	{
		#region Global

		public override void PostPatch(GameObject tableGo)
		{
			var playfieldGo = Playfield(tableGo);
			playfieldGo.isStatic = true;

			// playfield material
			RenderPipeline.Current.MaterialConverter.SetSmoothness(playfieldGo.GetComponent<Renderer>().sharedMaterial, 0.96f);

			// missing lights
			CreateFlasher28(playfieldGo);

			SetupTrough(tableGo, playfieldGo);
			SetupPinMame(tableGo, playfieldGo);
			SetupDmd(tableGo);
			SetupMapping(tableGo);

			// slingshots
			SetupLeftSlingshot(playfieldGo.transform.Find("Walls/LeftSlingshot").gameObject);
			SetupRightSlingshot(playfieldGo.transform.Find("Walls/RightSlingshot").gameObject);
		}

		private static void SetupTrough(GameObject tableGo, GameObject playfieldGo)
		{
			var troughComponent = CreateTrough(tableGo, playfieldGo);
			troughComponent.Type = TroughType.TwoCoilsNSwitches;
			troughComponent.BallCount = 3;
			troughComponent.SwitchCount = 3;
			troughComponent.JamSwitch = false;
			troughComponent.RollTime = 300;
		}

		private static void SetupPinMame(GameObject tableGo, GameObject playfieldGo)
		{
			#if !NO_PINMAME
			var tableComponent = tableGo.GetComponent<TableComponent>();

			// GLE
			Object.DestroyImmediate(tableGo.GetComponent<DefaultGamelogicEngine>());
			var pinmameGle = tableGo.AddComponent<Engine.PinMAME.PinMameGamelogicEngine>();
			pinmameGle.Game = new Engine.PinMAME.Games.Terminator2();
			pinmameGle.romId = "t2_l82";
			tableComponent.RepopulateHardware(pinmameGle);
			TableSelector.Instance.TableUpdated();

			// create GI light groups
			var gi = CreateEmptyGameObject(playfieldGo, "GI");
			var gi1 = CreateEmptyGameObject(gi, "CPU");
			var gi2 = CreateEmptyGameObject(gi, "Left Playfield");
			var gi3 = CreateEmptyGameObject(gi, "Right Playfield");
			var giCpu = AddLightGroup(tableGo, gi1, "Light2", "Light3", "Light4", "Light5");
			var giLeftPlayfield = AddLightGroup(tableGo, gi2, "GI_35", "GI_1", "GI_3", "GI_4", "GI_12", "GI_7",
				"GI_8", "GI_9", "GI_13", "GI_14", "GI_23", "GI_24", "GI_25", "GI_38");
			var giRightPlayfield = AddLightGroup(tableGo, gi3, "GI_36", "GI_2", "GI_5", "GI_6", "GI_10", "GI_11", "GI_15", "GI_16", "GI_18", "GI_19", "GI_17",
				"GI_20", "GI_21", "GI_22", "GI_26", "GI_27", "GI_28", "GI_30", "GI_29", "GI_31", "GI_32", "GI_33", "GI_34", "GI_37", "B1", "B2", "B3");

			// map GI light groups
			tableComponent.MappingConfig.Lamps.First(lm => lm.Id == "2").Device = giRightPlayfield;
			tableComponent.MappingConfig.Lamps.First(lm => lm.Id == "3").Device = giCpu;
			tableComponent.MappingConfig.Lamps.First(lm => lm.Id == "4").Device = giLeftPlayfield;
			#endif
		}

		private static void SetupMapping(GameObject tableGo)
		{
			var tc = tableGo.GetComponent<TableComponent>();

			var kickers = tableGo.GetComponentsInChildren<KickerComponent>();
			foreach (var kicker in kickers) {

				// shooter
				LinkSwitch(tc, "sw78", "78", kicker);
				LinkCoil(tc, "sw78", "09", kicker);

				// top lock
				LinkCoil(tc, "sw55", "10", kicker);

				// left lock
				LinkCoil(tc, "sw51", "16", kicker);

				// cannon
				LinkCoil(tc, "sw31", "02", kicker);
			}

			var plungers = tableGo.GetComponentsInChildren<PlungerComponent>();
			foreach (var plunger in plungers) {
				// kickback
				LinkCoil(tc, "Plunger1", "08", plunger, PlungerComponent.FireCoilId);
			}

			var teleporters = tableGo.GetComponentsInChildren<TeleporterComponent>();
			foreach (var teleporter in teleporters) {

				// skull kicker
				LinkCoil(tc, "sw76", "01", teleporter, TeleporterComponent.CoilItem);
			}

			var bumpers = tableGo.GetComponentsInChildren<BumperComponent>();
			foreach (var bumper in bumpers) {
				LinkSwitch(tc, "Bumper1", "41", bumper);
				LinkSwitch(tc, "Bumper2", "43", bumper);
				LinkSwitch(tc, "Bumper3", "42", bumper);
			}

			// cannon
			var cannonGo = tc.transform.Find("Playfield/Mechs/Cannon").gameObject;
			var cannonMech = cannonGo.GetComponent<StepRotatorMechComponent>();
			LinkSwitch(tc, "32", cannonMech, "gun_mark_switch");
			LinkSwitch(tc, "33", cannonMech, "gun_home_switch");
			LinkCoil(tc, "11", cannonMech);
		}

		private static void SetupDmd(GameObject tableGo)
		{
			const float scale = 0.43f;
			var cabinetGo = tableGo.transform.Find("Cabinet").gameObject;
			var go = new GameObject {
				name = "Dot Matrix Display",
				transform = {
					localEulerAngles = new Vector3(5.963f, 0, 0),
					localPosition = new Vector3(0f, 0.513f, 1.0329f),
					localScale = new Vector3(scale, scale, scale)
				}
			};
			go.transform.SetParent(cabinetGo.transform, false);

			var dmd = go.AddComponent<DotMatrixDisplayComponent>();
			dmd.UpdateDimensions(128, 32);
			dmd.Emission = 100f;
		}

		#endregion

		#region Geometry

		[NameMatch("_Apron")]
		[NameMatch("_BackWall")]
		[NameMatch("_Bulbs (GI - visible)")]
		[NameMatch("_ChromeRails")]
		[NameMatch("_ClearPlastics")]
		[NameMatch("_CliffyPosts")]
		[NameMatch("_HKShip")]
		[NameMatch("_LeftOVerBits")]
		[NameMatch("_LeftRampMetal")]
		[NameMatch("_MetalPosts")]
		[NameMatch("_Plastics")]
		[NameMatch("_Posts")]
		[NameMatch("_RedPlastics")]
		[NameMatch("_RightRampPlastic")]
		[NameMatch("_Rubbers")]
		[NameMatch("_Screws")]
		[NameMatch("_StarPosts")]
		[NameMatch("_SteelBits")]
		[NameMatch("_SteelWalls")]
		[NameMatch("_Switches")]
		[NameMatch("_Timber")]
		[NameMatch("_Washers")]
		[NameMatch("_WireSwitches")]
		[NameMatch("_Wireforms")]
		[NameMatch("_popbumperassy")]
		[NameMatch("_wires")]
		public void MakeStatic(GameObject go)
		{
			go.isStatic = true;
		}

		[NameMatch("RubberPostPrim001")]
		[NameMatch("RubberPostPrim002")]
		[NameMatch("RubberPostPrim003")]
		[NameMatch("RubberPostPrim004")]
		[NameMatch("RubberPostPrim005")]
		[NameMatch("RubberPostPrim006")]
		[NameMatch("RubberPostPrim007")]
		[NameMatch("RubberPostPrim008")]
		[NameMatch("RubberPostPrim009")]
		[NameMatch("RubberPostPrim010")]
		[NameMatch("RubberPostPrim011")]
		[NameMatch("RubberPostPrim012")]
		[NameMatch("RubberPostPrim013")]
		[NameMatch("RubberPostPrim014")]
		[NameMatch("RubberPostPrim015")]
		[NameMatch("RubberPostPrim016")]
		public void PrimitiveCollider(GameObject go)
		{
			go.isStatic = true;
			Object.DestroyImmediate(go.GetComponent<PrimitiveMeshComponent>());
			Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
			Object.DestroyImmediate(go.GetComponent<MeshFilter>());
		}

		[NameMatch("EndPointLp")]
		[NameMatch("EndPointRp")]
		[NameMatch("batleftshadow")]
		[NameMatch("batrightshadow")]
		[NameMatch("FlasherT1")]
		[NameMatch("FlasherT2")]
		public void Disable(GameObject go)
		{
			go.SetActive(false);
		}


		[NameMatch("Wall002")]
		[NameMatch("Wall003")]
		public void InvisibleWall(GameObject go)
		{
			foreach (var mr in go.GetComponentsInChildren<MeshRenderer>()) {
				mr.gameObject.SetActive(false);
			}
		}

		#endregion

		#region Flippers

		[NameMatch("batleft", Ref = "Playfield/Flippers/LeftFlipper")]
		[NameMatch("batright", Ref = "Playfield/Flippers/RightFlipper")]
		public void ReparentFlippers(PrimitiveComponent flipper, GameObject gameObject, ref GameObject parent)
		{
			PatcherUtil.Reparent(gameObject, parent);

			flipper.Position = Vector2.zero;
			// flipper.ObjectRotation.z = 0;
		}

		[NameMatch("LeftFlipper")]
		[NameMatch("RightFlipper")]
		public void DeleteFlipperMeshes(GameObject go)
		{
			go.GetComponentInChildren<FlipperBaseMeshComponent>().gameObject.SetActive(false);
			go.GetComponentInChildren<FlipperRubberMeshComponent>().gameObject.SetActive(false);
		}

		#endregion

		#region Slingshots

		[NameMatch("LSling2")]
		[NameMatch("RSling2")]
		[NameMatch("_SteelBits")]
		public void DisableObsoleteSlingshotElements(GameObject go)
		{
			go.SetActive(false);
		}

		[NameMatch("LSling")]
		public void AddLeftSlingshotDragPoints(RubberComponent rubberComponent)
		{
			var dp = rubberComponent.DragPoints.ToList();
			dp.RemoveAt(9);
			dp.RemoveAt(10);
			dp.Insert(9, new DragPointData(208.9f, 1597.3f));
			dp.Insert(10, new DragPointData(202.9f, 1580.6f));
			rubberComponent.DragPoints = dp.ToArray();
		}

		[NameMatch("RSling")]
		public void AddRightSlingshotDragPoints(RubberComponent rubberComponent)
		{
			var dp = rubberComponent.DragPoints.ToList();
			dp.RemoveAt(4);
			dp.RemoveAt(5);
			dp.Insert(4, new DragPointData(661.6f, 1583f));
			dp.Insert(5, new DragPointData(657f, 1595.7f));
			rubberComponent.DragPoints = dp.ToArray();
		}

		[NameMatch("SLING1")]
		[NameMatch("SLING2")]
		public void EnableSlingshotArm(GameObject go)
		{
			go.GetComponent<PrimitiveMeshComponent>().enabled = true;
		}

		private static void SetupLeftSlingshot(GameObject go)
		{
			var playfieldGo = go.GetComponentInParent<PlayfieldComponent>().gameObject;
			var ssParentGo = GetOrCreateGameObject(playfieldGo, "Slingshots");

			var ssGo = PrefabUtility.InstantiatePrefab(SlingshotComponent.LoadPrefab(), ssParentGo.transform) as GameObject;
			var ss = ssGo!.GetComponent<SlingshotComponent>();

			ss.name = "Left Slingshot";
			ss.SlingshotSurface = go.GetComponent<SurfaceColliderComponent>();
			ss.RubberOff = playfieldGo.transform.Find("Rubbers/LSling").GetComponent<RubberComponent>();
			ss.RubberOn = playfieldGo.transform.Find("Rubbers/LSling1").GetComponent<RubberComponent>();
			//ss.CoilArm = playfieldGo.transform.Find("Primitives/SLING2").GetComponent<PrimitiveComponent>();

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
			ss.RubberOff = playfieldGo.transform.Find("Rubbers/RSling").GetComponent<RubberComponent>();
			ss.RubberOn = playfieldGo.transform.Find("Rubbers/RSling1").GetComponent<RubberComponent>();
			//ss.CoilArm = playfieldGo.transform.Find("Primitives/SLING1").GetComponent<PrimitiveComponent>();

			EditorUtility.SetDirty(ssGo);
			PrefabUtility.RecordPrefabInstancePropertyModifications(ss);

			ss.RebuildMeshes();
		}

		#endregion

		#region Kickers

		[NameMatch("BallRelease")]
		public void FixSw17(KickerComponent kickerComponent)
		{
			kickerComponent.Coils[0].Name = "Eject";
			kickerComponent.Coils[0].Speed = 5;
			kickerComponent.Coils[0].Angle = 60;
		}

		[NameMatch("sw78")]
		public void Shooter(KickerComponent kickerComponent)
		{
			kickerComponent.Coils[0].Name = "Shooter";
			kickerComponent.Coils[0].Speed = 50;
			kickerComponent.Coils[0].Angle = 0;
		}

		[NameMatch("sw76")]
		public void SkullKicker(KickerComponent kickerComponent)
		{
			kickerComponent.Coils.Clear();
			var tp = kickerComponent.gameObject.AddComponent<TeleporterComponent>();
			tp.FromKicker = kickerComponent;
			tp.ToKicker = FindSiblingComponent<KickerComponent>(kickerComponent, "sw76a");
			tp.ToKickerItem = tp.ToKicker.AvailableCoils.First().Id;
			tp.EjectDelay = 0;
			tp.EjectAfterTeleportation = true;
		}

		[NameMatch("sw76a")]
		public void Teleporter(KickerComponent kickerComponent)
		{
			kickerComponent.Coils[0].Name = "Teleporter Out";
			kickerComponent.Coils[0].Speed = 3;
			kickerComponent.Coils[0].Angle = 72;
		}

		[NameMatch("sw55")]
		public void TopLock(KickerComponent kickerComponent)
		{
			kickerComponent.Coils[0].Name = "Kick Out";
			kickerComponent.Coils[0].Speed = 5;
			kickerComponent.Coils[0].Angle = 270;
		}

		[NameMatch("sw51")]
		public void LeftLock(KickerComponent kickerComponent)
		{
			kickerComponent.Coils[0].Name = "Kick Out";
			kickerComponent.Coils[0].Speed = 13;
			kickerComponent.Coils[0].Angle = 160;
		}

		#endregion

		#region Mechs

		[NameMatch("sw77")]
		public void CreateDropTargetBank(GameObject dropTargetGo, DropTargetComponent dropTargetComponent)
		{
			var dropTargetBank = dropTargetGo.AddComponent<DropTargetBankComponent>();
			dropTargetBank.BankSize = 1;
			dropTargetBank.DropTargets = new[] { dropTargetComponent };
		}

		[NameMatch("T2_Gun")]
		public void SetupCannon(GameObject primitiveGo, PrimitiveComponent cannonComp)
		{
			var playfieldGo = primitiveGo.GetComponentInParent<PlayfieldComponent>().gameObject;
			var mechsParent = GetOrCreateGameObject(playfieldGo, "Mechs");

			var rotatorGo = CreateEmptyGameObject(mechsParent, "Cannon");
			var rotatorComp = rotatorGo.AddComponent<RotatorComponent>();
			var mechComp = rotatorGo.AddComponent<StepRotatorMechComponent>();
			//mechComp.Target = rotatorComp;
			mechComp.NumSteps = 240;
			mechComp.Marks = new[] {
				new MechMark(MechMarkSwitchType.EnableBetween, "Gun Home", "gun_home_switch", 0, 5),
				new MechMark(MechMarkSwitchType.EnableBetween, "Gun Mark", "gun_mark_switch", 98, 105),
			};

			//rotatorComp.Target = cannonComp;
			rotatorComp.RotateWith = new IRotatableComponent[] {
				playfieldGo.transform.Find("Kickers/sw31").GetComponent<KickerComponent>(),
			};
		}

		#endregion

		#region Materials

		[NameMatch("_Plastics")]
		[NameMatch("_RedPlastics")]
		[NameMatch("_RightRampPlastic")]
		[NameMatch("_StarPosts")]
		public void FixTranslucentMaterials(GameObject go)
		{
			var material = go.GetComponent<Renderer>().sharedMaterial;
			RenderPipeline.Current.MaterialConverter.SetDiffusionProfile(material, DiffusionProfileTemplate.Plastics);
			RenderPipeline.Current.MaterialConverter.SetMaterialType(material, MaterialType.Translucent);
		}

		[NameMatch("Cab_DT")]
		[NameMatch("L52")]
		[NameMatch("LSling")]
		[NameMatch("Rubber2")]
		[NameMatch("Rubber7")]
		[NameMatch("SLING1")]
		[NameMatch("T2_Gun")]
		[NameMatch("_Apron")]
		[NameMatch("_ChromeRails")]
		[NameMatch("_CliffyPosts")]
		[NameMatch("_LeftRampMetal")]
		[NameMatch("_MetalPosts")]
		[NameMatch("_Rubbers")]
		[NameMatch("_Screws")]
		[NameMatch("_SteelWalls")]
		[NameMatch("_Targets")]
		[NameMatch("_Washers")]
		[NameMatch("_Wireforms")]
		[NameMatch("batleft")]
		[NameMatch("batright")]
		[NameMatch("sw53")]
		public void FixSolidMaterials(GameObject go)
		{
			var material = go.GetComponent<Renderer>().sharedMaterial;
			RenderPipeline.Current.MaterialConverter.SetMaterialType(material, MaterialType.Standard);
		}

		[NameMatch("_HKShip")]
		public void FixShip(GameObject go)
		{
			var material = go.GetComponent<Renderer>().sharedMaterial;
			RenderPipeline.Current.MaterialConverter.SetMaterialType(material, MaterialType.Standard);
			RenderPipeline.Current.MaterialConverter.SetSmoothness(material, 1f);
		}

		#endregion

		#region Lights

		#region Flashers

		[NameMatch("F118", FloatParam = 10000f)]
		[NameMatch("F119", FloatParam = 10000f)]
		[NameMatch("F120", FloatParam = 10000f)]
		[NameMatch("F121", FloatParam = 10000f)]
		[NameMatch("F122", FloatParam = 20000f)]
		[NameMatch("F123", FloatParam = 20000f)]
		[NameMatch("F125", FloatParam = 10000f)]
		[NameMatch("F126", FloatParam = 10000f)]
		[NameMatch("F126a", FloatParam = 10000f)]
		[NameMatch("F127", FloatParam = 10000f)]
		[NameMatch("F127a", FloatParam = 10000f)]
		public void FlasherIntensities(GameObject go, LightComponent lc, float param)
		{
			lc.FadeSpeedUp = 0f;
			lc.FadeSpeedDown = 0.15f;
			foreach (var l in go.GetComponentsInChildren<Light>()) {
				RenderPipeline.Current.LightConverter.SetIntensity(l, param);
				RenderPipeline.Current.LightConverter.SetTemperature(l, 3000);
				RenderPipeline.Current.LightConverter.SetShadow(l, true, true, 0.001f);
			}
		}

		[NameMatch("F120")]
		public void F120Pos(GameObject go) => LightPos(go, -160.8f, 17.6f, -6f);
		[NameMatch("F121")] public void F121Pos(GameObject go) => LightPos(go, 0f, -12.5f, 15.7f);
		[NameMatch("F122")] public void F122Pos(GameObject go) => LightPos(go, -4f, 97f, 25f);
		[NameMatch("F123")] public void F123Pos(GameObject go)
		{
			LightPos(go, -6.9f, -21.9f, 94.1f);
			LightRange(go, 0.3f);
		}
		[NameMatch("F125")] public void F125Pos(GameObject go)
		{
			LightPos(go, 42.5f, -75.8f, 12.8f);
			LightRange(go, 0.17f);
		}
		[NameMatch("F126")] public void F126(GameObject go, LightComponent lightComponent)
		{
			LightPos(go, 51.6f, 34.5f, 5f);
			LinkLights(go, lightComponent.name, "F126a");
		}
		[NameMatch("F126a")] public void F126aPos(GameObject go) => LightPos(go, -28.1f, 61.1f, 37f);

		[NameMatch("F127")] public void F127(GameObject go, LightComponent lightComponent)
		{
			LightPos(go, -33.2f, 2.9f, 5f);
			LinkLights(go, lightComponent.name, "F127a");
		}
		[NameMatch("F127a")] public void F127aPos(GameObject go) => LightPos(go, -31.5f, 89.5f, 37f);

		private static void CreateFlasher28(GameObject playfieldGo)
		{
			var parent = GetOrCreateGameObject(playfieldGo, "Lights");
			var lightComponent = CreateLight("F128", 302.4f, 361.4f, parent);
			var go = lightComponent.gameObject;
			SpotAngle(go, 104f, 53.4f);
			LightPos(go, 0, 0, -79f);
			LightIntensity(go, 10000f);
			LightTemperature(go, 3400f);
			LightRange(go, 0.11f);
		}

		#endregion

		#region Global Illumination

		[NameMatch("GI_1")]
		[NameMatch("GI_2")]
		[NameMatch("GI_3")]
		[NameMatch("GI_4")]
		[NameMatch("GI_5")]
		[NameMatch("GI_6")]
		[NameMatch("GI_7")]
		[NameMatch("GI_8")]
		[NameMatch("GI_9")]
		[NameMatch("GI_10")]
		[NameMatch("GI_11")]
		[NameMatch("GI_12")]
		[NameMatch("GI_13")]
		[NameMatch("GI_14")]
		[NameMatch("GI_15")]
		[NameMatch("GI_16")]
		[NameMatch("GI_17")]
		[NameMatch("GI_18")]
		[NameMatch("GI_19")]
		[NameMatch("GI_20")]
		[NameMatch("GI_21")]
		[NameMatch("GI_22")]
		[NameMatch("GI_23")]
		[NameMatch("GI_24")]
		[NameMatch("GI_25")]
		[NameMatch("GI_26")]
		[NameMatch("GI_27")]
		[NameMatch("GI_28")]
		[NameMatch("GI_29")]
		[NameMatch("GI_30")]
		[NameMatch("GI_31")]
		[NameMatch("GI_32")]
		[NameMatch("GI_33")]
		[NameMatch("GI_34")]
		[NameMatch("GI_35")]
		[NameMatch("GI_36")]
		[NameMatch("GI_37")]
		[NameMatch("GI_38")]
		public void GiIntensity(GameObject go)
		{
			foreach (var l in go.GetComponentsInChildren<Light>()) {
				RenderPipeline.Current.LightConverter.SetIntensity(l, 200f);
				RenderPipeline.Current.LightConverter.SetTemperature(l, 2700);
			}
		}

		[NameMatch("GI_3", FloatParam = 0.01f)]
		[NameMatch("GI_4", FloatParam = 0.01f)]
		[NameMatch("GI_5", FloatParam = 0.02f)]
		[NameMatch("GI_6", FloatParam = 0.01f)]

		public void GiDynamicShadow(GameObject go, float param) => LightShadow(go, false, false, param);

		[NameMatch("GI_10", FloatParam = 0.01f)]
		[NameMatch("GI_11", FloatParam = 0.01f)]
		[NameMatch("GI_12", FloatParam = 0.01f)]
		[NameMatch("GI_15", FloatParam = 0.02f)]
		[NameMatch("GI_16", FloatParam = 0.02f)]
		[NameMatch("GI_18", FloatParam = 0.01f)]
		[NameMatch("GI_19", FloatParam = 0.02f)]
		[NameMatch("GI_20", FloatParam = 0.02f)]
		[NameMatch("GI_33", FloatParam = 0.02f)]
		[NameMatch("GI_34", FloatParam = 0.02f)]
		[NameMatch("GI_37", FloatParam = 0.01f)]
		[NameMatch("GI_38", FloatParam = 0.01f)]
		[NameMatch("GI_7", FloatParam = 0.01f)] // leaks
		[NameMatch("GI_8", FloatParam = 0.01f)] // leaks
		[NameMatch("GI_9", FloatParam = 0.01f)] // leaks
		[NameMatch("GI_13", FloatParam = 0.01f)] // leaks
		[NameMatch("GI_14", FloatParam = 0.01f)] // leaks
		[NameMatch("GI_22", FloatParam = 0.01f)] // leaks
		[NameMatch("GI_23", FloatParam = 0.01f)] // leaks
		[NameMatch("GI_24", FloatParam = 0.01f)] // leaks (not too badly)
		[NameMatch("GI_25", FloatParam = 0.01f)] // leaks (not too badly)
		[NameMatch("GI_29", FloatParam = 0.01f)] // leaks
		[NameMatch("GI_30", FloatParam = 0.01f)] // leaks
		[NameMatch("GI_31", FloatParam = 0.01f)] // leaks
		[NameMatch("GI_32", FloatParam = 0.01f)] // leaks
		[NameMatch("GI_35", FloatParam = 0.01f)] // leaks (not too badly)
		public void GiStaticShadow(GameObject go, float param) => LightShadow(go, false, false, param);

		[NameMatch("GI_27")]
		[NameMatch("GI_28")]
		public void GiDisable(GameObject go) => go.SetActive(false);

		[NameMatch("B1")]
		[NameMatch("B2")]
		[NameMatch("B3")]
		public void Bumpers(GameObject go)
		{
			LightIntensity(go,  1000f);
			LightTemperature(go, 2700f);
		}
		#endregion

		#region Insert Links

		[NameMatch("L22a")] public void Link22(GameObject go) => LinkLights(go, "L22a", "L22b");
		[NameMatch("L51a")] public void Link51(GameObject go) => LinkLights(go, "L51a", "L51b");

		[NameMatch("L481")]
		[NameMatch("L581")]
		public void MakeInsert481(LightComponent lo)
		{
			var go = ConvertToInsertLight(lo);
			// those don't actually need a light source, because the first light shines through the insert mesh.
			go.transform.Find("Source").gameObject.SetActive(false);
		}

		[NameMatch("L43")]
		[NameMatch("L44")]
		[NameMatch("L45")]
		[NameMatch("L46")]
		[NameMatch("L47")]
		[NameMatch("L48")]
		[NameMatch("L481")]
		[NameMatch("L53")]
		[NameMatch("L54")]
		[NameMatch("L55")]
		[NameMatch("L56")]
		[NameMatch("L57")]
		[NameMatch("L58")]
		[NameMatch("L581")]
		public void Rectangles(GameObject go)
		{
			SpotAngle(go, 122f, 48f);
			LightIntensity(go, 4500f);
		}

		[NameMatch("L16")]
		[NameMatch("L22a")]
		[NameMatch("L22b")]
		[NameMatch("L23")]
		[NameMatch("L24")]
		public void MidSizedCircles(GameObject go)
		{
			SpotAngle(go, 64f, 67f);
			LightIntensity(go, 1200f);
		}

		[NameMatch("L76")]
		[NameMatch("L77")]
		[NameMatch("L78")]
		public void SmallSizedCircles(GameObject go)
		{
			SpotAngle(go, 45f, 13f);
			LightIntensity(go, 700f);
		}

		[NameMatch("L61")]
		[NameMatch("L62")]
		[NameMatch("L63")]
		[NameMatch("L64")]
		[NameMatch("L65")]
		[NameMatch("L71")]
		[NameMatch("L72")]
		[NameMatch("L73")]
		[NameMatch("L74")]
		[NameMatch("L75")]
		public void InnerYellowCircles(GameObject go)
		{
			LightColor(go, Color.yellow);
		}

		[NameMatch("L11")]
		[NameMatch("L12")]
		[NameMatch("L13")]
		[NameMatch("L14")]
		[NameMatch("L15")]
		[NameMatch("L21")]
		[NameMatch("L25")]
		[NameMatch("L26")]
		[NameMatch("L27")]
		[NameMatch("L28")]
		[NameMatch("L41")]
		[NameMatch("L42")]
		[NameMatch("L66")]
		[NameMatch("L67")]
		[NameMatch("L68")]
		[NameMatch("L81")]
		[NameMatch("L82")]
		[NameMatch("L83")]
		[NameMatch("L85")]
		public void MidSizedTriangles(GameObject go)
		{
			SpotAngle(go, 96.4f, 47.8f);
		}

		[NameMatch("L31")]
		[NameMatch("L32")]
		[NameMatch("L33")]
		[NameMatch("L34")]
		[NameMatch("L35")]
		[NameMatch("L36")]
		[NameMatch("L37")]
		[NameMatch("L38")]
		public void SmallSizedTriangles(GameObject go)
		{
			SpotAngle(go, 80f, 45f);
		}

		[NameMatch("L36")]
		[NameMatch("L37")]
		[NameMatch("L38")]
		public void SmallSizedTrianglesRed(GameObject go)
		{
			LightIntensity(go, 1000f);
		}

		[NameMatch("L31")]
		[NameMatch("L32")]
		[NameMatch("L33")]
		[NameMatch("L34")]
		[NameMatch("L35")]
		public void SmallSizedTrianglesWhite(GameObject go)
		{
			LightIntensity(go, 700f);
			LightColor(go, Color.white);
		}

		[NameMatch("L11")]
		[NameMatch("L12")]
		[NameMatch("L14")]
		[NameMatch("L15")]
		[NameMatch("L26")]
		[NameMatch("L41")]
		[NameMatch("L66")]
		public void MidSizedTrianglesGreen(GameObject go)
		{
			LightIntensity(go, 3400f);
		}

		[NameMatch("L13")]
		[NameMatch("L21")]
		[NameMatch("L27")]
		[NameMatch("L81")]
		[NameMatch("L82")]
		public void MidSizedTrianglesRed(GameObject go)
		{
			LightIntensity(go, 1850f);
		}

		[NameMatch("L85")]
		public void MidSizedTrianglesWhite(GameObject go)
		{
			LightIntensity(go, 1850f);
			LightColor(go, Color.white);
		}

		[NameMatch("L28")]
		public void MidSizedTrianglesBlue(GameObject go)
		{
			LightIntensity(go, 12000f);
			LightColor(go, new Color(0f, 0.4f, 1));
		}

		[NameMatch("L25")]
		[NameMatch("L42")]
		[NameMatch("L67")]
		public void MidSizedTrianglesYellow(GameObject go)
		{
			LightIntensity(go, 1850f);
			LightColor(go, Color.yellow);
		}

		#endregion

		#region Insert Positions

		[NameMatch("L11")] public void Insert2xPos(GameObject go) => LightPos(go, 28.6f, 9.6f, -50f);
		[NameMatch("L12")] public void Insert4xPos(GameObject go) => LightPos(go, 22.4f, 25.1f, -50f);
		[NameMatch("L13")] public void InsertHoldBonus(GameObject go) => LightPos(go, 0f, 35f, -50f);
		[NameMatch("L14")] public void Insert6xPos(GameObject go) => LightPos(go, -24.7f, 25.1f, -50f);
		[NameMatch("L15")] public void Insert8xPos(GameObject go) => LightPos(go, -31.3f, 10.7f, -50f);
		[NameMatch("L16")] public void BallSave(GameObject go) => LightPos(go, 0, -19.6f, -50f);

		[NameMatch("L17")]
		public void T2Mouth(GameObject go)
		{
			LightPos(go, 0f, 0f, -50f);
			SpotAngle(go, 73f, 10f);
			LightIntensity(go, 340f);
		}

		[NameMatch("L21")]
		public void Kickback(GameObject go) => LightPos(go, -3f, -13.5f, -50f);

		[NameMatch("L22a")]
		[NameMatch("L22b")]
		public void Special(GameObject go) => LightPos(go, 0, -15f, -50f);

		[NameMatch("L23")]
		[NameMatch("L24")]
		public void HurryUp(GameObject go) => LightPos(go, 0, -16f, -50f);

		[NameMatch("L25")]
		[NameMatch("L26")]
		[NameMatch("L27")]
		[NameMatch("L28")]
		[NameMatch("L85")]
		public void LockLane(GameObject go) => LightPos(go, -6f, -35f, -50f);


		[NameMatch("F117")]
		public void AutoFire(GameObject go)
		{
			SpotAngle(go, 70f, 15f);
			LightIntensity(go, 4000f);

			LightPos(go, -52f, 3.3f, -50f);
			DuplicateLight(go, -18f, 0f, -50f);
			DuplicateLight(go, 16f, 0f, -50f);
			DuplicateLight(go, 52f, 3.3f, -50f);
		}

		[NameMatch("L31")]
		[NameMatch("L32")]
		[NameMatch("L33")]
		[NameMatch("L34")]
		[NameMatch("L35")]
		public void LeftSmallTriangles(GameObject go) => LightPos(go, -21f, -10f, -50f);

		[NameMatch("L36")]
		[NameMatch("L37")]
		[NameMatch("L38")]
		public void TopSmallTriangles(GameObject go) => LightPos(go, 1.7f, -25f, -50f);

		[NameMatch("L41")]
		[NameMatch("L42")]
		public void LockVideo(GameObject go) => LightPos(go, -8.2f, -35f, -50f);

		[NameMatch("L43")]
		[NameMatch("L44")]
		[NameMatch("L45")]
		[NameMatch("L46")]
		[NameMatch("L47")]
		public void LeftRedRect(GameObject go) => LightPos(go, -6.1f, -12.3f, -50f);

		[NameMatch("L48")]
		public void LeftBottomRedRect(GameObject go) => LightPos(go, -2.4f, 16.6f, -50f);


		[NameMatch("L51a")]
		[NameMatch("L51b")]
		public void T2Eyes(GameObject go)
		{
			LightPos(go, -2.2f, 0.8f, -50f);
			SpotAngle(go, 27f, 60f);
			LightIntensity(go, 1000f);
		}

		[NameMatch("L53")]
		[NameMatch("L54")]
		[NameMatch("L55")]
		[NameMatch("L56")]
		[NameMatch("L57")]
		public void RightRedRect(GameObject go) => LightPos(go, 8.5f, -11.7f, -50f);
		[NameMatch("L58")] public void L58Pos(GameObject go) => LightPos(go, -2.3f, 20f, -50f);

		[NameMatch("L61")]
		[NameMatch("L62")]
		[NameMatch("L63")]
		[NameMatch("L64")]
		[NameMatch("L65")]
		public void LeftYellowCircle(GameObject go) => LightPos(go, -4.3f, -18.2f, -50f);

		[NameMatch("L66")]
		[NameMatch("L67")]
		public void LeftOuterTriangles(GameObject go) => LightPos(go, -14f, -24f, -50f);

		[NameMatch("L68")]
		public void Million(GameObject go) => LightPos(go, -6.6f, -31f, -50f);

		[NameMatch("L71")]
		[NameMatch("L72")]
		[NameMatch("L73")]
		[NameMatch("L74")]
		[NameMatch("L75")]
		public void RightYellowCircle(GameObject go) => LightPos(go, 2.6f, -15.9f, -50f);

		[NameMatch("L76")]
		[NameMatch("L77")]
		[NameMatch("L78")]
		public void LiteKickback(GameObject go) => LightPos(go, -10.6f, 0f, -50f);

		[NameMatch("L81")] public void L81(GameObject go) => LightPos(go, 15f, -40f, -50f);
		[NameMatch("L82")] public void L82(GameObject go) => LightPos(go, 10f, -28f, -50f);
		[NameMatch("L83")] public void L83(GameObject go) => LightPos(go, 13f, -37f, -50f);

		[NameMatch("L86")]
		[NameMatch("L87")]
		[NameMatch("L88")]
		public void TopTriangles(GameObject go)
		{
			LightPos(go, 0f, 6f, -50f);
			LightIntensity(go, 6700f);
			SpotAngle(go, 80f, 40f);
		}

		[NameMatch("Light2")]
		[NameMatch("Light3")]
		[NameMatch("Light4")]
		[NameMatch("Light5")]
		public void Boxes(GameObject go)
		{
			LightPos(go, 0f, -7, -50f);
			PyramidAngle(go, 40f, 2.74f);
		}

		[NameMatch("Light4")]
		public void BlueBox(GameObject go)
		{
			LightIntensity(go, 12000f);
			LightColor(go, new Color(0f, 0.4f, 1));
		}

		[NameMatch("Light5")]
		public void YellowBox(GameObject go)
		{
			LightIntensity(go, 670f);
			LightColor(go, Color.yellow);
		}

		#endregion

		#endregion
	}
}
