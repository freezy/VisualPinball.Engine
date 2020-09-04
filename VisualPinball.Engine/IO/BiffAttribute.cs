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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.IO
{
	/// <summary>
	/// Marks a field or property as part of the VPX file.<p/>
	///
	/// The file format of a VPX file is called COM Structured Storage, similar
	/// to a virtual file system containing storage containers and streams. Each
	/// stream is further divided into records using the BIFF format. Adding
	/// this attribute will link a property or field to the value of the BIFF
	/// record.
	/// </summary>
	///
	/// <see href="https://en.wikipedia.org/wiki/COM_Structured_Storage">COM Structured Storage</see>
	/// <see href="https://docs.microsoft.com/en-us/openspecs/office_file_formats/ms-xls/cd03cb5f-ca02-4934-a391-bb674cb8aa06">BIFF Format</see>
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
	public abstract class BiffAttribute : Attribute, ISortableBiffRecord
	{
		public double GetPosition() => Pos;
		public string GetName() => Name;

		/// <summary>
		/// Name of the BIFF record, usually four characters
		/// </summary>
		public string Name;

		/// <summary>
		/// The attribute position when writing.
		/// </summary>
		public double Pos = 0;

		/// <summary>
		/// Some records like CODE have their actual length set after
		/// the BIFF name. If that's the case set this field to `true`.
		/// </summary>
		public bool LengthAfterTag;

		/// <summary>
		/// For arrays, this defines how many values should be read
		/// </summary>
		public int Count = -1;

		/// <summary>
		/// For arrays, this defines that only one value should be read
		/// and stored at the given position.
		/// </summary>
		public int Index = -1;

		/// <summary>
		/// If true, write the size/tag header for each value, if an array.
		/// </summary>
		public bool TagAll = false;

		/// <summary>
		/// If true, this tag won't be written.
		///
		/// Useful if two tags write on the same field, e.g. due to legacy
		/// formats, and we want to support reading multiple formats but only
		/// write one format.
		/// </summary>
		public bool SkipWrite = false;

		/// <summary>
		/// If put on a field, this is the info from C#'s reflection API.
		/// </summary>
		public FieldInfo Field { get; set; }

		/// <summary>
		/// If put on a property, this is the info from C#'s reflection API.
		/// </summary>
		public PropertyInfo Property { get; set; }

		protected Type Type => Field != null ? Field.FieldType : Property.PropertyType;

		protected bool WriteHash(BiffData data) => true; //!SkipHash;

		protected BiffAttribute(string name)
		{
			Name = name;
		}

		public abstract void Parse<TItem>(TItem obj, BinaryReader reader, int len) where TItem : BiffData;

		public abstract void Write<TItem>(TItem obj, BinaryWriter writer, HashWriter hashWriter) where TItem : BiffData;

		/// <summary>
		/// Parses the value given a stream of binary data from the VPX file.<p/>
		///
		/// The position of the stream is where the data of the BIFF record
		/// starts (after the BIFF name).<br/>
		///
		/// Values that are autonomous (i.e. don't need any type-specific
		/// context) are handled here. If you need context, like you would need
		/// the number of vertices that are read previously into another field,
		/// you would need to extends this class and create another Attribute.
		/// </summary>
		///
		/// <param name="obj">Object instance that is being read</param>
		/// <param name="reader">Binary data from the VPX file</param>
		/// <param name="len">Length of the BIFF record</param>
		/// <param name="read">Read function</param>
		/// <typeparam name="TItem">Type of the item data we're currently parsing</typeparam>
		/// <typeparam name="TField">Type of the data field we're currently parsing</typeparam>
		protected void ParseValue<TItem, TField>(TItem obj, BinaryReader reader, int len, Func<BinaryReader, int, TField> read) where TItem : BiffData
		{
			if (Type == typeof(TField)) {
				SetValue(obj, read(reader, len));

			} else if (Type == typeof(TField[])) {
				var arr = GetValue(obj) as TField[];
				if (Count > 1) {
					if (arr == null) {
						arr = new TField[Count];
					}
					for (var i = 0; i < Count; i++) {
						arr[i] = read(reader, len);
					}

				} else if (Index >= 0) {
					arr[Index] = read(reader, len);

				} else {
					if (arr == null) {
						SetValue(obj, new []{ read(reader, len) });

					} else {
						SetValue(obj, arr.Concat(new []{ read(reader, len) }).ToArray());
					}
				}
			}
		}

		protected void WriteValue<TItem, TField>(TItem obj, BinaryWriter writer, Action<BinaryWriter, TField> write, HashWriter hashWriter, Func<int, int> overrideLength = null) where TItem : BiffData
		{
			var value = GetValue(obj);

			// don't write null values
			if (value == null) {
				return;
			}
			using (var stream = new MemoryStream())
			using (var dataWriter = new BinaryWriter(stream)) {
				if (Type == typeof(TField)) {
					write(dataWriter, (TField)value);

				} else if (Type == typeof(TField[])) {
					var arr = value as TField[];
					if (Index >= 0) {
						write(dataWriter, arr[Index]);

					} else {
						foreach (var val in arr) {
							if (TagAll) {
								using (var separateStream = new MemoryStream())
								using (var separateDataWriter = new BinaryWriter(separateStream)) {
									write(separateDataWriter, val);
									var separateData = separateStream.ToArray();
									var separateLength = overrideLength?.Invoke(separateData.Length) ?? separateData.Length;
									WriteStart(writer, separateLength, hashWriter);
									writer.Write(separateData);
									if (WriteHash(obj)) {
										hashWriter?.Write(separateData);
									}
								}
							} else {
								write(dataWriter, val);
							}
						}
					}
				} else {
					throw new InvalidOperationException("Unknown type for [" + GetType().Name + "] on field \"" + Name + "\".");
				}

				if (TagAll) {
					// everything's been written already
					return;
				}

				var data = stream.ToArray();
				var length = overrideLength?.Invoke(data.Length) ?? data.Length;
				WriteStart(writer, length, WriteHash(obj) ? hashWriter : null);
				writer.Write(data);
				if (WriteHash(obj)) {
					hashWriter?.Write(LengthAfterTag ? data.Skip(4).ToArray() : data);
				}
			}
		}

		/// <summary>
		/// Sets the value to either field or property, depending on which
		/// type this Attribute is attached to.
		/// </summary>
		///
		/// <param name="obj">Object instance that is being read</param>
		/// <param name="value">Value to be set the field or property this Attribute was attached to</param>
		public void SetValue(object obj, object value)
		{
			if (Property != null && Property.CanWrite) {
				Property.SetValue(obj, value);

			} else if (Field != null) {
				Field.SetValue(obj, value);
			}
		}

		/// <summary>
		/// Gets the value from either field or property, depending on which
		/// type this Attribute is attached to.
		/// </summary>
		///
		/// <param name="obj">Object instance that is being read</param>
		/// <returns></returns>
		public object GetValue(object obj)
		{
			if (Property != null && Property.CanRead) {
				return Property.GetValue(obj);
			}

			return Field != null ? Field.GetValue(obj) : null;
		}

		protected void WriteStart(BinaryWriter writer, int dataLength, HashWriter hashWriter)
		{
			var tag = Encoding.Default.GetBytes(Name);
			if (Name.Length < 4) {
				tag = tag.Concat(new byte[4 - Name.Length]).ToArray();
			}
			writer.Write(dataLength + 4);
			writer.Write(tag);
			hashWriter?.Write(tag); // only write tag
		}
	}

	public interface ISortableBiffRecord
	{
		double GetPosition();
		string GetName();

		void Write<TItem>(TItem obj, BinaryWriter writer, HashWriter hashWriter) where TItem : BiffData;

	}
}
