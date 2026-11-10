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
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.Test
{
	public class LampBrightnessTests
	{
		[Test]
		public void ShouldRescaleALitLampWithoutAnotherLampEvent()
		{
			using var fixture = new Fixture(LampType.SingleFading);
			fixture.Lamps.HandleLampEvent("175", 255f);

			fixture.Lamp.Brightness = 2f;

			fixture.Light.enabled.Should().BeTrue();
			fixture.Light.intensity.Should().BeApproximately(20f, 0.01f);
		}

		[Test]
		public void ShouldNotCompoundRepeatedBrightnessChanges()
		{
			using var fixture = new Fixture(LampType.SingleFading);
			fixture.Lamps.HandleLampEvent("175", 255f);

			fixture.Lamp.Brightness = 2f;
			fixture.Lamp.Brightness = 3f;
			fixture.Lamp.Brightness = 0.5f;

			fixture.Light.intensity.Should().BeApproximately(5f, 0.01f);

			fixture.Lamp.Brightness = 1f;
			fixture.Light.intensity.Should().BeApproximately(10f, 0.01f);
		}

		[Test]
		public void ShouldApplyBrightnessToLaterLampEvents()
		{
			using var fixture = new Fixture(LampType.SingleFading);

			fixture.Lamp.Brightness = 2f;
			fixture.Light.enabled.Should().BeFalse("brightness does not light a lamp that is off");

			fixture.Lamps.HandleLampEvent("175", 127f);
			fixture.Light.intensity.Should().BeApproximately(20f * 127f / 255f, 0.01f);

			fixture.Lamps.HandleLampEvent("175", 0f);
			fixture.Light.enabled.Should().BeFalse();
		}

		[Test]
		public void ShouldKeepBrightnessThroughAColorChange()
		{
			using var fixture = new Fixture(LampType.SingleFading);
			fixture.Lamps.HandleLampEvent("175", 255f);
			fixture.Lamp.Brightness = 2f;

			fixture.Lamp.Color = UnityEngine.Color.red;

			fixture.Light.color.Should().Be(UnityEngine.Color.red);
			fixture.Light.intensity.Should().BeApproximately(20f, 0.01f);
		}

		[Test]
		public void ShouldNotGoBelowZero()
		{
			using var fixture = new Fixture(LampType.SingleFading);
			fixture.Lamps.HandleLampEvent("175", 255f);

			fixture.Lamp.Brightness = -1f;

			fixture.Lamp.Brightness.Should().Be(0f);
			fixture.Light.intensity.Should().Be(0f);
		}

		private sealed class Fixture : IDisposable
		{
			public readonly LampPlayer Lamps;
			public readonly Light Light;
			public readonly LightComponent Lamp;
			private readonly GameObject _root = new("Lamp brightness fixture");

			public Fixture(LampType type)
			{
				var player = _root.AddComponent<Player>();
				var table = _root.AddComponent<TableComponent>();
				Lamps = (LampPlayer)typeof(Player)
					.GetField("_lampPlayer", BindingFlags.Instance | BindingFlags.NonPublic)!
					.GetValue(player);
				Lamps.Awake(player, table, _root.AddComponent<DefaultGamelogicEngine>());

				var lampObject = new GameObject("Globe Flash");
				lampObject.transform.SetParent(_root.transform);
				var lamp = Lamp = lampObject.AddComponent<LightComponent>();
				var source = new GameObject("Source");
				source.transform.SetParent(lampObject.transform);
				Light = source.AddComponent<Light>();
				Light.intensity = 10f;

				// Edit mode does not call Awake, which is where the component registers
				// and remembers its lights' intensities.
				typeof(LightComponent)
					.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)!
					.Invoke(lamp, null);
				player.Lamp(lamp).OnInit(null);

				table.MappingConfig.Lamps.Add(new LampMapping {
					Id = "175",
					Source = LampSource.Lamp,
					Device = lamp,
					Type = type,
					Channel = ColorChannel.Alpha,
					FadingSteps = 255,
				});
				Lamps.OnStart();
			}

			public void Dispose()
			{
				Lamps.OnDestroy();
				UnityEngine.Object.DestroyImmediate(_root);
			}
		}
	}
}
