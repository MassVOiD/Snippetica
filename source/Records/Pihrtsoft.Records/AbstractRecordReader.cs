// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using Pihrtsoft.Records.Operations;
using Pihrtsoft.Records.Utilities;
using static Pihrtsoft.Records.Utilities.ThrowHelper;

namespace Pihrtsoft.Records
{
    internal abstract class AbstractRecordReader
    {
        protected AbstractRecordReader(XElement element, EntityDefinition entity, DocumentOptions options)
        {
            Element = element;
            Entity = entity;
            Options = options;
        }

        protected XElement Element { get; }

        public EntityDefinition Entity { get; }

        public DocumentOptions Options { get; }

        private XElement Current { get; set; }

        private int Depth { get; set; } = -1;

        private StringKeyedCollection<PropertyOperationCollection> PropertyOperations { get; set; }

        private Stack<Variable> Variables { get; set; }

        public virtual bool ShouldCheckRequiredProperty { get; }

        public abstract Collection<Record> ReadRecords();

        protected abstract void AddRecord(Record record);

        protected abstract Record CreateRecord(string id);

        protected void Collect(IEnumerable<XElement> elements)
        {
            Depth++;

            foreach (XElement element in elements)
            {
                Current = element;

                switch (element.Kind())
                {
                    case ElementKind.New:
                        {
                            AddRecord(CreateRecord(element));

                            break;
                        }
                    case ElementKind.With:
                    case ElementKind.Without:
                    case ElementKind.Postfix:
                    case ElementKind.Prefix:
                        {
                            if (element.HasElements)
                            {
                                PushOperations(element);
                                Collect(element.Elements());
                                PopOperations();
                            }

                            break;
                        }
                    case ElementKind.Variable:
                        {
                            if (element.HasElements)
                            {
                                AddVariable(element);
                                Collect(element.Elements());
                                Variables.Pop();
                            }

                            break;
                        }
                    default:
                        {
                            ThrowOnUnknownElement(element);
                            break;
                        }
                }

                Current = null;
            }

            Depth--;
        }

        private void PushOperations(XElement element)
        {
            foreach (Operation operation in CreateOperationsFromElement(element))
            {
                PropertyOperations = PropertyOperations ?? new StringKeyedCollection<PropertyOperationCollection>();

                if (!PropertyOperations.TryGetValue(operation.PropertyName, out PropertyOperationCollection propertyOperations))
                {
                    propertyOperations = new PropertyOperationCollection(operation.PropertyDefinition);
                    PropertyOperations.Add(propertyOperations);
                }

                propertyOperations.Add(operation);
            }
        }

        private void PopOperations()
        {
            for (int i = 0; i < PropertyOperations.Count; i++)
            {
                PropertyOperationCollection propertyOperations = PropertyOperations[i];

                for (int j = propertyOperations.Count - 1; j >= 0; j--)
                {
                    if (propertyOperations[j].Depth == Depth)
                        propertyOperations.RemoveAt(j);
                }
            }
        }

        private void AddVariable(XElement element)
        {
            string name = element.GetAttributeValueOrThrow(AttributeNames.Name);
            string value = element.GetAttributeValueOrThrow(AttributeNames.Value);

            (Variables ?? (Variables = new Stack<Variable>())).Push(new Variable(name, value));
        }

        private Record CreateRecord(XElement element)
        {
            string id = null;

            Collection<Operation> operations = null;

            foreach (XAttribute attribute in element.Attributes())
            {
                if (DefaultComparer.NameEquals(attribute, AttributeNames.Id))
                {
                    id = GetValue(attribute);
                }
                else
                {
                    Operation operation = CreateOperationFromAttribute(element, ElementKind.New, attribute);

                    (operations ?? (operations = new Collection<Operation>())).Add(operation);
                }
            }

            Record record = CreateRecord(id);

            operations?.ExecuteAll(record);

            ExecuteChildOperations(element, record);

            ExecutePendingOperations(record);

            foreach (PropertyDefinition property in Entity.AllProperties())
            {
                if (property.DefaultValue != null)
                {
                    if (!record.ContainsProperty(property.Name))
                    {
                        record[property.Name] = property.DefaultValue;
                    }
                }
                else if (ShouldCheckRequiredProperty
                    && property.IsRequired
                    && !record.ContainsProperty(property.Name))
                {
                    Throw(ErrorMessages.PropertyIsRequired(property.Name));
                }
            }

            return record;
        }

        private void ExecuteChildOperations(XElement element, Record record)
        {
            foreach (XElement child in element.Elements())
            {
                Current = child;

                CreateOperationsFromElement(child).ExecuteAll(record);
            }

            Current = element;
        }

        private void ExecutePendingOperations(Record record)
        {
            if (PropertyOperations == null)
                return;

            foreach (PropertyOperationCollection propertyOperations in PropertyOperations)
            {
                Dictionary<OperationKind, string> pendingValues = null;

                foreach (Operation operation in propertyOperations)
                {
                    OperationKind kind = operation.Kind;

                    if (kind == OperationKind.With
                        || kind == OperationKind.Without)
                    {
                        operation.Execute(record);

                        if (pendingValues != null)
                            ProcessPendingValues(pendingValues, propertyOperations.PropertyDefinition);
                    }
                    else
                    {
                        pendingValues = pendingValues ?? new Dictionary<OperationKind, string>();

                        if (pendingValues.TryGetValue(kind, out string value))
                        {
                            Debug.Assert(kind == OperationKind.Prefix || kind == OperationKind.Postfix, kind.ToString());

                            pendingValues[kind] += operation.Value;
                        }
                        else
                        {
                            pendingValues[kind] = operation.Value;
                        }
                    }
                }

                if (pendingValues != null)
                    ProcessPendingValues(pendingValues, propertyOperations.PropertyDefinition);
            }

            void ProcessPendingValues(Dictionary<OperationKind, string> pendingValues, PropertyDefinition propertyDefinition)
            {
                string name = propertyDefinition.Name;

                foreach (KeyValuePair<OperationKind, string> kvp in pendingValues)
                {
                    OperationKind kind = kvp.Key;

                    if (kind == OperationKind.Postfix)
                    {
                        if (propertyDefinition.IsCollection)
                        {
                            if (record.TryGetCollection(name, out List<object> items))
                            {
                                for (int i = 0; i < items.Count; i++)
                                    items[i] += kvp.Value;
                            }
                        }
                        else
                        {
                            record[name] += kvp.Value;
                        }
                    }
                    else if (kind == OperationKind.Prefix)
                    {
                        if (propertyDefinition.IsCollection)
                        {
                            if (record.TryGetCollection(name, out List<object> items))
                            {
                                for (int i = 0; i < items.Count; i++)
                                    items[i] = kvp.Value + items[i];
                            }
                        }
                        else
                        {
                            record[name] = kvp.Value + record[name];
                        }
                    }
                    else
                    {
                        Debug.Fail(kind.ToString());
                    }
                }

                pendingValues.Clear();
            }
        }

        private Operation CreateOperationFromAttribute(
            XElement element,
            ElementKind kind,
            XAttribute attribute,
            bool throwOnId = false)
        {
            string attributeName = attribute.LocalName();

            if (throwOnId
                && DefaultComparer.NameEquals(attributeName, AttributeNames.Id))
            {
                Throw(ErrorMessages.CannotUseOperationOnProperty(element, attributeName));
            }

            PropertyDefinition property;

            string name = attribute.LocalName();

            if (name == PropertyDefinition.TagsName)
            {
                property = PropertyDefinition.Tags;
            }
            else
            {
                property = GetProperty(attribute);
            }

            switch (kind)
            {
                case ElementKind.With:
                    {
                        return new Operation(property, GetValue(attribute), Depth, OperationKind.With);
                    }
                case ElementKind.Without:
                    {
                        if (!property.IsCollection)
                            Throw(ErrorMessages.CannotUseOperationOnNonCollectionProperty(element, property.Name));

                        return new Operation(property, GetValue(attribute), Depth, OperationKind.Without);
                    }
                default:
                    {
                        Debug.Assert(kind == ElementKind.New, kind.ToString());

                        return new Operation(property, GetValue(attribute), Depth, OperationKind.With);
                    }
            }
        }

        private IEnumerable<Operation> CreateOperationsFromElement(XElement element)
        {
            Debug.Assert(element.HasAttributes, element.ToString());

            ElementKind kind = element.Kind();

            switch (kind)
            {
                case ElementKind.With:
                case ElementKind.Without:
                    {
                        foreach (XAttribute attribute in element.Attributes())
                            yield return CreateOperationFromAttribute(element, kind, attribute, throwOnId: true);

                        //TODO: Separator
                        //char separator = ',';

                        //foreach (XAttribute attribute in element.Attributes())
                        //{
                        //    switch (attribute.LocalName())
                        //    {
                        //        case AttributeNames.Separator:
                        //            {
                        //                string separatorText = attribute.Value;

                        //                if (separatorText.Length != 1)
                        //                    Throw("Separator must be a single character", attribute);

                        //                separator = separatorText[0];
                        //                break;
                        //            }
                        //        default:
                        //            {
                        //                yield return CreateOperationFromAttribute(element, kind, attribute, separator: separator, throwOnId: true);
                        //                break;
                        //            }
                        //    }
                        //}

                        break;
                    }
                case ElementKind.Postfix:
                    {
                        foreach (XAttribute attribute in element.Attributes())
                            yield return new Operation(GetProperty(attribute), GetValue(attribute), Depth, OperationKind.Postfix);

                        break;
                    }
                case ElementKind.Prefix:
                    {
                        foreach (XAttribute attribute in element.Attributes())
                            yield return new Operation(GetProperty(attribute), GetValue(attribute), Depth, OperationKind.Prefix);

                        break;
                    }
                default:
                    {
                        Throw(ErrorMessages.OperationIsNotDefined(element.LocalName()));
                        break;
                    }
            }
        }

        private PropertyDefinition GetProperty(XAttribute attribute)
        {
            string propertyName = attribute.LocalName();

            if (DefaultComparer.NameEquals(propertyName, PropertyDefinition.TagsName))
                return PropertyDefinition.Tags;

            if (Entity.TryGetProperty(propertyName, out PropertyDefinition property))
                return property;

            Throw(ErrorMessages.PropertyIsNotDefined(propertyName), attribute);

            return null;
        }

        private string GetValue(XAttribute attribute)
        {
            return GetValue(attribute.Value, attribute);
        }

        private string GetValue(string value, XObject xobject)
        {
            try
            {
                return ParseHelpers.ParseAttributeValue(value, this);
            }
            catch (InvalidValueException ex)
            {
                ThrowInvalidOperation("Error while parsing value.", xobject, ex);
            }

            return null;
        }

        internal Variable FindVariable(string name)
        {
            if (Variables != null)
            {
                Variable variable = Variables.FirstOrDefault(f => DefaultComparer.NameEquals(name, f.Name));

                if (variable != null)
                    return variable;
            }

            return Entity.FindVariable(name);
        }

        protected void Throw(string message, XObject @object = null)
        {
            ThrowInvalidOperation(message, @object ?? Current);
        }
    }
}
