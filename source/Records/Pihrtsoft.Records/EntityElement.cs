// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Linq;
using Pihrtsoft.Records.Utilities;
using static Pihrtsoft.Records.Utilities.ThrowHelper;

namespace Pihrtsoft.Records
{
    internal class EntityElement
    {
        private XElement _declarationsElement;
        private XElement _withElement;
        private XElement _recordsElement;
        private XElement _entitiesElement;

        public EntityElement(XElement element, DocumentSettings settings, EntityDefinition baseEntity = null)
        {
            Settings = settings;

            Scan(element.Elements());

            ExtendedKeyedCollection<string, PropertyDefinition> properties = null;
            ExtendedKeyedCollection<string, Variable> variables = null;

            if (_declarationsElement != null)
                ScanDeclarations(_declarationsElement.Elements(), out properties, out variables);

            Entity = new EntityDefinition(element, baseEntity, properties, variables);
        }

        public DocumentSettings Settings { get; }

        public EntityDefinition Entity { get; }

        private void Scan(IEnumerable<XElement> elements)
        {
            foreach (XElement element in elements)
            {
                switch (element.Kind())
                {
                    case ElementKind.Declarations:
                        {
                            if (_declarationsElement != null)
                                ThrowOnMultipleElementsWithEqualName(element);

                            _declarationsElement = element;
                            break;
                        }
                    case ElementKind.With:
                        {
                            if (_withElement != null)
                                ThrowOnMultipleElementsWithEqualName(element);

                            _withElement = element;
                            break;
                        }
                    case ElementKind.Records:
                        {
                            if (_recordsElement != null)
                                ThrowOnMultipleElementsWithEqualName(element);

                            _recordsElement = element;
                            break;
                        }
                    case ElementKind.Entities:
                        {
                            if (_entitiesElement != null)
                                ThrowOnMultipleElementsWithEqualName(element);

                            _entitiesElement = element;
                            break;
                        }
                    default:
                        {
                            ThrowOnUnknownElement(element);
                            break;
                        }
                }
            }
        }

        private static void ScanDeclarations(IEnumerable<XElement> elements, out ExtendedKeyedCollection<string, PropertyDefinition> properties, out ExtendedKeyedCollection<string, Variable> variables)
        {
            properties = null;
            variables = null;

            foreach (XElement element in elements)
            {
                switch (element.Kind())
                {
                    case ElementKind.Variable:
                        {
                            variables = variables ?? new ExtendedKeyedCollection<string, Variable>(DefaultComparer.StringComparer);

                            string variableName = element.GetAttributeValueOrThrow(AttributeNames.Name);

                            if (variables.Contains(variableName))
                                Throw(ErrorMessages.ItemAlreadyDefined(ElementNames.Variable, variableName), element);

                            var variable = new Variable(
                                variableName,
                                element.GetAttributeValueOrThrow(AttributeNames.Value));

                            variables.Add(variable);
                            break;
                        }
                    case ElementKind.Property:
                        {
                            properties = properties ?? new ExtendedKeyedCollection<string, PropertyDefinition>();

                            string name = null;
                            bool isCollection = false;
                            bool isRequired = false;
                            string defaultValue = null;
                            string description = null;
                            char[] separators = PropertyDefinition.Tags.SeparatorsArray;

                            foreach (XAttribute attribute in element.Attributes())
                            {
                                switch (attribute.LocalName())
                                {
                                    case AttributeNames.Name:
                                        {
                                            name = attribute.Value;
                                            break;
                                        }
                                    case AttributeNames.IsCollection:
                                        {
                                            isCollection = bool.Parse(attribute.Value);
                                            break;
                                        }
                                    case AttributeNames.IsRequired:
                                        {
                                            isRequired = bool.Parse(attribute.Value);
                                            break;
                                        }
                                    case AttributeNames.DefaultValue:
                                        {
                                            defaultValue = attribute.Value;
                                            break;
                                        }
                                    case AttributeNames.Description:
                                        {
                                            description = attribute.Value;
                                            break;
                                        }
                                    case AttributeNames.Separators:
                                        {
                                            separators = ParseHelpers.ParseSeparators(attribute.Value);
                                            break;
                                        }
                                    default:
                                        {
                                            Throw(ErrorMessages.UnknownAttribute(attribute), element);
                                            break;
                                        }
                                }
                            }

                            if (properties.Contains(name))
                                Throw(ErrorMessages.ItemAlreadyDefined(ElementNames.Property, name), element);

                            if (isCollection
                                && defaultValue != null)
                            {
                                Throw(ErrorMessages.CollectionPropertyCannotDefineDefaultValue(), element);
                            }

                            if (PropertyDefinition.IsReservedName(name))
                                ThrowInvalidOperation(ErrorMessages.PropertyNameIsReserved(name), element);

                            var property = new PropertyDefinition(
                                name,
                                isCollection,
                                isRequired,
                                defaultValue,
                                description,
                                separators);

                            properties.Add(property);
                            break;
                        }
                    default:
                        {
                            ThrowOnUnknownElement(element);
                            break;
                        }
                }
            }
        }

        private Collection<Record> ReadWith()
        {
            if (_withElement == null)
                return null;

            var reader = new WithRecordReader(_withElement, Entity, Settings);

            Collection<Record> records = reader.ReadRecords();

            if (records == null)
                return null;

            return new ExtendedKeyedCollection<string, Record>(records.ToArray(), DefaultComparer.StringComparer);
        }

        public Collection<Record> Records()
        {
            if (_recordsElement == null)
                return null;

            var reader = new RecordReader(_recordsElement, Entity, Settings, ReadWith());

            return reader.ReadRecords();
        }

        public IEnumerable<EntityElement> EntityElements()
        {
            if (_entitiesElement == null)
                yield break;

            foreach (XElement element in _entitiesElement.Elements())
            {
                if (element.Kind() != ElementKind.Entity)
                    ThrowOnUnknownElement(element);

                yield return new EntityElement(element, Settings, Entity);
            }
        }

        private static void Throw(string message, XObject @object)
        {
            ThrowInvalidOperation(message, @object);
        }
    }
}
