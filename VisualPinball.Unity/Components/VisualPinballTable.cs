// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Collection;
using VisualPinball.Engine.VPT.Decal;
using VisualPinball.Engine.VPT.DispReel;
using VisualPinball.Engine.VPT.Flasher;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.HitTarget;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Light;
using VisualPinball.Engine.VPT.LightSeq;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Primitive;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Sound;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.TextBox;
using VisualPinball.Engine.VPT.Timer;
using VisualPinball.Engine.VPT.Trigger;
using VisualPinball.Unity.Common;
using VisualPinball.Unity.Extensions;
using Light = VisualPinball.Engine.VPT.Light.Light;
using Logger = NLog.Logger;
using Texture = VisualPinball.Engine.VPT.Texture;

namespace VisualPinball.Unity.Components
{
	public class VisualPinballTable : ItemComponent<Table, TableData>
	{
		public Table Table => Item;

		[HideInInspector] public Dictionary<string, string> tableInfo = new SerializableDictionary<string, string>();
		[HideInInspector] public TextureData[] textures;
		[HideInInspector] public CustomInfoTags customInfoTags;
		[HideInInspector] public CollectionData[] collections;
		[HideInInspector] public DecalData[] decals;
		[HideInInspector] public DispReelData[] dispReels;
		[HideInInspector] public FlasherData[] flashers;
		[HideInInspector] public LightSeqData[] lightSeqs;
		[HideInInspector] public PlungerData[] plungers;
		[HideInInspector] public SoundData[] sounds;
		[HideInInspector] public TextBoxData[] textBoxes;
		[HideInInspector] public TimerData[] timers;

		[HideInInspector] public string textureFolder;

		protected override string[] Children => null;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected override Table GetItem()
		{
			return RecreateTable();
		}

		public Table RecreateTable()
		{
			Logger.Info("Restoring table...");
			// restore table data
			var table = new Table(data);

			// restore table info
			Logger.Info("Restoring table info...");
			foreach (var k in tableInfo.Keys) {
				table.TableInfo[k] = tableInfo[k];
			}

			// restore custom info tags
			table.CustomInfoTags = customInfoTags;

			// restore game items with no game object (yet!)
			table.Decals.Clear();
			table.Decals.AddRange(decals.Select(d => new Decal(d)));
			Restore(collections, table.Collections, d => new Collection(d));
			Restore(dispReels, table.DispReels, d => new DispReel(d));
			Restore(flashers, table.Flashers, d => new Flasher(d));
			Restore(lightSeqs, table.LightSeqs, d => new LightSeq(d));
			Restore(plungers, table.Plungers, d => new Plunger(d));
			Restore(sounds, table.Sounds, d => new Sound(d));
			Restore(textBoxes, table.TextBoxes, d => new TextBox(d));
			Restore(timers, table.Timers, d => new Timer(d));

			// restore game items
			Logger.Info("Restoring game items...");
			Restore<VisualPinballBumper, Bumper, BumperData>(table.Bumpers);
			Restore<VisualPinballFlipper, Flipper, FlipperData>(table.Flippers);
			Restore<VisualPinballGate, Gate, GateData>(table.Gates);
			Restore<VisualPinballHitTarget, HitTarget, HitTargetData>(table.HitTargets);
			Restore<VisualPinballKicker, Kicker, KickerData>(table.Kickers);
			Restore<VisualPinballLight, Light, LightData>(table.Lights);
			Restore<VisualPinballPrimitive, Primitive, PrimitiveData>(table.Primitives);
			Restore<VisualPinballRamp, Ramp, RampData>(table.Ramps);
			Restore<VisualPinballRubber, Rubber, RubberData>(table.Rubbers);
			Restore<VisualPinballSpinner, Spinner, SpinnerData>(table.Spinners);
			Restore<VisualPinballSurface, Surface, SurfaceData>(table.Surfaces);
			Restore<VisualPinballTrigger, Trigger, TriggerData>(table.Triggers);

			// restore textures
			Logger.Info("Restoring textures...");
			foreach (var textureData in textures) {
				var texture = new Texture(textureData);
				if (textureData.Binary != null && textureData.Binary.Size > 0) {
					textureData.Binary.Data = File.ReadAllBytes(texture.GetUnityFilename(textureFolder));

				} else if (textureData.Bitmap != null && textureData.Bitmap.Width > 0) {
					textureData.Bitmap.Data = File.ReadAllBytes(texture.GetUnityFilename(textureFolder));
				}

				table.Textures[texture.Name] = texture;
			}

			Logger.Info("Table restored.");
			return table;
		}

		private void Restore<TComp, TItem, TData>(IDictionary<string, TItem> dest) where TData : ItemData where TItem : Item<TData>, IRenderable where TComp : ItemComponent<TItem, TData>
		{
			foreach (var component in GetComponentsInChildren<TComp>()) {
				dest[component.name] = component.Item;
			}
		}

		private static void Restore<TItem, TData>(IEnumerable<TData> src, IDictionary<string, TItem> dest, Func<TData, TItem> create) where TData : ItemData where TItem : Item<TData>
		{
			foreach (var d in src) {
				dest[d.GetName()] = create(d);
			}
		}
	}
}
