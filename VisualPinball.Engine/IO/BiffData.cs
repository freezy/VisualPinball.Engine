// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NLog;
using OpenMcdf;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.IO
{
	public enum StoragePrefix
	{
		GameItem, Collection, Sound, Image, MappingConfig
	}

	/// <summary>
	/// The base class of all data classes dealing with BIFF records.<p/>
	///
	/// This statically indexes all fields and properties tagged with the Biff
	/// Attribute.
	/// </summary>
	[Serializable]
	public abstract class BiffData
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public string StorageName => _storageName ?? $"{StoragePrefix}{StorageIndex}";

		public StoragePrefix StoragePrefix;
		public int StorageIndex;
		public readonly List<UnknownBiffRecord> UnknownRecords = new List<UnknownBiffRecord>();
		private readonly string _storageName;

		protected BiffData(StoragePrefix prefix)
		{
			StoragePrefix = prefix;
		}

		protected BiffData(string storageName)
		{
			if (string.IsNullOrEmpty(storageName)) {
				_storageName = string.Empty;

			} else {
				var match = new Regex(@"^([a-zA-Z]+)(\d+)$").Match(storageName);
				if (match.Success) {
					Enum.TryParse(match.Groups[1].Value, true, out StoragePrefix);
					int.TryParse(match.Groups[2].Value, out StorageIndex);

				} else {
					_storageName = storageName;
				}
			}
		}

		public abstract void Write(BinaryWriter writer, HashWriter hashWriter);

		public void WriteData(CFStorage gameStorage, HashWriter hashWriter = null)
		{
			var itemData = gameStorage.AddStream(StorageName);
			itemData.SetData(GetBytes(hashWriter));
		}

		private byte[] GetBytes(HashWriter hashWriter)
		{
			using (var stream = new MemoryStream())
			using (var writer = new BinaryWriter(stream)) {
				Write(writer, hashWriter);
				return stream.ToArray();
			}
		}

		/// <summary>
		/// Indexes the BiffAttributes of the data class.<p/>
		///
		/// This must be run statically for every data class. I haven't found
		/// a way to do this automatically through inheritance, so it has to be
		/// done manually.
		/// </summary>
		/// <param name="type">Type of the data class</param>
		/// <param name="attributes">A dictionary to use for indexing</param>
		protected static void Init(Type type, Dictionary<string, List<BiffAttribute>> attributes)
		{
			// get all fields and properties via reflection
			var members = type.GetMembers().Where(member => member.MemberType == MemberTypes.Field || member.MemberType == MemberTypes.Property);
			foreach (var member in members) {

				// for each field, see if there's one or more BiffAttributes
				var attrs = Attribute
					.GetCustomAttributes(member, typeof(BiffAttribute))
					.Select(a => a as BiffAttribute)
					.Where(a => a != null);

				// index for each attribute into a given dictionary
				foreach (var attr in attrs) {
					switch (member) {
						case FieldInfo field:
							attr.Field = field;
							break;
						case PropertyInfo property:
							attr.Property = property;
							break;
					}

					if (!attributes.ContainsKey(attr.Name)) {
						attributes[attr.Name] = new List<BiffAttribute>();
					}
					attributes[attr.Name].Add(attr);
				}
			}
		}

		/// <summary>
		/// Reads the binary data from the VPX file and applies them to the
		/// data class.
		/// </summary>
		/// <param name="obj">Object instance that is being read</param>
		/// <param name="reader">Binary data from the VPX file</param>
		/// <param name="attributes">The indexed Attributes of that data class</param>
		/// <typeparam name="T">Type of the data class</typeparam>
		/// <returns></returns>
		protected static T Load<T>(T obj, BinaryReader reader, Dictionary<string, List<BiffAttribute>> attributes) where T : BiffData
		{

			var ignoredTags = typeof(T).GetCustomAttributes<BiffIgnoreAttribute>().Select(a => a.Name).ToArray();

			// initially read length and BIFF record name
			var len = reader.ReadInt32();
			var tag = ReadTag(reader);
			var pos = 0d;

			try {
				//Logger.Info("=== ITEM {0}", obj.StorageName);
				while (tag != "ENDB") {
					//Logger.Info("--- TAG {0}", tag);
					if (attributes.ContainsKey(tag)) {
						var attrs = attributes[tag];
						var i = 0;
						object val = null;
						foreach (var attr in attrs) {
							// parse data on the first
							if (i == 0) {
								pos = attr.Pos;
								if (attr.LengthAfterTag) {
									len = reader.ReadInt32();
									attr.Parse(obj, reader, len);

								} else {
									attr.Parse(obj, reader, len - 4);
								}
								val = attr.GetValue(obj);

							// only apply data on the others
							} else {
								attr.SetValue(obj, val);
							}
							i++;
						}

					} else if (ignoredTags.Contains(tag)) {
						reader.BaseStream.Seek(len - 4, SeekOrigin.Current);

					} else {
						Console.Error.WriteLine("[ItemData.Load] Unknown tag {0}", tag);
						pos += 0.001;
						obj.UnknownRecords.Add(new UnknownBiffRecord(pos, tag, reader.ReadBytes(len - 4)));
					}

					// read next length and tag name for next record
					len = reader.ReadInt32();
					tag = ReadTag(reader);
				}
			} catch (Exception e) {
				if (obj is ItemData itemData) {
					throw new Exception("Error parsing tag \"" + tag + "\" at \"" + itemData.GetName() + "\" (" + itemData.StorageName + ").", e);
				}
				throw new Exception("Error parsing tag \"" + tag + "\".", e);
			}
			return obj;
		}

		protected void WriteRecord(BinaryWriter writer, Dictionary<string, List<BiffAttribute>> attributes, HashWriter hashWriter)
		{
			// filter known records, join them with unknown records, and sort.
			var records = attributes.Values
				.Where(a => !a[0].SkipWrite && !SkipWrite(a[0]))
				.Select(a => a[0] as ISortableBiffRecord)
				.Concat(UnknownRecords ?? new List<UnknownBiffRecord>())
				.OrderBy(r => r.GetPosition());
			foreach (var record in records) {
				try {
					record.Write(this, writer, hashWriter);

				} catch (Exception e) {
					throw new InvalidOperationException("Error writing [" + record.GetType().Name + "] at \"" + record.GetName() + "\" of " + GetType().Name + " " + StorageName + ".", e);
				}
			}
		}

		protected virtual bool SkipWrite(BiffAttribute attr)
		{
			return false;
		}

		protected static void WriteEnd(BinaryWriter writer, HashWriter hashWriter)
		{
			var endTag = Encoding.Default.GetBytes("ENDB");
			writer.Write(4);
			writer.Write(endTag);
			hashWriter?.Write(endTag);
		}

		/// <summary>
		/// Returns the ASCII string of the next four (or less) chars on the
		/// current position of the stream.<p/>
		///
		/// Sometimes BIFF names are less than four chars, so we stop at 0x0.
		/// </summary>
		/// <param name="reader">Binary data from the VPX file, with pointer at the start of the BIFF tag</param>
		/// <returns>Name of the BIFF record</returns>
		private static string ReadTag(BinaryReader reader)
		{
			return Encoding.UTF8.GetString(reader.ReadBytes(4).Where(b => b != 0x0).ToArray());
		}
	}

	[Serializable]
	public class UnknownBiffRecord : ISortableBiffRecord
	{
		public double GetPosition() => Position;
		public string GetName() => Name;

		public double Position;
		public string Name;
		public byte[] Data;

		public UnknownBiffRecord(double position, string name, byte[] data)
		{
			Position = position;
			Name = name;
			Data = data;
		}

		public void Write<TItem>(TItem obj, BinaryWriter writer, HashWriter hashWriter) where TItem : BiffData
		{
			var tag = Encoding.Default.GetBytes(Name);
			if (Name.Length < 4) {
				tag = tag.Concat(new byte[4 - Name.Length]).ToArray();
			}
			writer.Write(Data.Length + 4);
			writer.Write(tag);
			writer.Write(Data);

			hashWriter?.Write(tag);
			hashWriter?.Write(Data);
		}
	}
}
