// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using ColorChannel = VisualPinball.Engine.Math.ColorChannel;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class InsertLightTests
	{

		[Test]
		public void ShouldExposeAnInsertAsADirectLampAndWireDestination()
		{
			using var fixture = new MappedInsert();
			fixture.Table.MappingConfig.PopulateLamps(new[] { new GamelogicEngineLamp("42") }, fixture.Table);
			Assert.That(fixture.Table.MappingConfig.Lamps.Single().Device, Is.SameAs(fixture.Control));
			Assert.That(fixture.Table.MappingConfig.Lamps.Single().DeviceItem, Is.EqualTo(LightComponent.LampIdDefault));
			fixture.Start();
			Assert.That(fixture.Player.Lamp(fixture.Control), Is.SameAs(fixture.Api));
			fixture.AssertLevel(0f);
			fixture.Lamps.HandleLampEvent("42", 1f);
			fixture.AssertLevel(1f);
			fixture.Lamps.HandleLampEvent("42", 0f);
			fixture.Api.OnLamp(Color.green);
			fixture.AssertLevel(0f);
			Assert.That(fixture.Control.Color, Is.EqualTo(Color.green));
			((IApiWireDeviceDest)fixture.Api).Wire(LightComponent.LampIdDefault).OnChange(true);
			fixture.AssertLevel(1f);
			((IApiWireDeviceDest)fixture.Api).Wire(LightComponent.LampIdDefault).OnChange(false);
			fixture.AssertLevel(0f);
		}

		[Test]
		public void ShouldTintTheBodyAndTheCoverWithTheLampWhileLit()
		{
			// With the toggle on, the body's block and the cover's block carry the
			// tint amount and the cover gets the lamp's level and colour too; off,
			// the tint is zero and nothing else of the cover's block changes.
			using var fixture = new MappedInsert();
			var coverShader = Shader.Find("Visual Pinball/Insert Cover");
			if (coverShader == null) { Assert.Ignore("Requires the HDRP insert cover shader."); }
			var cover = new Material(coverShader);
			var renderer = fixture.Control.GetComponent<MeshRenderer>();
			try {
				fixture.Control.GetComponent<InsertComponent>().SetCoverMaterial(cover);
				fixture.Control.TintWithLamp = true;
				fixture.Control.LampTint = 0.5f;
				fixture.Control.Color = Color.red;
				fixture.Control.Intensity = 0.75f;
				fixture.Control.Apply();
				var body = new MaterialPropertyBlock();
				var top = new MaterialPropertyBlock();
				renderer.GetPropertyBlock(body, 0);
				renderer.GetPropertyBlock(top, 1);
				Assert.That(body.GetFloat("_InsertLampTint"), Is.EqualTo(0.5f));
				Assert.That(top.GetFloat("_InsertLampTint"), Is.EqualTo(0.5f));
				Assert.That(top.GetFloat("_InsertLampLevel"), Is.EqualTo(0.75f));
				Assert.That(top.GetVector("_InsertLampColor"), Is.EqualTo((Vector4)Color.red.linear));

				fixture.Control.TintWithLamp = false;
				fixture.Control.Apply();
				renderer.GetPropertyBlock(body, 0);
				renderer.GetPropertyBlock(top, 1);
				Assert.That(body.GetFloat("_InsertLampTint"), Is.Zero);
				Assert.That(top.GetFloat("_InsertLampTint"), Is.Zero);
			} finally {
				fixture.Control.GetComponent<InsertComponent>().SetCoverMaterial(null);
				Object.DestroyImmediate(cover);
			}
		}

		[TestCase(LampType.SingleFading, 100, 130f / 255f)]
		[TestCase(LampType.Rgb, 255, 51f / 255f)]
		public void ShouldDriveTheLensAndSpillFromNormalizedRomValues(LampType type, int steps, float expected)
		{
			using var fixture = new MappedInsert();
			fixture.Map("42", type, steps: steps);
			fixture.Start();
			fixture.Lamps.HandleLampEvent("42", 51f);
			fixture.AssertLevel(expected);
			fixture.Api.OnLamp(new Color(0.25f, 0.5f, 1f));
			fixture.AssertLevel(expected);
			var block = new MaterialPropertyBlock();
			fixture.Control.GetComponent<MeshRenderer>().GetPropertyBlock(block, 0);
			Assert.That(block.GetVector("_InsertLampColor"), Is.EqualTo((Vector4)fixture.Control.Color.linear));
			fixture.Lamps.HandleLampEvent("42", 0f);
			fixture.AssertLevel(0f);
			fixture.Api.OnLamp(Color.red);
			fixture.AssertLevel(0f);
		}

		[TestCase(false)]
		[TestCase(true)]
		public void ShouldComposeRomRgbChannelsAndOptionalIntensity(bool alphaChannel)
		{
			using var fixture = new MappedInsert();
			fixture.Map("60", LampType.RgbMulti, ColorChannel.Red);
			fixture.Map("61", LampType.RgbMulti, ColorChannel.Green);
			fixture.Map("62", LampType.RgbMulti, ColorChannel.Blue);
			if (alphaChannel) { fixture.Map("63", LampType.RgbMulti, ColorChannel.Alpha); }
			fixture.Start();
			Assert.That(fixture.Control.SpillLight.enabled, Is.False, "RGB channels start black");
			fixture.Lamps.HandleLampEvent("60", 255f);
			fixture.Lamps.HandleLampEvent("61", 128f);
			fixture.Lamps.HandleLampEvent("62", 64f);
			if (alphaChannel) {
				fixture.AssertLevel(0f);
				fixture.Lamps.HandleLampEvent("63", 128f);
			}
			fixture.AssertLevel(alphaChannel ? 128f / 255f : 1f);
			Assert.That(fixture.Control.Color.r, Is.EqualTo(1f).Within(0.001f));
			Assert.That(fixture.Control.Color.g, Is.EqualTo(128f / 255f).Within(0.001f));
			Assert.That(fixture.Control.Color.b, Is.EqualTo(64f / 255f).Within(0.001f));
			fixture.Lamps.HandleLampEvent("61", 0f);
			Assert.That(fixture.Control.Color.r, Is.EqualTo(1f));
			Assert.That(fixture.Control.Color.g, Is.Zero);
			Assert.That(fixture.Control.Color.b, Is.EqualTo(64f / 255f).Within(0.001f));
			fixture.Lamps.HandleLampEvent("60", 0f);
			fixture.Lamps.HandleLampEvent("62", 0f);
			Assert.That(fixture.Control.SpillLight.enabled, Is.False);
		}

		[Test]
		public void ShouldKeepDirectMappingIndependentOfAParentLight()
		{
			using var fixture = new MappedInsert();
			var parent = fixture.Root.AddComponent<LightComponent>();
			// Populate the parent's legacy cache before direct mapping claims the insert.
			parent.Enabled = false;
			fixture.Map("42", LampType.SingleOnOff);
			fixture.Start();
			fixture.Lamps.HandleLampEvent("42", 1f);
			fixture.Api.OnLamp(Color.blue);
			parent.Enabled = false;
			parent.Color = Color.red;
			fixture.Control.Apply();
			fixture.AssertLevel(1f);
			Assert.That(fixture.Control.Color, Is.EqualTo(Color.blue));
		}

		[Test]
		public void ShouldReleaseDirectControlWhenThePlayerStops()
		{
			using var fixture = new MappedInsert();
			fixture.Api.OnLamp(0.5f);
			Assert.That(fixture.Control.IsControlledByApi, Is.True);
			fixture.Api.OnDestroy();
			Assert.That(fixture.Control.IsControlledByApi, Is.False);
		}

		[Test]
		public void ShouldDriveAnInsertThroughALightGroupAndEditorPreview()
		{
			using var fixture = new MappedInsert();
			var group = fixture.Root.AddComponent<LightGroupComponent>();
			group.Lights = new System.Collections.Generic.List<ILampDeviceComponent> { fixture.Control };
			fixture.Table.MappingConfig.Lamps.Add(new LampMapping { Id = "42", Device = group });
			fixture.Player.Register(group.GetApi(fixture.Player), group);
			group.GetApi(fixture.Player).OnInit(null);
			fixture.Start();
			fixture.Lamps.HandleLampEvent("42", 1f);
			fixture.AssertLevel(1f);
			fixture.Lamps.HandleLampEvent("42", 0f);
			fixture.AssertLevel(0f);
			var manager = typeof(VisualPinball.Unity.Editor.InsertBaker).Assembly.GetType("VisualPinball.Unity.Editor.LampManager");
			var preview = manager.GetMethod("SetLampDeviceEnabled", BindingFlags.NonPublic | BindingFlags.Static);
			preview.Invoke(null, new object[] { group, LightComponent.LampIdDefault, true });
			fixture.Control.Apply();
			fixture.AssertLevel(1f);
			preview.Invoke(null, new object[] { group, LightComponent.LampIdDefault, false });
			fixture.Control.Apply();
			fixture.AssertLevel(0f);
		}

		private sealed class MappedInsert : IDisposable
		{
			public readonly GameObject Root = new("Mapped insert test");
			public readonly Player Player;
			public readonly TableComponent Table;
			public readonly LampPlayer Lamps;
			public readonly InsertLightComponent Control;
			public readonly IApiLamp Api;
			private readonly Material _material;
			private readonly Mesh _mesh;

			public MappedInsert()
			{
				var shader = Shader.Find("Visual Pinball/Insert Hybrid");
				if (shader == null) { Assert.Ignore("Requires the HDRP insert shader."); }
				Player = Root.AddComponent<Player>();
				Table = Root.AddComponent<TableComponent>();
				Lamps = (LampPlayer)typeof(Player).GetField("_lampPlayer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Player);
				Lamps.Awake(Player, Table, Root.AddComponent<DefaultGamelogicEngine>());
				var go = new GameObject("l42");
				go.transform.SetParent(Root.transform);
				go.AddComponent<InsertComponent>();
				_mesh = go.GetComponent<MeshFilter>().sharedMesh;
				_material = new Material(shader);
				_material.SetFloat("_InsertLuminosity", 10f);
				go.GetComponent<MeshRenderer>().sharedMaterial = _material;
				Control = go.AddComponent<InsertLightComponent>();
				var spill = new GameObject("Spill");
				spill.transform.SetParent(go.transform);
				Control.SpillLight = spill.AddComponent<Light>();
				RenderPipeline.Current.LightConverter.UpdateLight(Control.SpillLight, new VisualPinball.Engine.VPT.Light.LightData(), true);
				Control.SpillLumens = 4f;
				Api = Control.GetApi(Player);
				Assert.That(Player.Lamp(Control), Is.SameAs(Api), "ExecuteAlways must register before play mode");
				Api.OnInit(null);
			}

			public void Map(string id, LampType type, ColorChannel channel = ColorChannel.Alpha, int steps = 255, string item = LightComponent.LampIdDefault)
				=> Table.MappingConfig.Lamps.Add(new LampMapping {
					Id = id, Source = LampSource.Lamp, Device = Control, DeviceItem = item,
					Type = type, Channel = channel, FadingSteps = steps,
				});

			/// <summary>Switches the material's bulbs beyond the first on, a radius each.</summary>
			public void Bulbs(int count)
			{
				var names = new[] { "_OpticalLampB", "_OpticalLampC", "_OpticalLampD" };
				for (var i = 0; i < names.Length; i++) {
					_material.SetVector(names[i], new Vector4(0.01f * (i + 1), -0.015f, 0f, i < count - 1 ? 0.008f : 0f));
				}
			}

			public Vector4 BulbDrive(int bulb)
			{
				var block = new MaterialPropertyBlock();
				Control.GetComponent<MeshRenderer>().GetPropertyBlock(block, 0);
				return block.GetVector(new[] { "_InsertLampColor", "_InsertLampColorB", "_InsertLampColorC", "_InsertLampColorD" }[bulb]);
			}

			public void Start() => Lamps.OnStart();

			public void AssertLevel(float expected)
			{
				Control.Apply();
				var renderer = Control.GetComponent<MeshRenderer>();
				var block = new MaterialPropertyBlock();
				renderer.GetPropertyBlock(block, 0);
				Assert.That(Control.Intensity, Is.EqualTo(expected).Within(1e-5f));
				Assert.That(block.GetFloat("_InsertLampLevel"), Is.EqualTo(expected).Within(1e-5f));
				Assert.That(Control.SpillLight.intensity, Is.EqualTo(expected * 40f).Within(1e-4f));
				Assert.That(Control.SpillLight.enabled, Is.EqualTo(expected > 0));
				Assert.That(renderer.enabled, Is.True);
				Assert.That(block.HasFloat("_InsertLuminosity"), Is.False);
				Assert.That(_material.GetFloat("_InsertLuminosity"), Is.EqualTo(10f));
				renderer.GetPropertyBlock(block);
				Assert.That(block.HasFloat("_InsertLampLevel"), Is.False, "Renderer-wide emission would reach the cover.");
			}

			public void Dispose()
			{
				Lamps.OnDestroy();
				Object.DestroyImmediate(Root);
				Object.DestroyImmediate(_mesh);
				Object.DestroyImmediate(_material);
			}
		}

		[Test]
		public void ShouldOfferOneLampPerBulbOfTheMaterial()
		{
			using var fixture = new MappedInsert();
			Assert.That(fixture.Control.BulbCount, Is.EqualTo(1));
			Assert.That(fixture.Control.AvailableLamps.Select(l => l.Id), Is.EqualTo(new[] { LightComponent.LampIdDefault }));
			Assert.That(fixture.Control.AvailableLamps.Single().Description, Is.Null.Or.Empty, "one bulb is one lamp, nothing to pick");

			fixture.Bulbs(3);
			Assert.That(fixture.Control.BulbCount, Is.EqualTo(3));
			Assert.That(fixture.Control.AvailableLamps.Select(l => l.Id), Is.EqualTo(new[] { LightComponent.LampIdDefault, "bulb_2", "bulb_3" }));
			Assert.That(fixture.Control.AvailableLamps.Select(l => l.Description), Is.EqualTo(new[] { "Bulb 1", "Bulb 2", "Bulb 3" }));
			fixture.Control.BulbNames[1] = " Left ";
			fixture.Control.BulbNames[2] = "";
			Assert.That(fixture.Control.AvailableLamps.Select(l => l.Description), Is.EqualTo(new[] { "Bulb 1", "Left", "Bulb 3" }), "named bulbs show their names");
			Assert.That(fixture.Control.BulbPosition(1).x, Is.EqualTo(10f).Within(1e-3f), "where the material puts it, in millimetres");
			Assert.That(InsertLightComponent.BulbIndex(null), Is.Zero);
			Assert.That(InsertLightComponent.BulbIndex(LightComponent.LampIdDefault), Is.Zero);
			Assert.That(InsertLightComponent.BulbIndex("bulb_3"), Is.EqualTo(2));
			Assert.That(InsertLightComponent.BulbIndex("bulb_9"), Is.EqualTo(3), "clamped to the last bulb");
			var device = (IApiLampDevice)fixture.Api;
			Assert.That(device.Lamp(LightComponent.LampIdDefault), Is.SameAs(fixture.Api));
			Assert.That(device.Lamp(null), Is.SameAs(fixture.Api));
			Assert.That(device.Lamp("bulb_2"), Is.Not.SameAs(fixture.Api));
			Assert.That(device.Lamp("bulb_2"), Is.SameAs(device.Lamp("bulb_2")), "one API per bulb");
			Assert.That(fixture.Player.Lamp(fixture.Control, "bulb_2"), Is.SameAs(device.Lamp("bulb_2")));
			Assert.That(fixture.Player.Lamp(fixture.Control, LightComponent.LampIdDefault), Is.SameAs(fixture.Api));
		}

		[Test]
		public void ShouldConfigureLampCountAndKeepEachPositionWhenDisabled()
		{
			using var fixture = new MappedInsert();
			var material = fixture.Control.GetComponent<MeshRenderer>().sharedMaterial;
			material.SetVector("_OpticalLamp", new Vector4(0f, -0.02f, 0f, 0.007f));

			Assert.That(fixture.Control.SetBulbCount(3), Is.True);
			Assert.That(fixture.Control.BulbCount, Is.EqualTo(3));
			Assert.That(fixture.Control.AvailableLamps.Select(l => l.Id), Is.EqualTo(new[] { LightComponent.LampIdDefault, "bulb_2", "bulb_3" }));
			Assert.That(material.GetVector("_OpticalLampB").w, Is.EqualTo(0.007f).Within(1e-6f));
			Assert.That(material.GetVector("_OpticalLampC").z, Is.EqualTo(0.035f).Within(1e-6f), "new bulbs start beyond the preceding one");

			var moved = new Vector3(12f, -15f, -6f);
			Assert.That(fixture.Control.SetBulbPosition(1, moved), Is.True);
			Assert.That(Vector3.Distance(fixture.Control.BulbPosition(1), moved), Is.LessThan(1e-3f));
			Assert.That(material.GetVector("_OpticalLampB").w, Is.EqualTo(0.007f).Within(1e-6f), "moving a bulb keeps its radius");

			fixture.Control.SetBulbCount(1);
			Assert.That(fixture.Control.BulbCount, Is.EqualTo(1));
			Assert.That(material.GetVector("_OpticalLampB").w, Is.Zero);
			fixture.Control.SetBulbCount(2);
			Assert.That(Vector3.Distance(fixture.Control.BulbPosition(1), moved), Is.LessThan(1e-3f));
			Assert.That(material.GetVector("_OpticalLampB").w, Is.EqualTo(0.007f).Within(1e-6f));
		}

		[Test]
		public void ShouldDriveEachBulbFromItsOwnMapping()
		{
			using var fixture = new MappedInsert();
			fixture.Bulbs(3);
			fixture.Map("42", LampType.SingleFading, steps: 100);
			fixture.Map("43", LampType.SingleFading, steps: 100, item: "bulb_2");
			fixture.Map("44", LampType.SingleOnOff, item: "bulb_3");
			fixture.Start();
			fixture.AssertLevel(0f);
			Assert.That(fixture.BulbDrive(1).w, Is.Zero, "bulbs start off");
			Assert.That(fixture.BulbDrive(2).w, Is.Zero);

			// The lamp state keeps the level as a byte, so a half is 127 of 255.
			fixture.Lamps.HandleLampEvent("43", 50f);
			fixture.Control.Apply();
			Assert.That(fixture.Control.Intensity, Is.Zero, "the first bulb is not the second");
			Assert.That(fixture.Control.ExtraBulbs[0].Intensity, Is.EqualTo(0.5f).Within(0.005f));
			Assert.That(fixture.BulbDrive(1).w, Is.EqualTo(0.5f).Within(0.005f));
			Assert.That(fixture.BulbDrive(2).w, Is.Zero);

			fixture.Lamps.HandleLampEvent("44", 1f);
			fixture.Lamps.HandleLampEvent("42", 100f);
			fixture.Control.Apply();
			fixture.AssertLevel(1f);
			Assert.That(fixture.BulbDrive(1).w, Is.EqualTo(0.5f).Within(0.005f));
			Assert.That(fixture.BulbDrive(2).w, Is.EqualTo(1f));

			fixture.Lamps.HandleLampEvent("44", 0f);
			fixture.Control.Apply();
			Assert.That(fixture.BulbDrive(2).w, Is.Zero);
			Assert.That(fixture.BulbDrive(1).w, Is.EqualTo(0.5f).Within(0.005f), "the other bulbs keep theirs");
			fixture.AssertLevel(1f);
		}

		[Test]
		public void ShouldComposeRgbChannelsPerBulb()
		{
			using var fixture = new MappedInsert();
			fixture.Bulbs(2);
			fixture.Map("60", LampType.RgbMulti, ColorChannel.Red);
			fixture.Map("61", LampType.RgbMulti, ColorChannel.Green);
			fixture.Map("62", LampType.RgbMulti, ColorChannel.Blue);
			fixture.Map("70", LampType.RgbMulti, ColorChannel.Red, item: "bulb_2");
			fixture.Map("71", LampType.RgbMulti, ColorChannel.Green, item: "bulb_2");
			fixture.Map("72", LampType.RgbMulti, ColorChannel.Blue, item: "bulb_2");
			fixture.Start();
			fixture.Lamps.HandleLampEvent("60", 255f);
			fixture.Lamps.HandleLampEvent("72", 255f);
			fixture.Control.Apply();
			Assert.That(fixture.Control.Color.r, Is.EqualTo(1f).Within(0.001f));
			Assert.That(fixture.Control.Color.b, Is.Zero, "the second bulb's blue is not the first's");
			Assert.That(fixture.Control.ExtraBulbs[0].Color.r, Is.Zero);
			Assert.That(fixture.Control.ExtraBulbs[0].Color.b, Is.EqualTo(1f).Within(0.001f));
			Assert.That(fixture.BulbDrive(1).z, Is.EqualTo(1f).Within(0.001f));
			Assert.That(fixture.BulbDrive(1).w, Is.EqualTo(1f), "RGB channels without an intensity channel drive the bulb fully");
			fixture.Lamps.HandleLampEvent("71", 128f);
			Assert.That(fixture.Control.ExtraBulbs[0].Color.g, Is.EqualTo(128f / 255f).Within(0.001f));
			Assert.That(fixture.Control.ExtraBulbs[0].Color.b, Is.EqualTo(1f).Within(0.001f), "composed from every channel of that bulb");
			Assert.That(fixture.Control.Color.g, Is.Zero);
		}

		[Test]
		public void ShouldLeaveTheFirstBulbToAParentLightWhenOnlyAnotherIsMapped()
		{
			using var fixture = new MappedInsert();
			fixture.Bulbs(2);
			fixture.Map("43", LampType.SingleOnOff, item: "bulb_2");
			fixture.Start();
			fixture.Lamps.HandleLampEvent("43", 1f);
			Assert.That(fixture.Control.IsControlledByApi, Is.False, "the second bulb's event does not claim the first");
			Assert.That(fixture.Control.AreExtraBulbsControlledByApi, Is.True);
			fixture.Api.OnDestroy();
			Assert.That(fixture.Control.AreExtraBulbsControlledByApi, Is.False);
		}

		[Test]
		public void ShouldPreviewEachBulbFromLampManager()
		{
			using var fixture = new MappedInsert();
			fixture.Bulbs(2);
			fixture.Control.SetBulbEnabled("bulb_2", true);
			Assert.That(fixture.Control.ExtraBulbs[0].Intensity, Is.EqualTo(1f));
			fixture.Control.SetBulbEnabled("bulb_2", false);
			Assert.That(fixture.Control.ExtraBulbs[0].Intensity, Is.Zero);
			Assert.That(fixture.Control.Intensity, Is.EqualTo(1f), "the first bulb is untouched");
			fixture.Control.SetBulbEnabled(LightComponent.LampIdDefault, false);
			Assert.That(fixture.Control.Intensity, Is.Zero);
		}

		[Test]
		public void ShouldComposeRgbFromTheBulbsOwnChannelsOnly()
		{
			using var fixture = new MappedInsert();
			fixture.Bulbs(2);
			// One ID is the first bulb's intensity and the second bulb's red: each
			// bulb reads only what the ID means to it. And the first bulb's channels
			// are named two ways, both meaning the first bulb.
			fixture.Map("60", LampType.RgbMulti, ColorChannel.Red, item: "");
			fixture.Map("61", LampType.RgbMulti, ColorChannel.Green);
			fixture.Map("63", LampType.RgbMulti, ColorChannel.Alpha);
			fixture.Map("63", LampType.RgbMulti, ColorChannel.Red, item: "bulb_2");
			fixture.Map("71", LampType.RgbMulti, ColorChannel.Green, item: "bulb_2");
			fixture.Start();
			fixture.Lamps.HandleLampEvent("60", 255f);
			fixture.Lamps.HandleLampEvent("61", 255f);
			Assert.That(fixture.Control.Color.r, Is.EqualTo(1f).Within(0.001f), "both names of the first bulb compose together");
			Assert.That(fixture.Control.Color.g, Is.EqualTo(1f).Within(0.001f));
			fixture.Lamps.HandleLampEvent("63", 128f);
			fixture.AssertLevel(128f / 255f);
			Assert.That(fixture.Control.ExtraBulbs[0].Color.r, Is.EqualTo(128f / 255f).Within(0.001f));
			Assert.That(fixture.Control.ExtraBulbs[0].Intensity, Is.EqualTo(1f), "the second bulb has no intensity channel: full");
			fixture.Lamps.HandleLampEvent("71", 255f);
			Assert.That(fixture.Control.ExtraBulbs[0].Intensity, Is.EqualTo(1f));
			Assert.That(fixture.Control.ExtraBulbs[0].Color.r, Is.EqualTo(128f / 255f).Within(0.001f));
			Assert.That(fixture.Control.ExtraBulbs[0].Color.g, Is.EqualTo(1f).Within(0.001f));
			fixture.AssertLevel(128f / 255f);
		}

		[Test]
		public void ShouldDriveEveryBulbMappedToTheSameLamp()
		{
			using var fixture = new MappedInsert();
			fixture.Bulbs(2);
			fixture.Map("42", LampType.SingleOnOff);
			fixture.Map("42", LampType.SingleOnOff, item: "bulb_2");
			fixture.Start();
			fixture.Lamps.HandleLampEvent("42", 1f);
			fixture.Control.Apply();
			fixture.AssertLevel(1f);
			Assert.That(fixture.BulbDrive(1).w, Is.EqualTo(1f));
			fixture.Lamps.HandleLampEvent("42", 0f);
			fixture.Control.Apply();
			fixture.AssertLevel(0f);
			Assert.That(fixture.BulbDrive(1).w, Is.Zero);
		}

		[Test]
		public void ShouldDriveOnlyTheBodyOfAnInsertWithACustomCover()
		{
			var shader = Shader.Find("HDRP/Lit");
			if (shader == null) { Assert.Ignore("Requires HDRP to verify ordinary material emission."); }
			var root = new GameObject("Covered ordinary lamp");
			var body = new Material(shader) { renderQueue = 3000 };
			var cover = new Material(shader);
			body.SetColor("_EmissiveColor", Color.white * 20f);
			body.SetColor("_EmissiveColorLDR", Color.white);
			cover.SetColor("_EmissiveColor", Color.black);
			Mesh mesh = null;
			try {
				var driver = root.AddComponent<LightComponent>();
				var lens = new GameObject("Lens");
				lens.transform.SetParent(root.transform);
				var insert = lens.AddComponent<InsertComponent>();
				var renderer = lens.GetComponent<MeshRenderer>();
				renderer.sharedMaterial = body;
				insert.SetCoverMaterial(cover);
				mesh = lens.GetComponent<MeshFilter>().sharedMesh;
				driver.Enabled = true;
				driver.Color = Color.red;
				var block = new MaterialPropertyBlock();
				renderer.GetPropertyBlock(block, 0);
				Assert.That(block.GetColor("_EmissiveColor").r, Is.GreaterThan(0f));
				renderer.GetPropertyBlock(block);
				Assert.That(block.HasColor("_EmissiveColor"), Is.False, "renderer-wide emission would reach the cover");
				renderer.GetPropertyBlock(block, 1);
				Assert.That(block.HasColor("_EmissiveColor"), Is.False);
				Assert.That(cover.GetColor("_EmissiveColor"), Is.EqualTo(Color.black));
			} finally {
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(mesh);
				Object.DestroyImmediate(body);
				Object.DestroyImmediate(cover);
			}
		}

		[TestCase(1f)]
		[TestCase(100f)]
		public void ShouldScaleAndColourTheLensAndSpillTogether(float luminosity)
		{
			var shader = Shader.Find("Visual Pinball/Insert Hybrid");
			if (shader == null) {
				Assert.Ignore("Requires the HDRP insert shader.");
			}
			var material = new Material(shader);
			material.SetFloat("_InsertLuminosity", luminosity);
			var root = new GameObject("Insert spill test");
			Mesh mesh = null;
			try {
				var insert = root.AddComponent<InsertComponent>();
				insert.RebuildMesh();
				mesh = root.GetComponent<MeshFilter>().sharedMesh;
				var renderer = root.GetComponent<MeshRenderer>();
				renderer.sharedMaterial = material;
				var control = root.AddComponent<InsertLightComponent>();
				var spillObject = new GameObject("Spill");
				spillObject.transform.SetParent(root.transform);
				var spill = spillObject.AddComponent<Light>();
				RenderPipeline.Current.LightConverter.UpdateLight(spill, new VisualPinball.Engine.VPT.Light.LightData(), true);
				control.SpillLight = spill;
				control.SpillLumens = 4;
				control.SpillRange = 0.12f;
				control.Intensity = 0.25f;
				control.Color = new Color(0.3f, 0.8f, 1f);
				control.Apply();
				var block = new MaterialPropertyBlock();
				renderer.GetPropertyBlock(block, 0);
				Assert.That(block.GetFloat("_InsertLampLevel"), Is.EqualTo(0.25f));
				Assert.That(block.GetVector("_InsertLampColor"), Is.EqualTo((Vector4)control.Color.linear));
				Assert.That(spill.intensity, Is.EqualTo(luminosity).Within(1e-4f));
				Assert.That(spill.range, Is.EqualTo(0.12f).Within(1e-5f));
				Assert.That(spill.color.r, Is.EqualTo(control.Color.r).Within(1e-4f));
				Assert.That(spill.enabled, Is.True);
				Assert.That(block.HasFloat("_InsertLuminosity"), Is.False, "Material brightness must remain editable in the inspector.");
				material.SetFloat("_InsertLuminosity", luminosity * 2f);
				control.Apply();
				Assert.That(spill.intensity, Is.EqualTo(2f * luminosity).Within(1e-4f), "Material edits must immediately scale the spill too.");
				control.Color = Color.green;
				control.Intensity = 0;
				control.Apply();
				renderer.GetPropertyBlock(block, 0);
				Assert.That(block.GetFloat("_InsertLampLevel"), Is.Zero);
				Assert.That(spill.enabled, Is.False);
				Assert.That(spill.intensity, Is.Zero);
				Assert.That(renderer.enabled, Is.True);
				control.Intensity = 1;
				material.SetFloat("_InsertLuminosity", 0);
				control.Apply();
				Assert.That(spill.enabled, Is.False);
				Assert.That(spill.intensity, Is.Zero);
				material.SetFloat("_InsertLuminosity", luminosity);
				control.Apply();
				Assert.That(spill.enabled, Is.True);
				renderer.sharedMaterial = null;
				control.Apply();
				Assert.That(spill.enabled, Is.False, "Removing the material, including Clear Bake to a null original, must stop the spill.");
			} finally {
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(material);
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void ShouldKeepPreviewAndRomLampEventsAfterTheInsertUpdates()
		{
			var shader = Shader.Find("Visual Pinball/Insert Hybrid");
			if (shader == null) {
				Assert.Ignore("Requires the HDRP insert shader.");
			}
			var material = new Material(shader);
			var root = new GameObject("Managed insert preview");
			Mesh mesh = null;
			try {
				var driver = root.AddComponent<LightComponent>();
				var lens = new GameObject("Lens");
				lens.transform.SetParent(root.transform);
				var insert = lens.AddComponent<InsertComponent>();
				insert.RebuildMesh();
				mesh = lens.GetComponent<MeshFilter>().sharedMesh;
				var renderer = lens.GetComponent<MeshRenderer>();
				renderer.sharedMaterial = material;
				var control = lens.AddComponent<InsertLightComponent>();
				var block = new MaterialPropertyBlock();
				driver.Enabled = false;
				driver.Color = Color.red;
				control.Apply();
				renderer.GetPropertyBlock(block, 0);
				Assert.That(block.GetFloat("_InsertLampLevel"), Is.Zero);
				Assert.That(control.Intensity, Is.Zero);
				Assert.That(control.Color, Is.EqualTo(Color.red));
				Assert.That(block.GetVector("_InsertLampColor"), Is.EqualTo((Vector4)Color.red.linear));
				driver.Enabled = true;
				control.Apply();
				renderer.GetPropertyBlock(block, 0);
				Assert.That(block.GetFloat("_InsertLampLevel"), Is.EqualTo(1));
				Assert.That(block.GetVector("_InsertLampColor"), Is.EqualTo((Vector4)Color.red.linear));

				// Exercise the same normalized intensity and RGB entry points as ROM events.
				typeof(LightComponent).GetField("_hasLights", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(driver, true);
				driver.FadeSpeedUp = driver.FadeSpeedDown = 0;
				var api = driver.GetApi(null);
				((IApi)api).OnInit(null);
				api.OnLamp(VisualPinball.Engine.Game.Engines.LampStatus.On);
				api.OnLamp(0.35f);
				api.OnLamp(Color.blue);
				control.Apply();
				renderer.GetPropertyBlock(block, 0);
				Assert.That(control.Intensity, Is.EqualTo(0.35f));
				Assert.That(control.Color, Is.EqualTo(Color.blue));
				Assert.That(block.GetFloat("_InsertLampLevel"), Is.EqualTo(0.35f));
				Assert.That(block.GetVector("_InsertLampColor"), Is.EqualTo((Vector4)Color.blue.linear));
				api.OnLamp(VisualPinball.Engine.Game.Engines.LampStatus.Off);
				api.OnLamp(Color.green);
				control.Apply();
				renderer.GetPropertyBlock(block, 0);
				Assert.That(control.Intensity, Is.Zero);
				Assert.That(block.GetFloat("_InsertLampLevel"), Is.Zero, "RGB events must leave an off ROM lamp dark.");
				Assert.That(control.Color, Is.EqualTo(Color.green));
			} finally {
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(mesh);
				Object.DestroyImmediate(material);
			}
		}

		[Test]
		public void ShouldKeepRgbAndLevelIndependentWithoutHidingTheInsert()
		{
			var shader = Shader.Find("Visual Pinball/Insert Hybrid");
			if (shader == null) {
				Assert.Ignore("Requires the HDRP insert shader.");
			}
			var material = new Material(shader);
			var root = new GameObject("RGB insert test");
			try {
				var light = root.AddComponent<LightComponent>();
				var lens = new GameObject("Lens");
				lens.transform.SetParent(root.transform);
				var renderer = lens.AddComponent<MeshRenderer>();
				renderer.sharedMaterial = material;
				var block = new MaterialPropertyBlock();
				var initialEmission = material.GetColor("_EmissiveColor");
				light.Enabled = false; // Collect the same renderer/material cache as Awake.
				light.Color = Color.red;
				renderer.GetPropertyBlock(block);
				Assert.That(block.GetFloat("_InsertLampLevel"), Is.Zero, "An RGB event must not light an off lamp.");
				Assert.That(block.GetVector("_InsertLampColor"), Is.EqualTo((Vector4)Color.red.linear));
				Assert.That(renderer.enabled, Is.True, "The plastic must still reflect the table when off.");
				Assert.That(renderer.forceRenderingOff, Is.False);
				light.Enabled = true;
				// Runtime Awake sets this after collecting the materials. Avoid needing
				// a whole Player and game logic engine for an emission regression test.
				typeof(LightComponent).GetField("_hasLights", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(light, true);
				light.FadeSpeedUp = light.FadeSpeedDown = 0;
				light.Color = new Color(0.25f, 0.5f, 1f);
				light.FadeTo(0.25f);
				renderer.GetPropertyBlock(block);
				Assert.That(block.GetFloat("_InsertLampLevel"), Is.EqualTo(0.25f));
				Assert.That(block.GetVector("_InsertLampColor"), Is.EqualTo((Vector4)light.Color.linear), "Fading must keep the latest RGB colour.");
				light.Color = Color.green;
				renderer.GetPropertyBlock(block);
				Assert.That(block.GetFloat("_InsertLampLevel"), Is.EqualTo(0.25f), "An RGB event must keep the current fade level.");
				Assert.That(material.GetColor("_EmissiveColor"), Is.EqualTo(initialEmission), "Lamp events must not mutate shared materials.");
				for (var i = 0; i < 10; i++) {
					light.Enabled = i % 2 == 0;
				}
				var materials = (System.Collections.ICollection)typeof(LightComponent).GetField("_materials", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(light);
				Assert.That(materials.Count, Is.EqualTo(1), "A surface without faux bulbs must not be collected again on every editor preview.");
			} finally {
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(material);
			}
		}
	}
}
