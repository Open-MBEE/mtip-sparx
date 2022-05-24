/*
 * Copyright 2022 The Aerospace Corporation
 * 
 * Author: Karina Martinez
 * 
 * Description: Constants for types used to assign node and attribute names when building HUDS XML
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MTIP.Constants
{
    public class HUDSConstants
    {
        public string attributes;
        public string data;
        public string dict;
        public string dtype;
        public string ea;
        public string element;
        public string id;
        public string intType;
        public string isComposite;
        public string key;
        public string list;
        public string name;
        public string relationshipMetadata;
        public string relationships;
        public string status;
        public string str;
        public string type;
        public string typedBy;
        public string value;
        public HUDSConstants()
        {
            attributes = "attributes";
            data = "data";
            dict = "dict";
            dtype = "_dtype";
            ea = "ea";
            element = "element";
            id = "id";
            intType = "int";
            isComposite = "isComposite";
            key = "key";
            list = "list";
            name = "name";
            relationshipMetadata = "relationship_metadata";
            relationships = "relationships";
            status = "status";
            str = "str";
            type = "type";
            typedBy = "typedBy";
            value = "value";
        }
    }
}
