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
using System.Linq;
using VisualPinball.Engine.VPT.Collection;
using VisualPinball.Engine.VPT.Sound;
using VisualPinball.Engine.VPT.Table;
using Material = VisualPinball.Engine.VPT.Material;
using Texture = VisualPinball.Engine.VPT.Texture;

namespace VisualPinball.Unity
{
	public class SceneTableContainer : TableContainer
	{
		public override Table Table => _table ??= new Table(_tableComponent.TableContainer, _tableComponent.LegacyContainer.TableData);
		public override Dictionary<string, string> TableInfo => _tableComponent.TableInfo;
		public override List<CollectionData> Collections => _tableComponent.Collections;
		[Obsolete("Use MappingConfig")]
		public override CustomInfoTags CustomInfoTags => _tableComponent.CustomInfoTags;

		public const int ChildObjectsLayer = 16;

		public override IEnumerable<Texture> Textures => _tableComponent.LegacyContainer.Textures
			.Where(texture => texture.IsSet)
			.Select(texture => texture.ToTexture());

		public override IEnumerable<Sound> Sounds => _tableComponent.LegacyContainer.Sounds
			.Where(sound => sound.IsSet)
			.Select(sound => sound.ToSound());

		private string[] TextureNames => _tableComponent.LegacyContainer.Textures
			.Select(t => t.Name)
			.ToArray();

		private string[] MaterialNames => _tableComponent.LegacyContainer.TableData.Materials
			.Select(m => m.Name)
			.ToArray();

		private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();

		public override Material GetMaterial(string name)
		{
			if (string.IsNullOrEmpty(name)) {
				return null;
			}
			return _materials.ContainsKey(name.ToLower()) ? _materials[name.ToLower()] : null;
		}

		public override Texture GetTexture(string name) => null;

		private readonly TableComponent _tableComponent;
		private Table _table;

		public SceneTableContainer(TableComponent ta)
		{
			_tableComponent = ta;
		}

		private IEnumerable<Sound> RetrieveSounds()
		{
			return Array.Empty<Sound>();
		}

		protected override void Clear()
		{
			base.Clear();
			_materials.Clear();
		}
	}
}
