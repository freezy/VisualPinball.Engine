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

using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.Test.VPT.Kicker;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Editor;

namespace VisualPinball.Unity.Test
{
	public class KickerTests
	{
		[Test]
		public void ShouldWriteImportedKickerData()
		{
			const string tmpFileName = "ShouldWriteKickerData.vpx";
			var go = VpxImportEngine.ImportIntoScene(VpxPath.Kicker, options: ConvertOptions.SkipNone);
			var ta = go.GetComponent<TableComponent>();
			ta.TableContainer.Export(tmpFileName);

			var writtenTable = FileTableContainer.Load(tmpFileName);
			KickerDataTests.ValidateKickerData(writtenTable.Kicker("Data").Data);

			File.Delete(tmpFileName);
			Object.DestroyImmediate(go);
		}

		[Test]
		public void ShouldKeepColliderRadiusStableThroughNestedScale()
		{
			var playfieldGo = new GameObject("Playfield");
			var groupGo = new GameObject("Group");
			var kickerGo = new GameObject("Kicker");
			try {
				playfieldGo.AddComponent<PlayfieldComponent>();
				groupGo.transform.SetParent(playfieldGo.transform, false);
				groupGo.transform.localScale = Vector3.one * 0.1f;
				kickerGo.transform.SetParent(groupGo.transform, false);
				kickerGo.transform.localScale = Vector3.one * 10f;
				var kicker = kickerGo.AddComponent<KickerComponent>();

				var matrix = Physics.GetLocalToPlayfieldMatrixInVpx(kickerGo.transform.localToWorldMatrix, playfieldGo.transform.worldToLocalMatrix);
				var radiusInPlayfield = kicker.UnscaledRadius * math.length(matrix.c0.xyz);

				Assert.That(kicker.Radius, Is.EqualTo(250f).Within(0.001f));
				Assert.That(kicker.UnscaledRadius, Is.EqualTo(25f).Within(0.001f));
				Assert.That(radiusInPlayfield, Is.EqualTo(25f).Within(0.001f));
			} finally {
				Object.DestroyImmediate(playfieldGo);
			}
		}

		[Test]
		public void ShouldUseRuntimeKickDirectionConventionForPreview()
		{
			var velocity = KickerApi.GetKickVelocity(0f, 4f, 90f);

			Assert.That(velocity.x, Is.EqualTo(0f).Within(0.001f));
			Assert.That(velocity.y, Is.EqualTo(0f).Within(0.001f));
			Assert.That(velocity.z, Is.EqualTo(4f).Within(0.001f));
		}

		[Test]
		public void ShouldCreateTheBallAtTheKickerHeight()
		{
			// a trough exit kicker sits below the playfield; its ball spawns down there, like in VP
			var playfieldGo = new GameObject("Playfield");
			var kickerGo = new GameObject("Kicker");
			try {
				playfieldGo.AddComponent<PlayfieldComponent>();
				kickerGo.transform.SetParent(playfieldGo.transform, false);
				kickerGo.transform.localPosition = new Vector3(0.4f, -0.05f, -1f);
				var kicker = kickerGo.AddComponent<KickerComponent>();

				var expected = kicker.PositionInPlayfield;
				var creation = kicker.GetBallCreationPosition();

				Assert.That(expected.z, Is.Not.EqualTo(0f).Within(0.001f), "the kicker is not at playfield level");
				Assert.That(creation.X, Is.EqualTo(expected.x).Within(0.001f));
				Assert.That(creation.Y, Is.EqualTo(expected.y).Within(0.001f));
				Assert.That(creation.Z, Is.EqualTo(expected.z).Within(0.001f));
			} finally {
				Object.DestroyImmediate(playfieldGo);
			}
		}

		[Test]
		public void ShouldHoldTheBallWhereTheColliderIsForAGroupedKicker()
		{
			var playfieldGo = new GameObject("Playfield");
			var groupGo = new GameObject("Group");
			var kickerGo = new GameObject("Kicker");
			try {
				playfieldGo.AddComponent<PlayfieldComponent>();
				groupGo.transform.SetParent(playfieldGo.transform, false);
				groupGo.transform.localPosition = new Vector3(0.1f, 0.02f, -0.3f);
				kickerGo.transform.SetParent(groupGo.transform, false);
				kickerGo.transform.localPosition = new Vector3(0.05f, -0.05f, -0.2f);
				var kicker = kickerGo.AddComponent<KickerComponent>();
				kickerGo.AddComponent<KickerColliderComponent>();

				var inPlayfield = kicker.PositionInPlayfield;
				var local = kicker.Position;
				Assert.That(math.distance((float3)inPlayfield, (float3)local), Is.GreaterThan(1f), "the group offsets the kicker");

				var state = kicker.CreateState();
				try {
					Assert.That(state.Static.Center.x, Is.EqualTo(inPlayfield.x).Within(0.001f));
					Assert.That(state.Static.Center.y, Is.EqualTo(inPlayfield.y).Within(0.001f));
					Assert.That(state.Static.ZLow, Is.EqualTo(inPlayfield.z).Within(0.001f));
				} finally {
					state.Dispose();
				}
			} finally {
				Object.DestroyImmediate(playfieldGo);
			}
		}
	}
}
