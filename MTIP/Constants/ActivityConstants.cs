/*
 * Copyright 2022 The Aerospace Corporation
 * 
 * Author: Karina Martinez
 * 
 * Description: Constants for types used to assign Activity element types
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MTIP.Constants
{
    public class ActivityConstants
    {
        public string acceptEventAction;
        public string action;
        public string actionPin;
        public string activity;
        public string activityParameter;
        public string activityPartion;
        public string callBehaviorAction;
        public string callOperationAction;
        public string centralBufferNode;
        public string change;
        public string conditionalNode;
        public string createObjectAction;
        public string decision;
        public string destroyObjectAction;
        public string eventType;
        public string forkNode;
        public string informationItem;
        public string interruptibleActivityRegion;
        public string mergeNode;
        public string objectNode;
        public string property;
        public string sendSignalAction;
        public string stateNode;
        public string synchronization;


        public ActivityConstants(){
            acceptEventAction = "AcceptEventAction";
            action = "Action";
            actionPin = "ActionPin";
            activity = "Activity";
            activityParameter = "ActivityParameter";
            activityPartion = "ActivityPartition";
            callBehaviorAction = "CallBehaviorAction";
            callOperationAction = "CallOperationAction";
            centralBufferNode = "CentralBufferNode";
            change = "Change";
            conditionalNode = "ConditionalNode";
            createObjectAction = "CreateObjectAction";
            decision = "Decision";
            destroyObjectAction = "DestroyObjectAction";
            eventType = "Event";
            forkNode = "ForkNode";
            informationItem = "InformationItem";
            interruptibleActivityRegion = "InterruptibleActivityRegion";
            mergeNode = "MergeNode";
            objectNode = "ObjectNode";
            property = "Property";
            sendSignalAction = "SendSignalAction";
            stateNode = "StateNode";
            synchronization = "Synchronization";
        }
    }
}
