// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;

namespace Pihrtsoft.Records.Operations
{
    //TODO: RightToLeft
    [DebuggerDisplay("{Kind} {PropertyName,nq} = {Value,nq}")]
    internal struct WithPrefixOperation : IPropertyOperation
    {
        public WithPrefixOperation(PropertyDefinition propertyDefinition, string value, int depth, bool rightToLeft = false)
        {
            PropertyDefinition = propertyDefinition;
            Value = value;
            Depth = depth;
            RightToLeft = rightToLeft;
        }

        public PropertyDefinition PropertyDefinition { get; }

        public string Value { get; }

        public int Depth { get; }

        public bool RightToLeft { get; }

        public OperationKind Kind => OperationKind.WithPrefix;

        public string PropertyName => PropertyDefinition.Name;

        public bool SupportsExecute => false;

        public void Execute(Record record) => throw new NotSupportedException();

        string IKey<string>.GetKey() => PropertyName;
    }
}
