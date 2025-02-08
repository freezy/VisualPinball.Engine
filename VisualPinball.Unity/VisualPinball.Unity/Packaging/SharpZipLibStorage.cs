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
using System.IO;
using ICSharpCode.SharpZipLib.Zip;

namespace VisualPinball.Unity.Editor
{
	public class SharpZipStorageManager : IStorageManager
	{
		public IPackageStorage CreateStorage(string path)
		{
			// Create new Zip file (write mode).
			return new ZipPackageStorage(path, true);
		}

		public IPackageStorage OpenStorage(string path)
		{
			// Open existing Zip file (read mode).
			return new ZipPackageStorage(path, false);
		}
	}

	internal class ZipPackageStorage : IPackageStorage
	{
		private readonly bool _isWriteMode;

		private ZipOutputStream _zipOutputStream;
		private ZipFile _zipFile;

		// A root folder abstraction to keep track of the top-level.
		private readonly ZipPackageFolder _rootFolder;

		public ZipPackageStorage(string path, bool writeMode)
		{
			_isWriteMode = writeMode;

			if (writeMode) {

				// Create a FileStream for writing
				var fs = File.Create(path);
				_zipOutputStream = new ZipOutputStream(fs);
				_zipOutputStream.SetComment("Visual Pinball Engine");
				_zipOutputStream.SetLevel(9);

				// Create a root folder with write context
				_rootFolder = new ZipPackageFolder("", null, this, true);

			} else {
				// Open read-only
				_zipFile = new ZipFile(File.OpenRead(path));
				// Build an in-memory tree of folders/files for quick lookup
				_rootFolder = new ZipPackageFolder("", null, this, false);

				BuildFolderTree();
			}
		}

		public IPackageFolder AddFolder(string name)
		{
			// Delegate to root folder
			return _rootFolder.AddFolder(name);
		}

		public IPackageFolder GetFolder(string name)
		{
			return _rootFolder.GetFolder(name);
		}

		public void Close()
		{
			// If writing, close the ZipOutputStream (which closes the underlying FileStream).
			if (_isWriteMode) {

				if (_zipOutputStream != null) {
					_zipOutputStream.Finish();
					_zipOutputStream.Close();
					_zipOutputStream = null;
				}

			} else  {
				// If reading, close the ZipFile
				if (_zipFile != null) {
					_zipFile.Close();
					_zipFile = null;
				}
			}
		}

		public void Dispose() => Close();

		public ZipOutputStream GetZipOutputStream()
		{
			if (!_isWriteMode) {
				throw new InvalidOperationException("Cannot get ZipOutputStream in read mode.");
			}
			return _zipOutputStream;
		}

		public ZipFile GetZipFile()
		{
			if (_isWriteMode) {
				throw new InvalidOperationException("Cannot get ZipFile in write mode.");
			}
			return _zipFile;
		}

		/// <summary>
		/// In read mode, we build an internal folder/file structure from the existing ZipFile.
		/// </summary>
		private void BuildFolderTree()
		{
			// Iterate over every entry in the zip and place it into the folder structure
			foreach (ZipEntry entry in _zipFile) {

				var entryName = entry.Name;
				var isDirectory = entry.IsDirectory;

				// Split path by forward slash
				var parts = entryName.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

				// Insert into our folder tree
				var currentFolder = _rootFolder;
				for (var i = 0; i < parts.Length; i++) {
					var lastPart = i == parts.Length - 1;
					if (lastPart && !isDirectory) {
						// It's a file
						currentFolder.AddFileInternal(parts[i], entry);
					} else {
						// It's a folder
						currentFolder = currentFolder.AddFolderInternal(parts[i]);
					}
				}
			}
		}
	}

	internal class ZipPackageFolder : IPackageFolder
	{
		private readonly string _folderName;
		private readonly ZipPackageFolder _parent;
		private readonly ZipPackageStorage _storage;
		private readonly bool _isWriteMode;

		// Children
		private readonly Dictionary<string, ZipPackageFolder> _subFolders = new(StringComparer.OrdinalIgnoreCase);

		private readonly Dictionary<string, ZipPackageFile> _files = new(StringComparer.OrdinalIgnoreCase);

		public ZipPackageFolder(string folderName, ZipPackageFolder parent, ZipPackageStorage storage, bool isWriteMode)
		{
			_folderName = folderName;
			_parent = parent;
			_storage = storage;
			_isWriteMode = isWriteMode;
		}

		public string Name => _folderName;

		public IPackageFolder AddFolder(string name)
		{
			if (!_isWriteMode) {
				throw new InvalidOperationException("Cannot add folder in read-only storage.");
			}
			return AddFolderInternal(name);
		}

		internal ZipPackageFolder AddFolderInternal(string name)
		{
			if (!_subFolders.TryGetValue(name, out var folder)) {

				folder = new ZipPackageFolder(name, this, _storage, _isWriteMode);
				_subFolders[name] = folder;

				// In a Zip, folders are typically represented by a trailing slash entry
				// We can immediately add a placeholder entry if we want to ensure folder presence
				if (_isWriteMode) {

					var fullFolderPath = GetFullPathForFolder(name);
					// Add an entry with trailing slash
					var entryName = fullFolderPath + "/";

					var zos = _storage.GetZipOutputStream();
					var folderEntry = new ZipEntry(entryName)
					{
						DateTime = DateTime.Now
					};

					zos.PutNextEntry(folderEntry);
					zos.CloseEntry();
				}
			}

			return folder;
		}

		public IPackageFolder GetFolder(string name)
		{
			if (!TryGetFolder(name, out var folder)) {
				throw new ArgumentException($"Folder '{name}' does not exist in '{_folderName}'");
			}

			return folder;
		}

		public bool TryGetFolder(string name, out IPackageFolder folder)
		{
			if (_subFolders.TryGetValue(name, out var found)) {
				folder = found;
				return true;
			}

			folder = null;
			return false;
		}

		public IPackageFile AddFile(string name, string ext = null)
		{
			if (!_isWriteMode) {
				throw new InvalidOperationException("Cannot add file in read-only storage.");
			}

			name += ext ?? string.Empty;
			if (!_files.TryGetValue(name, out var file)) {
				file = new ZipPackageFile(name, this, _storage);
				_files[name] = file;
			}

			return file;
		}

		internal void AddFileInternal(string name, ZipEntry zipEntry)
		{
			// For read mode, we store a reference to the existing entry
			if (!_files.TryGetValue(name, out var file)) {
				file = new ZipPackageFile(name, this, _storage, zipEntry);
				_files[name] = file;
			}
		}

		public IPackageFile GetFile(string name, string ext = null)
		{
			name += ext ?? string.Empty;
			if (!TryGetFile(name, out var file)) {
				throw new ArgumentException($"File '{name}' does not exist in folder '{_folderName}'");
			}

			return file;
		}

		public bool TryGetFile(string name, out IPackageFile file, string ext = null)
		{
			name += ext ?? string.Empty;
			if (_files.TryGetValue(name, out var found)) {
				file = found;
				return true;
			}

			file = null;
			return false;
		}

		public void VisitFolders(Action<IPackageFolder> action)
		{
			foreach (var sf in _subFolders.Values) {
				action(sf);
			}
		}

		public void VisitFiles(Action<IPackageFile> action)
		{
			foreach (var f in _files.Values) {
				action(f);
			}
		}

		private string GetFullPathForFolder(string childFolderName)
		{
			// Build the path from the root to this folder + childFolderName
			// For the zip entry, we use forward slashes
			var stack = new Stack<string>();
			stack.Push(childFolderName);

			var current = this;
			while (current != null && !string.IsNullOrEmpty(current._folderName)) {
				stack.Push(current._folderName);
				current = current._parent;
			}

			return string.Join("/", stack);
		}

		internal string GetFullPathForFile(string fileName)
		{
			// Build the path from the root to this folder + fileName
			var stack = new Stack<string>();
			stack.Push(fileName);

			var current = this;
			while (current != null && !string.IsNullOrEmpty(current._folderName)) {
				stack.Push(current._folderName);
				current = current._parent;
			}

			return string.Join("/", stack);
		}
	}


	internal class ZipPackageFile : IPackageFile
	{
		private readonly ZipPackageFolder _parentFolder;
		private readonly ZipPackageStorage _storage;
		private readonly ZipEntry _existingEntry; // non-null if read-only

		public ZipPackageFile(string fileName, ZipPackageFolder parentFolder, ZipPackageStorage storage)
		{
			Name = fileName;
			_parentFolder = parentFolder;
			_storage = storage;
			_existingEntry = null; // indicates we're in write mode (or a new file in read/write scenario)
		}

		public ZipPackageFile(string fileName, ZipPackageFolder parentFolder, ZipPackageStorage storage,
			ZipEntry existingEntry)
		{
			Name = fileName;
			_parentFolder = parentFolder;
			_storage = storage;
			_existingEntry = existingEntry; // read mode
		}

		public string Name { get; }

		public Stream AsStream()
		{
			// In write mode, return the ZipOutputStream for this entry.
			// In read mode, return the ZipFile's InputStream for this entry.
			if (_storage == null) {
				throw new InvalidOperationException("Storage reference lost.");
			}

			if (_existingEntry == null) {

				// write mode
				var zos = _storage.GetZipOutputStream();
				var fullPath = _parentFolder.GetFullPathForFile(Name);

				var entry = new ZipEntry(fullPath)
				{
					DateTime = DateTime.Now
				};
				zos.PutNextEntry(entry);

				// Return a stream that writes directly to zos until closed.
				// We'll wrap zos in a "closing" stream that calls CloseEntry() on Dispose.
				return new ZipOutputEntryStream(zos);
			}

			// read mode
			var zf = _storage.GetZipFile();
			return zf.GetInputStream(_existingEntry);
		}

		public void SetData(byte[] data)
		{
			// A convenience: write the data to the Zip stream immediately
			// then close the entry.
			using var s = AsStream();
			s.Write(data, 0, data.Length);
		}

		public byte[] GetData()
		{
			if (_existingEntry == null)
				throw new InvalidOperationException("Cannot read data from a file in write-only storage.");

			using var s = AsStream();
			// Read everything out of the stream directly
			using var ms = new MemoryStream();
			s.CopyTo(ms);
			return ms.ToArray();
		}

		/// <summary>
		/// A small helper stream that wraps ZipOutputStream for a single entry.
		/// When this is disposed, it calls CloseEntry() on the underlying ZipOutputStream.
		/// </summary>
		private class ZipOutputEntryStream : Stream
		{
			private ZipOutputStream _zos;
			private bool _disposed;

			public ZipOutputEntryStream(ZipOutputStream zos)
			{
				_zos = zos;
			}

			public override bool CanRead => false;
			public override bool CanSeek => false;
			public override bool CanWrite => !_disposed;
			public override long Length => throw new NotSupportedException();

			public override long Position
			{
				get => throw new NotSupportedException();
				set => throw new NotSupportedException();
			}

			public override void Flush()
			{
				// ZipOutputStream flush is effectively no-op
			}

			public override int Read(byte[] buffer, int offset, int count)
			{
				throw new NotSupportedException("Cannot read from a ZipOutputEntryStream");
			}

			public override long Seek(long offset, SeekOrigin origin)
			{
				throw new NotSupportedException("Seek not supported in ZipOutputEntryStream");
			}

			public override void SetLength(long value)
			{
				throw new NotSupportedException("SetLength not supported in ZipOutputEntryStream");
			}

			public override void Write(byte[] buffer, int offset, int count)
			{
				if (_disposed)
					throw new ObjectDisposedException(nameof(ZipOutputEntryStream));
				_zos.Write(buffer, offset, count);
			}

			protected override void Dispose(bool disposing)
			{
				if (!_disposed && disposing)
				{
					_zos.CloseEntry();
					_disposed = true;
				}

				base.Dispose(disposing);
			}
		}
	}
}
