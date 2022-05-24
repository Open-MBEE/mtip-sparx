/*
 * Copyright 2022 The Aerospace Corporation
 * 
 * Author: Karina Martinez
 * 
 * Description: Constants for types used to assign Diagram types
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MTIP.Constants
{
    public class DiagramConstants
    {
        public string activity;
        public string classType;
        public string component;
        public string compositeStructure;
        public string custom;
        public string logical;
        public string package;
        public string sequence;
        public string statechart;
        public string useCase;
        public DiagramConstants()
        {
            activity = "Activity";
            classType = "Class";
            component = "Component";
            compositeStructure = "CompositeStructure";
            custom = "Custom";
            logical = "Logical";
            package = "Package";
            sequence = "Sequence";
            statechart = "Statechart";
            useCase = "Use Case";
        }
    }
}
