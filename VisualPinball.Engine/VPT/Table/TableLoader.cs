using System.IO;
using NLog;
using OpenMcdf;
using VisualPinball.Engine.IO;

namespace VisualPinball.Engine.VPT.Table
{
	/// <summary>
	/// The entry point for loading and parsing the VPX file.
	/// </summary>
	public static class TableLoader
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static Table Load(string filename)
		{
			var cf = new CompoundFile(filename);
			try {
				var gameStorage = cf.RootStorage.GetStorage("GameStg");
				var gameData = gameStorage.GetStream("GameData");
				var bytes = gameData.GetData();

				using (var stream = new MemoryStream(bytes))
				using (var reader = new BinaryReader(stream)) {
					var table = new Table(reader);

					LoadTableInfo(table, cf.RootStorage);
					LoadGameItems(table, gameStorage);
					LoadTextures(table, gameStorage);

					table.SetupPlayfieldMesh();
					return table;
				}

			} finally {
				cf.Close();
			}
		}

		private static void LoadGameItems(Table table, CFStorage storage)
		{
			for (var i = 0; i < table.Data.NumGameItems; i++) {
				var itemName = $"GameItem{i}";
				var itemStream = storage.GetStream(itemName);
				var itemData = itemStream.GetData();
				if (itemData.Length < 4) {
					Logger.Warn("Skipping {itemName} because it has size of {itemDataLength}.", itemName, itemData.Length);
					continue;
				}

				var reader = new BinaryReader(new MemoryStream(itemData));
				var itemType = reader.ReadInt32();
				switch (itemType) {
					case ItemType.Bumper: {
						Logger.Info("Loading bumper {itemName}", itemName);
						var item = new VisualPinball.Engine.VPT.Bumper.Bumper(reader, itemName);
						table.Bumpers[item.Name] = item;
						break;
					}
					case ItemType.Flipper: {
						Logger.Info("Loading flipper {itemName}", itemName);
						var item = new VisualPinball.Engine.VPT.Flipper.Flipper(reader, itemName);
						table.Flippers[item.Name] = item;
						break;
					}
					case ItemType.Gate: {
						Logger.Info("Loading gate {itemName}", itemName);
						var item = new VisualPinball.Engine.VPT.Gate.Gate(reader, itemName);
						table.Gates[item.Name] = item;
						break;
					}
					case ItemType.HitTarget: {
						Logger.Info("Loading hit target {itemName}", itemName);
						var item = new VisualPinball.Engine.VPT.HitTarget.HitTarget(reader, itemName);
						table.HitTargets[item.Name] = item;
						break;
					}
					case ItemType.Kicker: {
						Logger.Info("Loading kicker {itemName}", itemName);
						var item = new VisualPinball.Engine.VPT.Kicker.Kicker(reader, itemName);
						table.Kickers[item.Name] = item;
						break;
					}
					case ItemType.Light: {
						Logger.Info("Loading light {itemName}", itemName);
						var item = new VisualPinball.Engine.VPT.Light.Light(reader, itemName);
						table.Lights[item.Name] = item;
						break;
					}
					case ItemType.Primitive: {
						Logger.Info("Loading primitive {itemName}", itemName);
						var item = new Primitive.Primitive(reader, itemName);
						table.Primitives[item.Name] = item;
						break;
					}
					case ItemType.Ramp: {
						Logger.Info("Loading ramp {itemName}", itemName);
						var item = new Ramp.Ramp(reader, itemName);
						table.Ramps[item.Name] = item;
						break;
					}
					case ItemType.Rubber: {
						Logger.Info("Loading rubber {itemName}", itemName);
						var item = new Rubber.Rubber(reader, itemName);
						table.Rubbers[item.Name] = item;
						break;
					}
					case ItemType.Surface: {
						Logger.Info("Loading surface {itemName}", itemName);
						var item = new Surface.Surface(reader, itemName);
						table.Surfaces[item.Name] = item;
						break;
					}
				}
			}
		}

		private static void LoadTextures(Table table, CFStorage storage)
		{
			for (var i = 0; i < table.Data.NumTextures; i++) {
				var textureName = $"Image{i}";
				var textureStream = storage.GetStream(textureName);
				var textureData = textureStream.GetData();
				if (textureData.Length < 4) {
					Logger.Warn("Skipping {itemName} because it has size of {itemDataLength}.", textureName, textureData.Length);
					continue;
				}

				var reader = new BinaryReader(new MemoryStream(textureData));
				Logger.Info("Loading texture {itemName}", textureName);
				var texture = new Texture(reader, textureName);
				table.Textures[texture.Name.ToLower()] = texture;
			}
		}

		private static void LoadTableInfo(Table table, CFStorage storage)
		{
			var tableInfoStorage = storage.GetStorage("TableInfo");
			tableInfoStorage.VisitEntries(item => {
				if (item.IsStream) {
					var itemStream = item as CFStream;
					if (itemStream != null) {
						table.TableInfo[item.Name] = BiffUtil.ParseWideString(itemStream.GetData());
					}
				}
			}, false);

		}
	}
}
