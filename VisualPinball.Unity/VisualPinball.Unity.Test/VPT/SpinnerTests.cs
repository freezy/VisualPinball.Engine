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
using VisualPinball.Engine.Test.VPT.Spinner;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Editor;

namespace VisualPinball.Unity.Test
{
	public class SpinnerTests
	{
		[Test]
		public void ShouldGenerateColliderAtThreeDimensionalOffset()
		{
			var go = new GameObject("Spinner Collider Offset Test");
			var nonTransformableColliderTransforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var colliders = new ColliderReference(ref nonTransformableColliderTransforms, Allocator.Temp);

			try {
				go.AddComponent<SpinnerComponent>();
				var colliderComponent = go.AddComponent<SpinnerColliderComponent>();
				colliderComponent.Offset = new Vector3(12f, 23f, 34f);

				var api = new SpinnerApi(go, null, null);
				((IApiColliderGenerator)api).CreateColliders(ref colliders, float4x4.identity, 0f);

				Assert.That(colliders.SpinnerColliders.Length, Is.EqualTo(1));
				var line = colliders.SpinnerColliders[0].LineSeg0;
				Assert.That((line.V1.x + line.V2.x) * 0.5f, Is.EqualTo(12f));
				Assert.That(line.V1.y, Is.EqualTo(23f));
				Assert.That(line.V2.y, Is.EqualTo(23f));
				Assert.That(line.ZHigh, Is.EqualTo(34f));
			} finally {
				colliders.Dispose();
				nonTransformableColliderTransforms.Dispose();
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldRoundTripColliderOffsetThroughPackable()
		{
			var go = new GameObject("Spinner Collider Offset Packable Test");
			try {
				var colliderComponent = go.AddComponent<SpinnerColliderComponent>();
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
		public void ShouldRestoreLegacyPackedZPositionAsOffset()
		{
			var go = new GameObject("Legacy Spinner Collider Offset Packable Test");
			try {
				var colliderComponent = go.AddComponent<SpinnerColliderComponent>();

				colliderComponent.Unpack(Encoding.UTF8.GetBytes("{\"ZPosition\":34.0}"));

				Assert.That(colliderComponent.Offset, Is.EqualTo(new Vector3(0f, 0f, 34f)));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldWriteImportedSpinnerData()
		{
			const string tmpFileName = "ShouldWriteSpinnerData.vpx";
			var go = VpxImportEngine.ImportIntoScene(VpxPath.Spinner, options: ConvertOptions.SkipNone);
			var ta = go.GetComponent<TableComponent>();
			ta.TableContainer.Export(tmpFileName);

			var writtenTable = FileTableContainer.Load(tmpFileName);
			SpinnerDataTests.ValidateSpinnerData(writtenTable.Spinner("Data").Data);

			File.Delete(tmpFileName);
			Object.DestroyImmediate(go);
		}

	}
}
