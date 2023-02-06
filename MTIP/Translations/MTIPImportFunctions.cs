/*
 * Copyright 2022 The Aerospace Corporation
 * 
 * Author: Karina Martinez
 * 
 * Description: Funtions used to import HUDS XML files as EA model
 * 
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using EA;
using MTIP.Constants;
using MTIP.Models;
using MTIP.Utilities;

namespace MTIP.Translations
{
    public class MTIPImportFunctions
    {
        public MTIP plugin;
        public List<string> selected_file_list;
        public EA.Repository repository;
        public EA.Package rootPkg;
        public List<string> exportLog;
        public Dictionary<string, XmlItem> compositeDiagramsToAdd;
        public List<string> headModelElements;
        public Dictionary<string, XmlItem> relationshipElements;
        public Dictionary<string, List<string>> parentMapping;
        public Dictionary<string, XmlItem> parsedXml;
        public List<GlossaryTerm> terms;
        public Dictionary<string, XmlItem> diagramElements;
        public Dictionary<string, XmlItem> classifiersToAdd;
        public List<string> orphanedIds;
        public Dictionary<string, XmlItem> propertyTypesToAdd;
        public Dictionary<string, XmlItem> constraintsToAdd;
        public Dictionary<string, List<XmlItem>> opaqueExpressionsToAdd;
        public Dictionary<string, XmlItem> hyperLinksToAdd;
        public Dictionary<string, XmlItem> actionsToAdd;
        public EA.Package orphanedPackage;

        public MTIPImportFunctions(MTIP plugin, List<string> selected_file_list)
        {
            this.plugin = plugin;
            this.selected_file_list = selected_file_list;
            repository = plugin.GetRepository();
            rootPkg = repository.GetTreeSelectedPackage();
            exportLog = new List<string>();
            compositeDiagramsToAdd = new Dictionary<string, XmlItem>();
            headModelElements = new List<string>();
            relationshipElements = new Dictionary<string, XmlItem>();
            parentMapping = new Dictionary<string, List<string>>();
            parsedXml = new Dictionary<string, XmlItem>();
            terms = new List<GlossaryTerm>();
            diagramElements = new Dictionary<string, XmlItem>();
            classifiersToAdd = new Dictionary<string, XmlItem>();
            orphanedIds = new List<string>();
            propertyTypesToAdd = new Dictionary<string, XmlItem>();
            constraintsToAdd = new Dictionary<string, XmlItem>();
            opaqueExpressionsToAdd = new Dictionary<string, List<XmlItem>>();
            hyperLinksToAdd = new Dictionary<string, XmlItem>();
            actionsToAdd = new Dictionary<string, XmlItem>();
        }

        // Initiate creation of HUDS XML for model to be exported
        public void StartMTIPImport()
        {
            exportLog.Add("Errors while importing:");

            System.Xml.XmlDocument first_xml = new XmlDocument();
            first_xml.Load(selected_file_list[0]);
            XmlNode core_xml_node = first_xml.SelectSingleNode("packet");

            // if there is more than one file, iterate over the additional files and add children under "packet"
            if (selected_file_list.Count() > 1)
            {
                foreach (string filename in selected_file_list.Skip(1))
                {
                    XmlDocument add_xml = new XmlDocument();
                    add_xml.Load(filename);

                    XmlNode packet_node = add_xml.SelectSingleNode("packet");

                    foreach (XmlNode node in packet_node)
                    {
                        XmlNode import_node = core_xml_node.OwnerDocument.ImportNode(node, true);
                        core_xml_node.AppendChild(import_node);
                    }
                }
            }
            MessageBox.Show("Beginning import. This may take a while.", "Beginning Import", MessageBoxButtons.OK);

            // Create XmlItem for each data node in all packets
            GetSysmlMTIPModel(rootPkg, core_xml_node);

            //System.IO.File.WriteAllLines("MTIPLog_" + date + ".txt", exportLog.ToArray());

            // Save import log
            XmlWriterSettings settings = new XmlWriterSettings()
            {
                CheckCharacters = false
            };

            string outputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            XmlDocument exportDocument = new XmlDocument();
            exportDocument.LoadXml("<exportLog></exportLog>");

            foreach (string logLine in exportLog)
            {
                XmlElement logElement = exportDocument.CreateElement("log");
                logElement.InnerText = logLine;
                exportDocument.DocumentElement.AppendChild(logElement);
            }
            //outputDirectory = outputDirectory.Replace("\\", "/");
            string date = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            string importedLname = Path.Combine(outputDirectory + "\\", "Documents\\MTIP_EA_ImportLog" + date + ".xml");
            XmlWriter xmlEWriter = XmlWriter.Create(importedLname, settings);
            exportDocument.Save(xmlEWriter);
            xmlEWriter.Close();

            exportLog = new List<string>();
        }
        //
        internal void GetSysmlMTIPModel(EA.Package rootPkg, XmlNode packet)
        {
            parsedXml = BuildModelDictionary(packet);
            BuildModel(rootPkg);
            AddClassifiers();
            AddConnectors();
            AddPropertyTypes();
            AddConstraints();
            //AddActionTypes();
            BuildDiagrams();
            AddHyperlinks();
            AddCompositeDiagrams();

            rootPkg.Update();

            // Clear all dictionaries to prep for another import if necessary
            parentMapping = new Dictionary<string, List<string>>();
            headModelElements = new List<string>();
            parsedXml = new Dictionary<string, XmlItem>();
            classifiersToAdd = new Dictionary<string, XmlItem>();
            constraintsToAdd = new Dictionary<string, XmlItem>();
            relationshipElements = new Dictionary<string, XmlItem>();
            diagramElements = new Dictionary<string, XmlItem>();
        }

        // Iterate through provided HUDS XML and create dictionary of XmlItems in the model
        internal Dictionary<string, Models.XmlItem> BuildModelDictionary(XmlNode packet)
        {
            Dictionary<string, Models.XmlItem> modelElements = new Dictionary<string, Models.XmlItem>();
           
            foreach (XmlNode dataNode in packet)
            {
                XmlItem modelElement = new XmlItem();

                foreach (XmlNode fieldNode in dataNode)
                {
                    // get model element type (ex. Sysml.Package)
                    if (fieldNode.Name == AttributeConstants.TYPE)
                    {
                        string type = fieldNode.InnerText;
                        string element = type.Split('.')[1];
                        modelElement.SetType(element);
                    }

                    //get ID associated with each model element (for now, EA GUID)
                    if (fieldNode.Name == AttributeConstants.ID)
                    {
                        if (fieldNode.SelectSingleNode("ea") != null) modelElement.SetMappingID(dataNode.SelectSingleNode("id").SelectSingleNode("ea").InnerText);
                        else if (fieldNode.SelectSingleNode("cameo") != null) modelElement.SetMappingID(dataNode.SelectSingleNode("id").SelectSingleNode("cameo").InnerText);
                        else if (fieldNode.SelectSingleNode("ontology") != null) modelElement.SetMappingID(dataNode.SelectSingleNode("id").SelectSingleNode("ontology").InnerText);
                        else if (fieldNode.SelectSingleNode("sysml") != null) modelElement.SetMappingID(dataNode.SelectSingleNode("id").SelectSingleNode("sysml").InnerText);
                    }

                    // Loop through attributes
                    if (fieldNode.Name == AttributeConstants.ATTRIBUTES)
                    {
                        modelElement = GetAttributes(fieldNode, modelElement);
                    }
                    if (fieldNode.Name == AttributeConstants.RELATIONSHIPS)
                    {
                        modelElement = GetRelationships(fieldNode, modelElement);
                    }
                }
                if ((modelElement.GetParent() == "" || modelElement.GetElementType() == SysmlConstants.MODEL) && !SysmlConstants.SYSMLRELATIONSHIPS.Contains(modelElement.GetElementType()))
                {
                    headModelElements.Add(modelElement.GetMappingID());
                }
                if (SysmlConstants.SYSMLRELATIONSHIPS.Contains(modelElement.GetElementType()))
                {
                    if (!relationshipElements.ContainsKey(modelElement.GetMappingID())) relationshipElements.Add(modelElement.GetMappingID(), modelElement);
                }
                if (!parentMapping.ContainsKey(modelElement.GetParent()))
                {
                    List<string> childElements = new List<string>();
                    childElements.Add(modelElement.GetMappingID());
                    parentMapping.Add(modelElement.GetParent(), childElements);
                }
                else parentMapping[modelElement.GetParent()].Add(modelElement.GetMappingID());
                if (!modelElements.ContainsKey(modelElement.GetMappingID())) modelElements.Add(modelElement.GetMappingID(), modelElement);

            }

            return modelElements;
        }

        // Begin building model
        internal void BuildModel(EA.Package rootPkg)
        {
            
            try
            {
                if (terms.Count > 0)
                {
                    foreach (GlossaryTerm term in terms)
                    {
                        Term newTerm = repository.Terms.AddNew(term.GetName(), term.GetTermType());
                        newTerm.Meaning = term.GetMeaning();
                    }
                    repository.Terms.Refresh();
                }
                foreach (string headCameoId in headModelElements)
                {
                    XmlItem headModelItem = parsedXml[headCameoId];
                    string name;
                    if (headModelItem.GetName() == "")
                    {
                        name = "Model";
                        headModelItem.AddAttribute(ModelConstants.STEREOTYPE, ModelConstants.MODEL);
                    }
                    else name = headModelItem.GetName();
                    EA.Package modelPkg = rootPkg.Packages.AddNew(name, "");
                    modelPkg.Update();

                    if (headModelItem.GetAttributes().ContainsKey(ModelConstants.STEREOTYPE) && headModelItem.GetAttribute(ModelConstants.STEREOTYPE) == ModelConstants.MODEL)
                    {
                        modelPkg.Element.Stereotype = ModelConstants.MODEL;
                    }
                    if (headModelItem.GetAttributes().ContainsKey(ModelConstants.STEREOTYPE) && headModelItem.GetAttribute(ModelConstants.STEREOTYPE) == ModelConstants.PROFILE)
                    {
                        modelPkg.Element.Stereotype = ModelConstants.PROFILE;
                    }
                    string notes = "";
                    if (headModelItem.GetAttributes().ContainsKey(ModelConstants.DOCUMENTATION))
                    {
                        notes += headModelItem.GetAttribute(ModelConstants.DOCUMENTATION);
                    }
                    if (headModelItem.GetAttributes().ContainsKey(ModelConstants.TEXT))
                    {
                        notes = " - " + headModelItem.GetAttribute(ModelConstants.TEXT);
                    }
                    modelPkg.Notes = notes;
                    modelPkg.Update();

                    parsedXml[headCameoId].SetEAID(modelPkg.PackageGUID);

                    GetChildren(headCameoId);
                }
            }
            catch
            {
                exportLog.Add("Could not build model. Please make sure there is at least one data element without hasParent relationship in the XML");
            }
        }
        internal void GetChildren(string parentId)
        {
            if (parentMapping.ContainsKey(parentId))
            {

                XmlItem parentModelElement = parsedXml[parentId];
                List<string> childElementIds = parentMapping[parentId];
                // Add model packages
                if (parentModelElement.GetElementType() == SysmlConstants.PACKAGE || parentModelElement.GetElementType() == SysmlConstants.PROFILE ||
                    parentModelElement.GetCategory() == SysmlConstants.MODEL || parentModelElement.GetElementType() == SysmlConstants.MODEL)
                {
                    foreach (string childElementId in childElementIds)
                    {
                        XmlItem childElement = parsedXml[childElementId];

                        // Add element to package
                        if (childElement.GetCategory() == SysmlConstants.ELEMENT && childElement.GetElementType() != SysmlConstants.PACKAGE)
                        {
                            if (childElement.GetElementType() == SysmlConstants.HYPERLINK)
                            {
                                hyperLinksToAdd.Add(childElement.GetMappingID(), childElement);
                            }
                            else
                            {
                                AddPkgElement(parentId, childElementId);
                            }
                            GetChildren(childElementId);
                        }
                        //Add package to package
                        else if (childElement.GetElementType() == SysmlConstants.PACKAGE || childElement.GetElementType() == SysmlConstants.PROFILE)
                        {
                            AddPkgPkg(parentId, childElementId);
                            GetChildren(childElementId);
                        }
                        else if (childElement.GetElementType() == SysmlConstants.STEREOTYPE)
                        {
                            AddPkgElement(parentId, childElementId);
                            GetChildren(childElementId);
                        }
                        // Add diagram to list
                        else if (childElement.GetCategory() == SysmlConstants.DIAGRAM)
                        {
                            diagramElements.Add(childElement.GetMappingID(), childElement);
                        }
                    }
                }
                else if (parentModelElement.GetCategory() == SysmlConstants.ELEMENT && parentModelElement.GetElementType() != SysmlConstants.PACKAGE)
                {
                    foreach (string childElementId in childElementIds)
                    {
                        XmlItem childElement = parsedXml[childElementId];
                        // Add element to element
                        if (childElement.GetElementType() == SysmlConstants.CONSTRAINT)
                        {

                            constraintsToAdd.Add(childElement.GetMappingID(), childElement);
                            GetChildren(childElementId);


                        }
                        else if (childElement.GetElementType() == SysmlConstants.OPAQUEEXPRESSION)
                        {
                            if (opaqueExpressionsToAdd.ContainsKey(childElement.GetParent())) opaqueExpressionsToAdd[childElement.GetParent()].Add(childElement);
                            else
                            {
                                List<XmlItem> opaqueList = new List<XmlItem>();
                                opaqueList.Add(childElement);
                                opaqueExpressionsToAdd.Add(childElement.GetParent(), opaqueList);
                            }
                        }
                        else if (childElement.GetElementType() == SysmlConstants.HYPERLINK)
                        {
                            hyperLinksToAdd.Add(childElement.GetParent(), childElement);

                        }
                        else if (childElement.GetCategory() == SysmlConstants.ELEMENT && childElement.GetElementType() != SysmlConstants.PACKAGE)
                        {
                            AddElementElement(parentId, childElementId);

                            GetChildren(childElementId);
                        }
                        else if (childElement.GetCategory() == SysmlConstants.DIAGRAM)
                        {
                            try
                            {
                                diagramElements.Add(childElement.GetMappingID(), childElement);
                            }
                            catch
                            {
                                exportLog.Add("Diagram " + childElement.GetMappingID() + " not found XML");
                                //Tools.Log("Diagram " + childElement.GetMappingID() + " exist in another XML");
                            }
                        }
                        else if (childElement.GetCategory() == SysmlConstants.RELATIONSHIP && !relationshipElements.ContainsKey(childElement.GetMappingID()))
                        {
                            relationshipElements.Add(childElement.GetMappingID(), childElement);
                        }
                    }
                }
            }
        }

        internal XmlItem GetAttributes(XmlNode fieldNode, XmlItem modelElement)
        {
            foreach (XmlNode attribute in fieldNode)
            {
                try
                {
                    if (attribute.Attributes["key"].Value == AttributeConstants.ATTRIBUTE)
                    {
                        foreach (XmlNode attributeAttrib in attribute)
                        {
                            Dictionary<string, string> attributeAttributes = new Dictionary<string, string>();
                            string name = "";
                            foreach (XmlNode attribAttrib in attributeAttrib)
                            {
                                if (attribAttrib.Attributes["key"].Value == AttributeConstants.TYPE) attributeAttributes.Add(AttributeConstants.TYPE, GetInnerAttribute(attribAttrib));
                                if (attribAttrib.Attributes["key"].Value == AttributeConstants.INITIALVALUE) attributeAttributes.Add(AttributeConstants.INITIALVALUE, GetInnerAttribute(attribAttrib));
                                if (attribAttrib.Attributes["key"].Value == AttributeConstants.VISIBILITY) attributeAttributes.Add(AttributeConstants.VISIBILITY, GetInnerAttribute(attribAttrib));
                                if (attribAttrib.Attributes["key"].Value == AttributeConstants.STEREOTYPE) attributeAttributes.Add(AttributeConstants.STEREOTYPE, GetInnerAttribute(attribAttrib));
                                if (attribAttrib.Attributes["key"].Value == AttributeConstants.ALIAS) attributeAttributes.Add(AttributeConstants.ALIAS, GetInnerAttribute(attribAttrib));
                                if (attribAttrib.Attributes["key"].Value == AttributeConstants.NAME) name = GetInnerAttribute(attribAttrib); ;
                            }
                            modelElement.AddElementAttribute(name, attributeAttributes);
                        }
                    }
                    else if (attribute.Attributes["key"].Value == AttributeConstants.STEREOTYPE)
                    {
                        foreach (XmlNode attribStereotype in attribute)
                        {
                            if (attribStereotype.Attributes["key"].Value == AttributeConstants.STEREOTYPENAME)
                            {
                                if (attribStereotype.InnerText == "Term") GetTerm(fieldNode);
                                if (!modelElement.GetAttributes().ContainsKey(AttributeConstants.STEREOTYPE)) modelElement.AddAttribute(AttributeConstants.STEREOTYPE, attribStereotype.InnerText);
                            }
                            if (attribStereotype.Attributes["key"].Value == AttributeConstants.PROFILEID) modelElement.SetProfileId(attribStereotype.InnerText);
                            if (attribStereotype.Attributes["key"].Value == AttributeConstants.PROFILENAME) modelElement.SetProfileName(attribStereotype.InnerText);
                        }
                    }
                    else if (attribute.Attributes["key"].Value == AttributeConstants.NAME)
                    {
                        modelElement.AddAttribute(AttributeConstants.NAME, GetInnerAttribute(attribute));
                    }
                    else if (attribute.Attributes["key"].Value == AttributeConstants.DOCUMENTATION)
                    {
                        modelElement.AddAttribute(AttributeConstants.DOCUMENTATION, GetInnerAttribute(attribute));
                    }
                    else if (attribute.Attributes["key"].Value == AttributeConstants.DISPLAYAS)
                    {
                        modelElement.AddAttribute(AttributeConstants.DISPLAYAS, GetInnerAttribute(attribute));
                    }
                    else if (attribute.Attributes["key"].Value == AttributeConstants.CONSTRAINT)
                    {
                        modelElement.AddConstraint(attribute.InnerText, GetInnerAttribute(attribute));
                    }
                    else if (attribute.Attributes["key"].Value == AttributeConstants.TAGGEDVALUE)
                    {
                        foreach (XmlNode taggedValue in attribute)
                        {
                            modelElement.AddTaggedValue(taggedValue.Attributes["key"].Value, GetInnerAttribute(taggedValue));
                        }
                    }
                    else if (attribute.Attributes["key"].Value == AttributeConstants.BEHAVIOR)
                    {
                        foreach (XmlNode attr in attribute)
                        {
                            Behavior behavior = new Behavior();
                            foreach (XmlNode method in attr)
                            {
                                if (method.Attributes["key"].Value == AttributeConstants.NAME) behavior.SetName(method.InnerText);
                                if (method.Attributes["key"].Value == AttributeConstants.TYPE) behavior.SetType(method.InnerText);
                                if (method.Attributes["key"].Value == AttributeConstants.VALUE) behavior.SetValue(method.InnerText);
                                modelElement.AddBehavior(behavior);
                            }
                        }

                    }
                    else if (attribute.Attributes["key"].Value == AttributeConstants.RECEIVEEVENT)
                    {
                        modelElement.SetClient(GetInnerAttribute(attribute));
                    }
                    else if (attribute.Attributes["key"].Value == AttributeConstants.SENDEVENT)
                    {
                        modelElement.SetSupplier(GetInnerAttribute(attribute));
                    }
                    else
                    {
                        if (modelElement.GetAttributes().ContainsKey(attribute.Name))
                        {
                            modelElement.SetAttribute(attribute.Attributes["key"].Value, GetInnerAttribute(attribute));
                        }
                        else
                        {
                            modelElement.AddAttribute(attribute.Attributes["key"].Value, GetInnerAttribute(attribute));
                        }
                    }
                }
                catch
                {
                    exportLog.Add("Unable to add element attribute: Element Name= " + modelElement.GetName() + " - Element ID= " + modelElement.GetMappingID());
                }
            }
            return modelElement;
        }
        internal string GetInnerAttribute(XmlNode attribute)
        {
            string value = "";
           
            foreach (XmlNode attrib in attribute)
            {
                if (attrib.Attributes["key"].Value == AttributeConstants.VALUE)
                {
                    value = attrib.InnerText;
                }
            }
            return value;
        }
        internal XmlItem GetRelationships(XmlNode fieldNode, XmlItem modelElement)
        {
            foreach (XmlNode relationship in fieldNode)
            {
                try
                {
                    if (relationship.Name == RelationshipConstants.HASPARENT)
                    {
                        foreach (XmlNode hasParent in relationship)
                        {
                            if (hasParent.Name == RelationshipConstants.ID) modelElement.SetParent(hasParent.InnerText);
                        }
                    }
                    if (relationship.Name == RelationshipConstants.ELEMENT)
                    {
                        foreach (XmlNode element in relationship)
                        {

                            DiagramObjectItem diagramObject = new DiagramObjectItem();
                            foreach (XmlNode elementItem in element)
                            {
                                if (elementItem.Name == RelationshipConstants.ID)
                                {
                                    diagramObject.SetMappingId(elementItem.InnerText);
                                }
                                if (elementItem.Name == RelationshipConstants.RELMETADATA)
                                {
                                    foreach (XmlNode metadata in elementItem)
                                    {
                                        if (metadata.Name == RelationshipConstants.RELMETADATATOP) diagramObject.SetTopCoor(metadata.InnerText);
                                        if (metadata.Name == RelationshipConstants.RELMETADATALEFT) diagramObject.SetLeftCoor(metadata.InnerText);
                                        if (metadata.Name == RelationshipConstants.RELMETADATABOTTOM) diagramObject.SetBottomCoor(metadata.InnerText);
                                        if (metadata.Name == RelationshipConstants.RELMETADATARIGHT) diagramObject.SetRightCoor(metadata.InnerText);
                                        if (metadata.Name == RelationshipConstants.RELMETADATASEQ) diagramObject.SetSequenceCoor(metadata.InnerText);
                                    }
                                }

                            }
                            modelElement.AddDiagramObjects(diagramObject);
                        }
                    }
                    if (relationship.Name == RelationshipConstants.DIAGRAMCONNECTOR)
                    {
                        foreach (XmlNode diagramLinkElem in relationship)
                        {
                            DiagramLinkItem diagramLink = new DiagramLinkItem();
                            foreach (XmlNode diagramConnElem in diagramLinkElem)
                            {
                                if (diagramConnElem.Name == RelationshipConstants.ID) diagramLink.SetMappingId(diagramConnElem.InnerText);
                                if (diagramConnElem.Name == RelationshipConstants.RELMETADATA)
                                {
                                    foreach (XmlNode relMetadata in diagramConnElem)
                                    {
                                        if (relMetadata.Name == RelationshipConstants.MESSAGENUMBER) diagramLink.SetSequence(relMetadata.InnerText);
                                    }

                                }
                            }
                            modelElement.AddDiagramLinks(diagramLink);
                        }

                    }
                    if (relationship.Name == RelationshipConstants.TYPEDBY)
                    {
                        foreach (XmlNode typedBy in relationship) if (typedBy.Name == RelationshipConstants.ID) modelElement.SetTypedBy(typedBy.InnerText);
                    }
                    if (relationship.Name == RelationshipConstants.CLASSIFIEDBY)
                    {
                        foreach (XmlNode classifiedBy in relationship) if (classifiedBy.Name == RelationshipConstants.ID) modelElement.SetClassifiedBy(classifiedBy.InnerText);
                    }
                    if (relationship.Name == RelationshipConstants.VALUESPECIFICATION)
                    {
                        foreach (XmlNode valueSpecification in relationship) if (valueSpecification.Name == RelationshipConstants.ID) modelElement.SetValueSpecification(valueSpecification.InnerText);
                    }
                    if (relationship.Name == RelationshipConstants.CLIENT)
                    {
                        foreach (XmlNode client in relationship) if (client.Name == RelationshipConstants.ID) modelElement.SetClient(client.InnerText);
                    }
                    if (relationship.Name == RelationshipConstants.SUPPLIER)
                    {
                        foreach (XmlNode supplier in relationship) if (supplier.Name == RelationshipConstants.ID) modelElement.SetSupplier(supplier.InnerText);
                    }
                    if (relationship.Name == RelationshipConstants.COMPOSITEDIAGRAM)
                    {
                        foreach (XmlNode compositeDiagram in relationship) if (compositeDiagram.Name == RelationshipConstants.ID) modelElement.SetCompositeDiagram(compositeDiagram.InnerText);
                        compositeDiagramsToAdd.Add(modelElement.GetMappingID(), modelElement);
                    }
                    if (relationship.Name == RelationshipConstants.SIGNATURE)
                    {
                        foreach (XmlNode signal in relationship) if (signal.Name == RelationshipConstants.ID) modelElement.SetSignal(signal.InnerText);

                    }
                    if (relationship.Name == RelationshipConstants.PROFILE)
                    {
                        foreach (XmlNode profile in relationship)
                        {
                            if (profile.Name == RelationshipConstants.ID) modelElement.SetProfileId(profile.InnerText);
                            if (profile.Name == RelationshipConstants.PROFILE) modelElement.SetProfile(profile.InnerText);
                        }
                    }
                    if (relationship.Name == RelationshipConstants.HYPERLINK)
                    {
                        foreach (XmlNode hyperlink in relationship)
                        {
                            if (hyperlink.Name == RelationshipConstants.ID) modelElement.SetHyperlink(hyperlink.InnerText);
                            if (hyperlink.Name == RelationshipConstants.TYPE) modelElement.SetHyperlinkType(hyperlink.InnerText);
                        }
                    }
                }
                catch
                {
                    exportLog.Add("Unable to add connector: Connector Name= " + modelElement.GetName() + " - Connector ID= " + modelElement.GetMappingID());
                }
            }
            return modelElement;
        }
        internal void AddPkgPkg(string parentId, string childId)
        {
            try
            {
                EA.Package parentPkg = repository.GetPackageByGuid(parsedXml[parentId].GetEAID());
                XmlItem childItem = parsedXml[childId];

                if (childItem.GetAttributes().ContainsKey(AttributeConstants.DISPLAYAS))
                {
                    if (childItem.GetAttribute(AttributeConstants.DISPLAYAS) == "Diagram") diagramElements.Add(childItem.GetMappingID(), childItem);
                }
                else if (childItem.GetElementType() == SysmlConstants.PROFILE)
                {
                    EA.Package childPkg = parentPkg.Packages.AddNew(childItem.GetAttribute(AttributeConstants.NAME), "");
                    childPkg.Update();
                    childPkg.Element.Stereotype = "Profile";
                    childPkg.Update();
                    parsedXml[childId].SetEAID(childPkg.PackageGUID);
                }
                else
                {
                    EA.Package childPkg = parentPkg.Packages.AddNew(childItem.GetAttribute(AttributeConstants.NAME), "");
                    string notes = "";
                    if (childItem.GetAttributes().ContainsKey(AttributeConstants.DOCUMENTATION))
                    {
                        notes += childItem.GetAttribute(AttributeConstants.DOCUMENTATION);
                    }
                    if (childItem.GetAttributes().ContainsKey(AttributeConstants.TEXT))
                    {
                        notes = " - " + childItem.GetAttribute(AttributeConstants.TEXT);
                    }
                    childPkg.Notes = notes;
                    childPkg.Update();
                    parsedXml[childId].SetEAID(childPkg.PackageGUID);
                }
            }
            catch
            {
                exportLog.Add("Could not add package: Package GUID: " + childId);
            }
        }
        internal void AddPkgElement(string parentId, string childId)
        {
            
            XmlItem childItem = parsedXml[childId];
            string stereotype = "";
            if (childItem.GetAttributes().ContainsKey(AttributeConstants.STEREOTYPE)) stereotype = childItem.GetAttribute(AttributeConstants.STEREOTYPE);
            if (GetEAType(childItem.GetElementType(), stereotype) != "")
            {
                try
                {
                    if (childItem.GetAttributes().ContainsKey(AttributeConstants.DISPLAYAS))
                    {
                        if (childItem.GetAttribute(AttributeConstants.DISPLAYAS) == "Diagram") diagramElements.Add(childItem.GetMappingID(), childItem);
                    }
                    else
                    {
                        string eaType = "";
                        EA.Package parentPkg = repository.GetPackageByGuid(parsedXml[parentId].GetEAID());
                        EA.Element childElement = parentPkg.Elements.AddNew(childItem.GetName(), GetEAType(childItem.GetElementType(), stereotype));

                        if (childItem.GetAttributes().ContainsKey(AttributeConstants.DOCUMENTATION))
                        {
                            childElement.Notes = childItem.GetAttribute(AttributeConstants.DOCUMENTATION);
                        }
                        GetElement(childElement, childItem, childId, SysmlConstants.SYSMLPACKAGE);
                    }
                }
                catch
                {
                    // Add reporting here
                    exportLog.Add("Unable to add element " + childItem.GetName() + ": Element ID: " + childItem.GetMappingID());
                }

            }
            else
            {
                // Add reporting here
                exportLog.Add("Unable to add element " + childItem.GetName() + ": Element ID: " + childItem.GetMappingID());
            }
        }
        internal void AddElementElement(string parentId, string childId)
        {
            
            XmlItem childItem = parsedXml[childId];

            string stereotype = "";
            if (childItem.GetAttributes().ContainsKey(AttributeConstants.STEREOTYPE)) stereotype = childItem.GetAttribute(AttributeConstants.STEREOTYPE);
            if (GetEAType(childItem.GetElementType(), stereotype) != "")
            {
                try
                {
                    if (childItem.GetAttributes().ContainsKey(AttributeConstants.DISPLAYAS))
                    {
                        if (childItem.GetAttribute(AttributeConstants.DISPLAYAS) == "Diagram") diagramElements.Add(childItem.GetMappingID(), childItem);
                    }
                    else
                    {
                        XmlItem parentItem = parsedXml[parentId];
                        if (IsPortType(parentItem.GetElementType()) && IsPortType(childItem.GetElementType()) && !orphanedIds.Contains(childId))
                        {
                            orphanedIds.Add(childId);
                        }
                        else if (childItem.GetElementType() == SysmlConstants.TRIGGER ||
                            (childItem.GetElementType() == SysmlConstants.CONSTRAINT && parentItem.GetElementType() == SysmlConstants.INTERACTION) ||
                            (childItem.GetElementType() == SysmlConstants.PROPERTY))
                        {
                            XmlItem parentToCheck = parentItem;
                            //while (parentToCheck.GetElementType() != SysmlConstants.PACKAGE)
                            //{
                            //    parentToCheck = parsedXml[parentToCheck.GetParent()];
                            //}
                            if (!(parentToCheck.GetElementType() == SysmlConstants.INTERACTION && childItem.GetElementType() == SysmlConstants.PROPERTY))
                            {
                                EA.Element parentElement = repository.GetElementByGuid(parsedXml[parentToCheck.GetMappingID()].GetEAID());
                                EA.Element childElement = parentElement.Elements.AddNew(childItem.GetName(), GetEAType(childItem.GetElementType(), stereotype));
                                string parentType = GetSysMLType(parentElement.Type, parentElement.Stereotype, parentElement.Subtype, parentElement.MetaType);
                                GetElement(childElement, childItem, childId, parentType);
                            }

                        }
                        else
                        {
                            EA.Element parentElement = repository.GetElementByGuid(parsedXml[parentId].GetEAID());
                            EA.Element childElement = parentElement.Elements.AddNew(childItem.GetName(), GetEAType(childItem.GetElementType(), stereotype));
                            string parentType = GetSysMLType(parentElement.Type, parentElement.Stereotype, parentElement.Subtype, parentElement.MetaType);
                            GetElement(childElement, childItem, childId, parentType);
                        }
                    }
                }
                catch (Exception e)
                {
                    // Add report here
                    exportLog.Add("Unable to add element " + childItem.GetName() + ": Element ID: " + childItem.GetMappingID());
                }

            }
            else
            {
                // Add reporting here
                exportLog.Add("Unable to add element " + childItem.GetName() + ": Element ID: " + childItem.GetMappingID());
            }

        }
        internal void GetElement(EA.Element childElement, XmlItem childItem, string childId, string parentType)
        {
            if (childElement.Type == SysmlConstants.STATENODE)
            {
                if (childItem.GetElementType() == SysmlConstants.INITIALPSEUDOSTATE) childElement.Subtype = 3;
                if (childItem.GetElementType() == SysmlConstants.FINALSTATE) childElement.Subtype = 4;
                if (childItem.GetElementType() == SysmlConstants.SHALLOWHISTORY) childElement.Subtype = 5;
                if (childItem.GetElementType() == SysmlConstants.CHOICEPSUEDOSTATE) childElement.Subtype = 11;
                if (childItem.GetElementType() == SysmlConstants.TERMINATE) childElement.Subtype = 12;
                if (childItem.GetElementType() == SysmlConstants.ENTRYPOINT) childElement.Subtype = 13;
                if (childItem.GetElementType() == SysmlConstants.EXITPOINT) childElement.Subtype = 14;
                if (childItem.GetElementType() == SysmlConstants.DEEPHISTORY) childElement.Subtype = 15;
                if (childItem.GetElementType() == SysmlConstants.ACTIVITYINITIALNODE) childElement.Subtype = 100;
                if (childItem.GetElementType() == SysmlConstants.ACTIVITYFINALNODE) childElement.Subtype = 101;
                if (childItem.GetElementType() == SysmlConstants.FLOWFINALNODE) childElement.Subtype = 102;
                childElement.MetaType = MetatypeConstants.pseudostate;
            }
            if (childElement.Type == SysmlConstants.ACTION)
            {
                if (childItem.GetElementType() == SysmlConstants.ACCEPTEVENTACTION)
                {
                    childElement.MetaType = SysmlConstants.ACCEPTEVENTACTION;
                    childElement.Stereotype = SysmlConstants.ACCEPTEVENTACTION;
                }
                if (childItem.GetElementType() == SysmlConstants.CALLBEHAVIORACTION) childElement.MetaType = SysmlConstants.CALLBEHAVIORACTION;
                if (childItem.GetElementType() == SysmlConstants.OPAQUEACTION) childElement.MetaType = SysmlConstants.OPAQUEACTION;
                if (childItem.GetElementType() == SysmlConstants.SENDSIGNALACTION)
                {
                    actionsToAdd.Add(childItem.GetMappingID(), childItem);
                    childElement.MetaType = SysmlConstants.SENDSIGNALACTION;

                }
            }
            if (childElement.Type == SysmlConstants.ACTIONPIN)
            {
                if (childItem.GetElementType() == SysmlConstants.INPUTPIN) childElement.Stereotype = StereotypeConstants.INPUT;
                if (childItem.GetElementType() == SysmlConstants.OUTPUTPIN) childElement.Stereotype = StereotypeConstants.OUTPUT;

            }
            if (childItem.GetElementType() == SysmlConstants.NAVIGATIONCELL) childElement.Stereotype = StereotypeConstants.NAVIGATIONCELL;
            if (childItem.GetElementType() == SysmlConstants.STEREOTYPE) childElement.Stereotype = StereotypeConstants.STEREOTYPE;
            if (childItem.GetElementType() == SysmlConstants.METACLASS) childElement.Stereotype = StereotypeConstants.METACLASS;
            if (childItem.GetElementType() == SysmlConstants.VALUEPROPERTY) childElement.Stereotype = StereotypeConstants.VALUETYPE;
            if (childItem.GetElementType() == SysmlConstants.PARTPROPERTY)
            {
                childElement.Stereotype = StereotypeConstants.PROPERTY;
            }

            if (childElement.Type == SysmlConstants.OBJECT)
            {
                if (childItem.GetElementType() == SysmlConstants.DATASTORENODE) childElement.Stereotype = StereotypeConstants.DATASTORE;

            }
            if (childElement.Type == SysmlConstants.STATE && childItem.GetAttributes().ContainsKey(AttributeConstants.SUBMACHINE))
            {
                childItem.SetClassifiedBy(childItem.GetAttribute(AttributeConstants.SUBMACHINE));
                classifiersToAdd.Add(childItem.GetMappingID(), childItem);
            }
            if (childElement.Type == SysmlConstants.CONSTRAINT)
            {
                constraintsToAdd.Add(childItem.GetMappingID(), childItem);
            }
            if (childElement.Type == SysmlConstants.OPAQUEEXPRESSION)
            {

                if (opaqueExpressionsToAdd.ContainsKey(childItem.GetParent())) opaqueExpressionsToAdd[childItem.GetParent()].Add(childItem);
                else
                {
                    List<XmlItem> opaqueList = new List<XmlItem>();
                    opaqueList.Add(childItem);
                    opaqueExpressionsToAdd.Add(childItem.GetParent(), opaqueList);
                }
            }
            if (childElement.Type == SysmlConstants.COMBINEDFRAGMENT)
            {
                if (childItem.GetAttributes().ContainsKey(AttributeConstants.INTERACTIONOPERATORKIND))
                {
                    string interactionKind = childItem.GetAttributes()[AttributeConstants.INTERACTIONOPERATORKIND];
                    if (interactionKind == "alt") childElement.Subtype = 0;
                    else if (interactionKind == "assert") childElement.Subtype = 7;
                    else if (interactionKind == "break") childElement.Subtype = 2;
                    else if (interactionKind == "consider") childElement.Subtype = 11;
                    else if (interactionKind == "critical") childElement.Subtype = 5;
                    else if (interactionKind == "ignore") childElement.Subtype = 10;
                    else if (interactionKind == "loop") childElement.Subtype = 4;
                    else if (interactionKind == "neg") childElement.Subtype = 6;
                    else if (interactionKind == "opt") childElement.Subtype = 1;
                    else if (interactionKind == "par") childElement.Subtype = 3;
                    else if (interactionKind == "seq") childElement.Subtype = 9;
                    else if (interactionKind == "strict") childElement.Subtype = 8;
                }
            }
            childElement.Update();
            childElement.Refresh();
            if (childItem.GetElementType() == SysmlConstants.JOIN || childItem.GetElementType() == SysmlConstants.FORK) childElement.Stereotype = childItem.GetElementType();
            if (childItem.GetAttributes().ContainsKey(AttributeConstants.DISPLAYAS))
            {
                if (childItem.GetAttribute(AttributeConstants.DISPLAYAS) == "Diagram") diagramElements.Add(childItem.GetMappingID(), childItem);
            }
            if (childItem.GetAttributes().ContainsKey(AttributeConstants.STEREOTYPE))
            {
                if (childItem.GetElementType() == SysmlConstants.STEREOTYPE) childElement.Stereotype = StereotypeConstants.STEREOTYPE;
                else
                {
                    childElement.Stereotype = childItem.GetAttribute(AttributeConstants.STEREOTYPE);
                }

                if (childItem.GetProfile() != "" && childItem.GetProfileID() != "")
                {
                    childElement.StereotypeEx = childItem.GetProfile() + "::" + childItem.GetAttribute(AttributeConstants.STEREOTYPE);
                }
            }
            if (childItem.GetAttributes().ContainsKey(AttributeConstants.EXTENSIONPOINT))
            {
                childElement.ExtensionPoints = childItem.GetAttribute(AttributeConstants.EXTENSIONPOINT);
            }
            if (childItem.GetClassifiedBy() != "" && !childItem.GetAttributes().ContainsKey(AttributeConstants.SUBMACHINE))
            {
                classifiersToAdd.Add(childItem.GetMappingID(), childItem);
            }
            string notes = "";
            if (childItem.GetAttributes().ContainsKey(AttributeConstants.DOCUMENTATION))
            {
                notes += childItem.GetAttribute(AttributeConstants.DOCUMENTATION);
            }
            if (childItem.GetAttributes().ContainsKey(AttributeConstants.TEXT))
            {
                notes = " - " + childItem.GetAttribute(AttributeConstants.TEXT);
            }
            childElement.Notes = notes;
            if (childItem.GetAttributes().ContainsKey(AttributeConstants.MULTIPLICITY))
            {
                childElement.Multiplicity = childItem.GetAttribute(AttributeConstants.MULTIPLICITY);
            }
            if (childItem.GetAttributes().ContainsKey(AttributeConstants.DEFAULTVALUE))
            {
                //childElement. = childItem.GetAttribute("extensionPoint");
            }
            //if (childItem.GetAttributes().ContainsKey("alias"))
            //{
            //    childElement.Alias = childItem.GetAttribute("alias");
            //}
            if (childItem.GetElementAttributes().Count() > 0)
            {
                foreach (KeyValuePair<string, Models.Attribute> attribute in childItem.GetElementAttributes())
                {
                    EA.Attribute childAttribute = childElement.Attributes.AddNew(attribute.Key, attribute.Value.GetAttribute(AttributeConstants.TYPE));
                    if (attribute.Value.GetAttributes().ContainsKey(AttributeConstants.DEFAULTVALUE)) childAttribute.Default = attribute.Value.GetAttribute(AttributeConstants.DEFAULTVALUE);
                    if (attribute.Value.GetAttributes().ContainsKey(AttributeConstants.INITIALVALUE)) childAttribute.Default = attribute.Value.GetAttribute(AttributeConstants.INITIALVALUE);
                    if (attribute.Value.GetAttributes().ContainsKey(AttributeConstants.STEREOTYPE)) childAttribute.Stereotype = attribute.Value.GetAttribute(AttributeConstants.STEREOTYPE);
                    if (attribute.Value.GetAttributes().ContainsKey(AttributeConstants.VISIBILITY)) childAttribute.Visibility = attribute.Value.GetAttribute(AttributeConstants.VISIBILITY);
                    if (attribute.Value.GetAttributes().ContainsKey(AttributeConstants.ALIAS)) childAttribute.Alias = attribute.Value.GetAttribute(AttributeConstants.ALIAS);
                    childAttribute.Update();
                }
            }
            if (childItem.GetTaggedValues().Count() > 0)
            {
                foreach (KeyValuePair<string, string> attribute in childItem.GetTaggedValues())
                {
                    EA.TaggedValue childTaggedValue = childElement.TaggedValues.AddNew(attribute.Key, attribute.Value);
                    childTaggedValue.Update();
                }
            }
            if (childItem.GetBehaviors().Count() > 0)
            {
                foreach (Behavior behavior in childItem.GetBehaviors())
                {
                    EA.Method childMethod = childElement.Methods.AddNew(behavior.GetName(), behavior.GetBehaviorType());
                    childMethod.Behavior = behavior.GetValue();
                    childMethod.Update();
                    childElement.Methods.Refresh();
                    childElement.Update();
                }
            }
            if (childItem.GetTypedBy() != "")
            {
                propertyTypesToAdd.Add(childItem.GetMappingID(), childItem);
            }
            List<string> attributesAllowed = new List<string>()
            {
                AttributeConstants.STEREOTYPE,
                AttributeConstants.DOCUMENTATION,
                AttributeConstants.TEXT,
                AttributeConstants.ALIAS,
                AttributeConstants.DISPLAYAS,
                AttributeConstants.NAME,
                AttributeConstants.SUBMACHINE,
                AttributeConstants.MULTIPLICITY,
                AttributeConstants.INTERACTIONOPERATORKIND
            };
            foreach (KeyValuePair<string, string> attribute in childItem.GetAttributes())
            {
                if (!attributesAllowed.Contains(attribute.Key))
                {
                    EA.TaggedValue attributeTaggedValue = childElement.TaggedValues.AddNew(attribute.Key, attribute.Value);
                    attributeTaggedValue.Update();
                }
            }
            childElement.Attributes.Refresh();
            childElement.Update();
            parsedXml[childId].SetEAID(childElement.ElementGUID);
            CheckElementSysMLCompliance(childElement, "sysml." + childItem.GetElementType(), "sysml." + parentType);

        }
        internal void GetTerm(XmlNode fieldNode)
        {
            
            GlossaryTerm term = new GlossaryTerm();
            foreach (XmlNode attribute in fieldNode)
            {
                if (attribute.Attributes["key"] != null)
                {
                    if (attribute.Attributes["key"].Value == AttributeConstants.NAME) term.SetName(attribute.InnerText);
                    else if (attribute.Attributes["key"].Value == AttributeConstants.DOCUMENTATION) term.SetMeaning(attribute.InnerText);
                    term.SetTermType("string");
                }
            }
            terms.Add(term);
        }
        internal void AddClassifiers()
        {
            foreach (KeyValuePair<string, XmlItem> xmlItem in classifiersToAdd)
            {
                try
                {
                    if (parsedXml.ContainsKey(xmlItem.Value.GetClassifiedBy()))
                    {
                        EA.Element element = repository.GetElementByGuid(xmlItem.Value.GetEAID());
                        int classifierId = repository.GetElementByGuid(parsedXml[xmlItem.Value.GetClassifiedBy()].GetEAID()).ElementID;
                        element.ClassfierID = classifierId;
                        element.Update();
                        element.Refresh();
                    }
                }
                catch
                {
                    exportLog.Add("Unable to add classifier type for element " + xmlItem.Value.GetMappingID() + " with type " + xmlItem.Value.GetTypedBy());

                }

            }
        }
        internal void AddConnectors()
        {
            foreach (KeyValuePair<string, XmlItem> relationshipItem in relationshipElements)
            {
                try
                {
                    if (parsedXml.ContainsKey(relationshipItem.Value.GetSupplier()) && parsedXml.ContainsKey(relationshipItem.Value.GetClient()))
                    {
                        XmlItem supplierItem = parsedXml[relationshipItem.Value.GetSupplier()];
                        XmlItem clientItem = parsedXml[relationshipItem.Value.GetClient()];

                        if (!orphanedIds.Contains(clientItem.GetMappingID()) && !orphanedIds.Contains(supplierItem.GetMappingID())
                            && clientItem.GetMappingID() != "" && supplierItem.GetMappingID() != "")
                        {
                            EA.Element clientElement = repository.GetElementByGuid(clientItem.GetEAID());
                            EA.Element supplierElement = repository.GetElementByGuid(supplierItem.GetEAID());

                            string type;
                            if (relationshipItem.Value.GetElementType() == SysmlConstants.ASSOCIATIONBLOCK || relationshipItem.Value.GetElementType() == SysmlConstants.COMPOSITION) type = SysmlConstants.ASSOCIATION;
                            else if (relationshipItem.Value.GetElementType() == SysmlConstants.REALIZATION) type = SysmlConstants.REALISATION;
                            else if (relationshipItem.Value.GetElementType() == SysmlConstants.TRANSITION) type = SysmlConstants.STATEFLOW;
                            else if (relationshipItem.Value.GetElementType() == SysmlConstants.USECASERELATIONSHIP) type = SysmlConstants.USECASE;
                            else if (relationshipItem.Value.GetElementType() == SysmlConstants.COMPOSITION) type = SysmlConstants.AGGREGATION;
                            else if (relationshipItem.Value.GetElementType() == SysmlConstants.RELATIONSHIPCONSTRAINT) type = SysmlConstants.DEPENDENCY;
                            //else if (relationshipItem.Value.GetElementType() == SysmlConstants.ITEMFLOW) type = SysmlConstants.INFORMATIONFLOW;
                            else if (relationshipItem.Value.GetElementType() == SysmlConstants.EXTEND) type = SysmlConstants.USECASE;
                            else type = relationshipItem.Value.GetElementType();

                            if (type == SysmlConstants.MESSAGE)
                            {
                                EA.Connector associationConnector = clientElement.Connectors.AddNew("", "Message");
                                associationConnector.Name = relationshipItem.Value.GetName();

                                if (relationshipItem.Value.GetAttributes().ContainsKey(AttributeConstants.MESSAGESORT))
                                {
                                    if (relationshipItem.Value.GetAttribute(AttributeConstants.MESSAGESORT) == RelationshipConstants.ASYNCHSIGNAL)
                                    {
                                        associationConnector.TransitionEvent = "Asynchronous";
                                        associationConnector.TransitionAction = "Signal";
                                        associationConnector.SupplierID = supplierElement.ElementID;
                                        //associationConnector.ClientID = clientElement.ElementID;
                                        associationConnector.Update();

                                        if (relationshipItem.Value.GetSignal() != "")
                                        {
                                            string signal = parsedXml[relationshipItem.Value.GetSignal()].GetEAID();
                                            EA.ConnectorTag tag = associationConnector.TaggedValues.AddNew(RelationshipConstants.SIGNALGUID, signal);
                                            //tag.Name = "signal_guid";
                                            //tag.Value = signal;
                                            tag.Update();
                                            associationConnector.TaggedValues.Refresh();
                                            associationConnector.Update();
                                        }
                                    }
                                    if (relationshipItem.Value.GetAttribute(AttributeConstants.MESSAGESORT) == RelationshipConstants.ASYNCHCALL)
                                    {
                                        associationConnector.TransitionEvent = "Asynchronous";
                                        associationConnector.TransitionAction = "Call";
                                        associationConnector.SupplierID = supplierElement.ElementID;
                                        associationConnector.ClientID = clientElement.ElementID;
                                    }
                                    if (relationshipItem.Value.GetAttribute(AttributeConstants.MESSAGESORT) == RelationshipConstants.SYNCHCALL)
                                    {
                                        associationConnector.TransitionEvent = "Synchronous";
                                        associationConnector.TransitionAction = "Call";
                                        associationConnector.SupplierID = supplierElement.ElementID;
                                        associationConnector.ClientID = clientElement.ElementID;
                                    }

                                    if (relationshipItem.Value.GetAttribute(AttributeConstants.MESSAGESORT) == RelationshipConstants.REPLY)
                                    {
                                        associationConnector.TransitionEvent = "Synchronous";
                                        associationConnector.TransitionAction = "Call";
                                        associationConnector.SupplierID = supplierElement.ElementID;
                                        associationConnector.ClientID = clientElement.ElementID;
                                    }
                                }
                                if (relationshipItem.Value.GetAttributes().ContainsKey(AttributeConstants.DOCUMENTATION))
                                {
                                    associationConnector.Notes = relationshipItem.Value.GetAttribute(AttributeConstants.DOCUMENTATION);
                                }
                                clientElement.Connectors.Refresh();
                                supplierElement.Connectors.Refresh();
                                supplierElement.Connectors.Refresh();
                                clientElement.Update();
                                supplierElement.Update();
                                relationshipItem.Value.SetEAID(associationConnector.ConnectorGUID);
                                associationConnector.Update();
                            }
                            else
                            {
                                EA.Connector associationConnector = supplierElement.Connectors.AddNew("", type);
                                associationConnector.Name = relationshipItem.Value.GetName();
                                associationConnector.ClientID = clientElement.ElementID;
                                associationConnector.SupplierID = supplierElement.ElementID;

                                if (relationshipItem.Value.GetElementType() == SysmlConstants.EXTEND)
                                {
                                    associationConnector.Stereotype = "extend";
                                    associationConnector.Subtype = "Extends";
                                    associationConnector.MetaType = "Extend";
                                }
                                if (relationshipItem.Value.GetAttributes().ContainsKey(AttributeConstants.STEREOTYPE))
                                {
                                    string stereotype = relationshipItem.Value.GetAttribute(AttributeConstants.STEREOTYPE);
                                    associationConnector.Stereotype = stereotype;
                                    associationConnector.Update();
                                    // Add the type of the metarelationship or stereotyped relationship as a tagged value
                                    // the GetAttribute is tied to the GetAttributes function
                                    if (stereotype == "stereotyped relationship")
                                    {
                                        string meta_ster = relationshipItem.Value.GetAttribute("stereotyped relationship");
                                        ConnectorTag meta_con = associationConnector.TaggedValues.GetByName("stereotype");
                                        meta_con.Value = meta_ster;
                                        meta_con.Update();
                                    }
                                    else if (stereotype == RelationshipConstants.METARELATIONSHIP)
                                    {
                                        string meta_ster = relationshipItem.Value.GetAttribute(RelationshipConstants.METARELATIONSHIP);
                                        ConnectorTag meta_con = associationConnector.TaggedValues.GetByName(RelationshipConstants.METACLASS);
                                        meta_con.Value = meta_ster;
                                        meta_con.Update();
                                    }
                                }
                                if (relationshipItem.Value.GetAttributes().ContainsKey(RelationshipConstants.GUARD))
                                {
                                    associationConnector.TransitionGuard = relationshipItem.Value.GetAttribute(RelationshipConstants.GUARD);
                                }
                                if (relationshipItem.Value.GetAttributes().ContainsKey(RelationshipConstants.EFFECT))
                                {
                                    associationConnector.TransitionAction = relationshipItem.Value.GetAttribute(RelationshipConstants.EFFECT);
                                }
                                if (relationshipItem.Value.GetElementType() == SysmlConstants.COMPOSITION)
                                {
                                    associationConnector.SupplierEnd.Aggregation = 2;
                                    associationConnector.Subtype = "Strong";
                                }
                                if (relationshipItem.Value.GetAttributes().ContainsKey(AttributeConstants.DOCUMENTATION))
                                {
                                    associationConnector.Notes = relationshipItem.Value.GetAttribute(AttributeConstants.DOCUMENTATION);
                                }
                                associationConnector.Update();

                                clientElement.Connectors.Refresh();
                                supplierElement.Connectors.Refresh();
                                clientElement.Update();
                                supplierElement.Update();
                                relationshipItem.Value.SetEAID(associationConnector.ConnectorGUID);
                            }

                            clientElement.Connectors.Refresh();
                            supplierElement.Connectors.Refresh();
                            clientElement.Update();
                            supplierElement.Update();
                        }
                    }
                }
                catch
                {
                    //Add error log
                    exportLog.Add("Unable to add connector: Connecter ID= " + relationshipItem.Value.GetMappingID() + " - Client ID=" + relationshipItem.Value.GetClient() + " - " + relationshipItem.Value.GetSupplier());
                }

            }
        }
        internal void AddPropertyTypes()
        {
            foreach (KeyValuePair<string, XmlItem> xmlItem in propertyTypesToAdd)
            {
                try
                {
                    EA.Element element = repository.GetElementByGuid(xmlItem.Value.GetEAID());
                    if (xmlItem.Value.GetTypedBy() != "0")
                    {
                        if (parsedXml.ContainsKey(xmlItem.Value.GetTypedBy()))
                        {
                            int propertyPartId = repository.GetElementByGuid(parsedXml[xmlItem.Value.GetTypedBy()].GetEAID()).ElementID;
                            if (xmlItem.Value.GetElementType() == SysmlConstants.LIFELINE)
                            {
                                element.ClassfierID = propertyPartId;
                            }
                            else
                            {
                                element.PropertyType = propertyPartId;
                            }
                            element.Update();
                        }
                    }
                }
                catch
                {
                    exportLog.Add("Unable to add property type for element " + xmlItem.Value.GetMappingID() + " with type " + xmlItem.Value.GetTypedBy());
                }
            }
        }
        internal void AddConstraints()
        {
            foreach (KeyValuePair<string, XmlItem> constraintItem in constraintsToAdd)
            {
                try
                {
                    XmlItem parentItem = parsedXml[constraintItem.Value.GetParent()];
                    List<XmlItem> opaqueExpressions = opaqueExpressionsToAdd[constraintItem.Value.GetMappingID()];
                    EA.Element constraintBlock = repository.GetElementByGuid(parentItem.GetEAID());
                    foreach (XmlItem opaqueToAdd in opaqueExpressions)
                    {

                        EA.Constraint constraint = constraintBlock.Constraints.AddNew(opaqueToAdd.GetAttribute("body"), "Invariant");
                        constraint.Update();
                    }
                    constraintBlock.Constraints.Refresh();
                    constraintBlock.Update();

                }
                catch
                {
                    // Add log for constraint errors
                    exportLog.Add("Unable to add constraint: Constraint ID= " + constraintItem.Value.GetMappingID() + " - Constraint Block ID=" + constraintItem.Value.GetParent());
                }
            }
        }
        internal void BuildDiagrams()
        {
         
            foreach (KeyValuePair<string, XmlItem> diagramItem in diagramElements)
            {
                try
                {
                    XmlItem parentItem = parsedXml[diagramItem.Value.GetParent()];

                    string stereotype = "";
                    if (diagramItem.Value.GetAttributes().ContainsKey(AttributeConstants.STEREOTYPE)) stereotype = diagramItem.Value.GetAttribute(AttributeConstants.STEREOTYPE);

                    List<string> childItemIds = new List<string>();

                    EA.Diagram relationshipDiagram = GetDiagram(parentItem, diagramItem);
                    diagramItem.Value.SetEAID(relationshipDiagram.DiagramGUID);
                    string parentType = parsedXml[diagramItem.Value.GetParent()].GetElementType();
                    CheckDiagramSysMLCompliance(relationshipDiagram, "sysml." + diagramItem.Value.GetElementType(), "sysml." + parentType);

                    List<DiagramObjectItem> childObjects = diagramItem.Value.GetDiagramObjects(parsedXml);

                    Boolean hasCoors = false;
                    if (childObjects.Count > 0)
                    {
                        if (childObjects[0].GetLeftCoor() != "" && childObjects[0].GetRightCoor() != ""
                            && childObjects[0].GetTopCoor() != "" && childObjects[0].GetBottomCoor() != "") hasCoors = true;
                    }

                    List<DiagramObjectItem> portsToAdd = new List<DiagramObjectItem>();
                    List<DiagramObjectItem> actParamNodesToAdd = new List<DiagramObjectItem>();

                    foreach (DiagramObjectItem childDiagramObject in childObjects)
                    {
                        string childMappingId = childDiagramObject.GetMappingId();
                        if (diagramItem.Value.GetMappingID() != childMappingId && parsedXml[childDiagramObject.GetMappingId()].GetEAID() != "")
                        {
                            if (parsedXml.ContainsKey(childMappingId) && !orphanedIds.Contains(childMappingId) && !childItemIds.Contains(childMappingId))
                            {
                                if (parsedXml[childMappingId].GetEAID() != "")
                                {
                                    XmlItem childItem = parsedXml[childMappingId];
                                    Boolean isPort = false;
                                    Boolean isActParamNodeType = false;
                                    if (IsPortType(childItem.GetElementType()))
                                    {
                                        isPort = true;
                                        portsToAdd.Add(childDiagramObject);
                                    }

                                    if (childItem.GetElementType() == SysmlConstants.ACTIVITYPARAMETERNODE)
                                    {
                                        isActParamNodeType = true;
                                        actParamNodesToAdd.Add(childDiagramObject);
                                    }
                                    childItemIds.Add(childMappingId);
                                    //if (childItem.GetCategory() != SysmlConstants.RELATIONSHIP && GetEAType(childItem.GetElementType(), stereotype) != "ActionPin" && isPort != true && GetEAType(childItem.GetElementType(), stereotype) != "Package")
                                    if (childItem.GetCategory() != SysmlConstants.RELATIONSHIP && GetEAType(childItem.GetElementType(), stereotype) != "Package" && isPort == false && isActParamNodeType == false)
                                    {
                                        DiagramObject diagramObject = relationshipDiagram.DiagramObjects.AddNew("", "");
                                        if (hasCoors == true)
                                        {
                                            diagramObject.left = Int32.Parse(childDiagramObject.GetLeftCoor());
                                            diagramObject.right = Int32.Parse(childDiagramObject.GetRightCoor());
                                            diagramObject.top = Int32.Parse(childDiagramObject.GetTopCoor());
                                            diagramObject.bottom = Int32.Parse(childDiagramObject.GetBottomCoor());
                                            if (childDiagramObject.GetSequenceCoor() != "") diagramObject.Sequence = Int32.Parse(childDiagramObject.GetSequenceCoor());

                                        }
                                        diagramObject.ElementID = repository.GetElementByGuid(parsedXml[childMappingId].GetEAID()).ElementID;
                                        diagramObject.Update();


                                        // Get Action Pins
                                        EA.Element childElement = repository.GetElementByGuid(childItem.GetEAID());
                                        if (GetSysMLType(childElement.Type, childElement.Stereotype, childElement.Subtype, childElement.MetaType) == SysmlConstants.ACTION)
                                        {
                                            foreach (EA.Element actionElement in childElement.Elements)
                                            {
                                                if (actionElement.Type == SysmlConstants.ACTIONPIN)
                                                {
                                                    DiagramObject actionDiagramObject = relationshipDiagram.DiagramObjects.AddNew("", "");
                                                    actionDiagramObject.ElementID = actionElement.ElementID;
                                                    actionDiagramObject.Update();
                                                }
                                            }
                                        }
                                    }
                                }

                            }
                        }
                    }
                    foreach (DiagramObjectItem portItem in portsToAdd)
                    {
                        string childMappingId = portItem.GetMappingId();
                        XmlItem childItem = parsedXml[childMappingId];

                        if (childItem.GetCategory() != SysmlConstants.RELATIONSHIP && GetEAType(childItem.GetElementType(), stereotype) != "ActionPin")
                        {
                            DiagramObject diagramObject = relationshipDiagram.DiagramObjects.AddNew("", "");
                            if (hasCoors == true)
                            {
                                diagramObject.left = Int32.Parse(portItem.GetLeftCoor());
                                diagramObject.right = Int32.Parse(portItem.GetRightCoor());
                                diagramObject.top = Int32.Parse(portItem.GetTopCoor());
                                diagramObject.bottom = Int32.Parse(portItem.GetBottomCoor());
                                if (portItem.GetSequenceCoor() != "") diagramObject.Sequence = Int32.Parse(portItem.GetSequenceCoor());

                            }
                            diagramObject.ElementID = repository.GetElementByGuid(parsedXml[childMappingId].GetEAID()).ElementID;
                            diagramObject.Update();


                            // Get Action Pins
                            EA.Element childElement = repository.GetElementByGuid(childItem.GetEAID());
                            if (GetSysMLType(childElement.Type, childElement.Stereotype, childElement.Subtype, childElement.MetaType) == SysmlConstants.SYSMLACTION)
                            {
                                foreach (EA.Element actionElement in childElement.Elements)
                                {
                                    if (actionElement.Type == SysmlConstants.ACTIONPIN)
                                    {
                                        DiagramObject actionDiagramObject = relationshipDiagram.DiagramObjects.AddNew("", "");
                                        actionDiagramObject.ElementID = actionElement.ElementID;
                                        actionDiagramObject.Update();
                                    }
                                }
                            }
                        }
                    }
                    foreach (DiagramObjectItem actParamNodeItem in actParamNodesToAdd)
                    {
                        string childMappingId = actParamNodeItem.GetMappingId();
                        XmlItem childItem = parsedXml[childMappingId];

                        DiagramObject diagramObject = relationshipDiagram.DiagramObjects.AddNew("", "");
                        //if (hasCoors == true)
                        //{
                        //    diagramObject.left = Int32.Parse(actParamNodeItem.GetLeftCoor());
                        //    diagramObject.right = Int32.Parse(actParamNodeItem.GetRightCoor());
                        //    diagramObject.top = Int32.Parse(actParamNodeItem.GetTopCoor());
                        //    diagramObject.bottom = Int32.Parse(actParamNodeItem.GetBottomCoor());
                        //    if (actParamNodeItem.GetSequenceCoor() != "") diagramObject.Sequence = Int32.Parse(actParamNodeItem.GetSequenceCoor());

                        //}
                        diagramObject.ElementID = repository.GetElementByGuid(parsedXml[childMappingId].GetEAID()).ElementID;
                        diagramObject.Update();
                    }

                    repository.ReloadDiagram(relationshipDiagram.DiagramID);
                    relationshipDiagram.DiagramObjects.Refresh();


                    List<DiagramLinkItem> childLinks = diagramItem.Value.GetDiagramLinks();
                    List<string> childLinkIds = new List<string>();

                    foreach (DiagramLinkItem diagramLink in childLinks)
                    {
                        if (parsedXml.ContainsKey(diagramLink.GetMappingId()))
                        {
                            string diagramLinkParsedEAId = parsedXml[diagramLink.GetMappingId()].GetEAID();
                            if (diagramLink.GetSequence() != "" && repository.GetConnectorByGuid(parsedXml[diagramLink.GetMappingId()].GetEAID()) != null)
                            {
                                EA.Connector diagramConnector = repository.GetConnectorByGuid(parsedXml[diagramLink.GetMappingId()].GetEAID());
                                diagramConnector.SequenceNo = Int32.Parse(diagramLink.GetSequence());
                                diagramConnector.Update();
                            }

                            childLinkIds.Add(diagramLinkParsedEAId);
                        }
                    }

                    repository.ReloadDiagram(relationshipDiagram.DiagramID);
                    if (childObjects.Count > 0) relationshipDiagram.DiagramObjects.Refresh();

                    EA.Collection theDiagramLinks = relationshipDiagram.DiagramLinks;

                    // iterate
                    foreach (EA.DiagramLink currentLink in theDiagramLinks)
                    {
                        currentLink.LineStyle = EA.LinkLineStyle.LineStyleAutoRouting;
                        currentLink.Update();
                    }

                    // Auto-layout the diagram
                    if (hasCoors == false && childObjects.Count > 0)
                    {
                        _ = repository.GetProjectInterface().LayoutDiagramEx(relationshipDiagram.DiagramGUID, 1, 5, 20, 20, false);    // constlayoutstyles
                        repository.ReloadDiagram(relationshipDiagram.DiagramID);
                        relationshipDiagram.DiagramObjects.Refresh();
                        relationshipDiagram.DiagramLinks.Refresh();
                        repository.SaveDiagram(relationshipDiagram.DiagramID);
                    }
                }
                catch
                {

                    exportLog.Add("Unable to add diagram: Diagram Name= " + diagramItem.Value.GetName() + " - Diagram ID= " + diagramItem.Value.GetMappingID());
                }
            }
        }
        internal void AddHyperlinks()
        {
            foreach (KeyValuePair<string, XmlItem> hyperlinkToAdd in hyperLinksToAdd)
            {
                
                try
                {
                    XmlItem parentItem = parsedXml[hyperlinkToAdd.Value.GetParent()];
                    XmlItem hyperlinkItem = hyperLinksToAdd[hyperlinkToAdd.Value.GetMappingID()];
                    EA.Package parentElement = repository.GetPackageByGuid(parentItem.GetEAID());
                    EA.Diagram targetDiagram = repository.GetDiagramByGuid(parsedXml[hyperlinkToAdd.Value.GetHyperlink()].GetEAID());
                    EA.Element hyperlinkElement = parentElement.Elements.AddNew("$" + hyperlinkToAdd.Value.GetHyperlinkType() + "://{" + targetDiagram.DiagramGUID + "}", "Text");
                    hyperlinkElement.Notes = hyperlinkItem.GetAttribute(AttributeConstants.DOCUMENTATION);
                    hyperlinkItem.SetEAID(hyperlinkElement.ElementGUID);
                    hyperlinkElement.Update();
                    parentElement.Elements.Refresh();
                    parentElement.Update();

                    foreach (KeyValuePair<string, XmlItem> diagramItem in diagramElements)
                    {
                        foreach (DiagramObjectItem diagramObjectItem in diagramItem.Value.GetDiagramObjects(parsedXml))
                        {
                            EA.Diagram relationshipDiagram = repository.GetDiagramByGuid(diagramItem.Value.GetEAID());
                            Boolean hasCoors = false;
                            if (diagramObjectItem.GetLeftCoor() != "" && diagramObjectItem.GetRightCoor() != ""
                                && diagramObjectItem.GetTopCoor() != "" && diagramObjectItem.GetBottomCoor() != "") hasCoors = true;

                            if (hyperlinkItem.GetMappingID() == diagramObjectItem.GetMappingId())
                            {
                                string childMappingId = diagramObjectItem.GetMappingId();
                                if (parsedXml.ContainsKey(childMappingId) && !orphanedIds.Contains(childMappingId))
                                {
                                    if (parsedXml[childMappingId].GetEAID() != "")
                                    {
                                        XmlItem childItem = parsedXml[childMappingId];
                                        DiagramObject diagramObject = relationshipDiagram.DiagramObjects.AddNew("", "");
                                        if (hasCoors == true)
                                        {
                                            diagramObject.left = Int32.Parse(diagramObjectItem.GetLeftCoor());
                                            diagramObject.right = Int32.Parse(diagramObjectItem.GetRightCoor());
                                            diagramObject.top = Int32.Parse(diagramObjectItem.GetTopCoor());
                                            diagramObject.bottom = Int32.Parse(diagramObjectItem.GetBottomCoor());
                                            if (diagramObjectItem.GetSequenceCoor() != "") diagramObject.Sequence = Int32.Parse(diagramObjectItem.GetSequenceCoor());

                                        }
                                        diagramObject.ElementID = repository.GetElementByGuid(parsedXml[childMappingId].GetEAID()).ElementID;
                                        diagramObject.Update();
                                        relationshipDiagram.Update();


                                        // Update the PDATA1 and set it to the target diagram's ID
                                        repository.Execute("UPDATE t_object SET PDATA1='" + targetDiagram.DiagramID + "' where object_ID = " + hyperlinkElement.ElementID + "");
                                        repository.ReloadDiagram(relationshipDiagram.DiagramID);
                                    }

                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Add log for constraint errors
                    exportLog.Add("Unable to add constraint: Constraint ID= " + hyperlinkToAdd.Value.GetMappingID());
                }
            }
        }
        internal void AddCompositeDiagrams()
        {
            foreach (KeyValuePair<string, XmlItem> compositeDiagramItem in compositeDiagramsToAdd)
            {
                try
                {
                    XmlItem diagramItem = parsedXml[compositeDiagramItem.Value.GetCompositeDiagram()];
                    EA.Diagram compositeDiagram = repository.GetDiagramByGuid(diagramItem.GetEAID());
                    EA.Element compositeElement = repository.GetElementByGuid(compositeDiagramItem.Value.GetEAID());
                    compositeElement.IsComposite = true;
                    compositeElement.SetCompositeDiagram(compositeDiagram.DiagramGUID);
                    compositeElement.Update();

                    foreach (KeyValuePair<string, XmlItem> searchDiagramItem in diagramElements)
                    {
                        foreach (DiagramObjectItem diagramObjectItem in searchDiagramItem.Value.GetDiagramObjects(parsedXml))
                        {
                            EA.Diagram relationshipDiagram = repository.GetDiagramByGuid(searchDiagramItem.Value.GetEAID());
                            if (compositeDiagramItem.Value.GetMappingID() == diagramObjectItem.GetMappingId())
                            {
                                EA.Diagram diagramObjectAppearsIn = repository.GetDiagramByGuid(searchDiagramItem.Value.GetEAID());
                                repository.ReloadDiagram(relationshipDiagram.DiagramID);
                            }
                        }
                    }
                }
                catch
                {
                    // Add log for constraint errors
                    exportLog.Add("Unable to add compositeDiagramItem: compositeDiagramItem ID= " + compositeDiagramItem.Value.GetMappingID());
                }
            }
        }
        internal EA.Diagram GetDiagram(XmlItem parentItem, KeyValuePair<string, XmlItem> diagramItem)
        {
            
            
            EA.Package pkg = repository.GetPackageByID(1);
            EA.Diagram relationshipDiagram = repository.GetPackageByID(1).Diagrams.AddNew(diagramItem.Value.GetAttribute(AttributeConstants.NAME), DiagramConstants.component); ;
            if (parentItem.GetElementType() == SysmlConstants.PACKAGE || parentItem.GetElementType() == SysmlConstants.PROFILE || parentItem.GetElementType() == SysmlConstants.MODEL)
            {
                EA.Package parentPkg = repository.GetPackageByGuid(parentItem.GetEAID());
                if (diagramItem.Value.GetElementType() == SysmlConstants.ACT)
                {
                    relationshipDiagram = parentPkg.Diagrams.AddNew(diagramItem.Value.GetAttribute(AttributeConstants.NAME), DiagramConstants.activity);
                    relationshipDiagram.MetaType = "SysML1.4::Activity";
                    relationshipDiagram.Update();
                    parentPkg.Diagrams.Refresh();
                    return relationshipDiagram;
                }
                if (diagramItem.Value.GetElementType() == SysmlConstants.BDD)
                {
                    relationshipDiagram = parentPkg.Diagrams.AddNew(diagramItem.Value.GetAttribute(AttributeConstants.NAME), DiagramConstants.logical);
                    relationshipDiagram.MetaType = "SysML1.4::BlockDefinition";
                    relationshipDiagram.Update();
                    parentPkg.Diagrams.Refresh();
                    return relationshipDiagram;
                }
                if (diagramItem.Value.GetElementType() == SysmlConstants.CLASS)
                {
                    relationshipDiagram = parentPkg.Diagrams.AddNew(diagramItem.Value.GetAttribute(AttributeConstants.NAME), DiagramConstants.classType);
                    relationshipDiagram.Update();
                    parentPkg.Diagrams.Refresh();
                    return relationshipDiagram;
                }
                if (diagramItem.Value.GetElementType() == SysmlConstants.IBD)
                {
                    relationshipDiagram = parentPkg.Diagrams.AddNew(diagramItem.Value.GetAttribute(AttributeConstants.NAME), DiagramConstants.compositeStructure);
                    relationshipDiagram.MetaType = "SysML1.4::InternalBlock";
                    relationshipDiagram.Update();
                    parentPkg.Diagrams.Refresh();
                    return relationshipDiagram;
                }
                if (diagramItem.Value.GetElementType() == SysmlConstants.PACKAGE)
                {
                    relationshipDiagram = parentPkg.Diagrams.AddNew(diagramItem.Value.GetAttribute(AttributeConstants.NAME), DiagramConstants.package);
                    relationshipDiagram.Update();
                    parentPkg.Diagrams.Refresh();
                    return relationshipDiagram;
                }
                if (diagramItem.Value.GetElementType() == SysmlConstants.PAR)
                {
                    relationshipDiagram = parentPkg.Diagrams.AddNew(diagramItem.Value.GetAttribute(AttributeConstants.NAME), DiagramConstants.compositeStructure);
                    relationshipDiagram.MetaType = "SysML1.4::Parametric";
                    relationshipDiagram.Update();
                    parentPkg.Diagrams.Refresh();
                    return relationshipDiagram;
                }
                if (diagramItem.Value.GetElementType() == SysmlConstants.REQ)
                {
                    relationshipDiagram = parentPkg.Diagrams.AddNew(diagramItem.Value.GetAttribute(AttributeConstants.NAME), DiagramConstants.custom);
                    relationshipDiagram.MetaType = "SysML1.4::Requirement";
                    relationshipDiagram.Update();
                    parentPkg.Diagrams.Refresh();
                    return relationshipDiagram;
                }
                if (diagramItem.Value.GetElementType() == SysmlConstants.SEQ)
                {
                    relationshipDiagram = parentPkg.Diagrams.AddNew(diagramItem.Value.GetAttribute(AttributeConstants.NAME), DiagramConstants.sequence);
                    relationshipDiagram.MetaType = "SysML1.4::Sequence";
                    relationshipDiagram.Update();
                    parentPkg.Diagrams.Refresh();
                    return relationshipDiagram;
                }
                if (diagramItem.Value.GetElementType() == SysmlConstants.STM)
                {
                    relationshipDiagram = parentPkg.Diagrams.AddNew(diagramItem.Value.GetAttribute(AttributeConstants.NAME), DiagramConstants.statechart);
                    relationshipDiagram.MetaType = "SysML1.4::StateMachine";
                    relationshipDiagram.Update();
                    parentPkg.Diagrams.Refresh();
                    return relationshipDiagram;
                }
                if (diagramItem.Value.GetElementType() == SysmlConstants.UC)
                {
                    relationshipDiagram = parentPkg.Diagrams.AddNew(diagramItem.Value.GetAttribute(AttributeConstants.NAME), DiagramConstants.useCase);
                    relationshipDiagram.MetaType = "SysML1.4::UseCase";
                    relationshipDiagram.Update();
                    parentPkg.Diagrams.Refresh();
                    return relationshipDiagram;
                }
            }
            else if (parentItem.GetCategory() == SysmlConstants.ELEMENT)
            {
                EA.Element parentElement = repository.GetElementByGuid(parentItem.GetEAID());
                if (diagramItem.Value.GetElementType() == SysmlConstants.ACT && parentItem.GetEAID() != DiagramConstants.activity)
                {
                    relationshipDiagram = parentElement.Diagrams.AddNew(diagramItem.Value.GetAttribute(AttributeConstants.NAME), DiagramConstants.activity);
                    relationshipDiagram.MetaType = "SysML1.4::Activity";
                    relationshipDiagram.Update();
                    parentElement.Diagrams.Refresh();
                    return relationshipDiagram;
                }

                if (diagramItem.Value.GetElementType() == SysmlConstants.BDD && parentItem.GetEAID() != "")
                {
                    relationshipDiagram = parentElement.Diagrams.AddNew(diagramItem.Value.GetAttribute(AttributeConstants.NAME), DiagramConstants.logical);
                    relationshipDiagram.MetaType = "SysML1.4::BlockDefinition";
                    relationshipDiagram.Update();
                    parentElement.Diagrams.Refresh();
                    return relationshipDiagram;
                }
                if (diagramItem.Value.GetElementType() == SysmlConstants.CLASS && parentItem.GetEAID() != "")
                {
                    relationshipDiagram = parentElement.Diagrams.AddNew(diagramItem.Value.GetAttribute(AttributeConstants.NAME), DiagramConstants.classType);
                    relationshipDiagram.Update();
                    parentElement.Diagrams.Refresh();
                    return relationshipDiagram;
                }
                if (diagramItem.Value.GetElementType() == SysmlConstants.IBD && parentItem.GetEAID() != "")
                {
                    relationshipDiagram = parentElement.Diagrams.AddNew(diagramItem.Value.GetAttribute(AttributeConstants.NAME), DiagramConstants.compositeStructure);
                    relationshipDiagram.MetaType = "SysML1.4::InternalBlock";
                    relationshipDiagram.Update();
                    parentElement.Diagrams.Refresh();
                    return relationshipDiagram;
                }
                if (diagramItem.Value.GetElementType() == SysmlConstants.PACKAGE && parentItem.GetEAID() != "")
                {
                    relationshipDiagram = parentElement.Diagrams.AddNew(diagramItem.Value.GetAttribute(AttributeConstants.NAME), DiagramConstants.package);
                    relationshipDiagram.Update();
                    parentElement.Diagrams.Refresh();
                    return relationshipDiagram;
                }
                if (diagramItem.Value.GetElementType() == SysmlConstants.PAR && parentItem.GetEAID() != "")
                {
                    relationshipDiagram = parentElement.Diagrams.AddNew(diagramItem.Value.GetAttribute(AttributeConstants.NAME), DiagramConstants.compositeStructure);
                    relationshipDiagram.MetaType = "SysML1.4::Parametric";
                    relationshipDiagram.Update();
                    parentElement.Diagrams.Refresh();
                    return relationshipDiagram;
                }
                if (diagramItem.Value.GetElementType() == SysmlConstants.REQ)
                {
                    relationshipDiagram = parentElement.Diagrams.AddNew(diagramItem.Value.GetAttribute(AttributeConstants.NAME), DiagramConstants.custom);
                    relationshipDiagram.MetaType = "SysML1.4::Requirement";
                    relationshipDiagram.Update();
                    parentElement.Diagrams.Refresh();
                    return relationshipDiagram;
                }
                if (diagramItem.Value.GetElementType() == SysmlConstants.SEQ && parentItem.GetEAID() != "")
                {
                    relationshipDiagram = parentElement.Diagrams.AddNew(diagramItem.Value.GetAttribute(AttributeConstants.NAME), DiagramConstants.sequence);
                    relationshipDiagram.MetaType = "SysML1.4::Sequence";
                    relationshipDiagram.Update();
                    parentElement.Diagrams.Refresh();
                    return relationshipDiagram;
                }
                if (diagramItem.Value.GetElementType() == SysmlConstants.STM && parentItem.GetEAID() != "")
                {
                    relationshipDiagram = parentElement.Diagrams.AddNew(diagramItem.Value.GetAttribute(AttributeConstants.NAME), DiagramConstants.statechart);
                    relationshipDiagram.MetaType = "SysML1.4::StateMachine";
                    relationshipDiagram.Update();
                    parentElement.Diagrams.Refresh();
                    return relationshipDiagram;
                }
                if (diagramItem.Value.GetElementType() == SysmlConstants.UC && parentItem.GetEAID() != "")
                {
                    relationshipDiagram = parentElement.Diagrams.AddNew(diagramItem.Value.GetAttribute(AttributeConstants.NAME), DiagramConstants.useCase);
                    relationshipDiagram.MetaType = "SysML1.4::UseCase";
                    relationshipDiagram.Update();
                    parentElement.Diagrams.Refresh();
                    return relationshipDiagram;
                }

            }
            else
            {
                relationshipDiagram = orphanedPackage.Diagrams.AddNew(diagramItem.Value.GetAttribute(AttributeConstants.NAME), DiagramConstants.component);
                relationshipDiagram.Update();
                orphanedPackage.Diagrams.Refresh();
                return relationshipDiagram;
            }
            return relationshipDiagram;
        }
        public void CheckDiagramSysMLCompliance(EA.Diagram diagram, string diagramType, string parentType)
        {
            if (diagramType == SysmlConstants.SYSMLACTIVITYDIAGRAM)
            {
                if (parentType != SysmlConstants.SYSMLACTIVITY)
                {
                    exportLog.Add("Diagram location is not SysML compliant: Activity diagrams must be a child of an activity");
                    exportLog.Add("     Diagram name: " + diagram.Name + " - Diagram GUID: " + diagram.DiagramGUID);
                }
            }
            if (diagramType == SysmlConstants.SYSMLBLOCKDEFINITIONDIAGRAM)
            {
                if (parentType == SysmlConstants.SYSMLBLOCK || parentType == SysmlConstants.SYSMLPACKAGE || parentType == SysmlConstants.SYSMLCONSTRAINTBLOCK || parentType == SysmlConstants.SYSMLACTIVITY) { }
                else
                {
                    exportLog.Add("Diagram location is not SysML compliant: Block definition diagrams must be a child of a block, package, constraint block, or activity");
                    exportLog.Add("     Diagram name: " + diagram.Name + " - Diagram GUID: " + diagram.DiagramGUID);
                }
            }
            if (diagramType == SysmlConstants.SYSMLINTERNALBLOCKDIAGRAM)
            {
                if (parentType == SysmlConstants.SYSMLBLOCK || parentType == SysmlConstants.SYSMLCONSTRAINTBLOCK) { }
                else
                {
                    exportLog.Add("Diagram location is not SysML compliant: Internal block diagrams must be a child of a block or constraint block");
                    exportLog.Add("     Diagram name: " + diagram.Name + " - Diagram GUID: " + diagram.DiagramGUID);
                }
            }
            if (diagramType == SysmlConstants.SYSMLPACKAGEDIAGRAM)
            {
                if (parentType == "sysml.Model" || parentType == "sysml.Package" || parentType == "sysml.ModelLibrary" || parentType == "sysml.Profile")
                { }
                else
                {
                    exportLog.Add("Diagram location is not SysML compliant: Package diagrams must be a child of a model, package, model library or profile");
                    exportLog.Add("     Diagram name: " + diagram.Name + " - Diagram GUID: " + diagram.DiagramGUID);
                }
            }
            if (diagramType == SysmlConstants.SYSMLPARAMETRICDIAGRAM)
            {
                if (parentType == SysmlConstants.SYSMLBLOCK || parentType == SysmlConstants.SYSMLCONSTRAINTBLOCK) { }
                else
                {
                    exportLog.Add("Diagram location is not SysML compliant: Parametric diagrams must be a child of a block or constraint block");
                    exportLog.Add("     Diagram name: " + diagram.Name + " - Diagram GUID: " + diagram.DiagramGUID);
                }
            }
            if (diagramType == SysmlConstants.SYSMLREQUIREMENTSDIAGRAM)
            {
                if (parentType == SysmlConstants.SYSMLPACKAGE || parentType == SysmlConstants.SYSMLREQUIREMENT || parentType == SysmlConstants.SYSMLMODELIBRARY || parentType == SysmlConstants.SYSMLMODEL) { }
                else
                {
                    exportLog.Add("Diagram location is not SysML compliant: Requirements diagrams must be a child of a package, requirement, model library or model");
                    exportLog.Add("     Diagram name: " + diagram.Name + " - Diagram GUID: " + diagram.DiagramGUID);
                }
            }
            if (diagramType == SysmlConstants.SYSMLSEQUENCEDIAGRAM)
            {
                if (parentType != SysmlConstants.SYSMLINTERACTION)
                {
                    exportLog.Add("Diagram location is not SysML compliant: Sequence diagrams must be a child of a interaction");
                    exportLog.Add("     Diagram name: " + diagram.Name + " - Diagram GUID: " + diagram.DiagramGUID);
                }
            }
            if (diagramType == SysmlConstants.SYSMLSTATEMACHINEDIAGRAM)
            {
                if (parentType != SysmlConstants.SYSMLSTATEMACHINE)
                {
                    exportLog.Add("Diagram location is not SysML compliant: State machine diagrams must be a child of a state machine");
                    exportLog.Add("     Diagram name: " + diagram.Name + " - Diagram GUID: " + diagram.DiagramGUID);
                }
            }
            if (diagramType == SysmlConstants.SYSMLUSECASEDIAGRAM)
            {
                if (parentType == SysmlConstants.SYSMLMODEL || parentType == SysmlConstants.SYSMLPACKAGE || parentType == SysmlConstants.SYSMLMODELIBRARY || parentType == SysmlConstants.SYSMLBLOCK) { }
                else
                {
                    exportLog.Add("Diagram location is not SysML compliant: Package diagrams must be a child of a model, package, model library or block");
                    exportLog.Add("     Diagram name: " + diagram.Name + " - Diagram GUID: " + diagram.DiagramGUID);
                }
            }

        }
        public void CheckElementSysMLCompliance(EA.Element element, string elementType, string parentType)
        {
            // Check is not complete. Need to add check for other types
            List<string> activityNodesEdges = new List<string> {
                SysmlConstants.SYSMLACCEPTEVENTACTION, SysmlConstants.SYSMLACTION, SysmlConstants.SYSMLACTIVITYFINALNODE, SysmlConstants.SYSMLACTIVITYPARAMETERNODE,
                SysmlConstants.SYSMLACTIVITYPARTITION, SysmlConstants.SYSMLCALLBEHAVIORACTION, SysmlConstants.SYSMLCALLOPERATIONACTION, SysmlConstants.SYSMLCENTRALBUFFERNODE,
                SysmlConstants.SYSMLCONDITIONALNODE, SysmlConstants.SYSMLCREATEOBJECTACTION, SysmlConstants.SYSMLDATASTORENODE, SysmlConstants.SYSMLDECISIONNODE,
                SysmlConstants.SYSMLDESTROYOBJECTACTION, SysmlConstants.SYSMLFLOWFINALNODE, SysmlConstants.SYSMLFORKNODE, SysmlConstants.SYSMLINITIALNODE, SysmlConstants.SYSMLINPUTPIN,
                SysmlConstants.SYSMLJOINNODE, SysmlConstants.SYSMLLOOPNODE, SysmlConstants.SYSMLMERGENODE, SysmlConstants.SYSMLOPAQUEACTION, SysmlConstants.SYSMLSENDSIGNALACTION,
                SysmlConstants.SYSMLSTRUCTUREDACTIVITYACTION, SysmlConstants.SYSMLCONTROLFLOW, SysmlConstants.SYSMLOBJECTFLOW
            };
            if (activityNodesEdges.Contains(elementType) && parentType != SysmlConstants.SYSMLACTIVITY)
            {
                elementType = elementType.Split('.')[1];
                exportLog.Add("Element location is not SysML compliant: " + elementType + " must be a child of an activity");
                exportLog.Add("          Name=" + element.Name + " - GUID=" + element.ElementGUID + " - Type=" + element.Type + " - Stereotype=" + element.Stereotype);
            }
        }
        public bool IsPortType(string type)
        {
            bool isPort = false;
            if (type == "Port" || type == "FlowPort" || type == "FullPort" || type == "ProxyPort")
            {
                isPort = true;
            }
            return isPort;
        }
        internal string GetEAType(string sysmlType, string stereotype)
        {
            string type = "";
            if (sysmlType == SysmlConstants.ACTIVITY) type = ActivityConstants.ACTIVITY;
            else if (sysmlType == SysmlConstants.ACTIVITYFINALNODE) type = ActivityConstants.STATENODE;
            else if (sysmlType == SysmlConstants.ACTIVITYPARAMETERNODE) type = ActivityConstants.ACTIVITYPARAMETER;
            else if (sysmlType == SysmlConstants.ACTIVITYPARTITION) type = ActivityConstants.ACTIVITYPARTION;
            else if (sysmlType == SysmlConstants.BLOCK)
            {
                if (stereotype == StereotypeConstants.CONSTRAINTBLOCKCAP || stereotype == StereotypeConstants.CONSTRAINTBLOCK) type = BlockConstants.CONSTRAINTBLOCK;
                else type = BlockConstants.BLOCK;
            }
            else if (sysmlType == SysmlConstants.ACCEPTEVENTACTION) type = ActivityConstants.ACCEPTEVENTACTION;
            else if (sysmlType == SysmlConstants.ACTION) type = ActivityConstants.ACTION;
            else if (sysmlType == SysmlConstants.ACTIONPIN) type = ActivityConstants.ACTIONPIN;
            else if (sysmlType == SysmlConstants.ACTIVITYINITIALNODE) type = ActivityConstants.STATENODE;
            else if (sysmlType == SysmlConstants.ACTOR) type = UseCaseConstants.ACTOR;
            else if (sysmlType == SysmlConstants.ASSOCIATIONBLOCK) type = BlockConstants.ASSOCIATION;
            else if (sysmlType == SysmlConstants.BOUNDREFERENCE) type = InternalBlockConstants.BOUNDREFERENCE;
            else if (sysmlType == SysmlConstants.BOUNDARY) type = InternalBlockConstants.BOUNDARY;
            else if (sysmlType == SysmlConstants.BUSINESSREQUIREMENT) type = RequirementConstants.REQUIREMENT;
            else if (sysmlType == SysmlConstants.CLASSIFIERBEHAVIORPROPERTY) type = InternalBlockConstants.PROPERTY;
            else if (sysmlType == SysmlConstants.CALLBEHAVIORACTION) type = ActivityConstants.CALLBEHAVIORACTION;
            else if (sysmlType == SysmlConstants.CALLOPERATIONACTION) type = ActivityConstants.CALLOPERATIONACTION;
            else if (sysmlType == SysmlConstants.CENTRALBUFFERNODE) type = ActivityConstants.CENTRALBUFFERNODE;
            else if (sysmlType == SysmlConstants.CHANGE) type = ActivityConstants.CHANGE;
            else if (sysmlType == SysmlConstants.CHOICEPSUEDOSTATE) type = ActivityConstants.STATENODE;
            else if (sysmlType == SysmlConstants.CLASS) type = ProfileConstants.CLASSTYPE;
            else if (sysmlType == SysmlConstants.CLASSIFICATION) type = ProfileConstants.PART;
            else if (sysmlType == SysmlConstants.COLLABORATION) type = SequenceConstants.COLLABORATION;
            else if (sysmlType == SysmlConstants.CONSTRAINTBLOCK) type = BlockConstants.CONSTRAINTBLOCK;
            else if (sysmlType == SysmlConstants.CONSTRAINT) type = ProfileConstants.CONSTRAINT;
            else if (sysmlType == SysmlConstants.CONSTRAINTBLOCK) type = BlockConstants.BLOCK;
            else if (sysmlType == SysmlConstants.CONSTRAINTPARAMETER) type = ActivityConstants.PROPERTY;
            else if (sysmlType == SysmlConstants.CONDITIONALNODE) type = ActivityConstants.CONDITIONALNODE;
            else if (sysmlType == SysmlConstants.COMBINEDFRAGMENT) type = SequenceConstants.INTERACTIONFRAGMENT;
            else if (sysmlType == SysmlConstants.CREATEOBJECTACTION) type = ActivityConstants.CREATEOBJECTACTION;
            else if (sysmlType == SysmlConstants.DATASTORENODE) type = ProfileConstants.OBJECTTYPE;
            else if (sysmlType == SysmlConstants.DECISIONNODE) type = ActivityConstants.DECISION;
            else if (sysmlType == SysmlConstants.DEEPHISTORY) type = StateMachineConstants.STATENODE;
            else if (sysmlType == SysmlConstants.DESIGNCONSTRAINT) type = RequirementConstants.DESIGNCONSTRAINT;
            else if (sysmlType == SysmlConstants.DESTROYOBJECTACTION) type = ActivityConstants.DESTROYOBJECTACTION;
            else if (sysmlType == SysmlConstants.DOMAIN) type = BlockConstants.BLOCK;
            else if (sysmlType == SysmlConstants.ENTRYPOINT) type = StateMachineConstants.ENTRYPOINT;
            else if (sysmlType == SysmlConstants.ENUMERATION) type = BlockConstants.ENUMERATION;
            else if (sysmlType == SysmlConstants.EVENT) type = ActivityConstants.EVENTTYPE;
            else if (sysmlType == SysmlConstants.EXCEPTIONHANDLER) type = ProfileConstants.EXCEPTIONHANDLER;
            else if (sysmlType == SysmlConstants.EXTENDEDREQUIREMENT) type = RequirementConstants.EXTENDEDREQUIREMENT;
            else if (sysmlType == SysmlConstants.EXITPOINT) type = StateMachineConstants.EXITPOINT;
            else if (sysmlType == SysmlConstants.EXTERNAL) type = BlockConstants.EXTERNAL;
            else if (sysmlType == SysmlConstants.LIFELINE) type = SequenceConstants.SEQUENCE;
            else if (sysmlType == SysmlConstants.PACKAGE) type = ProfileConstants.PACKAGE;
            else if (sysmlType == SysmlConstants.PROPERTY) type = InternalBlockConstants.PROPERTY;
            else if (sysmlType == SysmlConstants.PORT) type = BlockConstants.PORT;
            else if (sysmlType == SysmlConstants.PROXYPORT) type = BlockConstants.PROXYPORT;
            else if (sysmlType == SysmlConstants.FINALSTATE) type = ActivityConstants.STATENODE;
            else if (sysmlType == SysmlConstants.FULLPORT) type = BlockConstants.FULLPORT;
            else if (sysmlType == SysmlConstants.FLOWFINALNODE) type = ActivityConstants.STATENODE;
            else if (sysmlType == SysmlConstants.FLOWPORT) type = BlockConstants.FLOWPORT;
            else if (sysmlType == SysmlConstants.FUNCTIONALREQUIREMENT) type = RequirementConstants.FUNCTIONALREQUIREMENT;
            else if (sysmlType == SysmlConstants.HYPERLINK) type = ProfileConstants.TEXT;
            else if (sysmlType == SysmlConstants.INFORMATIONITEM) type = ActivityConstants.INFORMATIONITEM;
            else if (sysmlType == SysmlConstants.INITIALPSEUDOSTATE) type = ActivityConstants.STATENODE;
            else if (sysmlType == SysmlConstants.INPUTPIN) type = ActivityConstants.ACTIONPIN;
            else if (sysmlType == SysmlConstants.INTERACTION) type = SequenceConstants.INTERACTION;
            else if (sysmlType == SysmlConstants.INTERFACE) type = BlockConstants.INTERFACETYPE;
            else if (sysmlType == SysmlConstants.INTERFACEREQUIREMENT) type = RequirementConstants.INTERFACEREQUIREMENT;
            else if (sysmlType == SysmlConstants.INTERFACEBLOCK) type = BlockConstants.INTERFACEBLOCK;
            else if (sysmlType == SysmlConstants.INSTANCESPECIFICATION) type = ProfileConstants.OBJECTTYPE;
            else if (sysmlType == SysmlConstants.INTERRUPTIBLEACTIVITYREGION) type = ActivityConstants.INTERRUPTIBLEACTIVITYREGION;
            else if (sysmlType == SysmlConstants.MERGENODE) type = ActivityConstants.MERGENODE;
            else if (sysmlType == SysmlConstants.METACLASS) type = StereotypeConstants.METACLASS;
            else if (sysmlType == SysmlConstants.NAVIGATIONCELL) type = ProfileConstants.TEXT;
            else if (sysmlType == SysmlConstants.NOTE) type = ProfileConstants.NOTE;
            else if (sysmlType == SysmlConstants.OBJECT) type = ProfileConstants.OBJECTTYPE;
            else if (sysmlType == SysmlConstants.OBJECTNODE) type = ActivityConstants.OBJECTNODE;
            else if (sysmlType == SysmlConstants.OBJECTIVEFUNCTION) type = ProfileConstants.OBJECTIVEFUNCTION;
            else if (sysmlType == SysmlConstants.OPERATION) type = BlockConstants.OPERATION;
            else if (sysmlType == SysmlConstants.OPAQUEACTION) type = ActivityConstants.ACTION;
            else if (sysmlType == SysmlConstants.OUTPUTPIN) type = ActivityConstants.ACTIONPIN;
            else if (sysmlType == SysmlConstants.JOIN) type = StateMachineConstants.SYNCHRONIZATION;
            else if (sysmlType == SysmlConstants.JOINNODE) type = ActivityConstants.SYNCHRONIZATION;
            else if (sysmlType == SysmlConstants.FORK) type = StateMachineConstants.SYNCHRONIZATION;
            else if (sysmlType == SysmlConstants.FLOWPROPERTY) type = InternalBlockConstants.FLOWPROPERTY;
            else if (sysmlType == SysmlConstants.FORKNODE) type = ActivityConstants.SYNCHRONIZATION;
            else if (sysmlType == SysmlConstants.VALUEPROPERTY) type = BlockConstants.DATATYPE;
            else if (sysmlType == SysmlConstants.PARTPROPERTY) type = BlockConstants.PROPERTY;
            else if (sysmlType == SysmlConstants.REFERENCEPROPERTY) type = InternalBlockConstants.REFERENCEPROPERTY;
            else if (sysmlType == SysmlConstants.REQUIREMENT) type = RequirementConstants.REQUIREMENT;
            else if (sysmlType == SysmlConstants.REGION) type = StateMachineConstants.REGION;
            else if (sysmlType == SysmlConstants.CONSTRAINTPROPERTY) type = InternalBlockConstants.CONSTRAINTPROPERTY;
            else if (sysmlType == SysmlConstants.PARTICIPANTPROPERTY) type = InternalBlockConstants.PARTICIPANTPROPERTY;
            else if (sysmlType == SysmlConstants.PERFORMANCEREQUIREMENT) type = RequirementConstants.PERFORMANCEREQUIREMENT;
            else if (sysmlType == SysmlConstants.PHYSICALREQUIREMENT) type = RequirementConstants.PHYSICALREQUIREMENT;
            else if (sysmlType == SysmlConstants.PORT) type = BlockConstants.PORT;
            else if (sysmlType == SysmlConstants.SENDSIGNALACTION) type = ActivityConstants.SENDSIGNALACTION;
            else if (sysmlType == SysmlConstants.SHALLOWHISTORY) type = StateMachineConstants.STATENODE;
            else if (sysmlType == SysmlConstants.SIGNAL) type = BlockConstants.SIGNAL;
            else if (sysmlType == SysmlConstants.STEREOTYPE) type = ProfileConstants.CLASSTYPE;
            else if (sysmlType == SysmlConstants.STATE) type = StateMachineConstants.STATE;
            else if (sysmlType == SysmlConstants.STATEINVARIANT) type = SequenceConstants.INTERACTIONSTATE;
            else if (sysmlType == SysmlConstants.STATEMACHINE) type = StateMachineConstants.STATEMACHINE;
            else if (sysmlType == SysmlConstants.SUBSYSTEM) type = BlockConstants.BLOCK;
            else if (sysmlType == SysmlConstants.SYNCHRONIZATION) type = StateMachineConstants.SYNCHRONIZATION;
            else if (sysmlType == SysmlConstants.SYSTEM) type = BlockConstants.BLOCK;
            else if (sysmlType == SysmlConstants.SYSTEMCONTEXT) type = BlockConstants.BLOCK;
            else if (sysmlType == SysmlConstants.TERMINATE) type = StateMachineConstants.STATENODE;
            else if (sysmlType == SysmlConstants.TEXT) type = ProfileConstants.TEXT;
            else if (sysmlType == SysmlConstants.TRIGGER) type = StateMachineConstants.TRIGGER;
            else if (sysmlType == SysmlConstants.UNIT) type = BlockConstants.UNIT;
            else if (sysmlType == SysmlConstants.USECASE) type = UseCaseConstants.USECASE;
            else if (sysmlType == SysmlConstants.QUANTITYKIND) type = BlockConstants.QUANTITYKIND;
            else if (sysmlType == SysmlConstants.VALUETYPE) type = BlockConstants.VALUETYPE;
            else if (sysmlType == SysmlConstants.FLOWSPECIFICATION) type = BlockConstants.INTERFACETYPE;
            return type;
        }
        private string GetSysMLType(string type, string stereotype, int subtype, string metatype)
        {
            string elementType = "";
            if (stereotype == StereotypeConstants.BLOCK) elementType = SysmlConstants.SYSMLBLOCK;
            else if (stereotype == StereotypeConstants.HARDWARE) elementType = SysmlConstants.SYSMLBLOCK;
            else if (stereotype == StereotypeConstants.BOUNDREFERENCE) elementType = SysmlConstants.SYSMLBOUNDREFERENCE;
            else if (stereotype == StereotypeConstants.VALUEPROPERTY) elementType = SysmlConstants.SYSMLVALUEPROPERTY;
            else if (stereotype == StereotypeConstants.PARTICIPANTPROPERTY) elementType = SysmlConstants.SYSMLPARTICIPANTPROPERTY;
            else if (stereotype == StereotypeConstants.DECISION) elementType = SysmlConstants.SYSMLDECISIONNODE;
            else if (stereotype == StereotypeConstants.DOMAIN) elementType = SysmlConstants.SYSMLBLOCK;
            else if (stereotype == StereotypeConstants.OBJECTSTEREOTYPE) elementType = SysmlConstants.SYSMLOBJECT;
            else if (stereotype == StereotypeConstants.CONSTRAINTPROPERTY) elementType = SysmlConstants.SYSMLCONSTRAINTPROPERTY;
            else if (stereotype == StereotypeConstants.VALUETYPE) elementType = SysmlConstants.SYSMLVALUEPROPERTY;
            else if (stereotype == StereotypeConstants.FLOWPROPERTY) elementType = SysmlConstants.SYSMLFLOWPROPERTY;
            else if (stereotype == StereotypeConstants.CONSTRAINTPARAMETER) elementType = SysmlConstants.SYSMLCONSTRAINTPARAMETER;
            else if (stereotype == StereotypeConstants.CONSTRAINTBLOCK || stereotype == StereotypeConstants.CONSTRAINTBLOCKCAP) elementType = SysmlConstants.SYSMLCONSTRAINTBLOCK;
            else if (stereotype == StereotypeConstants.CLASSIFIERBEHAVIORPROPERTY) elementType = SysmlConstants.SYSMLCLASSIFIERBEHAVIORPROPERTY;
            else if (stereotype == StereotypeConstants.STEREOTYPE) elementType = SysmlConstants.SYSMLSTEREOTYPE;
            else if (stereotype == StereotypeConstants.OBJECTIVEFUNCTION) elementType = SysmlConstants.SYSMLOBJECTIVEFUNCTION;
            else if (stereotype == StereotypeConstants.METACLASS && type == SysmlConstants.CLASS) elementType = SysmlConstants.SYSMLMETACLASS;
            else if (stereotype == "" && type == SysmlConstants.CLASS) elementType = SysmlConstants.SYSMLCLASS;
            else if (stereotype != "" && stereotype != StereotypeConstants.METACLASS && stereotype != StereotypeConstants.STEREOTYPE && stereotype != StereotypeConstants.INTERFACEBLOCK
                        && stereotype != StereotypeConstants.DOMAIN && stereotype != StereotypeConstants.EXTERNAL && stereotype != StereotypeConstants.SYSTEM && stereotype != StereotypeConstants.SUBSYSTEM
                        && stereotype != StereotypeConstants.SYSTEMCONTEXT && type == SysmlConstants.CLASS) elementType = SysmlConstants.SYSMLCLASS;
            else if (stereotype == StereotypeConstants.VALUETYPE && type != SysmlConstants.ENUMERATION) elementType = SysmlConstants.SYSMLVALUEPROPERTY;
            else if (stereotype == StereotypeConstants.VALUETYPE && type == SysmlConstants.ENUMERATION) elementType = SysmlConstants.SYSMLENUMERATION;
            else if (type == SysmlConstants.ACTIVITY) elementType = SysmlConstants.SYSMLACTIVITY;
            else if (type == SysmlConstants.ACTIVITYPARAMETER) elementType = SysmlConstants.SYSMLACTIVITYPARAMETERNODE;
            else if (type == SysmlConstants.ACTIVITYPARTITION) elementType = SysmlConstants.SYSMLACTIVITYPARTITION;
            else if (type == SysmlConstants.ACTOR) elementType = SysmlConstants.SYSMLACTOR;
            else if (type == SysmlConstants.BOUNDARY) elementType = SysmlConstants.SYSMLBOUNDARY;
            else if (type == SysmlConstants.SIGNAL) elementType = SysmlConstants.SYSMLSIGNAL;
            else if (type == SysmlConstants.CENTRALBUFFERNODE) elementType = SysmlConstants.SYSMLCENTRALBUFFERNODE;
            else if (type == SysmlConstants.COLLABORATION) elementType = SysmlConstants.SYSMLCOLLABORATION;
            else if (type == SysmlConstants.CONSTRAINT) elementType = SysmlConstants.SYSMLCONSTRAINT;
            else if (type == SysmlConstants.CHANGE) elementType = SysmlConstants.SYSMLCHANGE;
            else if (type == SysmlConstants.DECISION) elementType = SysmlConstants.SYSMLDECISIONNODE;
            else if (type == SysmlConstants.ENTRYPOINT) elementType = SysmlConstants.SYSMLENTRYPOINT;
            else if (type == SysmlConstants.ENUMERATION) elementType = SysmlConstants.SYSMLENUMERATION;
            else if (type == SysmlConstants.EXCEPTIONHANDLER) elementType = SysmlConstants.SYSMLEXCEPTIONHANDLER;
            else if (type == SysmlConstants.EXITPOINT) elementType = SysmlConstants.SYSMLEXITPOINT;
            else if (type == SysmlConstants.SEQUENCE) elementType = SysmlConstants.SYSMLLIFELINE;
            else if (type == SysmlConstants.REGION) elementType = SysmlConstants.SYSMLREGION;
            else if (type == SysmlConstants.REQUIREMENT && stereotype == StereotypeConstants.BUSINESSREQUIREMENT) elementType = SysmlConstants.SYSMLREQUIREMENT;
            else if (type == SysmlConstants.REQUIREMENT) elementType = SysmlConstants.SYSMLREQUIREMENT;
            else if (type == SysmlConstants.EXTENDEDREQUIREMENT) elementType = SysmlConstants.SYSMLEXTENDEDREQUIREMENT;
            else if (type == SysmlConstants.FUNCTIONALREQUIREMENT) elementType = SysmlConstants.SYSMLFUNCTIONALREQUIREMENT;
            else if (type == SysmlConstants.INTERFACEREQUIREMENT) elementType = SysmlConstants.SYSMLINTERFACEREQUIREMENT;
            else if (type == SysmlConstants.INTERACTIONFRAGMENT) elementType = SysmlConstants.SYSMLCOMBINEDFRAGMENT;
            else if (type == SysmlConstants.INTERACTIONSTATE && subtype == 0) elementType = SysmlConstants.SYSMLSTATEINVARIANT;
            else if (type == SysmlConstants.INFORMATIONITEM) elementType = SysmlConstants.SYSMLINFORMATIONITEM;
            else if (type == SysmlConstants.PERFORMANCEREQUIREMENT) elementType = SysmlConstants.SYSMLPERFORMANCEREQUIREMENT;
            else if (type == SysmlConstants.PHYSICALREQUIREMENT) elementType = SysmlConstants.SYSMLPHYSICALREQUIREMENT;
            else if (type == SysmlConstants.DESIGNCONSTRAINT) elementType = SysmlConstants.SYSMLDESIGNCONSTRAINT;
            else if (type == SysmlConstants.CONDITIONALNODE) elementType = SysmlConstants.SYSMLCONDITIONALNODE;
            else if (type == SysmlConstants.INTERACTION) elementType = SysmlConstants.SYSMLINTERACTION;
            else if (type == SysmlConstants.OBJECT) elementType = SysmlConstants.SYSMLOBJECT;
            else if (type == SysmlConstants.OBJECTNODE) elementType = SysmlConstants.SYSMLOBJECTNODE;
            else if (type == SysmlConstants.USECASE) elementType = SysmlConstants.SYSMLUSECASE;
            else if (type == SysmlConstants.OBJECT && stereotype == StereotypeConstants.DATASTORE) elementType = SysmlConstants.SYSMLDATASTORENODE;
            else if (type == SysmlConstants.SYNCHRONIZATION && stereotype == StereotypeConstants.FORK) elementType = SysmlConstants.SYSMLFORKNODE;
            else if (type == SysmlConstants.PORT)
            {
                if (stereotype == StereotypeConstants.FLOWPORT) elementType = SysmlConstants.SYSMLFLOWPORT;
                else if (stereotype == StereotypeConstants.FULLPORT) elementType = SysmlConstants.SYSMLFULLPORT;
                else if (stereotype == StereotypeConstants.PROXYPORT) elementType = SysmlConstants.SYSMLPROXYPORT;
                else
                {
                    elementType = SysmlConstants.SYSMLPORT;
                }
            }
            else if (stereotype == StereotypeConstants.INTERFACEBLOCK) elementType = SysmlConstants.SYSMLINTERFACEBLOCK;
            else if (type == SysmlConstants.INTERFACE && stereotype != StereotypeConstants.FLOWSPECIFICATION) elementType = SysmlConstants.SYSMLINTERFACE;
            else if (type == SysmlConstants.ACTIVITYPARAMETER) elementType = SysmlConstants.SYSMLACTIVITYPARAMETER;
            else if (type == SysmlConstants.ARTIFACT) elementType = SysmlConstants.SYSMLARTIFACT;
            else if (type == SysmlConstants.TRIGGER) elementType = SysmlConstants.SYSMLTRIGGER;
            else if (type == SysmlConstants.TEXT && stereotype == StereotypeConstants.NAVIGATIONCELL) elementType = SysmlConstants.EANAVIGATIONCELL;
            else if (type == SysmlConstants.TEXT) elementType = SysmlConstants.SYSMLTEXT;
            else if (type == SysmlConstants.NOTE) elementType = SysmlConstants.SYSMLNOTE;
            else if (type == SysmlConstants.STATENODE)
            {

                if (subtype == 3) elementType = SysmlConstants.SYSMLINITIALPSEUDOSTATE;
                else if (subtype == 4) elementType = SysmlConstants.SYSMLFINALSTATE;
                else if (subtype == 5) elementType = SysmlConstants.SYSMLSHALLOWHISTORY;
                else if (subtype == 11) elementType = SysmlConstants.SYSMLCHOICEPSEUDOSTATE;
                else if (subtype == 12) elementType = SysmlConstants.SYSMLTERMINATE;
                else if (subtype == 13) elementType = SysmlConstants.SYSMLENTRYPOINT;
                else if (subtype == 14) elementType = SysmlConstants.SYSMLEXITPOINT;
                else if (subtype == 15) elementType = SysmlConstants.SYSMLDEEPHISTORY;
                else if (subtype == 100) elementType = SysmlConstants.SYSMLINITIALNODE;
                else if (subtype == 101) elementType = SysmlConstants.SYSMLACTIVITYFINALNODE;
                else if (subtype == 102) elementType = SysmlConstants.SYSMLFLOWFINALNODE;
            }
            else if (type == SysmlConstants.STATE) elementType = SysmlConstants.SYSMLSTATE;
            else if (type == SysmlConstants.CLASS) elementType = SysmlConstants.SYSMLCLASS;
            else if (type == SysmlConstants.STATEMACHINE) elementType = SysmlConstants.SYSMLSTATEMACHINE;
            else if (type == SysmlConstants.PART && stereotype == "") elementType = SysmlConstants.SYSMLPARTPROPERTY;
            else if (type == SysmlConstants.PART && stereotype == StereotypeConstants.PARTPROPERTY) elementType = SysmlConstants.SYSMLPARTPROPERTY;
            else if (type == SysmlConstants.PART && stereotype == StereotypeConstants.PROPERTY) elementType = SysmlConstants.SYSMLPARTPROPERTY;
            else if (type == SysmlConstants.PART && stereotype == StereotypeConstants.CONSTRAINTPROPERTY) elementType = SysmlConstants.SYSMLCONSTRAINTPROPERTY;
            else if (type == SysmlConstants.PART && stereotype == StereotypeConstants.CLASSIFICATION) elementType = SysmlConstants.SYSMLCLASSIFICATION;
            else if (type == SysmlConstants.REQUIREDINTERFACE) elementType = SysmlConstants.SYSMLREQUIREDINTERFACE;
            else if (type == SysmlConstants.NOTE) elementType = SysmlConstants.SYSMLNOTE;
            else if (type == SysmlConstants.PACKAGE) elementType = SysmlConstants.SYSMLPACKAGE;
            else if (stereotype == StereotypeConstants.ALLOCATED || type == SysmlConstants.ACTION || type == SysmlConstants.ACTIVITYPARAMETER ||
                            type == SysmlConstants.ACTIONPIN || type == SysmlConstants.EVENT)
            {
                if (stereotype == StereotypeConstants.ALLOCATED) elementType = SysmlConstants.SYSMLALLOCATED;
                else if (type == SysmlConstants.ACTION && metatype == MetatypeConstants.acceptEventAction) elementType = SysmlConstants.SYSMLACCEPTEVENTACTION;
                else if (type == SysmlConstants.ACTION && metatype == MetatypeConstants.callBehaviorAction) elementType = SysmlConstants.SYSMLCALLBEHAVIORACTION;
                else if (type == SysmlConstants.ACTION && metatype == MetatypeConstants.opaqueAction) elementType = SysmlConstants.SYSMLOPAQUEACTION;
                else if (type == SysmlConstants.ACTION && metatype == MetatypeConstants.sendSignalAction) elementType = SysmlConstants.SYSMLSENDSIGNALACTION;
                else if (type == SysmlConstants.ACTION) elementType = SysmlConstants.SYSMLACTION;
                else if (type == SysmlConstants.ACTIVITYPARAMETER) elementType = SysmlConstants.SYSMLACTIVITYPARAMETER;
                else if (type == SysmlConstants.ACTIONPIN && stereotype == StereotypeConstants.OUTPUT) elementType = SysmlConstants.SYSMLOUTPUTPIN;
                else if (type == SysmlConstants.ACTIONPIN && stereotype == StereotypeConstants.INPUT) elementType = SysmlConstants.SYSMLINPUTPIN;
                else if (type == SysmlConstants.ACTIONPIN) elementType = SysmlConstants.SYSMLACTIONPIN;
                else if (type == SysmlConstants.EVENT) elementType = SysmlConstants.SYSMLEVENT;
            }
            else if (type == SysmlConstants.MERGENODE) elementType = SysmlConstants.SYSMLMERGENODE;
            else if (type == SysmlConstants.SYNCHRONIZATION)
            {
                if (stereotype == StereotypeConstants.JOIN) elementType = SysmlConstants.SYSMLJOIN;
                else if (stereotype == StereotypeConstants.FORK) elementType = SysmlConstants.SYSMLFORK;
                else elementType = SysmlConstants.SYSMLSYNCHRONIZATION;
            }
            else if (type == SysmlConstants.INTERRUPTIBLEACTIVITYREGION ) elementType = SysmlConstants.SYSMLINTERRUPTIBLEACTIVITYREGION;
            else if (stereotype == StereotypeConstants.FLOWSPECIFICATION) elementType = SysmlConstants.SYSMLFLOWSPECIFICATION;
            else if (stereotype == StereotypeConstants.EXTERNAL || stereotype == StereotypeConstants.SUBSYSTEM || stereotype == StereotypeConstants.SYSTEM || stereotype == StereotypeConstants.SYSTEMCONTEXT) elementType = SysmlConstants.SYSMLBLOCK;
            return elementType;
        }
    }
}
