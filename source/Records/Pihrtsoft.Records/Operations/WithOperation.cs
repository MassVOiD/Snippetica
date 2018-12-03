// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Pihrtsoft.Records.Operations
{
    [DebuggerDisplay("{Kind} {PropertyName,nq} = {Value,nq}")]
    internal struct WithOperation : IPropertyOperation
    {
        public WithOperation(PropertyDefinition propertyDefinition, string value, int depth)
        {
            PropertyDefinition = propertyDefinition;
            Value = value;
            Depth = depth;
        }

        public PropertyDefinition PropertyDefinition { get; }

        public string Value { get; }

        public int Depth { get; }

        public OperationKind Kind => OperationKind.With;

        public string PropertyName => PropertyDefinition.Name;

        public bool SupportsExecute => true;

        public void Execute(Record record)
        {
            if (!PropertyDefinition.IsCollection)
            {
                record[PropertyName] = Value;
                return;
            }

            char[] separators = PropertyDefinition.SeparatorsArray;

            if (PropertyDefinition == PropertyDefinition.Tags)
            {
                if (separators.Length > 0)
                {
                    foreach (string value2 in Value.Split(separators, StringSplitOptions.RemoveEmptyEntries))
                        record.Tags.Add(value2);
                }
                else
                {
                    record.Tags.Add(Value);
                }

                return;
            }

            List<object> items = record.GetOrAddCollection(PropertyName);

            if (separators.Length > 0)
            {
                foreach (string value2 in Value.Split(separators, StringSplitOptions.RemoveEmptyEntries))
                    items.Add(value2);
            }
            else
            {
                items.Add(Value);
            }
        }

        string IKey<string>.GetKey()
        {
            return PropertyName;
        }
    }
}
