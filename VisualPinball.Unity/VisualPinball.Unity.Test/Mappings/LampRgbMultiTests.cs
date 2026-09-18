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
using System.Collections.Generic;
using System.Reflection;
using FluentAssertions;
using NUnit.Framework;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.Math;
using Color = VisualPinball.Engine.Math.Color;

namespace VisualPinball.Unity.Test
{
	public class LampRgbMultiTests
	{
		[Test]
		public void ShouldComposeAllChannelsOfAMultiChannelLamp()
		{
			using var fixture = new Fixture();

			// A machine drives the three channels of one light under three IDs. Each
			// arrives on its own, so the light has to end up with all three, not with
			// whichever channel happened to be written last.
			fixture.LampPlayer.HandleLampEvent("60", 255f);
			fixture.LampPlayer.HandleLampEvent("61", 128f);
			fixture.LampPlayer.HandleLampEvent("62", 0f);

			fixture.LampApi.Color.r.Should().BeApproximately(1f, 0.01f);
			fixture.LampApi.Color.g.Should().BeApproximately(128f / 255f, 0.01f);
			fixture.LampApi.Color.b.Should().Be(0f);
		}

		[Test]
		public void ShouldKeepTheOtherChannelsWhenOneChannelChanges()
		{
			using var fixture = new Fixture();

			fixture.LampPlayer.HandleLampEvent("60", 255f);
			fixture.LampPlayer.HandleLampEvent("61", 255f);
			fixture.LampPlayer.HandleLampEvent("62", 255f);
			fixture.LampPlayer.HandleLampEvent("61", 0f);

			fixture.LampApi.Color.r.Should().BeApproximately(1f, 0.01f);
			fixture.LampApi.Color.g.Should().Be(0f);
			fixture.LampApi.Color.b.Should().BeApproximately(1f, 0.01f);
		}

		[Test]
		public void ShouldNotLetOneChannelWriteIntoAnothersState()
		{
			using var fixture = new Fixture();

			fixture.LampPlayer.HandleLampEvent("60", 255f);
			fixture.LampPlayer.HandleLampEvent("61", 64f);

			// Each ID owns exactly one channel; composing must not write back into the
			// states it reads, because Color is a reference type.
			fixture.LampPlayer.LampStates["60"].Color.R.Should().BeApproximately(1f, 0.01f);
			fixture.LampPlayer.LampStates["61"].Color.G.Should().BeApproximately(64f / 255f, 0.01f);
		}

		private sealed class Fixture : IDisposable
		{
			private readonly GameObject _root = new("Rgb multi lamp fixture");

			public readonly LampPlayer LampPlayer = new();
			public readonly RecordingLampApi LampApi = new();

			public Fixture()
			{
				var player = _root.AddComponent<Player>();
				var table = _root.AddComponent<TableComponent>();

				var lampObject = new GameObject("Shield Left");
				lampObject.transform.SetParent(_root.transform);
				var lamp = lampObject.AddComponent<LightComponent>();
				lamp.Color = UnityEngine.Color.white;

				foreach (var (id, channel) in new[] {
					("60", ColorChannel.Red),
					("61", ColorChannel.Green),
					("62", ColorChannel.Blue),
				}) {
					table.MappingConfig.Lamps.Add(new LampMapping {
						Id = id,
						Source = LampSource.Lamp,
						Device = lamp,
						Type = LampType.RgbMulti,
						Channel = channel,
						FadingSteps = 255,
					});
				}

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
			public UnityEngine.Color Color { get; private set; }

#pragma warning disable CS0067
			public event EventHandler Init;
#pragma warning restore CS0067

			public void OnChange(bool enabled) { }
			public void OnLamp(LampStatus newStatus) { }
			public void OnLamp(float intensity) { }
			public void OnLamp(UnityEngine.Color color) => Color = color;
			public void OnInit(BallManager ballManager) { }
			public void OnDestroy() { }
		}

	}
}
