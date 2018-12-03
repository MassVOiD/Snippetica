// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml.Linq;
using Pihrtsoft.Records.Utilities;

namespace Pihrtsoft.Records
{
    internal class RecordReader : AbstractRecordReader
    {
        public RecordReader(XElement element, EntityDefinition entity, DocumentSettings settings, IEnumerable<Record> withRecords = null)
            : base(element, entity, settings)
        {
            WithRecords = (withRecords != null)
                ? new WithRecordCollection(withRecords)
                : Empty.WithRecordCollection;
        }

        public WithRecordCollection WithRecords { get; }

        private Collection<Record> Records { get; set; }

        public override bool ShouldCheckRequiredProperty
        {
            get { return true; }
        }

        public override Collection<Record> ReadRecords()
        {
            Collect(Element.Elements());

            return Records;
        }

        protected override void AddRecord(Record record)
        {
            (Records ?? (Records = new Collection<Record>())).Add(record);
        }

        protected override Record CreateRecord(string id)
        {
            if (id != null && WithRecords != null)
            {
                Record record = WithRecords.Find(id);

                if (record != null)
                    return record.WithEntity(Entity);
            }

            return new Record(Entity, id);
        }
    }
}
