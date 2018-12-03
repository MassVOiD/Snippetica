// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Pihrtsoft.Records.Operations
{
    [DebuggerDisplay("{Kind} {PropertyName,nq} = {Value,nq}")]
    internal struct WithoutOperation : IPropertyOperation
    {
        public WithoutOperation(PropertyDefinition propertyDefinition, string value, int depth)
        {
            PropertyDefinition = propertyDefinition;
            Value = value;
            Depth = depth;
        }

        public PropertyDefinition PropertyDefinition { get; }

        public string Value { get; }

        public int Depth { get; }

        public OperationKind Kind => OperationKind.Without;

        public string PropertyName => PropertyDefinition.Name;

        public bool SupportsExecute => true;

        public void Execute(Record record)
        {
            char[] separators = PropertyDefinition.SeparatorsArray;

            if (PropertyDefinition == PropertyDefinition.Tags)
            {
                if (separators.Length > 0)
                {
                    foreach (string value2 in Value.Split(separators, StringSplitOptions.RemoveEmptyEntries))
                        record.Tags.Remove(value2);
                }
                else
                {
                    record.Tags.Remove(Value);
                }
            }
            else if (record.TryGetCollection(PropertyName, out List<object> items))
            {
                if (separators.Length > 0)
                {
                    foreach (string value2 in Value.Split(separators, StringSplitOptions.RemoveEmptyEntries))
                        items.Remove(value2);
                }
                else
                {
                    items.Remove(Value);
                }
            }
            else
            {
                //TODO: throw?
            }
        }

        string IKey<string>.GetKey() => PropertyName;
    }
}
