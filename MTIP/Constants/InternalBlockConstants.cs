/*
 * Copyright 2022 The Aerospace Corporation
 * 
 * Author: Karina Martinez
 * 
 * Description: Constants for types used to assign Internal Block types
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MTIP.Constants
{
    public class InternalBlockConstants
    {
        public string boundary;
        public string boundReference;
        public string constraintProperty;
        public string flowProperty;
        public string participantProperty;
        public string property;
        public string referenceProperty;

        public InternalBlockConstants()
        {
            boundary = "Boundary";
            boundReference = "BoundReference";
            constraintProperty = "ConstraintProperty";
            flowProperty = "FlowProperty";
            participantProperty = "ParticipantProperty";
            property = "Property";
            referenceProperty = "ReferenceProperty";
        }
    }
}
