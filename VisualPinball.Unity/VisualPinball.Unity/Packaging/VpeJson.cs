// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace VisualPinball.Unity
{
	/// <summary>
	/// How the JSON in a package is named: every key camelCase, whether it comes from a public field, a property or a
	/// private serialized field (whose leading underscore is dropped). Names given with <c>[JsonProperty("…")]</c> are
	/// kept as they are, and so are dictionary keys, which are data.
	/// </summary>
	public static class VpeJson
	{
		/// <summary>Shared, so contracts are built once per type.</summary>
		public static readonly IContractResolver Resolver = new ContractResolver();

		public static JsonSerializerSettings Settings(JsonSerializerSettings settings = null)
		{
			settings ??= new JsonSerializerSettings();
			settings.ContractResolver = Resolver;
			return settings;
		}

		private sealed class NamingStrategy : CamelCaseNamingStrategy
		{
			public NamingStrategy() : base(processDictionaryKeys: false, overrideSpecifiedNames: false) { }

			protected override string ResolvePropertyName(string name) => base.ResolvePropertyName(name.TrimStart('_'));
		}

		private sealed class ContractResolver : DefaultContractResolver
		{
			public ContractResolver() => NamingStrategy = new NamingStrategy();

			protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
			{
				var property = base.CreateProperty(member, memberSerialization);
				// A get-only property of a value type is computed from what is stored (a vector's magnitude, a sound's
				// Loop next to its _loop): nothing can read it back, and without its underscore the field it reads from
				// would have the same name.
				if (member is PropertyInfo info && !property.Writable && !property.HasMemberAttribute
					&& (info.PropertyType.IsValueType || info.PropertyType == typeof(string))) {
					property.Ignored = true;
				}
				return property;
			}
		}
	}
}
