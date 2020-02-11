// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global

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
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.HitTarget;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Light;
using VisualPinball.Engine.VPT.Primitive;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
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

		[HideInInspector] public Dictionary<string, string> TableInfo = new SerializableDictionary<string, string>();
		[HideInInspector] public TextureData[] Textures;
		[HideInInspector] public CustomInfoTags CustomInfoTags;
		[HideInInspector] public CollectionData[] Collections;
		[HideInInspector] public DecalData[] Decals;
		[HideInInspector] public DispReelData[] DispReels;

		[HideInInspector] public string TextureFolder;

		protected override string[] Children => null;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected override Table GetItem()
		{
			return new Table(data);
		}

		public Table RecreateTable()
		{
			Logger.Info("Restoring table...");
			// restore table data
			var table = new Table(data);

			// restore table info
			Logger.Info("Restoring table info...");
			foreach (var k in TableInfo.Keys) {
				table.TableInfo[k] = TableInfo[k];
			}

			// restore custom info tags
			table.CustomInfoTags = CustomInfoTags;

			// restore game items with no game object
			Logger.Info("Restoring collections...");
			foreach (var d in Collections) {
				table.Collections[data.Name] = new Collection(d);
			}
			table.Decals.AddRange(Decals.Select(d => new Decal(d)));
			foreach (var d in DispReels) {
				table.DispReels[data.Name] = new DispReel(d);
			}

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
			foreach (var textureData in Textures) {
				var texture = new Texture(textureData);
				if (textureData.Binary != null) {
					textureData.Binary.Data = File.ReadAllBytes(texture.GetUnityFilename(TextureFolder));
				}
				if (textureData.Bitmap != null) {
					textureData.Bitmap.Data = File.ReadAllBytes(texture.GetUnityFilename(TextureFolder));
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

		protected override void OnDataSet()
		{
		}

		protected override void OnFieldsUpdated()
		{
		}
	}
}
