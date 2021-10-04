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

using UnityEngine;

namespace VisualPinball.Unity.Patcher
{
	[MetaMatch(TableName = "Terminator 2 (Williams 1991)", AuthorName = "NFOZZY")]
	[MetaMatch(TableName = "Terminator 2 - Judgment Day (Williams 1991)", AuthorName = "g5k")]
	public class Terminator2
	{
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


		#region Lights

		[NameMatch("L11")]
		[NameMatch("L12")]
		[NameMatch("L14")]
		[NameMatch("L15")]
		public void InsertMultipliers(GameObject go)
		{
			SpotAngle(go, 96.4f, 47.8f);
			Intensity(go, 3400f);
		}

		[NameMatch("L11")] public void Insert2xPos(GameObject go) => LightPos(go, 28.6f, 9.6f, -50f);
		[NameMatch("L12")] public void Insert4xPos(GameObject go) => LightPos(go, 22.4f, 25.1f, -50f);
		[NameMatch("L13")] public void InsertHoldBonus(GameObject go)
		{
			SpotAngle(go, 96.4f, 47.8f);
			LightPos(go, 0f, 35f, -50f);
			Intensity(go, 1850f);
		}
		[NameMatch("L14")] public void Insert6xPos(GameObject go) => LightPos(go, -24.7f, 25.1f, -50f);
		[NameMatch("L15")] public void Insert8xPos(GameObject go) => LightPos(go, -31.3f, 10.7f, -50f);

		[NameMatch("L15")]
		public void BallSave(GameObject go)
		{
			LightPos(go, 0, -19.6f, -50f);
			SpotAngle(go, 64f, 67f);
			Intensity(go, 1200f);
		}

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

		[NameMatch("L53")]
		[NameMatch("L54")]
		[NameMatch("L55")]
		[NameMatch("L56")]
		[NameMatch("L57")]
		[NameMatch("L58")]
		public void RightRedRect(GameObject go)
		{
			LightPos(go, 8.5f, -11.7f, -50f);
			SpotAngle(go, 122f, 48f);
			Intensity(go, 4500f);
		}

		[NameMatch("L61")]
		[NameMatch("L62")]
		[NameMatch("L63")]
		[NameMatch("L64")]
		[NameMatch("L65")]
		public void LeftYellowRound(GameObject go)
		{
			LightPos(go, -4.3f, -18.2f, -50f);
			LightColor(go, Color.yellow);
		}

		[NameMatch("L71")]
		[NameMatch("L72")]
		[NameMatch("L73")]
		[NameMatch("L74")]
		[NameMatch("L75")]
		public void RightYellowRound(GameObject go)
		{
			LightPos(go, 2.6f, -15.9f, -50f);
			LightColor(go, Color.yellow);
		}

		#region Helpers

		private static void LightColor(GameObject go, Color color)
		{
			var lights = go.GetComponentsInChildren<Light>();
			foreach (var light in lights) {
				RenderPipeline.Current.LightConverter.SetColor(light, color);
			}
		}

		private static void SpotAngle(GameObject go, float outer, float inner)
		{
			var lights = go.GetComponentsInChildren<Light>();
			foreach (var light in lights) {
				RenderPipeline.Current.LightConverter.SpotLight(light, outer, inner);
			}
		}

		private static void Intensity(GameObject go, float intensityLumen)
		{
			var lights = go.GetComponentsInChildren<Light>();
			foreach (var light in lights) {
				RenderPipeline.Current.LightConverter.SetIntensity(light, intensityLumen);
			}
		}

		private static void LightPos(GameObject go, float x, float y, float z)
		{
			var light = go.GetComponentInChildren<Light>();
			if (light != null) {
				light.gameObject.transform.localPosition = new Vector3(x, y, z);
			}
		}

		private static void Duplicate(GameObject go, float x, float y, float z)
		{
			var light = go.GetComponentInChildren<Light>();
			if (light != null) {
				var newGo = Object.Instantiate(light.gameObject, go.transform, true);
				newGo.transform.localPosition = new Vector3(x, y, z);
			}
		}

		#endregion

		#endregion
	}
}
