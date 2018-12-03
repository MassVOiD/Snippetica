// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Pihrtsoft.Records.Utilities;

namespace Pihrtsoft.Records
{
    [DebuggerDisplay("Name = {Name,nq}, IsCollection = {IsCollection}, IsRequired = {IsRequired}, DefaultValue = {DefaultValue}")]
    public class PropertyDefinition : IKey<string>
    {
        internal PropertyDefinition(
            string name,
            bool isCollection = false,
            bool isRequired = false,
            string defaultValue = null,
            string description = null,
            char[] separators = null)
        {
            Name = name;
            IsCollection = isCollection;
            IsRequired = isRequired;
            DefaultValue = defaultValue;
            Description = description;

            if (separators != null)
            {
                SeparatorsArray = separators.ToArray();
                Separators = new ReadOnlyCollection<char>(separators);
            }
            else
            {
                SeparatorsArray = Empty.Array<char>();
                Separators = Empty.ReadOnlyCollection<char>();
            }
        }

        internal static string IdName { get; } = "Id";

        internal static string TagsName { get; } = "Tags";

        internal static PropertyDefinition Id { get; } = new PropertyDefinition(IdName);

        internal static PropertyDefinition Tags { get; } = new PropertyDefinition(TagsName, isCollection: true, separators: new char[] { ',' });

        public string Name { get; }

        public bool IsCollection { get; }

        public bool IsRequired { get; }

        public string DefaultValue { get; }

        public string Description { get; }

        internal char[] SeparatorsArray { get; }

        public ReadOnlyCollection<char> Separators { get; }

        string IKey<string>.GetKey()
        {
            return Name;
        }

        internal static bool IsReservedName(string name)
        {
            return DefaultComparer.NameEquals(name, IdName)
                || DefaultComparer.NameEquals(name, TagsName);
        }
    }
}
