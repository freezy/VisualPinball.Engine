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
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.Test.VPT.Gate;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Editor;

namespace VisualPinball.Unity.Test
{
	public class GateTests
	{
		[Test]
		public void ShouldGenerateColliderAtThreeDimensionalOffset()
		{
			var go = new GameObject("Gate Collider Offset Test");
			var nonTransformableColliderTransforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var colliders = new ColliderReference(ref nonTransformableColliderTransforms, Allocator.Temp);

			try {
				go.AddComponent<GateComponent>();
				var colliderComponent = go.AddComponent<GateColliderComponent>();
				colliderComponent.Offset = new Vector3(12f, 23f, 34f);

				var api = new GateApi(go, null, null);
				((IApiColliderGenerator)api).CreateColliders(ref colliders, float4x4.identity, 0f);

				Assert.That(colliders.GateColliders.Length, Is.EqualTo(1));
				var gateLine = colliders.GateColliders[0].LineSeg0;
				Assert.That((gateLine.V1.x + gateLine.V2.x) * 0.5f, Is.EqualTo(12f));
				Assert.That(gateLine.V1.y, Is.EqualTo(23f));
				Assert.That(gateLine.V2.y, Is.EqualTo(23f));
				Assert.That(gateLine.ZLow, Is.EqualTo(34f));

				Assert.That(colliders.LineColliders.Length, Is.EqualTo(1));
				var blockingLine = colliders.LineColliders[0];
				Assert.That((blockingLine.V1.x + blockingLine.V2.x) * 0.5f, Is.EqualTo(12f));
				Assert.That(blockingLine.V1.y, Is.EqualTo(23f));
				Assert.That(blockingLine.ZLow, Is.EqualTo(34f));
			} finally {
				colliders.Dispose();
				nonTransformableColliderTransforms.Dispose();
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldRoundTripColliderOffsetThroughPackable()
		{
			var go = new GameObject("Gate Collider Offset Packable Test");
			try {
				var colliderComponent = go.AddComponent<GateColliderComponent>();
				var expected = new Vector3(12f, 23f, 34f);
				colliderComponent.Offset = expected;

				var bytes = colliderComponent.Pack();
				colliderComponent.Offset = Vector3.zero;
				colliderComponent.Unpack(bytes);

				Assert.That(colliderComponent.Offset, Is.EqualTo(expected));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldRestoreLegacyPackedDistanceAndZLowAsOffset()
		{
			var go = new GameObject("Legacy Gate Collider Offset Packable Test");
			try {
				var colliderComponent = go.AddComponent<GateColliderComponent>();

				colliderComponent.Unpack(Encoding.UTF8.GetBytes("{\"Distance\":23.0,\"ZLow\":34.0}"));

				Assert.That(colliderComponent.Offset, Is.EqualTo(new Vector3(0f, 23f, 34f)));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldWriteImportedGateData()
		{
			const string tmpFileName = "ShouldWriteGateData.vpx";
			var go = VpxImportEngine.ImportIntoScene(VpxPath.Gate, options: ConvertOptions.SkipNone);
			var ta = go.GetComponent<TableComponent>();
			ta.TableContainer.Export(tmpFileName);

			var writtenTable = FileTableContainer.Load(tmpFileName);
			GateDataTests.ValidateGateData(writtenTable.Gate("Data").Data);

			File.Delete(tmpFileName);
			Object.DestroyImmediate(go);
		}

	}
}
