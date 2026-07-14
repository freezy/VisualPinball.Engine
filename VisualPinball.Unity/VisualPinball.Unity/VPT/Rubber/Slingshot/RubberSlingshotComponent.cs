// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity
{
	[Serializable]
	public struct SlingshotSwitchZone
	{
		[Range(0f, 1f)] public float Position01;
		[Min(0f)] public float CloseDeflection;
		[Min(0f)] public float OpenDeflection;
		[Min(0f)] public float MinimumClosedTimeMs;
	}

	[PackAs("RubberSlingshot")]
	[AddComponentMenu("Pinball/Game Item/Rubber Slingshot")]
	public sealed class RubberSlingshotComponent : MonoBehaviour,
		ISwitchDeviceComponent, ICoilDeviceComponent, IPackable
	{
		public const string SwitchItem = "slingshot_switch";
		public const string CoilItem = "slingshot_coil";

		[SerializeField] private RubberComponent _rubber;
		[SerializeField] private RubberSpanBinding _span;
		[SerializeField] private SlingshotSwitchZone[] _switchZones = {
			new() { Position01 = 0.3f, CloseDeflection = 4f, OpenDeflection = 2f, MinimumClosedTimeMs = 8f },
			new() { Position01 = 0.7f, CloseDeflection = 4f, OpenDeflection = 2f, MinimumClosedTimeMs = 8f },
		};
		[SerializeField, Range(0f, 1f)] private float _armContactPosition01 = 0.5f;
		[SerializeField, Min(0f)] private float _armRestClearance = 2f;
		[SerializeField, Min(0f)] private float _armTipTravel = 18f;
		[SerializeField] private SlingshotActuatorAsset _actuator;
		[SerializeField] private GameObject _coilArmVisual;
		[SerializeField] private Axis _visualRotationAxis;
		[SerializeField] private float _visualRestAngle;
		[SerializeField] private float _visualFiredAngle = 20f;

		public RubberComponent Rubber { get => _rubber; set => _rubber = value; }
		public RubberSpanBinding Span { get => _span; set => _span = value; }
		public SlingshotSwitchZone[] SwitchZones {
			get => _switchZones ?? Array.Empty<SlingshotSwitchZone>();
			set => _switchZones = value ?? Array.Empty<SlingshotSwitchZone>();
		}
		public float ArmContactPosition01 { get => _armContactPosition01; set => _armContactPosition01 = value; }
		public float ArmRestClearance { get => _armRestClearance; set => _armRestClearance = value; }
		public float ArmTipTravel { get => _armTipTravel; set => _armTipTravel = value; }
		public SlingshotActuatorAsset Actuator { get => _actuator; set => _actuator = value; }
		public GameObject CoilArmVisual { get => _coilArmVisual; set => _coilArmVisual = value; }
		public Axis VisualRotationAxis { get => _visualRotationAxis; set => _visualRotationAxis = value; }
		public float VisualRestAngle { get => _visualRestAngle; set => _visualRestAngle = value; }
		public float VisualFiredAngle { get => _visualFiredAngle; set => _visualFiredAngle = value; }

		public static bool RuntimeAvailable => false;

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => new[] {
			new GamelogicEngineSwitch(SwitchItem) {
				Description = "Slingshot blade switches",
				IsPulseSwitch = false,
			},
		};

		public IEnumerable<GamelogicEngineCoil> AvailableCoils => new[] {
			new GamelogicEngineCoil(CoilItem) { Description = "Slingshot actuator" },
		};

		public SwitchDefault SwitchDefault => SwitchDefault.NormallyOpen;

		IEnumerable<GamelogicEngineSwitch> IDeviceComponent<GamelogicEngineSwitch>.AvailableDeviceItems
			=> AvailableSwitches;
		IEnumerable<GamelogicEngineCoil> IDeviceComponent<GamelogicEngineCoil>.AvailableDeviceItems
			=> AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableComponent.AvailableWireDestinations
			=> AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IDeviceComponent<IGamelogicEngineDeviceItem>.AvailableDeviceItems
			=> AvailableCoils;

		IApiCoil ICoilDeviceComponent.CoilDevice(string deviceId) => null;

		public byte[] Pack() => RubberSlingshotPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files)
			=> RubberSlingshotReferencesPackable.Pack(this, root, refs, files);

		public void Unpack(byte[] bytes) => RubberSlingshotPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs,
			PackagedFiles files)
			=> RubberSlingshotReferencesPackable.Unpack(data, this, root, refs, files);

		public bool TryResolveSpan(out ResolvedRubberSpan resolved, out string error)
			=> RubberSpanResolver.TryResolve(_rubber, _span, out resolved, out error);

		public IReadOnlyList<string> ValidateConfiguration(bool includeSceneUniqueness = false)
		{
			var errors = new List<string>();
			if (!TryResolveSpan(out var resolved, out var spanError)) {
				errors.Add(spanError);
			}
			if (_switchZones == null || _switchZones.Length != 2) {
				errors.Add("A physical slingshot requires exactly two switch zones.");
			} else {
				for (var i = 0; i < _switchZones.Length; i++) {
					var zone = _switchZones[i];
					if (!IsNormalized(zone.Position01)) {
						errors.Add($"Switch zone {i + 1} position must be in [0, 1].");
					}
					if (!float.IsFinite(zone.OpenDeflection) || zone.OpenDeflection < 0f
						|| !float.IsFinite(zone.CloseDeflection)
						|| zone.CloseDeflection <= zone.OpenDeflection) {
						errors.Add($"Switch zone {i + 1} requires 0 <= open deflection < close deflection.");
					}
					if (!float.IsFinite(zone.MinimumClosedTimeMs)
						|| zone.MinimumClosedTimeMs < 0f) {
						errors.Add($"Switch zone {i + 1} minimum closed time must be non-negative.");
					}
				}
			}
			if (!IsNormalized(_armContactPosition01)) {
				errors.Add("Arm contact position must be in [0, 1].");
			}
			if (!IsFiniteNonNegative(_armRestClearance)
				|| !IsFiniteNonNegative(_armTipTravel)) {
				errors.Add("Arm clearance and travel must be finite and non-negative.");
			}
			if (!_actuator) {
				errors.Add("Assign a calibrated slingshot actuator asset.");
			} else {
				errors.AddRange(_actuator.ValidateData());
			}

			if (includeSceneUniqueness && errors.Count == 0) {
				foreach (var other in FindObjectsByType<RubberSlingshotComponent>(
					FindObjectsInactive.Include, FindObjectsSortMode.None)) {
					if (other == this || other._rubber != _rubber
						|| !other.TryResolveSpan(out var otherResolved, out _)) {
						continue;
					}
					if (otherResolved.PathElementIndex == resolved.PathElementIndex) {
						errors.Add($"Rubber span is already bound by '{other.name}'.");
						break;
					}
				}
			}
			return errors.Where(error => !string.IsNullOrEmpty(error)).ToArray();
		}

		internal void RestoreReferences(RubberComponent rubber,
			RubberGuideComponent startGuide, RubberGuideComponent endGuide,
			SlingshotActuatorAsset actuator, GameObject coilArmVisual)
		{
			_rubber = rubber;
			_span.StartSupport.Guide = startGuide;
			_span.EndSupport.Guide = endGuide;
			_actuator = actuator;
			_coilArmVisual = coilArmVisual;
		}

		public void ValidateAfterUnpack()
		{
			var errors = ValidateConfiguration();
			if (errors.Count == 0) {
				return;
			}
			enabled = false;
			Debug.LogWarning(
				$"Rubber slingshot '{name}' was unpacked with an invalid configuration: {string.Join(" ", errors)}",
				this);
		}

		private void Awake()
		{
			if (Application.isPlaying) {
				enabled = false;
				Debug.LogError($"Physical slingshot runtime unavailable for '{name}'. Authoring data is preserved, but this component does not register runtime controls.", this);
			}
		}

		private static bool IsNormalized(float value)
			=> float.IsFinite(value) && value >= 0f && value <= 1f;

		private static bool IsFiniteNonNegative(float value)
			=> float.IsFinite(value) && value >= 0f;
	}
}
