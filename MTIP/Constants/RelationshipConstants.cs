/*
 * Copyright 2022 The Aerospace Corporation
 * 
 * Author: Karina Martinez
 * 
 * Description: Constants for types used to create connectors/relationships between elements
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MTIP.Constants
{
    public class RelationshipConstants
    {
        public string asynchCall;
        public string asynchSignal;
        public string classifiedBy;
        public string client;
        public string compositeDiagram;
        public string diagramConnector;
        public string effect;
        public string element;
        public string guard;
        public string hasParent;
        public string hyperlink;
        public string id;
        public string messageNumber;
        public string metaclass;
        public string metarelationship;
        public string profile;
        public string relationships;
        public string relMetadata;
        public string relMetadataBottom;
        public string relMetadataLeft;
        public string relMetadataRight;
        public string relMetadataSeq;
        public string relMetadataTop;
        public string reply;
        public string signalGuid;
        public string signature;
        public string supplier;
        public string synchCall;
        public string trigger;
        public string type;
        public string typedBy;
        public string valueSpecification;

        public RelationshipConstants()
        {
            this.asynchCall = "asynchCall";
            this.asynchSignal = "asynchSignal";
            this.classifiedBy = "classifiedBy";
            this.client = "client";
            this.compositeDiagram = "compositeDiagram";
            this.diagramConnector = "diagramConnector";
            this.effect = "effect";
            this.element = "element";
            this.guard = "guard";
            this.hasParent = "hasParent";
            this.hyperlink = "hyperlink";
            this.id = "id";
            this.messageNumber = "messageNumber";
            this.metaclass = "metaclass";
            this.metarelationship = "metarelationship";
            this.profile = "profile";
            this.relationships = "relationships";
            this.relMetadata = "relationship_metadata";
            this.relMetadataBottom = "bottom";
            this.relMetadataLeft = "left";
            this.relMetadataRight = "right";
            this.relMetadataSeq = "sequence";
            this.relMetadataTop = "top";
            this.reply = "reply";
            this.signalGuid = "signal_guid";
            this.signature = "signature";
            this.supplier = "supplier";
            this.synchCall = "synchCall";
            this.trigger = "trigger";
            this.type = "type";
            this.typedBy = "typedBy";
            this.valueSpecification = "valueSpecification";
        }
    }
}
