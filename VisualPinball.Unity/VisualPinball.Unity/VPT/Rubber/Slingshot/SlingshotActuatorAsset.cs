// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	[PackAs("SlingshotActuator")]
	[PackWith(typeof(SlingshotActuatorPacker))]
	[CreateAssetMenu(fileName = "Slingshot Actuator", menuName = "Pinball/Slingshot Actuator")]
	public sealed class SlingshotActuatorAsset : ScriptableObject
	{
		[Min(0f)] public float SupplyVoltage = 48f;
		[Min(0f)] public float CoilResistanceOhm = 4f;
		[Min(0f)] public float TurnOffClampVoltage = 50f;
		[Min(0f)] public float StrokeMeters = 0.025f;
		[Min(0f)] public float MaximumCurrentAmps = 12f;
		[Min(0f)] public float EffectiveMovingMassKg = 0.08f;
		[Min(0f)] public float ReturnSpringNewtonPerMeter = 250f;
		[Min(0f)] public float ReturnSpringPreloadNewton = 5f;
		[Min(0f)] public float ViscousDampingNewtonSecondPerMeter = 1f;
		[Min(0f)] public float EndStopStiffnessNewtonPerMeter = 50000f;
		[Min(0f)] public float EndStopDampingNewtonSecondPerMeter = 20f;
		[Min(2)] public int CurrentSampleCount = 2;
		[Min(2)] public int StrokeSampleCount = 2;
		public float[] ForceNewtonLut = new float[4];
		public float[] FluxLinkageWeberTurnLut = { 0f, 0f, 0.02f, 0.015f };

		public IReadOnlyList<string> ValidateData()
		{
			var errors = new List<string>();
			ValidateFiniteNonNegative(nameof(SupplyVoltage), SupplyVoltage, false, errors);
			ValidateFiniteNonNegative(nameof(CoilResistanceOhm), CoilResistanceOhm, true, errors);
			ValidateFiniteNonNegative(nameof(TurnOffClampVoltage), TurnOffClampVoltage, false, errors);
			ValidateFiniteNonNegative(nameof(StrokeMeters), StrokeMeters, true, errors);
			ValidateFiniteNonNegative(nameof(MaximumCurrentAmps), MaximumCurrentAmps, true, errors);
			ValidateFiniteNonNegative(nameof(EffectiveMovingMassKg), EffectiveMovingMassKg, true, errors);
			ValidateFiniteNonNegative(nameof(ReturnSpringNewtonPerMeter), ReturnSpringNewtonPerMeter, false, errors);
			ValidateFiniteNonNegative(nameof(ReturnSpringPreloadNewton), ReturnSpringPreloadNewton, false, errors);
			ValidateFiniteNonNegative(nameof(ViscousDampingNewtonSecondPerMeter), ViscousDampingNewtonSecondPerMeter, false, errors);
			ValidateFiniteNonNegative(nameof(EndStopStiffnessNewtonPerMeter), EndStopStiffnessNewtonPerMeter, false, errors);
			ValidateFiniteNonNegative(nameof(EndStopDampingNewtonSecondPerMeter), EndStopDampingNewtonSecondPerMeter, false, errors);

			if (CurrentSampleCount < 2 || StrokeSampleCount < 2) {
				errors.Add("The actuator LUT needs at least two current and two stroke samples.");
				return errors;
			}
			int expected;
			try {
				expected = checked(CurrentSampleCount * StrokeSampleCount);
			} catch (OverflowException) {
				errors.Add("The actuator LUT dimensions are too large.");
				return errors;
			}
			ValidateLut(nameof(ForceNewtonLut), ForceNewtonLut, expected, false, errors);
			ValidateLut(nameof(FluxLinkageWeberTurnLut), FluxLinkageWeberTurnLut,
				expected, true, errors);
			return errors;
		}

		public int Index(int currentIndex, int strokeIndex)
			=> currentIndex * StrokeSampleCount + strokeIndex;

		public float SampleForce(float current01, float stroke01)
			=> Sample(ForceNewtonLut, current01, stroke01);

		public float SampleFlux(float current01, float stroke01)
			=> Sample(FluxLinkageWeberTurnLut, current01, stroke01);

		public bool TryGetCurrent(float fluxLinkage, float stroke01, out float currentAmps)
			=> TryGetCurrent(fluxLinkage, stroke01, out currentAmps, out _);

		public bool TryGetCurrent(float fluxLinkage, float stroke01, out float currentAmps,
			out bool saturated)
		{
			currentAmps = 0f;
			saturated = false;
			if (ValidateData().Count != 0 || !float.IsFinite(fluxLinkage)
				|| !float.IsFinite(stroke01)) {
				return false;
			}
			stroke01 = math.saturate(stroke01);
			var minimum = SampleFluxAtCurrentIndex(0, stroke01);
			var maximum = SampleFluxAtCurrentIndex(CurrentSampleCount - 1, stroke01);
			saturated = fluxLinkage < minimum || fluxLinkage > maximum;
			var target = math.clamp(fluxLinkage, minimum, maximum);
			var low = 0;
			var high = CurrentSampleCount - 1;
			while (low < high) {
				var middle = low + (high - low) / 2;
				if (SampleFluxAtCurrentIndex(middle, stroke01) < target) {
					low = middle + 1;
				} else {
					high = middle;
				}
			}
			var upperIndex = low;
			var upperFlux = SampleFluxAtCurrentIndex(upperIndex, stroke01);
			if (upperIndex == 0 || upperFlux <= target) {
				currentAmps = upperIndex / (CurrentSampleCount - 1f) * MaximumCurrentAmps;
				return true;
			}
			var lowerIndex = upperIndex - 1;
			var lowerFlux = SampleFluxAtCurrentIndex(lowerIndex, stroke01);
			var fraction = (target - lowerFlux) / (upperFlux - lowerFlux);
			currentAmps = (lowerIndex + fraction) / (CurrentSampleCount - 1f)
				* MaximumCurrentAmps;
			return true;
		}

		private float Sample(float[] lut, float current01, float stroke01)
		{
			if (lut == null || lut.Length != CurrentSampleCount * StrokeSampleCount
				|| CurrentSampleCount < 2 || StrokeSampleCount < 2) {
				throw new InvalidOperationException("Validate the actuator LUT before sampling it.");
			}
			var current = math.saturate(current01) * (CurrentSampleCount - 1);
			var stroke = math.saturate(stroke01) * (StrokeSampleCount - 1);
			var current0 = (int)math.floor(current);
			var stroke0 = (int)math.floor(stroke);
			var current1 = math.min(current0 + 1, CurrentSampleCount - 1);
			var stroke1 = math.min(stroke0 + 1, StrokeSampleCount - 1);
			var alongStroke0 = math.lerp(lut[Index(current0, stroke0)],
				lut[Index(current0, stroke1)], stroke - stroke0);
			var alongStroke1 = math.lerp(lut[Index(current1, stroke0)],
				lut[Index(current1, stroke1)], stroke - stroke0);
			return math.lerp(alongStroke0, alongStroke1, current - current0);
		}

		private float SampleFluxAtCurrentIndex(int currentIndex, float stroke01)
		{
			var stroke = stroke01 * (StrokeSampleCount - 1);
			var stroke0 = (int)math.floor(stroke);
			var stroke1 = math.min(stroke0 + 1, StrokeSampleCount - 1);
			return math.lerp(FluxLinkageWeberTurnLut[Index(currentIndex, stroke0)],
				FluxLinkageWeberTurnLut[Index(currentIndex, stroke1)], stroke - stroke0);
		}

		private static void ValidateFiniteNonNegative(string field, float value,
			bool strictlyPositive, ICollection<string> errors)
		{
			if (!float.IsFinite(value) || value < 0f || strictlyPositive && value <= 0f) {
				errors.Add($"{field} must be finite and {(strictlyPositive ? "positive" : "non-negative")}.");
			}
		}

		private void ValidateLut(string field, float[] values, int expected,
			bool requireMonotoneCurrent, ICollection<string> errors)
		{
			if (values == null || values.Length != expected) {
				errors.Add($"{field} must contain exactly {expected} samples.");
				return;
			}
			if (values.Any(value => !float.IsFinite(value))) {
				errors.Add($"{field} contains a non-finite sample.");
				return;
			}
			if (!requireMonotoneCurrent) {
				return;
			}
			for (var stroke = 0; stroke < StrokeSampleCount; stroke++) {
				for (var current = 1; current < CurrentSampleCount; current++) {
					if (values[Index(current, stroke)] < values[Index(current - 1, stroke)]) {
						errors.Add($"{field} must be monotone in current at stroke sample {stroke}.");
						return;
					}
				}
			}
		}
	}

	public sealed class SlingshotActuatorPacker : IPacker<SlingshotActuatorAsset>,
		IPacker<ScriptableObject>
	{
		public MetaPackable Pack(int instanceId, SlingshotActuatorAsset asset,
			PackagedFiles files) => SlingshotActuatorPackable.From(instanceId, asset);

		public MetaPackable Unpack(byte[] bytes, SlingshotActuatorAsset asset,
			PackagedFiles files)
		{
			var data = PackageApi.Packer.Unpack<SlingshotActuatorPackable>(bytes);
			data.Apply(asset);
			return data;
		}

		MetaPackable IPacker<ScriptableObject>.Pack(int instanceId, ScriptableObject obj,
			PackagedFiles files) => Pack(instanceId, (SlingshotActuatorAsset)obj, files);

		MetaPackable IPacker<ScriptableObject>.Unpack(byte[] bytes, ScriptableObject obj,
			PackagedFiles files) => Unpack(bytes, (SlingshotActuatorAsset)obj, files);
	}

	public sealed class SlingshotActuatorPackable : MetaPackable
	{
		public float SupplyVoltage;
		public float CoilResistanceOhm;
		public float TurnOffClampVoltage;
		public float StrokeMeters;
		public float MaximumCurrentAmps;
		public float EffectiveMovingMassKg;
		public float ReturnSpringNewtonPerMeter;
		public float ReturnSpringPreloadNewton;
		public float ViscousDampingNewtonSecondPerMeter;
		public float EndStopStiffnessNewtonPerMeter;
		public float EndStopDampingNewtonSecondPerMeter;
		public int CurrentSampleCount;
		public int StrokeSampleCount;
		public float[] ForceNewtonLut;
		public float[] FluxLinkageWeberTurnLut;

		public static SlingshotActuatorPackable From(int instanceId,
			SlingshotActuatorAsset asset)
		{
			return new SlingshotActuatorPackable {
				InstanceId = instanceId,
				SupplyVoltage = asset.SupplyVoltage,
				CoilResistanceOhm = asset.CoilResistanceOhm,
				TurnOffClampVoltage = asset.TurnOffClampVoltage,
				StrokeMeters = asset.StrokeMeters,
				MaximumCurrentAmps = asset.MaximumCurrentAmps,
				EffectiveMovingMassKg = asset.EffectiveMovingMassKg,
				ReturnSpringNewtonPerMeter = asset.ReturnSpringNewtonPerMeter,
				ReturnSpringPreloadNewton = asset.ReturnSpringPreloadNewton,
				ViscousDampingNewtonSecondPerMeter = asset.ViscousDampingNewtonSecondPerMeter,
				EndStopStiffnessNewtonPerMeter = asset.EndStopStiffnessNewtonPerMeter,
				EndStopDampingNewtonSecondPerMeter = asset.EndStopDampingNewtonSecondPerMeter,
				CurrentSampleCount = asset.CurrentSampleCount,
				StrokeSampleCount = asset.StrokeSampleCount,
				ForceNewtonLut = asset.ForceNewtonLut?.ToArray(),
				FluxLinkageWeberTurnLut = asset.FluxLinkageWeberTurnLut?.ToArray(),
			};
		}

		public void Apply(SlingshotActuatorAsset asset)
		{
			asset.SupplyVoltage = SupplyVoltage;
			asset.CoilResistanceOhm = CoilResistanceOhm;
			asset.TurnOffClampVoltage = TurnOffClampVoltage;
			asset.StrokeMeters = StrokeMeters;
			asset.MaximumCurrentAmps = MaximumCurrentAmps;
			asset.EffectiveMovingMassKg = EffectiveMovingMassKg;
			asset.ReturnSpringNewtonPerMeter = ReturnSpringNewtonPerMeter;
			asset.ReturnSpringPreloadNewton = ReturnSpringPreloadNewton;
			asset.ViscousDampingNewtonSecondPerMeter = ViscousDampingNewtonSecondPerMeter;
			asset.EndStopStiffnessNewtonPerMeter = EndStopStiffnessNewtonPerMeter;
			asset.EndStopDampingNewtonSecondPerMeter = EndStopDampingNewtonSecondPerMeter;
			asset.CurrentSampleCount = CurrentSampleCount;
			asset.StrokeSampleCount = StrokeSampleCount;
			asset.ForceNewtonLut = ForceNewtonLut?.ToArray();
			asset.FluxLinkageWeberTurnLut = FluxLinkageWeberTurnLut?.ToArray();
			var errors = asset.ValidateData();
			if (errors.Count != 0) {
				throw new InvalidDataException($"Invalid slingshot actuator: {string.Join(" ", errors)}");
			}
		}
	}
}
