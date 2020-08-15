namespace VisualPinball.Unity.Patcher
{
	/// <summary>
	/// Matches by Table Info (in Visual Pinball under the "Table" menu). <p/>
	///
	/// All of the provided fields must match. If none provided, it always matches.
	/// </summary>
	public class MetaMatchAttribute : TableMatchAttribute
	{
		public string TableName;
		public string AuthorName;

		public override bool Matches(Engine.VPT.Table.Table table, string fileName)
		{
			if (TableName != null && table.InfoName != TableName) {
				return false;
			}
			if (AuthorName != null && table.InfoAuthorName != AuthorName) {
				return false;
			}

			return true;
		}
	}
}
