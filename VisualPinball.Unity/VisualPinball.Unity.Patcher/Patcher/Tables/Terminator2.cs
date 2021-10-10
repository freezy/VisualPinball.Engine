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

using UnityEditor;
using UnityEngine;
using VisualPinball.Unity.VisualPinball.Unity.Patcher.Matcher;

namespace VisualPinball.Unity.Patcher
{
	[MetaMatch(TableName = "Terminator 2 (Williams 1991)", AuthorName = "NFOZZY")]
	[MetaMatch(TableName = "Terminator 2 - Judgment Day (Williams 1991)", AuthorName = "g5k")]
	public class Terminator2 : TablePatcher
	{

		public override void PostPatch(GameObject tableGo)
		{
			var pf = Playfield(tableGo);

			// create GI light groups
			var gi = CreateEmptyGameObject(pf, "GI");
			var gi1 = CreateEmptyGameObject(gi, "CPU");
			var gi2 = CreateEmptyGameObject(gi, "Left Playfield");
			var gi3 = CreateEmptyGameObject(gi, "Right Playfield");
			AddLightGroup(tableGo, gi1, "Light2", "Light3", "Light4", "Light5");
			AddLightGroup(tableGo, gi2, "GI_35", "GI_1", "GI_3", "GI_4", "GI_12", "GI_7",
				"GI_8", "GI_9", "GI_13", "GI_14", "GI_23", "GI_24", "GI_25", "GI_38");
			AddLightGroup(tableGo, gi3, "GI_36", "GI_2", "GI_5", "GI_6", "GI_10", "GI_11", "GI_15", "GI_16", "GI_18", "GI_19", "GI_17",
				"GI_20", "GI_21", "GI_22", "GI_26", "GI_27", "GI_28", "GI_30", "GI_29", "GI_31", "GI_32", "GI_33", "GI_34", "GI_37", "B1", "B2", "B3");


			base.PostPatch(tableGo);
		}

		[NameMatch("batleft", Ref = "Playfield/Flippers/LeftFlipper")]
		[NameMatch("batright", Ref = "Playfield/Flippers/RightFlipper")]
		public void ReparentFlippers(PrimitiveComponent flipper, GameObject gameObject, ref GameObject parent)
		{
			PatcherUtil.Reparent(gameObject, parent);

			flipper.Position.x = 0;
			flipper.Position.y = 0;

			flipper.ObjectRotation.z = 0;
		}

		[NameMatch("LeftRampCover")]
		[NameMatch("LeftRampSign")]
		[NameMatch("RightRampCover")]
		[NameMatch("RightRampSign")]
		[NameMatch("Plastics_LVL2")]
		[NameMatch("BumperCaps")]
		[NameMatch("RightRamp")]
		public void FixZPosition(PrimitiveComponent primitive)
		{
			primitive.Position.z = 0;
		}

		[NameMatch("Drain")]
		public void FixDrain(KickerComponent kickerComponent)
		{
			kickerComponent.Coils[0].Name = "Drain";
			kickerComponent.Coils[0].Speed = 15;
			kickerComponent.Coils[0].Angle = 60;
		}

		[NameMatch("sw17")]
		public void FixSw17(KickerComponent kickerComponent)
		{
			kickerComponent.Coils[0].Name = "Eject";
			kickerComponent.Coils[0].Speed = 5;
			kickerComponent.Coils[0].Angle = 60;
		}

		#region Materials

		[NameMatch("_Plastics")]
		public void fixPlasticsMaterial(GameObject go)
		{
			var material = go.GetComponent<Renderer>().sharedMaterial;
			RenderPipeline.Current.MaterialConverter.SetDiffusionProfile(material, DiffusionProfileTemplate.Plastics);
			RenderPipeline.Current.MaterialConverter.SetMaterialType(material, MaterialType.Translucent);
		}

		#endregion

		#region Lights

		#region Global Illumination

		[NameMatch("B1")]
		[NameMatch("B2")]
		[NameMatch("B3")]
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
				RenderPipeline.Current.LightConverter.SetIntensity(l, 1000f);
				RenderPipeline.Current.LightConverter.SetTemperature(l, 2700);
			}
		}

		[NameMatch("GI_3", FloatParam = 0.01f)]
		[NameMatch("GI_4", FloatParam = 0.01f)]
		[NameMatch("GI_5", FloatParam = 0.02f)]
		[NameMatch("GI_6", FloatParam = 0.01f)]
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
		public void GiDynamicShadow(GameObject go, float param)
		{
			foreach (var l in go.GetComponentsInChildren<Light>()) {
				RenderPipeline.Current.LightConverter.SetShadow(l, true, false, param);
			}
		}

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
		public void GiStaticShadow(GameObject go, float param)
		{
			foreach (var l in go.GetComponentsInChildren<Light>()) {
				RenderPipeline.Current.LightConverter.SetShadow(l, true, false, param);
			}
		}

		[NameMatch("GI_27")]
		[NameMatch("GI_28")]
		public void GiDisable(GameObject go)
		{
			go.SetActive(false);
		}

		#endregion

		#region Insert Shapes

		[NameMatch("L43")]
		[NameMatch("L44")]
		[NameMatch("L45")]
		[NameMatch("L46")]
		[NameMatch("L47")]
		[NameMatch("L48")]
		[NameMatch("L53")]
		[NameMatch("L54")]
		[NameMatch("L55")]
		[NameMatch("L56")]
		[NameMatch("L57")]
		[NameMatch("L58")]
		public void Rectangles(GameObject go)
		{
			SpotAngle(go, 122f, 48f);
			Intensity(go, 4500f);
		}

		[NameMatch("L16")]
		[NameMatch("L22a")]
		[NameMatch("L22b")]
		[NameMatch("L23")]
		[NameMatch("L24")]
		public void MidSizedCircles(GameObject go)
		{
			SpotAngle(go, 64f, 67f);
			Intensity(go, 1200f);
		}

		[NameMatch("L76")]
		[NameMatch("L77")]
		[NameMatch("L78")]
		public void SmallSizedCircles(GameObject go)
		{
			SpotAngle(go, 45f, 13f);
			Intensity(go, 700f);
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
			Intensity(go, 1000f);
		}

		[NameMatch("L31")]
		[NameMatch("L32")]
		[NameMatch("L33")]
		[NameMatch("L34")]
		[NameMatch("L35")]
		public void SmallSizedTrianglesWhite(GameObject go)
		{
			Intensity(go, 700f);
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
			Intensity(go, 3400f);
		}

		[NameMatch("L13")]
		[NameMatch("L21")]
		[NameMatch("L27")]
		[NameMatch("L81")]
		[NameMatch("L82")]
		public void MidSizedTrianglesRed(GameObject go)
		{
			Intensity(go, 1850f);
		}

		[NameMatch("L85")]
		public void MidSizedTrianglesWhite(GameObject go)
		{
			Intensity(go, 1850f);
			LightColor(go, Color.white);
		}

		[NameMatch("L28")]
		public void MidSizedTrianglesBlue(GameObject go)
		{
			Intensity(go, 12000f);
			LightColor(go, new Color(0f, 0.4f, 1));
		}

		[NameMatch("L25")]
		[NameMatch("L42")]
		[NameMatch("L67")]
		public void MidSizedTrianglesYellow(GameObject go)
		{
			Intensity(go, 1850f);
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
			Intensity(go, 340f);
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
			Intensity(go, 4000f);

			LightPos(go, -52f, 3.3f, -50f);
			Duplicate(go, -18f, 0f, -50f);
			Duplicate(go, 16f, 0f, -50f);
			Duplicate(go, 52f, 3.3f, -50f);
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
			Intensity(go, 1000f);
		}

		[NameMatch("L53")]
		[NameMatch("L54")]
		[NameMatch("L55")]
		[NameMatch("L56")]
		[NameMatch("L57")]
		[NameMatch("L58")]
		public void RightRedRect(GameObject go) => LightPos(go, 8.5f, -11.7f, -50f);

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
			Intensity(go, 6700f);
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
			Intensity(go, 12000f);
			LightColor(go, new Color(0f, 0.4f, 1));
		}

		[NameMatch("Light5")]
		public void YellowBox(GameObject go)
		{
			Intensity(go, 670f);
			LightColor(go, Color.yellow);
		}

		#endregion

		#endregion
	}
}
