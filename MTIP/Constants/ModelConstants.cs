/*
 * Copyright 2022 The Aerospace Corporation
 * 
 * Author: Karina Martinez
 * 
 * Description: Constants for types used to assign Model element types
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MTIP.Constants
{
    public class ModelConstants
    {
        public string documentation;
        public string model;
        public string profile;
        public string stereotype;
        public string text;
        public ModelConstants()
        {
            this.documentation = "documentation";
            this.model = "Model";
            this.profile = "profile";
            this.stereotype = "stereotype";
            this.text = "text";
        }
    }
}
