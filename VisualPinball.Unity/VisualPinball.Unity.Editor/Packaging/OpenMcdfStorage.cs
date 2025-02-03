using System;
using System.IO;
using OpenMcdf;
using OpenMcdf.Extensions;

namespace VisualPinball.Unity.Editor.Packaging
{
	public class OpenMcdfStorage : IVpeStorage
	{
		private readonly CompoundFile _cf;

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

		public void SaveAs(string path)
		{
			_cf.SaveAs(path);
			_cf.Close();
		}

		public void Close()
		{
			_cf.Close();
		}

		public void Dispose()
		{
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
		public IPackageFile AddFile(string name) => new OpenMcdfFile(_storage.AddStream(name));
		public IPackageFolder AddFolder(string name) => new OpenMcdfFolder(_storage.AddStorage(name));
		public bool TryGetFolder(string name, out IPackageFolder storage)
		{
			if (_storage.TryGetStorage(name, out var cfStorage)) {
				storage = new OpenMcdfFolder(cfStorage);
				return true;
			}
			storage = null;
			return false;
		}

		public bool TryGetFile(string name, out IPackageFile stream)
		{
			if (_storage.TryGetStream(name, out var cfStream)) {
				stream = new OpenMcdfFile(cfStream);
				return true;
			}
			stream = null;
			return false;
		}

		public IPackageFile GetFile(string name) => new OpenMcdfFile(_storage.GetStream(name));
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

		public Stream AsStream() => _stream.AsIOStream();
		public void SetData(byte[] data) => _stream.Append(data);
		public byte[] GetData() => _stream.GetData();
	}
}
