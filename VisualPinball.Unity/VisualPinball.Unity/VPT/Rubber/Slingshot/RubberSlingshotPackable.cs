// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Linq;
using UnityEngine;

namespace VisualPinball.Unity
{
	public struct SlingshotSwitchZonePackable
	{
		public float Position01;
		public float CloseDeflection;
		public float OpenDeflection;
		public float MinimumClosedTimeMs;

		public static SlingshotSwitchZonePackable From(SlingshotSwitchZone zone)
		{
			return new SlingshotSwitchZonePackable {
				Position01 = zone.Position01,
				CloseDeflection = zone.CloseDeflection,
				OpenDeflection = zone.OpenDeflection,
				MinimumClosedTimeMs = zone.MinimumClosedTimeMs,
			};
		}

		public SlingshotSwitchZone ToZone()
		{
			return new SlingshotSwitchZone {
				Position01 = Position01,
				CloseDeflection = CloseDeflection,
				OpenDeflection = OpenDeflection,
				MinimumClosedTimeMs = MinimumClosedTimeMs,
			};
		}
	}

	public struct RubberSlingshotPackable
	{
		// Version 2 adds the runtime fields of the physical slingshot. Version 1 (and
		// unversioned) payloads predate the runtime and restore its defaults. Version 3
		// drops the arm visual's axis and angles; the arm is a Motion Controller now.
		private const int CurrentVersion = 3;

		public int Version;

		public ulong StartSlotIdA;
		public ulong StartSlotIdB;
		public ulong EndSlotIdA;
		public ulong EndSlotIdB;
		public SlingshotSwitchZonePackable[] SwitchZones;
		public float ArmContactPosition01;
		public float ArmRestClearance;
		public float ArmTipTravel;

		public bool SelfWired;
		public float WireDelayMs;
		public float SelfWirePulseMs;
		public float KickSpeed;
		public float ArmHalfWidth01;
		public float KickWindowMs;
		public float MinimumSwitchPulseMs;

		public static byte[] Pack(RubberSlingshotComponent component)
		{
			var span = component.Span;
			return PackageApi.Packer.Pack(new RubberSlingshotPackable {
				Version = CurrentVersion,
				StartSlotIdA = span.StartSupport.SlotId.A,
				StartSlotIdB = span.StartSupport.SlotId.B,
				EndSlotIdA = span.EndSupport.SlotId.A,
				EndSlotIdB = span.EndSupport.SlotId.B,
				SwitchZones = component.SwitchZones.Select(SlingshotSwitchZonePackable.From).ToArray(),
				ArmContactPosition01 = component.ArmContactPosition01,
				ArmRestClearance = component.ArmRestClearance,
				ArmTipTravel = component.ArmTipTravel,
				SelfWired = component.SelfWired,
				WireDelayMs = component.WireDelayMs,
				SelfWirePulseMs = component.SelfWirePulseMs,
				KickSpeed = component.KickSpeed,
				ArmHalfWidth01 = component.ArmHalfWidth01,
				KickWindowMs = component.KickWindowMs,
				MinimumSwitchPulseMs = component.MinimumSwitchPulseMs,
			});
		}

		public static void Unpack(byte[] bytes, RubberSlingshotComponent component)
		{
			var data = PackageApi.Packer.Unpack<RubberSlingshotPackable>(bytes);
			component.Span = new RubberSpanBinding(
				new RubberGuideBinding(null, new SerializedGuid(data.StartSlotIdA, data.StartSlotIdB)),
				new RubberGuideBinding(null, new SerializedGuid(data.EndSlotIdA, data.EndSlotIdB)));
			component.SwitchZones = data.SwitchZones?.Select(zone => zone.ToZone()).ToArray()
				?? Array.Empty<SlingshotSwitchZone>();
			component.ArmContactPosition01 = data.ArmContactPosition01;
			component.ArmRestClearance = data.ArmRestClearance;
			component.ArmTipTravel = data.ArmTipTravel;

			// pre-runtime payloads carry none of the fields below
			var current = data.Version >= 2;
			component.SelfWired = current
				? data.SelfWired
				: RubberSlingshotComponent.DefaultSelfWired;
			component.WireDelayMs = current
				? data.WireDelayMs
				: RubberSlingshotComponent.DefaultWireDelayMs;
			component.SelfWirePulseMs = current
				? data.SelfWirePulseMs
				: RubberSlingshotComponent.DefaultSelfWirePulseMs;
			component.KickSpeed = current
				? data.KickSpeed
				: RubberSlingshotComponent.DefaultKickSpeed;
			component.ArmHalfWidth01 = current
				? data.ArmHalfWidth01
				: RubberSlingshotComponent.DefaultArmHalfWidth01;
			component.KickWindowMs = current
				? data.KickWindowMs
				: RubberSlingshotComponent.DefaultKickWindowMs;
			component.MinimumSwitchPulseMs = current
				? data.MinimumSwitchPulseMs
				: RubberSlingshotComponent.DefaultMinimumSwitchPulseMs;
		}
	}

	public struct RubberSlingshotReferencesPackable
	{
		/// <summary>
		/// The slingshot is a component of its rubber, so the rubber is implied by the
		/// game object. The reference is still written so the layout does not change,
		/// and ignored when read.
		/// </summary>
		public ReferencePackable RubberRef;
		public ReferencePackable StartGuideRef;
		public ReferencePackable EndGuideRef;
		public int ActuatorRef;
		public ReferencePackable MotionControllerRef;

		public static byte[] Pack(RubberSlingshotComponent component, Transform root,
			PackagedRefs refs, PackagedFiles files)
		{
			var span = component.Span;
			return PackageApi.Packer.Pack(new RubberSlingshotReferencesPackable {
				RubberRef = refs.PackReference(component.Rubber),
				StartGuideRef = refs.PackReference(span.StartSupport.Guide),
				EndGuideRef = refs.PackReference(span.EndSupport.Guide),
				ActuatorRef = files.AddAsset(component.Actuator),
				MotionControllerRef = refs.PackReference(component.MotionController),
			});
		}

		public static void Unpack(byte[] bytes, RubberSlingshotComponent component,
			Transform root, PackagedRefs refs, PackagedFiles files)
		{
			if (bytes == null || bytes.Length == 0) {
				return;
			}
			var data = PackageApi.Packer.Unpack<RubberSlingshotReferencesPackable>(bytes);
			component.RestoreReferences(
				refs.Resolve<RubberGuideComponent>(data.StartGuideRef),
				refs.Resolve<RubberGuideComponent>(data.EndGuideRef),
				files.GetAsset<SlingshotActuatorAsset>(data.ActuatorRef),
				refs.Resolve<MotionControllerComponent>(data.MotionControllerRef));
		}
	}
}
