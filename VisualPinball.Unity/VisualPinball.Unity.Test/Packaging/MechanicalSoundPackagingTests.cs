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
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VisualPinball.Unity.Editor;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class MechanicalSoundPackagingTests
	{
		[UnityTest]
		public IEnumerator RuntimePackagePreservesFourBanksAndSharedAndNullReferences() {
			var assetFolder = "Assets/__MechanicalSoundTest_" + Guid.NewGuid().ToString("N");
			var package = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".vpe");
			var source = new GameObject("Table");
			GameObject imported = null;
			try {
				Directory.CreateDirectory(assetFolder);
				var wav = assetFolder + "/click.wav";
				using (var writer = new BinaryWriter(File.Create(wav))) {
					writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
					writer.Write(38);
					writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
					writer.Write(16);
					writer.Write((short)1);
					writer.Write((short)1);
					writer.Write(44100);
					writer.Write(88200);
					writer.Write((short)2);
					writer.Write((short)16);
					writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
					writer.Write(2);
					writer.Write((short)1000);
				}
				AssetDatabase.ImportAsset(wav);
				var first = ScriptableObject.CreateInstance<SoundEffectAsset>();
				first.Clips = new[] { AssetDatabase.LoadAssetAtPath<AudioClip>(wav) };
				first.BusId = SoundBuses.Transients;
				AssetDatabase.CreateAsset(first, assetFolder + "/first.asset");
				var second = ScriptableObject.CreateInstance<SoundEffectAsset>();
				second.Clips = first.Clips;
				second.BusId = "future.bus";
				AssetDatabase.CreateAsset(second, assetFolder + "/second.asset");
				source.AddComponent<TableComponent>();
				source.isStatic = true;
				var child = new GameObject("Flipper sound");
				child.transform.SetParent(source.transform, false);
				var sound = child.AddComponent<FlipperSoundComponent>();
				sound.Attack = first;
				sound.Up = second;
				sound.Reflip = first;
				sound.Down = null;
				sound.Volume = .3f;
				sound.ReflipDegrees = 19;
				var input = child.AddComponent<InputActionSoundComponent>();
				input.ActionName = "Start Game";
				input.SoundAsset = second;
				new PackageWriter(source).WritePackageSync(package);
				var task = new RuntimePackageReader(package).ImportIntoScene();
				while (!task.IsCompleted)
					yield return null;
				if (task.IsFaulted)
					throw task.Exception.GetBaseException();
				imported = task.Result;
				var restored = imported.GetComponentInChildren<FlipperSoundComponent>(true);
				Assert.That(restored, Is.Not.Null, "flipper component");
				Assert.That(restored.Attack, Is.Not.Null, "attack bank");
				Assert.That(restored.Up, Is.Not.Null, "up bank");
				Assert.That(restored.Attack.BusId, Is.EqualTo(SoundBuses.Transients));
				Assert.That(restored.Up.BusId, Is.EqualTo("future.bus"), "unknown routes survive round-trip");
				Assert.That(SoundBuses.Resolve(restored.Up, SoundBuses.Rolling), Is.EqualTo(SoundBuses.Rolling), "unknown routes use the component's compatible automatic default");
				Assert.That(restored.Attack, Is.SameAs(restored.Reflip));
				Assert.That(restored.Down, Is.Null);
				Assert.That(restored.Attack.Clips[0], Is.Not.Null, "packed audio clip");
				Assert.That(restored.Volume, Is.EqualTo(.3f));
				Assert.That(restored.ReflipDegrees, Is.EqualTo(19));
				var restoredInput = imported.GetComponentInChildren<InputActionSoundComponent>(true);
				Assert.That(restoredInput.ActionName, Is.EqualTo("Start Game"));
				Assert.That(restoredInput.SoundAsset, Is.SameAs(restored.Up));
			}
			finally {
				if (imported)
					Object.DestroyImmediate(imported);
				Object.DestroyImmediate(source);
				AssetDatabase.DeleteAsset(assetFolder);
				if (File.Exists(package))
					File.Delete(package);
			}
		}
	}
}
