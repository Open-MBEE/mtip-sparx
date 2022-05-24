/*
 * Copyright 2022 The Aerospace Corporation
 * 
 * Author: Karina Martinez
 * 
 * Description: Constants for types used to assign Sequence element types
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MTIP.Constants
{
    public class SequenceConstants
    {
        public string collaboration;
        public string interaction;
        public string interactionFragment;
        public string interactionState;
        public string sequence;
        public SequenceConstants()
        {
            collaboration = "Collaboration";
            interaction = "Interaction";
            interactionFragment = "InteractionFragment";
            interactionState = "InteractionState";
            sequence = "Sequence";
        }
    }
}
