using System;
using System.IO;
using System.Reflection;
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
	public abstract class BiffAttribute : Attribute
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
		/// For arrays, this defines how many values should be read
		/// </summary>
		public int Count = -1;

		/// <summary>
		/// For arrays, this defines that only one value should be read
		/// and stored at the given position.
		/// </summary>
		public int Index = -1;

		/// <summary>
		/// If put on a field, this is the info from C#'s reflection API.
		/// </summary>
		public FieldInfo Field { get; set; }
		/// <summary>
		/// If put on a property, this is the info from C#'s reflection API.
		/// </summary>
		public PropertyInfo Property { get; set; }

		protected Type Type => Field != null ? Field.FieldType : Property.PropertyType;

		protected BiffAttribute(string name)
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
		public abstract void Parse<T>(T obj, BinaryReader reader, int len) where T : ItemData;

		/// <summary>
		/// Sets the value to either field or property, depending on which
		/// type this Attribute is attached to.
		/// </summary>
		///
		/// <param name="obj">Object instance that is being read</param>
		/// <param name="value">Value to be set the field or property this Attribute was attached to</param>
		public void SetValue(object obj, dynamic value)
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
		public dynamic GetValue(object obj)
		{
			if (Property != null && Property.CanRead) {
				return Property.GetValue(obj);
			}

			return Field != null ? Field.GetValue(obj) : null;
		}
	}
}
