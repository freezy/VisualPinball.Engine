// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VisualPinball.Unity.Editor;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class SwitchMappingPackagingTests
	{
		[UnityTest]
		public IEnumerator PackageKeepsSwitchDevicesThatShareAnObject()
		{
			var path = Path.Combine(Path.GetTempPath(), $"vpe-switch-mappings-{Guid.NewGuid():N}.vpe");
			var source = new GameObject("Table");
			GameObject imported = null;
			try {
				var table = source.AddComponent<TableComponent>();
				source.isStatic = true;
				var firstMotor = source.AddComponent<ScoreMotorComponent>();
				firstMotor.Duration = 700;
				var tiltBob = source.AddComponent<TiltBobComponent>();
				var secondMotor = source.AddComponent<ScoreMotorComponent>();
				secondMotor.Duration = 900;
				table.MappingConfig.Switches.Add(new SwitchMapping {
					Id = "s_tilt", Device = tiltBob, DeviceItem = TiltBobComponent.SwitchItem,
				});
				table.MappingConfig.Switches.Add(new SwitchMapping {
					Id = "s_motor", Device = secondMotor, DeviceItem = ScoreMotorComponent.MotorRunningSwitchItem,
				});

				new PackageWriter(source).WritePackageSync(path);
				var import = new RuntimePackageReader(path).ImportIntoScene();
				while (!import.IsCompleted) { yield return null; }
				imported = import.GetAwaiter().GetResult();

				var mappings = imported.GetComponent<TableComponent>().MappingConfig.Switches;
				var importedTiltBob = imported.GetComponent<TiltBobComponent>();
				Assert.That(importedTiltBob, Is.Not.Null);
				Assert.That(mappings[0].Device, Is.SameAs(importedTiltBob));
				var motors = imported.GetComponents<ScoreMotorComponent>();
				Assert.That(motors, Has.Length.EqualTo(2));
				Assert.That(motors[1].Duration, Is.EqualTo(900));
				Assert.That(mappings[1].Device, Is.SameAs(motors[1]));
			} finally {
				Object.DestroyImmediate(source);
				if (imported) { Object.DestroyImmediate(imported); }
				File.Delete(path);
			}
		}

		[TestCase(TiltBobComponent.SwitchItem, false, typeof(TiltBobComponent))]
		[TestCase(ScoreMotorComponent.MotorRunningSwitchItem, false, typeof(ScoreMotorComponent))]
		[TestCase(ScoreMotorComponent.MotorRunningSwitchItem, true, null)]
		[TestCase("unknown_switch", false, null)]
		public void LegacyMappingUsesTheOnlyDeviceWithTheRequestedSwitch(string item, bool addSecondMotor, Type expectedDevice)
		{
			var source = new GameObject("Table");
			try {
				source.AddComponent<ScoreMotorComponent>();
				source.AddComponent<TiltBobComponent>();
				if (addSecondMotor) {
					source.AddComponent<ScoreMotorComponent>();
				}
				var mapping = RestoreLegacyMapping(source, item);
				Assert.That(mapping.Device, Is.SameAs(expectedDevice == null ? null : source.GetComponent(expectedDevice)));
			} finally {
				Object.DestroyImmediate(source);
			}
		}

		[Test]
		public void LegacyMappingKeepsItsOnlyDeviceWhenTheItemIsUnavailable()
		{
			var source = new GameObject("Table");
			try {
				var motor = source.AddComponent<ScoreMotorComponent>();
				var mapping = RestoreLegacyMapping(source, "unavailable_switch");

				Assert.That(mapping.Device, Is.SameAs(motor));
				Assert.That(mapping.DeviceItem, Is.EqualTo("unavailable_switch"));
			} finally {
				Object.DestroyImmediate(source);
			}
		}

		[Test]
		public void LegacyMappingCanResolveWhenAnotherDeviceCannotListItsSwitches()
		{
			var source = new GameObject("Table");
			try {
				source.AddComponent<TroughComponent>().Type = -1;
				var tiltBob = source.AddComponent<TiltBobComponent>();
				var mapping = RestoreLegacyMapping(source, TiltBobComponent.SwitchItem);

				Assert.That(mapping.Device, Is.SameAs(tiltBob));
			} finally {
				Object.DestroyImmediate(source);
			}
		}

		private static SwitchMapping RestoreLegacyMapping(GameObject source, string item)
		{
			var refs = new PackagedRefs(source.transform);
			refs.SetNodeIdsForRead(new Dictionary<string, Transform> { ["table-node"] = source.transform });
			var json = $"{{\"Id\":\"s_test\",\"DeviceItem\":\"{item}\",\"_deviceId\":\"table-node\"}}";
			var mapping = PackageApi.Packer.Unpack<SwitchMapping>(Encoding.UTF8.GetBytes(json));
			mapping.RestoreReference(refs);
			return mapping;
		}
	}
}
