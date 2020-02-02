using OpenMcdf;

namespace VisualPinball.Engine.VPT.Table
{
	public static class TableWriter
	{
		public static void WriteTable(Table table, string fileName)
		{
			var cf = new CompoundFile();
			var gameStorage = cf.RootStorage.AddStorage("GameStg");

			table.Data.WriteData(gameStorage);

			cf.Save(fileName);
			cf.Close();
		}
	}
}
