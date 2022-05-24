/*
 * Copyright 2022 The Aerospace Corporation
 * 
 * Author: Karina Martinez
 * 
 * Description: Constants for types used to assign MetaTypes
 * 
 * Note: Meta
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MTIP.Constants
{
    public class MetatypeConstants
    {
        public string acceptEventAction;
        public string callBehaviorAction;
        public string callOperationAction;
        public string createObjectAction;
        public string destroyObjectAction;
        public string inputPin;
        public string opaqueAction;
        public string outputPin;
        public string pseudostate;
        public string sendSignalAction;
        public MetatypeConstants()
        {
            acceptEventAction = "AcceptEventAction";
            callBehaviorAction = "CallBehaviorAction";
            callOperationAction = "CallOperationAction";
            createObjectAction = "CreateObjectAction";
            destroyObjectAction = "DestroyObjectAction";
            inputPin = "InputPin";
            opaqueAction = "OpaqueAction";
            outputPin = "OutputPin";
            pseudostate = "Pseudostate";
            sendSignalAction = "SendSignalAction";
        }
    }
}
