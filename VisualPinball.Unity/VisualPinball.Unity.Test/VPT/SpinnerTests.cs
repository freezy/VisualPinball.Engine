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
		public void ShouldMatchVpxAngularVelocityTransferAtDefaultHeight()
		{
			var collider = CreateSpinnerCollider(60f, float4x4.identity);
			var movement = new SpinnerMovementState();
			var state = new SpinnerStaticState { Damping = 0.9f };
			var collEvent = new CollisionEventData {
				HitNormal = new float3(0f, 1f, 0f),
				HitFlag = false
			};
			var ball = new BallState { Velocity = new float3(0f, 12f, 0f) };

			collider.Collide(in ball, ref collEvent, ref movement, in state);

			Assert.That(movement.AngleSpeed, Is.EqualTo(12f / 30f * 0.9f).Within(1e-6f));
		}

		[Test]
		public void ShouldNotUseColliderOffsetAsAngularVelocityLever()
		{
			var collider = CreateSpinnerCollider(60f, float4x4.identity, colliderHeight: 120f);

			var angleSpeed = Collide(in collider, new float3(0f, 12f, 0f), new float3(0f, 1f, 0f));

			Assert.That(angleSpeed, Is.EqualTo(12f / 30f).Within(1e-6f));
		}

		[Test]
		public void ShouldNotMovePlateEdgeFasterThanIncomingBall()
		{
			const float ballSpeed = 12f;
			var collider = CreateSpinnerCollider(0f, float4x4.identity);

			var angleSpeed = Collide(in collider, new float3(0f, ballSpeed, 0f), new float3(0f, 1f, 0f));

			Assert.That(angleSpeed * SpinnerCollider.DefaultPlateRadius, Is.LessThanOrEqualTo(ballSpeed));
		}

		[Test]
		public void ShouldKeepAngularVelocityPhysicalAcrossColliderTransforms()
		{
			const float height = 0f;
			const float worldSpeed = 12f;
			var scale = new float3(2f);
			var matrix = float4x4.TRS(new float3(100f, 200f, 300f), quaternion.RotateZ(math.radians(37f)), scale);
			var transformedCollider = CreateSpinnerCollider(height, matrix);
			var transformedNormal = new float3(transformedCollider.LineSeg0.Normal, 0f);
			var transformedSpeed = Collide(in transformedCollider, transformedNormal * worldSpeed, transformedNormal);

			var localCollider = CreateSpinnerCollider(height, matrix, true);
			var localNormal = new float3(localCollider.LineSeg0.Normal, 0f);
			var localSpeed = Collide(in localCollider, localNormal * (worldSpeed / scale.y), localNormal);

			Assert.That(transformedSpeed, Is.EqualTo(worldSpeed / (SpinnerCollider.DefaultPlateRadius * scale.y)).Within(1e-6f));
			Assert.That(localSpeed, Is.EqualTo(transformedSpeed).Within(1e-6f));
			Assert.That(transformedCollider.Bounds.Aabb, Is.EqualTo(transformedCollider.LineSeg0.Bounds.Aabb));
		}

		[Test]
		public void ShouldApplyVpxPlateRotationDirection()
		{
			var go = new GameObject("Spinner Plate Animation Test");
			try {
				var animation = go.AddComponent<SpinnerPlateAnimationComponent>();
				animation.RotationVector = Vector3.right;
				InvokeNonPublic(animation, "Start");

				InvokeNonPublic(animation, "OnAnimationValueChanged", math.PI * 0.5f);

				var expected = Quaternion.AngleAxis(-90f, Vector3.right);
				Assert.That(Quaternion.Angle(animation.transform.localRotation, expected), Is.LessThan(1e-4f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

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

		private static float Collide(in SpinnerCollider collider, float3 velocity, float3 normal)
		{
			var movement = new SpinnerMovementState();
			var state = new SpinnerStaticState { Damping = 1f };
			var collEvent = new CollisionEventData { HitNormal = normal };
			var ball = new BallState { Velocity = velocity };
			var mutableCollider = collider;
			mutableCollider.Collide(in ball, ref collEvent, ref movement, in state);
			return movement.AngleSpeed;
		}

		private static SpinnerCollider CreateSpinnerCollider(float height, float4x4 matrix, bool isKinematic = false, float colliderHeight = 0f)
		{
			var go = new GameObject("Spinner Collision Test");
			var nonTransformableColliderTransforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var colliders = new ColliderReference(ref nonTransformableColliderTransforms, Allocator.Temp, isKinematic);

			try {
				var spinner = go.AddComponent<SpinnerComponent>();
				spinner.Position = new Vector3(0f, 0f, height);
				var colliderComponent = go.AddComponent<SpinnerColliderComponent>();
				colliderComponent.ZPosition = colliderHeight;

				var api = new SpinnerApi(go, null, null);
				((IApiColliderGenerator)api).CreateColliders(ref colliders, matrix, 0f);

				return colliders.SpinnerColliders[0];
			} finally {
				colliders.Dispose();
				nonTransformableColliderTransforms.Dispose();
				Object.DestroyImmediate(go);
			}
		}

		private static void InvokeNonPublic(object target, string methodName, params object[] args)
		{
			var method = target.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
			Assert.That(method, Is.Not.Null, $"Could not find {methodName} on {target.GetType().Name}.");
			method.Invoke(target, args);
		}

	}
}
