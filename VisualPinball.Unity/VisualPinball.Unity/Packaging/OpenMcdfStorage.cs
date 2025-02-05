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
using System.IO;
using OpenMcdf;
using OpenMcdf.Extensions;

namespace VisualPinball.Unity.Editor.Packaging
{
	public class OpenMcdfStorageManager : IStorageManager
	{
		public IPackageStorage CreateStorage(string path)
		{
			var s = new OpenMcdfStorage();
			s.Path = path;
			return s;
		}

		public IPackageStorage OpenStorage(string path)
		{
			return new OpenMcdfStorage(path);
		}
	}


	public class OpenMcdfStorage : IPackageStorage
	{
		private readonly CompoundFile _cf;
		public string Path;

		public OpenMcdfStorage()
		{
			_cf = new CompoundFile();
		}

		public OpenMcdfStorage(string path)
		{
			_cf = new CompoundFile(path);
		}

		public IPackageFolder AddFolder(string name) => new OpenMcdfFolder(_cf.RootStorage.AddStorage(name));

		public IPackageFolder GetFolder(string name) => new OpenMcdfFolder(_cf.RootStorage.GetStorage(name));

		public void Close()
		{
			_cf.SaveAs(Path);
			_cf.Close();
		}

		public void Dispose()
		{
			_cf.Close();
			_cf.Dispose();
		}
	}

	public class OpenMcdfFolder : IPackageFolder
	{
		private readonly CFStorage _storage;

		public OpenMcdfFolder(CFStorage storage)
		{
			_storage = storage;
			_storage.ModifyDate = DateTime.Now;
			_storage.CreationDate = DateTime.Now;
		}

		public string Name => _storage.Name;
		public IPackageFile AddFile(string name, string ext = null) => new OpenMcdfFile(_storage.AddStream(name + (ext ?? string.Empty)));
		public IPackageFolder AddFolder(string name) => new OpenMcdfFolder(_storage.AddStorage(name));
		public bool TryGetFolder(string name, out IPackageFolder folder)
		{
			if (_storage.TryGetStorage(name, out var cfStorage)) {
				folder = new OpenMcdfFolder(cfStorage);
				return true;
			}
			folder = null;
			return false;
		}

		public bool TryGetFile(string name, out IPackageFile file, string ext = null)
		{
			if (_storage.TryGetStream(name + (ext ?? string.Empty), out var cfStream)) {
				file = new OpenMcdfFile(cfStream);
				return true;
			}
			file = null;
			return false;
		}

		public IPackageFile GetFile(string name, string ext = null) => new OpenMcdfFile(_storage.GetStream(name + (ext ?? string.Empty)));
		public IPackageFolder GetFolder(string name) => new OpenMcdfFolder(_storage.GetStorage(name));
		public void VisitFiles(Action<IPackageFile> action)
		{
			_storage.VisitEntries(entry => {
				if (entry is CFStream stream) {
					action(new OpenMcdfFile(stream));
				}
			}, false);
		}

		public void VisitFolders(Action<IPackageFolder> action)
		{
			_storage.VisitEntries(entry => {
				if (entry is CFStorage storage) {
					action(new OpenMcdfFolder(storage));
				}
			}, false);
		}
	}

	public class OpenMcdfFile : IPackageFile
	{
		private readonly CFStream _stream;

		public OpenMcdfFile(CFStream stream)
		{
			_stream = stream;
		}

		public string Name => _stream.Name;
		public Stream AsStream() => _stream.AsIOStream();
		public void SetData(byte[] data) => _stream.Append(data);
		public byte[] GetData() => _stream.GetData();
	}
}
