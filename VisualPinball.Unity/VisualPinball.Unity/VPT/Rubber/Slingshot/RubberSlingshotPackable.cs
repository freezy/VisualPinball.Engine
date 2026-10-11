// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

namespace VisualPinball.Unity
{
	public struct RubberSlingshotPackable
	{
		public ulong StartSlotIdA;
		public ulong StartSlotIdB;
		public ulong EndSlotIdA;
		public ulong EndSlotIdB;
		public float ArmContactPosition01;
		public float ArmRestClearance;
		public float ArmTipTravel;
		public bool SelfWired;
		public float SelfWirePulseMs;

		public static byte[] Pack(RubberSlingshotComponent component)
		{
			var span = component.Span;
			return PackageApi.Packer.Pack(new RubberSlingshotPackable {
				StartSlotIdA = span.StartSupport.SlotId.A,
				StartSlotIdB = span.StartSupport.SlotId.B,
				EndSlotIdA = span.EndSupport.SlotId.A,
				EndSlotIdB = span.EndSupport.SlotId.B,
				ArmContactPosition01 = component.ArmContactPosition01,
				ArmRestClearance = component.ArmRestClearance,
				ArmTipTravel = component.ArmTipTravel,
				SelfWired = component.SelfWired,
				SelfWirePulseMs = component.SelfWirePulseMs,
			});
		}

		public static void Unpack(byte[] bytes, RubberSlingshotComponent component)
		{
			var data = PackageApi.Packer.Unpack<RubberSlingshotPackable>(bytes);
			component.Span = new RubberSpanBinding(
				new RubberGuideBinding(null, new SerializedGuid(data.StartSlotIdA, data.StartSlotIdB)),
				new RubberGuideBinding(null, new SerializedGuid(data.EndSlotIdA, data.EndSlotIdB)));
			component.ArmContactPosition01 = data.ArmContactPosition01;
			component.ArmRestClearance = data.ArmRestClearance;
			component.ArmTipTravel = data.ArmTipTravel;
			component.SelfWired = data.SelfWired;
			component.SelfWirePulseMs = data.SelfWirePulseMs;
		}
	}

	public struct RubberSlingshotReferencesPackable
	{
		public ReferencePackable StartGuideRef;
		public ReferencePackable EndGuideRef;
		public ReferencePackable SlingshotWallRef;
		public ReferencePackable MotionControllerRef;

		public static byte[] Pack(RubberSlingshotComponent component, PackagedRefs refs)
		{
			var span = component.Span;
			return PackageApi.Packer.Pack(new RubberSlingshotReferencesPackable {
				StartGuideRef = refs.PackReference(span.StartSupport.Guide),
				EndGuideRef = refs.PackReference(span.EndSupport.Guide),
				SlingshotWallRef = refs.PackReference(component.SlingshotWall),
				MotionControllerRef = refs.PackReference(component.MotionController),
			});
		}

		public static void Unpack(byte[] bytes, RubberSlingshotComponent component,
			PackagedRefs refs)
		{
			if (bytes == null || bytes.Length == 0) {
				return;
			}
			var data = PackageApi.Packer.Unpack<RubberSlingshotReferencesPackable>(bytes);
			component.RestoreReferences(
				refs.Resolve<RubberGuideComponent>(data.StartGuideRef),
				refs.Resolve<RubberGuideComponent>(data.EndGuideRef),
				refs.Resolve<SurfaceComponent>(data.SlingshotWallRef),
				refs.Resolve<MotionControllerComponent>(data.MotionControllerRef));
		}
	}
}
