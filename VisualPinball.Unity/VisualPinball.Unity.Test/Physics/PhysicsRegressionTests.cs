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
	/// Regression coverage for physics transform and collision bugs.
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
		public void BallTransformRoundTripPreservesRadiusForLargeRotatedUniformScale()
		{
			var ball = new BallState { Radius = 25f };
			var matrix = float4x4.TRS(
				float3.zero,
				quaternion.EulerXYZ(math.radians(new float3(10f, 62f, 17f))),
				new float3(10f)
			);

			ball.Transform(math.inverse(matrix));
			Assert.That(ball.Radius, Is.EqualTo(2.5f).Within(Tolerance));

			ball.Transform(matrix);
			Assert.That(ball.Radius, Is.EqualTo(25f).Within(Tolerance));
		}

		[Test]
		public void BallTransformRotatesExternalAccelerationWithColliderFrame()
		{
			var ball = new BallState {
				ExternalAcceleration = new float3(1f, 0f, 0f)
			};
			var matrix = float4x4.TRS(float3.zero, quaternion.RotateZ(math.radians(90f)), new float3(1f));

			ball.Transform(matrix);

			AssertFloat3(ball.ExternalAcceleration, new float3(0f, 1f, 0f));
		}

		[Test]
		public void CoOrientedContactsOnTheSameItemAreDeduplicated()
		{
			var current = new ContactBufferElement(7, new CollisionEventData {
				ColliderId = 11,
				HitNormal = math.normalizesafe(new float3(1f, 0.02f, 0f))
			});
			var previous = new ContactBufferElement(7, new CollisionEventData {
				ColliderId = 10,
				HitNormal = new float3(1f, 0f, 0f)
			});
			var currentHeader = new ColliderHeader { ItemId = 42 };
			var previousHeader = new ColliderHeader { ItemId = 42 };

			Assert.That(ContactPhysics.IsDuplicateContact(in current, in currentHeader, in previous, in previousHeader), Is.True);

			currentHeader.ItemId = 43;
			Assert.That(ContactPhysics.IsDuplicateContact(in current, in currentHeader, in previous, in previousHeader), Is.False,
				"parallel contacts from distinct physical items must both be resolved");
			currentHeader.ItemId = 42;
			current.CollEvent.IsKinematic = true;
			Assert.That(ContactPhysics.IsDuplicateContact(in current, in currentHeader, in previous, in previousHeader), Is.False,
				"static and kinematic contacts use different collider frames");
			previous.CollEvent.IsKinematic = true;
			Assert.That(ContactPhysics.IsDuplicateContact(in current, in currentHeader, in previous, in previousHeader), Is.True,
				"facets of one kinematic item share the same transform and surface velocity");
			current.CollEvent.IsKinematic = false;
			previous.CollEvent.IsKinematic = false;
			current.CollEvent.HitNormal = new float3(math.cos(math.radians(15f)), math.sin(math.radians(15f)), 0f);
			Assert.That(ContactPhysics.IsDuplicateContact(in current, in currentHeader, in previous, in previousHeader), Is.False,
				"adjacent mesh facets with meaningfully different normals are separate contacts");
		}

		[Test]
		public void SkiddingBallReceivesKineticFrictionAfterNormalSupport()
		{
			var ball = new BallState {
				Mass = 1f,
				Radius = 1f,
				Velocity = new float3(5f, 0f, 0f)
			};
			var contact = new CollisionEventData { HitNormal = new float3(0f, 0f, 1f) };
			var gravity = new float3(0f, 0f, -1f);
			var frictionVelocity = ball.Velocity;
			var frictionAngularMomentum = ball.AngularMomentum;

			BallCollider.HandleStaticContact(ref ball, in contact, 0.3f, 0.1f, in gravity, float3.zero,
				in gravity, in frictionVelocity, in frictionAngularMomentum);

			Assert.That(ball.Velocity.x, Is.LessThan(5f), "kinetic friction must reduce planar skid speed");
			Assert.That(math.lengthsq(ball.AngularMomentum), Is.GreaterThan(0f), "friction must start converting skid into rolling spin");
		}

		[Test]
		public void OrthogonalContactFrictionIgnoresNormalSolverExitVelocity()
		{
			var ball = new BallState {
				Mass = 1f,
				Radius = 1f,
				ExternalAcceleration = new float3(-1f, 0f, 0f)
			};
			var wallContact = new CollisionEventData { HitNormal = new float3(1f, 0f, 0f) };
			BallCollider.HandleStaticContact(ref ball, in wallContact, 0f, 0.1f, float3.zero, float3.zero);
			var wallExitVelocity = ball.Velocity.x;

			var floorContact = new CollisionEventData { HitNormal = new float3(0f, 0f, 1f) };
			var floorAcceleration = new float3(0f, 0f, -1f);
			var preContactVelocity = float3.zero;
			var preContactAngularMomentum = float3.zero;
			BallCollider.HandleStaticContact(ref ball, in floorContact, 1f, 0.1f, in floorAcceleration,
				float3.zero, in floorAcceleration, in preContactVelocity, in preContactAngularMomentum);

			Assert.That(ball.Velocity.x, Is.EqualTo(wallExitVelocity).Within(Tolerance),
				"friction at another contact must not consume a normal-solver artifact as physical slip");
		}

		[Test]
		public void FrictionLoadExcludesAccelerationSupportedByAnotherContact()
		{
			var acceleration = new float3(-2f, 1f, -1f);
			var wallNormal = new float3(1f, 0f, 0f);

			var frictionAcceleration = acceleration - ContactPhysics.SupportedAcceleration(in acceleration, in wallNormal);

			AssertFloat3(frictionAcceleration, new float3(0f, 1f, -1f));
		}

		[Test]
		public void ObliqueContactProjectionDoesNotLeavePhantomFrictionLoad()
		{
			var frictionAcceleration = new float3(0f, -1f, 0f);
			var firstNormal = new float3(1f, 0f, 0f);
			var secondNormal = new float3(-0.5f, math.sqrt(0.75f), 0f);

			for (var pass = 0; pass < 8; pass++) {
				frictionAcceleration -= ContactPhysics.SupportedAcceleration(in frictionAcceleration, in firstNormal);
				frictionAcceleration -= ContactPhysics.SupportedAcceleration(in frictionAcceleration, in secondNormal);
			}

			Assert.That(math.length(frictionAcceleration), Is.LessThan(1e-4f),
				"loads fully supported by an oblique wedge must not become tangential friction load");
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
		public void CollisionEventClearResetsTransformedHitVelocity()
		{
			var collEvent = new CollisionEventData { HitVelocity = new float2(0f, 1f) };
			var matrix = float4x4.TRS(
				float3.zero,
				quaternion.RotateX(math.radians(45f)),
				new float3(1f)
			);

			collEvent.Transform(matrix);
			collEvent.ClearCollider();

			AssertFloat2(collEvent.HitVelocity, float2.zero);

			collEvent.HitVelocity = new float2(0f, 1f);
			collEvent.Transform(matrix);

			AssertFloat2(collEvent.HitVelocity, new float2(0f, math.sqrt(0.5f)));
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
