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

			SetupFlippers(playfieldGo);
			SetupDropTargetBanks(tableGo, playfieldGo);
			SetupTrough(tableGo, playfieldGo);
			SetupPinMame(tableGo, playfieldGo);
			SetupDisplays(tableGo);
		}

		private static void SetupFlippers(GameObject playfieldGo)
		{ 

			var lowerLeftFlipper = playfieldGo.transform.Find("Flippers/LeftFlipper1").gameObject;
			lowerLeftFlipper.name = "LowerLeftFlipper";

			var leftFlipper2 = playfieldGo.transform.Find("Flippers/LeftFlipper2").gameObject;
			leftFlipper2.GetComponentInChildren<FlipperRubberMeshComponent>().gameObject.SetActive(false);
			PatcherUtil.Reparent(leftFlipper2, lowerLeftFlipper);

			var leftFlipper3 = playfieldGo.transform.Find("Flippers/LeftFlipper3").gameObject;
			leftFlipper3.GetComponentInChildren<FlipperRubberMeshComponent>().gameObject.SetActive(false);
			PatcherUtil.Reparent(leftFlipper3, lowerLeftFlipper);

			var leftFlipper4 = playfieldGo.transform.Find("Flippers/LeftFlipper4").gameObject;
			leftFlipper4.GetComponentInChildren<FlipperRubberMeshComponent>().gameObject.SetActive(false);			
			PatcherUtil.Reparent(leftFlipper4, lowerLeftFlipper);

			var lowerRightFlipper = playfieldGo.transform.Find("Flippers/RightFlipper1").gameObject;
			lowerRightFlipper.name = "LowerRightFlipper";

			var rightFlipper2 = playfieldGo.transform.Find("Flippers/RightFlipper2").gameObject;
			rightFlipper2.GetComponentInChildren<FlipperRubberMeshComponent>().gameObject.SetActive(false);
			PatcherUtil.Reparent(rightFlipper2, lowerRightFlipper);

			var rightFlipper3 = playfieldGo.transform.Find("Flippers/RightFlipper3").gameObject;
			rightFlipper3.GetComponentInChildren<FlipperRubberMeshComponent>().gameObject.SetActive(false);
			PatcherUtil.Reparent(rightFlipper3, lowerRightFlipper);

			var rightFlipper4 = playfieldGo.transform.Find("Flippers/RightFlipper4").gameObject;
			rightFlipper4.GetComponentInChildren<FlipperRubberMeshComponent>().gameObject.SetActive(false);
			PatcherUtil.Reparent(rightFlipper4, lowerRightFlipper);

			var upperLeftFlipper = playfieldGo.transform.Find("Flippers/LeftFlipper5").gameObject;
			upperLeftFlipper.name = "UpperLeftFlipper";

			var upperRightFlipper = playfieldGo.transform.Find("Flippers/RightFlipper5").gameObject;
			upperRightFlipper.name = "UpperRightFlipper";
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
			segment.LitColor = Color.green;
		}

		#endregion
	}
}
