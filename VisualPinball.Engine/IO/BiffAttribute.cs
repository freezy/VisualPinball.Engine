using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;

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
	public class BiffAttribute : Attribute
	{
		/// <summary>
		/// Name of the BIFF record, usually four characters
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// Some records like CODE have their actual length set after
		/// the BIFF name. If that's the case set this field to `true`.
		/// </summary>
		public bool IsStreaming;

		/// <summary>
		/// Wide strings have a zero byte between each character.
		/// </summary>
		public bool IsWideString;

		/// <summary>
		/// For arrays, this defines how many values should be read
		/// </summary>
		public int Count = 1;

		/// <summary>
		/// For arrays, this defines that only one value should be read
		/// and stored at the given position.
		/// </summary>
		public uint Index;

		/// <summary>
		/// If put on a field, this is the info from C#'s reflection API.
		/// </summary>
		public FieldInfo Field { get; set; }
		/// <summary>
		/// If put on a property, this is the info from C#'s reflection API.
		/// </summary>
		public PropertyInfo Property { get; set; }

		private Type Type => Field != null ? Field.FieldType : Property.PropertyType;

		public BiffAttribute(string name)
		{
			Name = name;
		}

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
		/// <typeparam name="T">Type of the item data we're currently parsing</typeparam>
		public virtual void Parse<T>(T obj, BinaryReader reader, int len) where T : ItemData
		{
			if (Type == typeof(float)) {
				SetValue(obj, reader.ReadSingle());

			} else if (Type == typeof(int)) {
				SetValue(obj, reader.ReadInt32());

			} else if (Type == typeof(bool)) {
				SetValue(obj, reader.ReadInt32() > 0);

			} else if (Type == typeof(float[])) {
				if (GetValue(obj) is float[] arr) {
					arr[Index] = reader.ReadSingle();
				}

			} else if (Type == typeof(uint[])) {
				if (!(GetValue(obj) is uint[] arr)) {
					return;
				}
				if (Count > 1) {
					for (var i = 0; i < Count; i++) {
						arr[i] = reader.ReadUInt32();
					}
				} else {
					arr[Index] = reader.ReadUInt32();
				}

			} else if (Type == typeof(string[])) {
				if (GetValue(obj) is string[] arr) {
					arr[Index] = Encoding.ASCII.GetString(reader.ReadBytes(len));
				}

			} else if (Type == typeof(string)) {
				byte[] bytes;
				if (IsWideString) {
					var wideLen = reader.ReadInt32();
					bytes = reader.ReadBytes(wideLen).Where((x, i) => i % 2 == 0).ToArray();
				} else {
					bytes = IsStreaming ? reader.ReadBytes(len) : reader.ReadBytes(len).Skip(4).ToArray();
				}
				SetValue(obj, Encoding.ASCII.GetString(bytes));

			} else if (Type == typeof(Vertex3D)) {
				SetValue(obj, new Vertex3D(reader));

			} else if (Type == typeof(Vertex2D)) {
				SetValue(obj, new Vertex2D(reader));

			} else {
				Console.Error.WriteLine("[BiffAttribute.Parse] Unknown type \"{0}\" for tag {1}", Type, Name);
				reader.BaseStream.Seek(len, SeekOrigin.Current);
			}
		}

		/// <summary>
		/// Sets the value to either field or property, depending on which
		/// type this Attribute is attached to.
		/// </summary>
		///
		/// <param name="obj">Object instance that is being read</param>
		/// <param name="value">Value to be set the field or property this Attribute was attached to</param>
		protected void SetValue(object obj, object value)
		{
			if (Field != null) {
				Field.SetValue(obj, value);
			} else {
				Property.SetValue(obj, value);
			}
		}

		/// <summary>
		/// Gets the value from either field or property, depending on which
		/// type this Attribute is attached to.
		/// </summary>
		///
		/// <param name="obj">Object instance that is being read</param>
		/// <returns></returns>
		protected object GetValue(object obj)
		{
			return Field != null ? Field.GetValue(obj) : Property.GetValue(obj);
		}
	}
}
