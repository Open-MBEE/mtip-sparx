/*
 * Copyright 2022 The Aerospace Corporation
 * 
 * Author: Karina Martinez
 * 
 * Description: Constants for values that do not fall within any diagram element type
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MTIP.Constants
{
    public class ProfileConstants
    {
        public string classType;
        public string constraint;
        public string exceptionHandler;
        public string note;
        public string objectiveFunction;
        public string objectType;
        public string package;
        public string part;
        public string text;

        public ProfileConstants(){
            classType = "Class";
            constraint = "Constraint";
            exceptionHandler = "ExceptionHandler";
            note = "Note";
            objectiveFunction = "ObjectiveFunction";
            objectType = "Object";
            package = "Package";
            part = "Part";
            text = "Text";
        }

    }
}
