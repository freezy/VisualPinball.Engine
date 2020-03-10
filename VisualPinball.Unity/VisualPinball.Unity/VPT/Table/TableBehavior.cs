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
using VisualPinball.Unity.VPT.Bumper;
using VisualPinball.Unity.VPT.Flipper;
using VisualPinball.Unity.VPT.Gate;
using VisualPinball.Unity.VPT.HitTarget;
using VisualPinball.Unity.VPT.Kicker;
using VisualPinball.Unity.VPT.Light;
using VisualPinball.Unity.VPT.Primitive;
using VisualPinball.Unity.VPT.Ramp;
using VisualPinball.Unity.VPT.Rubber;
using VisualPinball.Unity.VPT.Spinner;
using VisualPinball.Unity.VPT.Surface;
using VisualPinball.Unity.VPT.Trigger;
using Logger = NLog.Logger;
using Texture = VisualPinball.Engine.VPT.Texture;

namespace VisualPinball.Unity.VPT.Table
{
	[AddComponentMenu("Visual Pinball/Playfield")]
	public class TableBehavior : ItemBehavior<Engine.VPT.Table.Table, TableData>
	{
		public Engine.VPT.Table.Table Table => Item;

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

		protected override Engine.VPT.Table.Table GetItem()
		{
			return RecreateTable();
		}

		public Engine.VPT.Table.Table CreateTable()
		{
			Logger.Info("Restoring table...");
			// restore table data
			var table = new Engine.VPT.Table.Table(data);

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
			Restore(textBoxes, table.TextBoxes, d => new TextBox(d));
			Restore(timers, table.Timers, d => new Timer(d));

			// restore game items
			Logger.Info("Restoring game items...");
			Restore<BumperBehavior, Engine.VPT.Bumper.Bumper, BumperData>(table.Bumpers);
			Restore<FlipperBehavior, Engine.VPT.Flipper.Flipper, FlipperData>(table.Flippers);
			Restore<GateBehavior, Engine.VPT.Gate.Gate, GateData>(table.Gates);
			Restore<HitTargetBehavior, Engine.VPT.HitTarget.HitTarget, HitTargetData>(table.HitTargets);
			Restore<KickerBehavior, Engine.VPT.Kicker.Kicker, KickerData>(table.Kickers);
			Restore<LightBehavior, Engine.VPT.Light.Light, LightData>(table.Lights);
			Restore<PrimitiveBehavior, Engine.VPT.Primitive.Primitive, PrimitiveData>(table.Primitives);
			Restore<RampBehavior, Engine.VPT.Ramp.Ramp, RampData>(table.Ramps);
			Restore<RubberBehavior, Engine.VPT.Rubber.Rubber, RubberData>(table.Rubbers);
			Restore<SpinnerBehavior, Engine.VPT.Spinner.Spinner, SpinnerData>(table.Spinners);
			Restore<SurfaceBehavior, Engine.VPT.Surface.Surface, SurfaceData>(table.Surfaces);
			Restore<TriggerBehavior, Engine.VPT.Trigger.Trigger, TriggerData>(table.Triggers);

			return table;
		}

		public Engine.VPT.Table.Table RecreateTable()
		{
			var table = CreateTable();

			Restore(sounds, table.Sounds, d => new Sound(d));

			// restore textures
			Logger.Info("Restoring textures...");
			foreach (var textureData in textures) {
				var texture = new Texture(textureData);
				if (File.Exists(texture.GetUnityFilename(textureFolder))) {
					if (textureData.Binary != null && textureData.Binary.Size > 0) {
						textureData.Binary.Data = File.ReadAllBytes(texture.GetUnityFilename(textureFolder));
						textureData.Bitmap = null;

					} else if (textureData.Bitmap != null && textureData.Bitmap.Width > 0) {
						textureData.Bitmap.Data = File.ReadAllBytes(texture.GetUnityFilename(textureFolder));
						textureData.Binary = null;
					}

				} else {
					Logger.Warn($"Cannot find {texture.GetUnityFilename(textureFolder)}.");
				}

				table.Textures[texture.Name] = texture;
			}

			Logger.Info("Table restored.");
			return table;
		}

		private void Restore<TComp, TItem, TData>(IDictionary<string, TItem> dest) where TData : ItemData where TItem : Item<TData>, IRenderable where TComp : ItemBehavior<TItem, TData>
		{
			foreach (var component in GetComponentsInChildren<TComp>(true)) {
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
