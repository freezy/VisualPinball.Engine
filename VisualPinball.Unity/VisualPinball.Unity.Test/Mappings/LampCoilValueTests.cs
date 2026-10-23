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

using System;
using System.Reflection;
using FluentAssertions;
using NUnit.Framework;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity.Test
{
	public class LampCoilValueTests
	{
		[Test]
		public void ShouldDriveTheLampWithTheCoilStrength()
		{
			using var fixture = new Fixture();

			// a PWM-integrated flasher (e.g. PinMAME's physical outputs) reports its brightness
			fixture.LampPlayer.HandleCoilEvent("17", 0.5f);

			fixture.LampApi.Status.Should().Be(LampStatus.On);
			fixture.LampApi.Intensity.Should().BeApproximately(0.5f, 0.001f);
			fixture.LampPlayer.LampStates["17"].IsOn.Should().BeTrue();
			fixture.LampPlayer.LampStates["17"].Intensity.Should().BeApproximately(0.5f, 0.01f);
		}

		[Test]
		public void ShouldFollowTheStrengthUpToFullAndOff()
		{
			using var fixture = new Fixture();

			fixture.LampPlayer.HandleCoilEvent("17", 0.3f);
			fixture.LampPlayer.HandleCoilEvent("17", 1f);
			fixture.LampApi.Status.Should().Be(LampStatus.On);
			fixture.LampApi.Intensity.Should().Be(1f);

			fixture.LampPlayer.HandleCoilEvent("17", 0.05f);
			fixture.LampApi.Intensity.Should().BeApproximately(0.05f, 0.001f);

			fixture.LampPlayer.HandleCoilEvent("17", 0f);
			fixture.LampApi.Status.Should().Be(LampStatus.Off);
			fixture.LampPlayer.LampStates["17"].IsOn.Should().BeFalse();
		}

		[Test]
		public void ShouldSwitchPlainCoilsOnAtFullIntensity()
		{
			using var fixture = new Fixture();

			fixture.LampPlayer.HandleCoilEvent("17", 1f);
			fixture.LampApi.Status.Should().Be(LampStatus.On);
			fixture.LampApi.Intensity.Should().Be(1f);

			fixture.LampPlayer.HandleCoilEvent("17", 0f);
			fixture.LampApi.Status.Should().Be(LampStatus.Off);
		}

		private sealed class Fixture : IDisposable
		{
			private readonly GameObject _root = new("Lamp coil value fixture");

			public readonly LampPlayer LampPlayer = new();
			public readonly RecordingLampApi LampApi = new();

			public Fixture()
			{
				var player = _root.AddComponent<Player>();
				var table = _root.AddComponent<TableComponent>();

				var lampObject = new GameObject("Flasher 17");
				lampObject.transform.SetParent(_root.transform);
				var lamp = lampObject.AddComponent<LightComponent>();

				// what the coil manager creates for a coil with the Lamp destination
				table.MappingConfig.Lamps.Add(new LampMapping {
					Id = "17",
					Source = LampSource.Lamp,
					IsCoil = true,
					Device = lamp,
				});

				LampPlayer.Awake(player, table, _root.AddComponent<DefaultGamelogicEngine>());
				typeof(LampPlayer)
					.GetMethod("RegisterLamp", BindingFlags.Instance | BindingFlags.NonPublic)!
					.Invoke(LampPlayer, new object[] { lamp, LampApi });
				LampPlayer.OnStart();
			}

			public void Dispose()
			{
				LampPlayer.OnDestroy();
				UnityEngine.Object.DestroyImmediate(_root);
			}
		}

		private sealed class RecordingLampApi : IApiLamp
		{
			public LampStatus Status { get; private set; }
			public float Intensity { get; private set; } = 1f;

#pragma warning disable CS0067
			public event EventHandler Init;
#pragma warning restore CS0067

			public void OnChange(bool enabled) { }
			public void OnLamp(LampStatus newStatus) => Status = newStatus;
			public void OnLamp(float intensity) => Intensity = intensity;
			public void OnLamp(UnityEngine.Color color) { }
			public void OnInit(BallManager ballManager) { }
			public void OnDestroy() { }
		}
	}
}
