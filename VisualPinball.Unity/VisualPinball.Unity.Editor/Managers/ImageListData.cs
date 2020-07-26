namespace VisualPinball.Unity.Editor.Managers
{
	public class ImageListData : IManagerListData
	{
		[ManagerListColumn(Order = 0, Width = 200)]
		public string Name => TextureData?.Name ?? "";
		[ManagerListColumn(Order = 1, HeaderName = "Image Size", Width = 100)]
		public string ImageSize => TextureData == null ? "" : $"{TextureData.Width}x{TextureData.Height}";
		[ManagerListColumn(Order = 2, HeaderName = "In Use", Width = 50)]
		public bool InUse;
		[ManagerListColumn(Order = 3, HeaderName = "Raw Size", Width = 100)]
		public int RawSize { get {
				if (TextureData == null) { return 0; }
				if (TextureData.HasBitmap) {
					return TextureData.Bitmap.Data.Length;
				}
				return TextureData.Binary.Bytes?.Length ?? 0;
			} }

		public Engine.VPT.TextureData TextureData;
	}
}
