namespace VisualPinball.Unity.Editor
{
	public class CollectionListData : IManagerListData
	{
		[ManagerListColumn(Order = 0, Width = 200)]
		public string Name => CollectionData?.Name ?? "";

		[ManagerListColumn(Order = 1, HeaderName = "Nb Items", Width = 100)]
		public int NbItems => CollectionData?.ItemNames?.Length ?? 0;

		[ManagerListColumn(Order = 2, HeaderName = "Fire Events", Width = 100)]
		public bool FireEvents => CollectionData?.FireEvents ?? false;

		[ManagerListColumn(Order = 3, HeaderName = "Group Elements", Width = 100)]
		public bool GroupElements => CollectionData?.GroupElements ?? false;

		[ManagerListColumn(Order = 4, HeaderName = "Stop Single Events", Width = 100)]
		public bool StopSingleEvents => CollectionData?.StopSingleEvents ?? false;

		public Engine.VPT.Collection.CollectionData CollectionData;
	}
}
