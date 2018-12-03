// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Pihrtsoft.Records.Operations;

namespace Pihrtsoft.Records
{
    internal static class EnumerableExtensions
    {
        public static void ExecuteAll(this IEnumerable<Operation> propertyOperations, Record record)
        {
            foreach (Operation propertyOperation in propertyOperations)
                propertyOperation.Execute(record);
        }
    }
}
