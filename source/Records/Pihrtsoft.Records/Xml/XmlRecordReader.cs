// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using Pihrtsoft.Records.Utilities;
using static Pihrtsoft.Records.Utilities.ThrowHelper;

namespace Pihrtsoft.Records.Xml
{
    internal class XmlRecordReader
    {
        private XElement _documentElement;
        private XElement _entitiesElement;
        private XElement _entityElement;

        private readonly Queue<EntitiesInfo> _entities = new Queue<EntitiesInfo>();

        private XElement _declarationsElement;
        private XElement _withElement;
        private XElement _recordsElement;
        private XElement _childEntitiesElement;

        private EntityDefinition _entityDefinition;

        public XmlRecordReader(XDocument document, DocumentOptions options)
        {
            Document = document;
            Options = options;
        }

        public XDocument Document { get; }

        public DocumentOptions Options { get; }

        public ImmutableArray<Record>.Builder Records { get; } = ImmutableArray.CreateBuilder<Record>();

        public void ReadAll()
        {
            _documentElement = Document.FirstElement();

            if (_documentElement == null
                || !DefaultComparer.NameEquals(_documentElement, ElementNames.Document))
            {
                ThrowInvalidOperation(ErrorMessages.MissingElement(ElementNames.Document));
            }

            string versionText = _documentElement.AttributeValueOrDefault(AttributeNames.Version);

            if (versionText != null)
            {
                if (!Version.TryParse(versionText, out Version version))
                {
                    ThrowInvalidOperation(ErrorMessages.InvalidDocumentVersion());
                }
                else if (version > Pihrtsoft.Records.Document.SchemaVersion)
                {
                    ThrowInvalidOperation(ErrorMessages.DocumentVersionIsNotSupported(version, Pihrtsoft.Records.Document.SchemaVersion));
                }
            }

            foreach (XElement element in _documentElement.Elements())
            {
                switch (element.Kind())
                {
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

            if (_entitiesElement == null)
                return;

            _entities.Enqueue(new EntitiesInfo(_entitiesElement));

            while (_entities.Count > 0)
            {
                EntitiesInfo entities = _entities.Dequeue();

                foreach (XElement element in entities.Element.Elements())
                {
                    if (element.Kind() != ElementKind.Entity)
                        ThrowOnUnknownElement(element);

                    _entityElement = element;

                    ScanEntity();

                    ExtendedKeyedCollection<string, PropertyDefinition> properties = null;
                    ExtendedKeyedCollection<string, Variable> variables = null;

                    if (_declarationsElement != null)
                        ScanDeclarations(out properties, out variables);

                    _entityDefinition = CreateEntityDefinition(_entityElement, baseEntity: entities.BaseEntity, properties, variables);

                    if (_recordsElement != null)
                    {
                        var reader = new RecordReader(_entityDefinition, Options);

                        reader.ReadRecords(_recordsElement, _withElement);

                        Records.AddRange(reader.Records);
                    }

                    if (_childEntitiesElement != null)
                        _entities.Enqueue(new EntitiesInfo(_childEntitiesElement, _entityDefinition));

                    _entityDefinition = null;
                    _entityElement = null;
                    _declarationsElement = null;
                    _withElement = null;
                    _recordsElement = null;
                    _childEntitiesElement = null;
                }
            }
        }

        private void ScanEntity()
        {
            foreach (XElement element in _entityElement.Elements())
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
                            if (_childEntitiesElement != null)
                                ThrowOnMultipleElementsWithEqualName(element);

                            _childEntitiesElement = element;
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

        private void ScanDeclarations(out ExtendedKeyedCollection<string, PropertyDefinition> properties, out ExtendedKeyedCollection<string, Variable> variables)
        {
            properties = null;
            variables = null;

            foreach (XElement element in _declarationsElement.Elements())
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

        private static EntityDefinition CreateEntityDefinition(
            XElement element,
            EntityDefinition baseEntity = null,
            ExtendedKeyedCollection<string, PropertyDefinition> properties = null,
            ExtendedKeyedCollection<string, Variable> variables = null)
        {
            string name = element.GetAttributeValueOrThrow(AttributeNames.Name);

            if (baseEntity != null
                && properties != null)
            {
                foreach (PropertyDefinition property in properties)
                {
                    if (baseEntity.FindProperty(property.Name) != null)
                        ThrowInvalidOperation(ErrorMessages.PropertyAlreadyDefined(property.Name, name), element);
                }
            }

            return new EntityDefinition(name, baseEntity ?? EntityDefinition.Global, properties, variables);
        }

        private static void Throw(string message, XObject @object)
        {
            ThrowInvalidOperation(message, @object);
        }

        [DebuggerDisplay("{DebuggerDisplay,nq}")]
        private struct EntitiesInfo
        {
            public EntitiesInfo(XElement element, EntityDefinition baseEntity = null)
            {
                Element = element;
                BaseEntity = baseEntity;
            }

            public XElement Element { get; }

            public EntityDefinition BaseEntity { get; }

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            private string DebuggerDisplay => (BaseEntity != null) ? $"{BaseEntity.Name} {Element}" : Element?.ToString();
        }
    }
}
