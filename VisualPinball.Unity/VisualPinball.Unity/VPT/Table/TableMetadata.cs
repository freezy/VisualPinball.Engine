// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace VisualPinball.Unity
{
	[Serializable]
	public class TableMetadata
	{
		[JsonProperty("tableName")]
		public string TableName;

		[JsonProperty("primaryAuthors")]
		public List<TableAuthor> PrimaryAuthors = new();

		[JsonProperty("secondaryAuthors")]
		public List<TableAuthor> SecondaryAuthors = new();

		[JsonProperty("releaseDate")]
		public string ReleaseDate;

		[JsonProperty("originalReleaseYear")]
		public int OriginalReleaseYear;

		[JsonProperty("manufacturer")]
		public string Manufacturer;
	}

	[Serializable]
	public class TableAuthor
	{
		[JsonProperty("name")]
		public string Name;

		[JsonProperty("vpuHandle")]
		public string VpuHandle;
	}
}
