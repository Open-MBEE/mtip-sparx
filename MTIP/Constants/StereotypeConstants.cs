/*
 * Copyright 2022 The Aerospace Corporation
 * 
 * Author: Karina Martinez
 * 
 * Description: Constants for types used store stereotype properties for creating elements and 
 *              relationships
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MTIP.Constants
{
    public class StereotypeConstants
    {
        // Block Stereotypes
        public string adjunctProperty;
        public string allocated;
        public string block;
        public string boundReference;
        public string businessRequirement;
        public string classification;
        public string classifierBehaviorProperty;
        public string constraintBlock;
        public string constraintBlockCap;
        public string constraintParameter;
        public string constraintProperty;
        public string datastore;
        public string decision;
        public string domain;
        public string external;
        public string flowPort;
        public string flowProperty;
        public string flowSpecification;
        public string fork;
        public string fullPort;
        public string hardware;
        public string input;
        public string interfaceBlock;
        public string join;
        public string metaclass;
        public string model;
        public string navigationCell;
        public string objectiveFunction;
        public string objectStereotype;
        public string output;
        public string participantProperty;
        public string partProperty;
        public string profile;
        public string property;
        public string proxyPort;
        public string stereotype;
        public string subsystem;
        public string system;
        public string systemContext;
        public string valueProperty;
        public string valueType;
        public StereotypeConstants()
        {
            //Block Stereotypes
            constraintBlock = "constraintBlock";
            constraintBlockCap = "ConstraintBlock";
            input = "input";
            output = "output";
            navigationCell = "NavigationCell";
            stereotype = "stereotype";
            metaclass = "metaclass";
            valueType = "ValueType";
            property = "property";
            datastore = "datastore";
            block = "block";
            hardware = "hardware";
            boundReference = "BoundReference";
            valueProperty = "ValueProperty";
            participantProperty = "participantProperty";
            decision = "Decision";
            domain = "Domain";
            objectStereotype = "Object";
            constraintProperty = "constraintProperty";
            flowProperty = "FlowProperty";
            constraintParameter = "ConstraintParameter";
            classifierBehaviorProperty = "ClassifierBehaviorProperty";
            objectiveFunction = "objectiveFunction";
            interfaceBlock = "InterfaceBlock";
            system = "System";
            subsystem = "Subsystem";
            systemContext = "System context";
            businessRequirement = "BusinessRequirement";
            fork = "Fork";
            flowPort = "FlowPort";
            fullPort = "FullPort";
            proxyPort = "ProxyPort";
            flowSpecification = "flowSpecification";
            partProperty = "PartProperty";
            property = "property";
            classification = "Classification";
            allocated = "allocated";
            join = "Join";
            external = "External";
            model = "model";
            profile = "profile";
            adjunctProperty = "AdjunctProperty";
        }
    }
}
