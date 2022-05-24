/*
 * Copyright 2022 The Aerospace Corporation
 * 
 * Author: Karina Martinez
 * 
 * Description: Constants for types used to assign Block types
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MTIP.Constants
{
    public class BlockConstants
    {
        public string association;
        public string block;
        public string constraintBlock;
        public string dataType;
        public string enumeration;
        public string external;
        public string flowPort;
        public string fullPort;
        public string interfaceBlock;
        public string interfaceType;
        public string operation;
        public string port;
        public string property;
        public string proxyPort;
        public string quantityKind;
        public string signal;
        public string unit;
        public string valueType;
        public BlockConstants()
        {
            association = "Association";
            block = "Block";
            constraintBlock = "ConstraintBlock";
            dataType = "DataType";
            enumeration = "Enumeration";
            external = "External";
            flowPort = "FlowPort";
            fullPort = "FullPort";
            interfaceBlock = "InterfaceBlock";
            interfaceType = "Interface";
            operation = "Operation";
            port = "Port";
            property = "Property";
            proxyPort = "ProxyPort";
            quantityKind = "QuantityKind";
            signal = "Signal";
            unit = "Unit";
            valueType = "ValueType";
        }
    }
}
