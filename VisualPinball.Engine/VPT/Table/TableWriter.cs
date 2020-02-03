using System;
using System.Linq;
using System.Text;
using OpenMcdf;

namespace VisualPinball.Engine.VPT.Table
{
	public static class TableWriter
	{
		public static int VpFileFormatVersion = 1060;

		public static void WriteTable(Table table, string fileName)
		{
			var cf = new CompoundFile();
			var gameStorage = cf.RootStorage.AddStorage("GameStg");
			var tableInfo = cf.RootStorage.AddStorage("TableInfo");

			// game items, images, etc
			foreach (var writeable in table.Writeables) {
				writeable.WriteData(gameStorage);
			}

			// table info
			foreach (var key in table.TableInfo.Keys) {
				var stream = tableInfo.AddStream(key);
				stream.SetData(Encoding.ASCII.GetBytes(table.TableInfo[key]).SelectMany(b => new byte[] {b, 0x0}).ToArray());
			}

			// version
			gameStorage.AddStream("Version").SetData(BitConverter.GetBytes(VpFileFormatVersion));

			// TODO custom info tags

			cf.Save(fileName);
			cf.Close();
		}
	}
}
