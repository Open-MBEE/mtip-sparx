/*
 * Copyright 2022 The Aerospace Corporation
 * 
 * Author: Karina Martinez
 * 
 * Description: Constants for types used to store property metadata for elements, 
 *              diagrams and connectors
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MTIP.Constants
{
    public class AttributeConstants
    {
        public string alias;
        public string attribute;
        public string attributes;
        public string behavior;
        public string body;
        public string constraint;
        public string defaultValue;
        public string displayAs;
        public string documentation;
        public string extensionPoint;
        public string id;
        public string initialValue;
        public string interactionOperatorKind;
        public string isComposite;
        public string messageSort;
        public string multiplicity;
        public string name;
        public string profileId;
        public string profileName;
        public string receiveEvent;
        public string relationships;
        public string sendEvent;
        public string stereotype;
        public string stereotypeName;
        public string submachine;
        public string taggedValue;
        public string text;
        public string type;
        public string value;
        public string visibility;

        public AttributeConstants()
        {
            this.alias = "alias";
            this.attribute = "attribute";
            this.attributes = "attributes";
            this.behavior = "behavior";
            this.body = "body";
            this.constraint = "constraint";
            this.defaultValue = "defaultValue";
            this.displayAs = "displayAs";
            this.documentation = "documentation";
            this.extensionPoint = "extensionPoint";
            this.id = "id";
            this.initialValue = "initialValue";
            this.interactionOperatorKind = "interactionOperatorKind";
            this.isComposite = "isComposite";
            this.messageSort = "messageSort";
            this.multiplicity = "multiplicity";
            this.name = "name";
            this.profileId = "profileId";
            this.profileName = "profileName";
            this.receiveEvent = "receiveEvent";
            this.relationships = "relationships";
            this.sendEvent = "sendEvent";
            this.stereotype = "stereotype";
            this.stereotypeName = "stereotypeName";
            this.submachine = "submachine";
            this.taggedValue = "taggedValue";
            this.text = "text";
            this.type = "type";
            this.value = "value";
            this.visibility = "visibility";
        }

    }
}
