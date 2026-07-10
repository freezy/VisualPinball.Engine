// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

using System;
using System.Reflection;
using System.Reflection.Emit;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// Regression coverage for known transform and collision bugs. These tests intentionally
	/// remain red until the corresponding production fixes are implemented.
	/// </summary>
	public class PhysicsRegressionTests
	{
		private const float Tolerance = 1e-5f;

		private static readonly OpCode[] OneByteOpCodes = new OpCode[0x100];
		private static readonly OpCode[] TwoByteOpCodes = new OpCode[0x100];

		static PhysicsRegressionTests()
		{
			foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)) {
				var opCode = (OpCode)field.GetValue(null);
				var value = (ushort)opCode.Value;
				if (value < 0x100) {
					OneByteOpCodes[value] = opCode;
				} else if ((value & 0xff00) == 0xfe00) {
					TwoByteOpCodes[value & 0xff] = opCode;
				}
			}
		}

		[Test]
		public void PointColliderTransformKeepsBoundsAtTransformedPoint()
		{
			var collider = new PointCollider(new float3(1f, 2f, 3f), new ColliderInfo { ItemId = 1 });
			var matrix = float4x4.Translate(new float3(10f, 20f, 30f));

			collider = collider.Transform(matrix);

			AssertFloat3(collider.P, new float3(11f, 22f, 33f));
			AssertFloat3(collider.Bounds.Aabb.Min, collider.P);
			AssertFloat3(collider.Bounds.Aabb.Max, collider.P);
		}

		[Test]
		public void LineZColliderTransformAppliesFullMatrixToBothEndpoints()
		{
			var source = new LineZCollider(new float2(1f, 2f), 10f, 20f, new ColliderInfo { ItemId = 1 });
			var matrix = float4x4.TRS(
				new float3(10f, 20f, 30f),
				quaternion.RotateZ(math.radians(90f)),
				new float3(2f, 3f, 4f)
			);
			var expectedLow = matrix.MultiplyPoint(new float3(source.XY, source.ZLow));
			var expectedHigh = matrix.MultiplyPoint(new float3(source.XY, source.ZHigh));

			var transformed = source.Transform(matrix);

			AssertFloat2(transformed.XY, expectedLow.xy);
			Assert.That(transformed.ZLow, Is.EqualTo(expectedLow.z).Within(Tolerance));
			Assert.That(transformed.ZHigh, Is.EqualTo(expectedHigh.z).Within(Tolerance));
		}

		[Test]
		public void GetScaleReturnsTheTrsAxisScaleAfterRotation()
		{
			var matrix = RotatedNonUniformScale();

			AssertFloat3(matrix.GetScale(), new float3(2f, 1f, 3f));
		}

		[Test]
		public void CircleColliderRejectsRotatedNonUniformXyScale()
		{
			Assert.That(CircleCollider.IsTransformable(RotatedNonUniformScale()), Is.False);
		}

		[Test]
		public void BallTransformScalesRadiusWithUniformScale()
		{
			var ball = new BallState {
				Position = new float3(150f, 0f, 0f),
				Radius = 25f
			};

			ball.Transform(float4x4.Scale(new float3(0.5f)));

			AssertFloat3(ball.Position, new float3(75f, 0f, 0f));
			Assert.That(ball.Radius, Is.EqualTo(12.5f).Within(Tolerance));
		}

		[Test]
		public void CollisionEventTransformRoundTripPreservesHitVelocity()
		{
			var collEvent = new CollisionEventData { HitVelocity = new float2(0f, 1f) };
			var matrix = float4x4.TRS(
				float3.zero,
				quaternion.RotateX(math.radians(45f)),
				new float3(1f)
			);

			collEvent.Transform(matrix);
			collEvent.Transform(math.inverse(matrix));

			AssertFloat2(collEvent.HitVelocity, new float2(0f, 1f));
		}

		[Test]
		public void Line3DColliderFiresHitEventForApproachingBallAboveThreshold()
		{
			var collider = new Line3DCollider(
				new float3(0f, 0f, -1f),
				new float3(0f, 0f, 1f),
				new ColliderInfo {
					ItemId = 1,
					ItemType = ItemType.Primitive,
					HitThreshold = 1f,
					FireEvents = true
				}
			);
			var ball = new BallState {
				Id = 1,
				Position = new float3(1f, 0f, 0f),
				EventPosition = new float3(100f, 0f, 0f),
				Velocity = new float3(-10f, 0f, 0f),
				Radius = 1f,
				Mass = 1f
			};
			var collEvent = new CollisionEventData {
				HitNormal = new float3(1f, 0f, 0f),
				HitDistance = 0f
			};
			var state = new PhysicsState();
			var events = new NativeQueue<EventData>(Allocator.Temp);

			try {
				var writer = events.AsParallelWriter();
				collider.Collide(ref ball, ref writer, in collEvent, ref state);

				Assert.That(events.Count, Is.EqualTo(1));
			} finally {
				events.Dispose();
			}
		}

		[TestCase(typeof(Aabb), TestName = "AabbBoxedEqualsDelegatesToTypedOverload")]
		[TestCase(typeof(ColliderHeader), TestName = "ColliderHeaderBoxedEqualsDelegatesToTypedOverload")]
		public void BoxedEqualsDelegatesToTypedOverload(Type type)
		{
			var boxedEquals = type.GetMethod(
				nameof(object.Equals),
				BindingFlags.Instance | BindingFlags.Public,
				null,
				new[] { typeof(object) },
				null
			);
			var typedEquals = type.GetMethod(
				nameof(object.Equals),
				BindingFlags.Instance | BindingFlags.Public,
				null,
				new[] { type },
				null
			);

			Assert.That(boxedEquals, Is.Not.Null);
			Assert.That(typedEquals, Is.Not.Null);
			Assert.That(CallsMethod(boxedEquals, typedEquals), Is.True,
				$"{type.Name}.Equals(object) does not delegate to Equals({type.Name}).");
		}

		private static float4x4 RotatedNonUniformScale()
		{
			return float4x4.TRS(
				float3.zero,
				quaternion.RotateZ(math.radians(45f)),
				new float3(2f, 1f, 3f)
			);
		}

		private static bool CallsMethod(MethodInfo caller, MethodInfo expectedCallee)
		{
			var body = caller.GetMethodBody();
			var il = body?.GetILAsByteArray();
			if (il == null) {
				return false;
			}

			var offset = 0;
			while (offset < il.Length) {
				var opCode = ReadOpCode(il, ref offset);
				if (opCode.OperandType == OperandType.InlineMethod) {
					var token = BitConverter.ToInt32(il, offset);
					var calledMethod = caller.Module.ResolveMethod(token);
					if (HasSameSignature(calledMethod, expectedCallee)) {
						return true;
					}
				}
				offset += GetOperandSize(opCode.OperandType, il, offset);
			}
			return false;
		}

		private static bool HasSameSignature(MethodBase actual, MethodInfo expected)
		{
			if (actual.DeclaringType != expected.DeclaringType || actual.Name != expected.Name) {
				return false;
			}
			var actualParameters = actual.GetParameters();
			var expectedParameters = expected.GetParameters();
			if (actualParameters.Length != expectedParameters.Length) {
				return false;
			}
			for (var i = 0; i < actualParameters.Length; i++) {
				if (actualParameters[i].ParameterType != expectedParameters[i].ParameterType) {
					return false;
				}
			}
			return true;
		}

		private static OpCode ReadOpCode(byte[] il, ref int offset)
		{
			var value = il[offset++];
			return value == 0xfe ? TwoByteOpCodes[il[offset++]] : OneByteOpCodes[value];
		}

		private static int GetOperandSize(OperandType operandType, byte[] il, int offset)
		{
			switch (operandType) {
				case OperandType.InlineNone:
					return 0;
				case OperandType.ShortInlineBrTarget:
				case OperandType.ShortInlineI:
				case OperandType.ShortInlineVar:
					return 1;
				case OperandType.InlineVar:
					return 2;
				case OperandType.InlineBrTarget:
				case OperandType.InlineField:
				case OperandType.InlineI:
				case OperandType.InlineMethod:
				case OperandType.InlineSig:
				case OperandType.InlineString:
				case OperandType.InlineTok:
				case OperandType.InlineType:
				case OperandType.ShortInlineR:
					return 4;
				case OperandType.InlineI8:
				case OperandType.InlineR:
					return 8;
				case OperandType.InlineSwitch:
					return 4 + BitConverter.ToInt32(il, offset) * 4;
				default:
					throw new ArgumentOutOfRangeException(nameof(operandType), operandType, null);
			}
		}

		private static void AssertFloat2(float2 actual, float2 expected)
		{
			Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
			Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
		}

		private static void AssertFloat3(float3 actual, float3 expected)
		{
			Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
			Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
			Assert.That(actual.z, Is.EqualTo(expected.z).Within(Tolerance));
		}
	}
}
