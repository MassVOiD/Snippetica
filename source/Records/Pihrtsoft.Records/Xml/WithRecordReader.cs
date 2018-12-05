// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Pihrtsoft.Records.Utilities;

namespace Pihrtsoft.Records.Xml
{
    internal class WithRecordReader : RecordReader
    {
        public WithRecordReader(EntityDefinition entity, DocumentOptions options)
            : base(entity, options)
        {
        }

        public override bool ShouldCheckRequiredProperty => false;

        protected override void AddRecord(Record record)
        {
            if (WithRecords == null)
            {
                WithRecords = new Dictionary<string, Record>(DefaultComparer.StringComparer);
            }
            else if (WithRecords.ContainsKey(record.Id))
            {
                Throw(ErrorMessages.ItemAlreadyDefined(PropertyDefinition.IdName, record.Id));
            }

            WithRecords.Add(record.Id, record);
        }

        protected override Record CreateRecord(string id)
        {
            if (id == null)
                Throw(ErrorMessages.MissingWithRecordIdentifier());

            return new Record(Entity, id);
        }
    }
}
