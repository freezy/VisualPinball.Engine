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
using System.IO;
using System.Linq;
using System.Text;

using NUnit.Framework;
using OpenMcdf;

using VisualPinball.Engine.IO.FuturePinball;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Light;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Engine.Test.IO.FuturePinball
{
	[TestFixture]
	public class FuturePinballNativeItemTests
	{
		private const uint NameTag = 0xA4F4D1D7;
		private const uint PositionTag = 0x9BFCCFCF;
		private const uint SurfaceTag = 0xA3EFBDD2;
		private const uint GlowCenterTag = 0x9BFCCFDD;

		[Test]
		public void MapsKnownValuesAndRetainsDefaultsForIncompatibleScales()
		{
			WithTable(table =>
			{
				var element = table.Elements.Single(item => item.ElementType == FuturePinballElementType.Flipper);

				Assert.That(FuturePinballNativeItemConverter.TryConvert(element, out var converted), Is.True);
				var data = ((Flipper)converted.Item).Data;
				Assert.That(data.Center.X, Is.EqualTo(FuturePinballCoordinateConverter.ToVpx(100f)));
				Assert.That(data.Center.Y, Is.EqualTo(FuturePinballCoordinateConverter.ToVpx(200f)));
				Assert.That(data.Surface, Is.EqualTo("Upper PF"));
				Assert.That(data.StartAngle, Is.EqualTo(122f));
				Assert.That(data.EndAngle, Is.EqualTo(70f));
				Assert.That(data.Strength, Is.EqualTo(2200f), "FP's discrete strength scale must not overwrite VPE's physical units.");
				Assert.That(data.Elasticity, Is.EqualTo(0.8f), "FP's material enum must not be treated as a coefficient.");
				Assert.That(converted.DefaultedParameters, Does.Contain("strength scale"));
			});
		}

		[Test]
		public void MapsAutoPlungerOptoRubberGateAndLightSemantics()
		{
			WithTable(table =>
			{
				var autoPlunger = Convert<Plunger>(table, FuturePinballElementType.AutoPlunger).Data;
				Assert.That(autoPlunger.AutoPlunger, Is.True);
				Assert.That(autoPlunger.IsMechPlunger, Is.False);

				var opto = Convert<Trigger>(table, FuturePinballElementType.OptoTrigger).Data;
				Assert.That(opto.Shape, Is.EqualTo(TriggerShape.TriggerNone));
				Assert.That(opto.IsVisible, Is.False);
				Assert.That(opto.Radius, Is.EqualTo(FuturePinballCoordinateConverter.ToVpx(30f)));

				var rubber = Convert<Rubber>(table, FuturePinballElementType.ShapeableRubber).Data;
				Assert.That(rubber.DragPoints, Has.Length.EqualTo(3));
				Assert.That(rubber.Height, Is.EqualTo(FuturePinballCoordinateConverter.ToVpx(14f)));

				var gate = Convert<Gate>(table, FuturePinballElementType.Gate).Data;
				Assert.That(gate.Rotation, Is.EqualTo(270f));
				Assert.That(gate.TwoWay, Is.False);

				var light = Convert<Light>(table, FuturePinballElementType.RoundLight).Data;
				Assert.That(light.State, Is.EqualTo(LightStatus.LightStateBlinking));
				Assert.That(light.BlinkInterval, Is.EqualTo(175));
				Assert.That(light.BlinkPattern, Is.EqualTo("1010"));
				Assert.That(light.MeshRadius, Is.EqualTo(FuturePinballCoordinateConverter.ToVpx(10f)));
				Assert.That(light.DragPoints, Has.Length.EqualTo(8));
				Assert.That(light.Color.Red, Is.EqualTo(0x33));
				Assert.That(light.Color.Green, Is.EqualTo(0x22));
				Assert.That(light.Color.Blue, Is.EqualTo(0x11));
			});
		}

		[Test]
		public void DoesNotClaimUnsupportedFunctionalToys()
		{
			WithTable(table =>
			{
				var diverter = table.Elements.Single(item => item.ElementType == FuturePinballElementType.Diverter);
				Assert.That(FuturePinballNativeItemConverter.TryConvert(diverter, out _), Is.False);
			});
		}

		[Test]
		public void MapsSurfaceEventsAndRetainsPassiveMechanismSemantics()
		{
			WithTable(table =>
			{
				var surface = Convert<Surface>(table, FuturePinballElementType.Surface).Data;
				Assert.That(surface.HitEvent, Is.True);

				var bumper = Convert<Bumper>(table, FuturePinballElementType.Bumper).Data;
				Assert.That(bumper.Force, Is.Zero, "A bumper without an FP trigger skirt must not kick the ball.");

				var trigger = Convert<Trigger>(table, FuturePinballElementType.Trigger).Data;
				Assert.That(trigger.IsVisible, Is.False);
			});
		}

		[Test]
		public void MapsShapeableLightGeometryAndDefaultsUnknownStateToOff()
		{
			WithTable(table =>
			{
				var light = Convert<Light>(table, FuturePinballElementType.ShapeableLight).Data;
				Assert.That(light.Center.X, Is.EqualTo(FuturePinballCoordinateConverter.ToVpx(210f)));
				Assert.That(light.Center.Y, Is.EqualTo(FuturePinballCoordinateConverter.ToVpx(310f)));
				Assert.That(light.DragPoints, Has.Length.EqualTo(3));
				Assert.That(light.DragPoints[0].Center.X, Is.EqualTo(FuturePinballCoordinateConverter.ToVpx(180f)));
				Assert.That(light.DragPoints[0].Center.Y, Is.EqualTo(FuturePinballCoordinateConverter.ToVpx(280f)));
				Assert.That(light.State, Is.EqualTo(LightStatus.LightStateOff));
				Assert.That(light.OffImage, Is.EqualTo(FuturePinballNativeItemConverter.PlayfieldImage));

				var tableContainer = new FileTableContainer();
				tableContainer.Table.Data.Image = FuturePinballNativeItemConverter.PlayfieldImage;
				Assert.That(new Light(light).IsInsertLight(tableContainer.Table), Is.True,
					"Imported FP playfield lights must select VPE's insert-light prefab.");
			});
		}

		[Test]
		public void UsesShapePointForSurfacePlacementAndDefaultsMissingKickerType()
		{
			WithTable(table =>
			{
				var rubber = table.Elements.Single(item => item.ElementType == FuturePinballElementType.ShapeableRubber);
				var probe = FuturePinballElementGeometry.SurfaceProbePosition(rubber);
				Assert.That(probe.X, Is.EqualTo(125f));
				Assert.That(probe.Y, Is.EqualTo(225f));

				var defaultKicker = table.Elements.Single(item => item.Name == "Table Element 11");
				Assert.That(((Kicker)ConvertItem(defaultKicker).Item).Data.FallThrough, Is.False,
					"A missing FP kicker type must retain the VPE default rather than becoming a gobble hole.");

				var gobbleHole = table.Elements.Single(item => item.Name == "Table Element 12");
				Assert.That(((Kicker)ConvertItem(gobbleHole).Item).Data.FallThrough, Is.True);
			});
		}

		private static T Convert<T>(FuturePinballTable table, FuturePinballElementType type) where T : class, IItem
		{
			var element = table.Elements.Single(item => item.ElementType == type);
			Assert.That(FuturePinballNativeItemConverter.TryConvert(element, out var converted), Is.True);
			return converted.Item as T;
		}

		private static FuturePinballNativeItem ConvertItem(FuturePinballSourceStream element)
		{
			Assert.That(FuturePinballNativeItemConverter.TryConvert(element, out var converted), Is.True);
			return converted;
		}

		private static void WithTable(Action<FuturePinballTable> assertion)
		{
			var path = Path.Combine(Path.GetTempPath(), $"vpe-fp-native-{Guid.NewGuid():N}.fpt");
			try {
				CreateTable(path);
				assertion(FuturePinballTableReader.Load(path));
			} finally {
				if (File.Exists(path)) File.Delete(path);
			}
		}

		private static void CreateTable(string path)
		{
			using (var table = RootStorage.Create(path, OpenMcdf.Version.V3, StorageModeFlags.None)) {
				var storage = table.CreateStorage("Future Pinball");
				void WriteTableElement(int index, FuturePinballElementType type, params byte[][] records)
				{
					Write(storage.CreateStream($"Table Element {index}"), Join(new[] { UInt32((uint)type) }.Concat(records).Concat(new[] { End() }).ToArray()));
				}
				Write(storage.CreateStream("File Version"), UInt32(1));
				Write(storage.CreateStream("Table Data"), Join(
					IntegerRecord(0x95FDCDD2, 13), IntegerRecord(0xA2F4C9D2, 0),
					IntegerRecord(0xA5F3BFD2, 0), IntegerRecord(0x96ECC5D2, 0),
					IntegerRecord(0xA5F2C5D2, 0), IntegerRecord(0x95F5C9D2, 0),
					IntegerRecord(0x95F5C6D2, 0), IntegerRecord(0x9BFBCED2, 0), End()
				));
				Write(storage.CreateStream("Table MAC"), new byte[16]);
				WriteTableElement(1, FuturePinballElementType.Flipper,
					Common("Left Flipper", 100f, 200f, "Upper PF"),
					IntegerRecord(0xA900BED2, 122), IntegerRecord(0xA2EABFE4, -52),
					IntegerRecord(0xA1FABED2, 6), IntegerRecord(0x9700C6E0, 0));
				WriteTableElement(2, FuturePinballElementType.AutoPlunger,
					Common("Auto Plunger", 480f, 950f, "Playfield"));
				WriteTableElement(3, FuturePinballElementType.OptoTrigger,
					Common("Opto", 300f, 400f, "Playfield"), IntegerRecord(0x95FDC9CE, 60));
				WriteTableElement(4, FuturePinballElementType.ShapeableRubber,
					WideStringRecord(NameTag, "Rubber"), StringRecord(SurfaceTag, "Playfield"),
					IntegerRecord(0x96FBCCD6, 14), Point(125f, 225f), Point(175f, 225f), Point(150f, 265f));
				WriteTableElement(5, FuturePinballElementType.Gate,
					Common("Gate", 200f, 300f, "Playfield"), IntegerRecord(0xA8EDC3D3, 270), IntegerRecord(0x9100BBD6, 1));
				WriteTableElement(6, FuturePinballElementType.RoundLight,
					Common("Lamp", 220f, 320f, "Playfield"), IntegerRecord(0x9D00C9E1, 20),
					IntegerRecord(0x9600BED2, 2), IntegerRecord(0x95F3C9E3, 175),
					StringRecord(0x9600C2E3, "1010"), IntegerRecord(0x9DF2CFD9, 0x00112233));
				WriteTableElement(7, FuturePinballElementType.Diverter,
					Common("Diverter", 250f, 350f, "Playfield"), IntegerRecord(0xA900BED2, 25), IntegerRecord(0xA2EABFE4, 83));
				WriteTableElement(8, FuturePinballElementType.Surface,
					WideStringRecord(NameTag, "Surface"), FloatRecord(0x99F2BEDD, 25f), FloatRecord(0x95F2D0DD, 5f),
					IntegerRecord(0x95EBCDDD, 1), Point(0f, 0f), Point(100f, 0f), Point(100f, 100f));
				WriteTableElement(9, FuturePinballElementType.Bumper,
					Common("No Skirt Bumper", 280f, 380f, "Playfield"), IntegerRecord(0xA0EED1D5, 0), IntegerRecord(0x9EEED1DD, 0));
				WriteTableElement(10, FuturePinballElementType.Trigger,
					Common("Invisible Trigger", 320f, 420f, "Playfield"), IntegerRecord(0xA5F2C5D3, 0));
				WriteTableElement(11, FuturePinballElementType.Kicker,
					Common("Default Kicker", 340f, 440f, "Playfield"));
				WriteTableElement(12, FuturePinballElementType.Kicker,
					Common("Gobble Hole", 360f, 460f, "Playfield"), IntegerRecord(0x99E8BEDA, 1));
				WriteTableElement(13, FuturePinballElementType.ShapeableLight,
					WideStringRecord(NameTag, "Shaped Lamp"), StringRecord(SurfaceTag, "Playfield"),
					VectorRecord(GlowCenterTag, 210f, 310f), IntegerRecord(0x9600BED2, 99),
					Point(180f, 280f), Point(240f, 280f), Point(210f, 340f));
				table.Flush(true);
			}
		}

		private static byte[] Common(string name, float x, float y, string surface)
		{
			return Join(WideStringRecord(NameTag, name), VectorRecord(PositionTag, x, y), StringRecord(SurfaceTag, surface));
		}

		private static byte[] Point(float x, float y)
		{
			return Join(Record(FuturePinballElementGeometry.PointTag, Array.Empty<byte>()),
				VectorRecord(PositionTag, x, y), IntegerRecord(FuturePinballElementGeometry.SmoothTag, 1), End());
		}

		private static byte[] End() => Record(0xA7FDC4E0, Array.Empty<byte>());
		private static byte[] VectorRecord(uint tag, float x, float y) => Record(tag, Join(Single(x), Single(y)));
		private static byte[] IntegerRecord(uint tag, int value) => Record(tag, UInt32(unchecked((uint)value)));
		private static byte[] FloatRecord(uint tag, float value) => Record(tag, Single(value));
		private static byte[] StringRecord(uint tag, string value) => Record(tag, StringBytes(value, Encoding.ASCII));
		private static byte[] WideStringRecord(uint tag, string value) => Record(tag, StringBytes(value, Encoding.Unicode));
		private static byte[] StringBytes(string value, Encoding encoding) => Join(UInt32((uint)encoding.GetByteCount(value)), encoding.GetBytes(value));
		private static byte[] Record(uint tag, byte[] payload) => Join(UInt32((uint)(payload.Length + 4)), UInt32(tag), payload);
		private static byte[] Single(float value) => BitConverter.GetBytes(value);
		private static byte[] UInt32(uint value) => new[] { (byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24) };
		private static byte[] Join(params byte[][] parts)
		{
			var result = new byte[parts.Sum(part => part.Length)];
			var offset = 0;
			foreach (var part in parts) {
				Buffer.BlockCopy(part, 0, result, offset, part.Length);
				offset += part.Length;
			}
			return result;
		}
		private static void Write(CfbStream stream, byte[] data)
		{
			stream.SetLength(data.Length);
			stream.Write(data, 0, data.Length);
			stream.Flush();
		}
	}
}
