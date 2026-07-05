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
using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Unity.Simulation;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Shared cabinet input settings for runtime configuration, player config,
	/// and editor presets.
	/// </summary>
	/// <remarks>
	/// This type intentionally keeps player/cabinet hardware choices separate
	/// from table-authored physics values such as gravity and table nudge time.
	/// The fields are Unity/JsonUtility-serializable so the same object shape can
	/// be embedded in player JSON or wrapped by <see cref="CabinetInputSettingsAsset"/>
	/// for editor defaults.
	/// </remarks>
	[Serializable]
	public sealed class CabinetInputSettings
	{
		[Tooltip("Enable native input polling (requires the VisualPinball.NativeInput native plugin for the current platform).")]
		public bool enableNativeInput = true;

		[Tooltip("Input polling interval in microseconds (default 500 us = 2000 Hz).")]
		[Range(100, 2000)]
		public int inputPollingIntervalUs = 500;

		public CabinetNudgeSettings nudge = new();

		/// <summary>
		/// Clamps values and guarantees nested objects/lists are non-null.
		/// </summary>
		public void Normalize()
		{
			inputPollingIntervalUs = Mathf.Clamp(inputPollingIntervalUs, 100, 2000);
			nudge ??= new CabinetNudgeSettings();
			nudge.Normalize();
		}

		/// <summary>
		/// Applies all settings that are relevant to a loaded table root.
		/// </summary>
		/// <remarks>
		/// The simulation-thread component owns native input polling settings,
		/// while the physics engine owns nudge behavior. Looking both up from the
		/// root lets the player app apply one settings object after loading a
		/// table without knowing which component currently stores each concern.
		/// </remarks>
		public void ApplyTo(GameObject tableRoot)
		{
			if (tableRoot == null) {
				return;
			}

			Normalize();
			ApplyTo(tableRoot.GetComponentInChildren<PhysicsEngine>(true));
			ApplyTo(tableRoot.GetComponentInChildren<SimulationThreadComponent>(true), false);
		}

		/// <summary>
		/// Applies nudge behavior to a physics engine.
		/// </summary>
		public void ApplyTo(PhysicsEngine physicsEngine)
		{
			Normalize();
			nudge.ApplyTo(physicsEngine);
		}

		/// <summary>
		/// Applies native input and default sensor mappings to a simulation-thread
		/// component.
		/// </summary>
		/// <param name="applyNudgeToPhysics">
		/// When true, also pushes the nested nudge settings to the sibling physics
		/// engine. Pass false when the caller already applied those settings.
		/// </param>
		public void ApplyTo(SimulationThreadComponent simulationThread, bool applyNudgeToPhysics = true)
		{
			if (simulationThread == null) {
				return;
			}

			Normalize();
			simulationThread.ApplyCabinetInputSettings(this, applyNudgeToPhysics);
		}

		/// <summary>
		/// Captures the current component fields into the shared settings shape.
		/// </summary>
		public static CabinetInputSettings From(PhysicsEngine physicsEngine, SimulationThreadComponent simulationThread = null)
		{
			var settings = new CabinetInputSettings();
			if (physicsEngine != null) {
				settings.nudge = CabinetNudgeSettings.From(physicsEngine);
			}
			if (simulationThread != null) {
				settings.enableNativeInput = simulationThread.EnableNativeInput;
				settings.inputPollingIntervalUs = simulationThread.InputPollingIntervalUs;
				settings.nudge ??= new CabinetNudgeSettings();
				settings.nudge.sensors = CabinetNudgeSensorSettings.FromSimulationThreadSensors(simulationThread.NudgeSensors);
			}
			settings.Normalize();
			return settings;
		}
	}

	/// <summary>
	/// Shared nudge behavior settings for keyboard nudges, visual cabinet motion,
	/// player tilt-bob source selection, and analog sensors.
	/// </summary>
	/// <remarks>
	/// Field names match the original player JSON config so existing
	/// <c>player-config.json</c> files deserialize directly after moving the class
	/// from the player assembly into the runtime assembly.
	/// </remarks>
	[Serializable]
	public sealed class CabinetNudgeSettings
	{
		public int keyboardMode = (int)KeyboardNudgeMode.CabModel;
		public float keyboardStrength = 1f;
		public float keyboardCabinetDamping = CabinetPhysicsState.DefaultKeyboardDampingRatio;
		public float visualStrength = 1f;
		public CabinetPlumbSettings plumb = new();
		public List<CabinetNudgeSensorSettings> sensors = new();

		/// <summary>
		/// Clamps values and guarantees nested objects/lists are non-null.
		/// </summary>
		public void Normalize()
		{
			plumb ??= new CabinetPlumbSettings();
			sensors ??= new List<CabinetNudgeSensorSettings>();
			keyboardMode = Mathf.Clamp(keyboardMode, (int)KeyboardNudgeMode.PushRetract, (int)KeyboardNudgeMode.CabModel);
			keyboardStrength = Mathf.Clamp(keyboardStrength, 0f, 2f);
			keyboardCabinetDamping = Mathf.Clamp(keyboardCabinetDamping,
				CabinetPhysicsState.MinKeyboardDampingRatio,
				CabinetPhysicsState.MaxKeyboardDampingRatio);
			visualStrength = Mathf.Clamp(visualStrength, 0f, 2f);
			plumb.Normalize();
			if (sensors.Count > NudgeState.MaxSensors) {
				sensors.RemoveRange(NudgeState.MaxSensors, sensors.Count - NudgeState.MaxSensors);
			}
			for (var i = 0; i < sensors.Count; i++) {
				sensors[i] ??= new CabinetNudgeSensorSettings();
				sensors[i].Normalize();
			}
		}

		/// <summary>
		/// Applies this settings object to a physics engine, including live state
		/// when the engine is already initialized.
		/// </summary>
		public void ApplyTo(PhysicsEngine physicsEngine)
		{
			if (physicsEngine == null) {
				return;
			}

			physicsEngine.ConfigureNudge(this);
			TiltBobComponent.ApplySettings(physicsEngine, plumb);
		}

		/// <summary>
		/// Converts persisted/editor sensor settings to physics-engine configs.
		/// </summary>
		public List<NudgeSensorConfig> ToEngineSensorConfigs()
		{
			Normalize();
			var sensorConfigs = new List<NudgeSensorConfig>(sensors.Count);
			foreach (var sensor in sensors) {
				sensorConfigs.Add(sensor.ToEngineConfig());
			}
			return sensorConfigs;
		}

		/// <summary>
		/// Converts sensor settings to the legacy component shape used by the
		/// simulation-thread inspector and packer.
		/// </summary>
		public List<SimulationThreadNudgeSensorConfig> ToSimulationThreadSensorConfigs()
		{
			Normalize();
			var sensorConfigs = new List<SimulationThreadNudgeSensorConfig>(sensors.Count);
			foreach (var sensor in sensors) {
				sensorConfigs.Add(sensor.ToSimulationThreadConfig());
			}
			return sensorConfigs;
		}

		/// <summary>
		/// Captures the nudge fields currently stored on a physics engine.
		/// </summary>
		public static CabinetNudgeSettings From(PhysicsEngine physicsEngine)
		{
			var settings = new CabinetNudgeSettings();
			if (physicsEngine == null) {
				return settings;
			}

			settings.keyboardMode = (int)physicsEngine.KeyboardNudgeMode;
			settings.keyboardStrength = physicsEngine.KeyboardNudgeStrength;
			settings.keyboardCabinetDamping = physicsEngine.KeyboardCabinetDamping;
			settings.visualStrength = physicsEngine.VisualNudgeStrength;
			settings.plumb = new CabinetPlumbSettings {
				enabled = true,
				mode = TiltBobComponent.FindFor(physicsEngine)?.Mode ?? TiltBobMode.Simulated
			};
			settings.Normalize();
			return settings;
		}
	}

	/// <summary>
	/// Player plumb-bob tilt settings.
	/// </summary>
	[Serializable]
	public sealed class CabinetPlumbSettings
	{
		/// <summary>
		/// Legacy compatibility flag from the pre-component plumb setting. New
		/// behavior is controlled by the presence of <see cref="TiltBobComponent"/>
		/// on the table and by <see cref="mode"/>.
		/// </summary>
		[HideInInspector]
		public bool enabled = true;
		public TiltBobMode mode = TiltBobMode.Simulated;

		/// <summary>
		/// Legacy player-config fields retained for JSON compatibility. Current
		/// tilt-bob tuning is authored on <see cref="TiltBobComponent"/>.
		/// </summary>
		[HideInInspector]
		public float damping = 1f;

		[HideInInspector]
		public float thresholdDeg = 2f;

		/// <summary>
		/// Clamps the tilt-bob source mode and legacy tuning fields to useful ranges.
		/// </summary>
		public void Normalize()
		{
			enabled = true;
			mode = (TiltBobMode)Mathf.Clamp((int)mode, (int)TiltBobMode.Simulated, (int)TiltBobMode.Physical);
			damping = Mathf.Clamp(damping, 0f, 2f);
			thresholdDeg = Mathf.Clamp(thresholdDeg, 0.5f, 4f);
		}
	}

	/// <summary>
	/// Serializable settings for one logical nudge sensor.
	/// </summary>
	/// <remarks>
	/// Mapping fields are stored as compact strings because native device ids can
	/// contain punctuation and because this is the format already used by table
	/// packages and player JSON. Conversion methods parse those strings into
	/// <see cref="SensorMapping"/> objects only when runtime input needs them.
	/// </remarks>
	[Serializable]
	public sealed class CabinetNudgeSensorSettings
	{
		public const string TypeGamepad = "gamepad";
		public const string TypeCabinetIntent = "cabinetIntent";
		public const string TypeCabinetDirect = "cabinetDirect";

		public string type = TypeGamepad;
		public float strength = 1f;
		public float cabinetMassKg = 113f;
		public string x = string.Empty;
		public string y = string.Empty;
		public string accX = string.Empty;
		public string accY = string.Empty;
		public string velX = string.Empty;
		public string velY = string.Empty;
		public int mountRotation;
		public bool mountMirror;

		/// <summary>
		/// Clamps values, normalizes type names, and guarantees mapping strings are
		/// non-null.
		/// </summary>
		public void Normalize()
		{
			if (type != TypeCabinetIntent && type != TypeCabinetDirect) {
				type = TypeGamepad;
			}
			strength = Mathf.Clamp(strength, 0f, 2f);
			cabinetMassKg = Mathf.Clamp(cabinetMassKg <= 0f ? 113f : cabinetMassKg, 0f, 200f);
			mountRotation = Mathf.Clamp(mountRotation, (int)NudgeSensorMountRotation.Rotation0, (int)NudgeSensorMountRotation.Rotation270);
			x ??= string.Empty;
			y ??= string.Empty;
			accX ??= string.Empty;
			accY ??= string.Empty;
			velX ??= string.Empty;
			velY ??= string.Empty;
		}

		/// <summary>
		/// Converts persisted/editor settings into the runtime physics-engine
		/// sensor config.
		/// </summary>
		public NudgeSensorConfig ToEngineConfig()
		{
			Normalize();
			return new NudgeSensorConfig {
				Type = type switch {
					TypeCabinetIntent => NudgeSensorType.CabinetIntent,
					TypeCabinetDirect => NudgeSensorType.CabinetDirect,
					_ => NudgeSensorType.GamepadIntent
				},
				Strength = strength,
				CabinetMassKg = cabinetMassKg,
				MountRotation = (NudgeSensorMountRotation)mountRotation,
				MountMirror = mountMirror,
				X = ParseMapping(x),
				Y = ParseMapping(y),
				AccelerationX = ParseMapping(accX),
				AccelerationY = ParseMapping(accY),
				VelocityX = ParseMapping(velX),
				VelocityY = ParseMapping(velY)
			};
		}

		/// <summary>
		/// Converts to the component shape used by the simulation-thread inspector.
		/// </summary>
		public SimulationThreadNudgeSensorConfig ToSimulationThreadConfig()
		{
			Normalize();
			return new SimulationThreadNudgeSensorConfig {
				Type = type switch {
					TypeCabinetIntent => NudgeSensorType.CabinetIntent,
					TypeCabinetDirect => NudgeSensorType.CabinetDirect,
					_ => NudgeSensorType.GamepadIntent
				},
				Strength = strength,
				CabinetMassKg = cabinetMassKg,
				MountRotation = (NudgeSensorMountRotation)mountRotation,
				MountMirror = mountMirror,
				X = x,
				Y = y,
				AccelerationX = accX,
				AccelerationY = accY,
				VelocityX = velX,
				VelocityY = velY
			};
		}

		/// <summary>
		/// Captures a runtime physics-engine sensor config back into a serializable
		/// settings object.
		/// </summary>
		public static CabinetNudgeSensorSettings From(NudgeSensorConfig sensor)
		{
			sensor ??= new NudgeSensorConfig();
			sensor.Normalize();
			var settings = new CabinetNudgeSensorSettings {
				type = sensor.Type switch {
					NudgeSensorType.CabinetIntent => TypeCabinetIntent,
					NudgeSensorType.CabinetDirect => TypeCabinetDirect,
					_ => TypeGamepad
				},
				strength = sensor.Strength,
				cabinetMassKg = sensor.CabinetMassKg,
				mountRotation = (int)sensor.MountRotation,
				mountMirror = sensor.MountMirror,
				x = sensor.X?.ToString() ?? string.Empty,
				y = sensor.Y?.ToString() ?? string.Empty,
				accX = sensor.AccelerationX?.ToString() ?? string.Empty,
				accY = sensor.AccelerationY?.ToString() ?? string.Empty,
				velX = sensor.VelocityX?.ToString() ?? string.Empty,
				velY = sensor.VelocityY?.ToString() ?? string.Empty
			};
			settings.Normalize();
			return settings;
		}

		/// <summary>
		/// Captures a simulation-thread component sensor config back into the
		/// shared settings object.
		/// </summary>
		public static CabinetNudgeSensorSettings From(SimulationThreadNudgeSensorConfig sensor)
		{
			sensor ??= new SimulationThreadNudgeSensorConfig();
			sensor.Normalize();
			var settings = new CabinetNudgeSensorSettings {
				type = sensor.Type switch {
					NudgeSensorType.CabinetIntent => TypeCabinetIntent,
					NudgeSensorType.CabinetDirect => TypeCabinetDirect,
					_ => TypeGamepad
				},
				strength = sensor.Strength,
				cabinetMassKg = sensor.CabinetMassKg,
				mountRotation = (int)sensor.MountRotation,
				mountMirror = sensor.MountMirror,
				x = sensor.X,
				y = sensor.Y,
				accX = sensor.AccelerationX,
				accY = sensor.AccelerationY,
				velX = sensor.VelocityX,
				velY = sensor.VelocityY
			};
			settings.Normalize();
			return settings;
		}

		/// <summary>
		/// Converts a simulation-thread sensor list to shared settings objects.
		/// </summary>
		public static List<CabinetNudgeSensorSettings> FromSimulationThreadSensors(
			IReadOnlyList<SimulationThreadNudgeSensorConfig> sensors)
		{
			var result = new List<CabinetNudgeSensorSettings>();
			if (sensors == null) {
				return result;
			}

			for (var i = 0; i < sensors.Count && i < NudgeState.MaxSensors; i++) {
				result.Add(From(sensors[i]));
			}
			return result;
		}

		private static SensorMapping ParseMapping(string value)
		{
			return SensorMapping.TryParse(value, out var mapping) ? mapping : new SensorMapping();
		}
	}
}
