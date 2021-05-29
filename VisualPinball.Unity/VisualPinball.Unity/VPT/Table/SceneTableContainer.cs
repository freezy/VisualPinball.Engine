// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Collection;
using VisualPinball.Engine.VPT.Mappings;
using VisualPinball.Engine.VPT.Sound;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	public class SceneTableContainer : ITableContainer
	{
		public Table Table => _tableAuthoring.Table;
		public CustomInfoTags CustomInfoTags { get; }
		public Dictionary<string, string> TableInfo { get; }
		public bool Has<T>(string name) where T : IItem
		{
			throw new NotImplementedException();
		}
		public T Get<T>(string name) where T : IItem
		{
			throw new NotImplementedException();
		}
		public void Remove<T>(string name) where T : IItem
		{
			throw new NotImplementedException();
		}
		public string GetNewName<T>(string prefix) where T : IItem
		{
			throw new NotImplementedException();
		}
		public Material GetMaterial(string name)
		{
			throw new NotImplementedException();
		}
		public Texture GetTexture(string name)
		{
			throw new NotImplementedException();
		}
		public IEnumerable<IItem> GameItems { get; }
		public IEnumerable<IRenderable> Renderables { get; }
		public IEnumerable<IItem> NonRenderables { get; }
		public IEnumerable<ItemData> ItemDatas { get; }
		public Dictionary<string, Collection> Collections { get; }
		public ITableResourceContainer<Texture> Textures { get; }
		public ITableResourceContainer<Sound> Sounds { get; }
		public Mappings Mappings { get; }
		public IEnumerable<ISwitchable> Switchables { get; }
		public IEnumerable<ISwitchableDevice> SwitchableDevices { get; }
		public IEnumerable<ICoilable> Coilables { get; }
		public IEnumerable<ICoilableDevice> CoilableDevices { get; }
		public IEnumerable<ILightable> Lightables { get; }
		public void Save(string fileName)
		{
			throw new NotImplementedException();
		}

		private readonly TableAuthoring _tableAuthoring;

		public SceneTableContainer(TableAuthoring ta)
		{
			_tableAuthoring = ta;
		}
	}
}
