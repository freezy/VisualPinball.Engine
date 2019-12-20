using System;
using System.IO;
using OpenMcdf;

namespace VisualPinball.Engine.VPT.Table
{
	/// <summary>
	/// The entry point for loading and parsing the VPX file.
	/// </summary>
	public static class TableLoader
	{
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

					LoadGameItems(table, gameStorage);

					// print some random data
					Console.WriteLine("left = {0}", table.Data.Left);
					Console.WriteLine("BgRotation = {0}", string.Join("/", table.Data.BgRotation));
					Console.WriteLine("name = {0}", table.Data.Name);

					return table;
				}

			} finally {
				cf.Close();
			}
		}

		private static void LoadGameItems(VisualPinball.Engine.VPT.Table.Table table, CFStorage storage)
		{
			for (var i = 0; i < table.Data.NumGameItems; i++) {
				var itemName = $"GameItem{i}";
				var itemStream = storage.GetStream(itemName);
				var itemData = itemStream.GetData();
				if (itemData.Length < 4) {
					Console.WriteLine($"Skipping {itemName} because it has size of {itemData.Length}.");
					continue;
				}

				var reader = new BinaryReader(new MemoryStream(itemData));
				var itemType = reader.ReadInt32();
				switch (itemType) {
					case ItemType.Primitive: {
						Console.WriteLine($"Loading primitive {itemName}");
						var item = new VisualPinball.Engine.VPT.Primitive.Primitive(reader, itemName);
						table.Primitives[item.Name] = item;
						break;
					}
				}
			}
		}
	}
}