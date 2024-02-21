/*
 * Copyright 2022 The Aerospace Corporation
 * 
 * Author: Karina Martinez
 * 
 * Description: Functions used to export model to HUDS XML
 * 
 */

using EA;
using MTIP.Constants;
using MTIP.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace MTIP.Translations
{
    public class MTIPExportFunctions
    {
        public EA.Repository repository;
        private string outputDirectory;
        public Dictionary<string, EA.Package> profilePackages;
        public List<string> exportLog;
        public Dictionary<string, string> customProfiles;

        public MTIPExportFunctions(MTIP plugin)
        {
            repository = plugin.GetRepository();
            profilePackages = new Dictionary<string, EA.Package>();
            exportLog = new List<string>();
            customProfiles = new Dictionary<string, string>();
        }
        public void ExportToMTIPXML()
        {
            GetCustomProfiles();
            //Create XML document that will be exported
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.LoadXml("<packet></packet>");

            // Get selected package to export
            EA.Package selectedPackage = repository.GetTreeSelectedPackage();

            selectOutputDir();
            MessageBox.Show("Beginning export. This might take a while.", "Begin Export", MessageBoxButtons.OK);

            UnpackagePackage(selectedPackage, xmlDocument, null, null, true);

            // Save XML
            XmlWriterSettings settings = new XmlWriterSettings()
            {
                CheckCharacters = false
            };

            //Remove illegal characters
            string correctedXmlString = xmlDocument.InnerXml;
            correctedXmlString = Regex.Replace(correctedXmlString, "&#xB;", "");
            correctedXmlString = Regex.Replace(correctedXmlString, "&amp;", "and");
            correctedXmlString = Regex.Replace(correctedXmlString, "-&gt;", "to");
            correctedXmlString = Regex.Replace(correctedXmlString, "&#x1D;", "");
            correctedXmlString = Regex.Replace(correctedXmlString, "&gt;", "greater than");
            correctedXmlString = Regex.Replace(correctedXmlString, "&lt;", "less than");
            //correctedXmlString = Regex.Replace(correctedXmlString, "&lt;", "(");
            //correctedXmlString = Regex.Replace(correctedXmlString, "&lt;", ")");
            correctedXmlString = Regex.Replace(correctedXmlString, @"[\u0000-\u0008,\u000B,\u000C,\u000E-\u001F]", "");
            xmlDocument.LoadXml(correctedXmlString);

            settings.Indent = true;
            string exportedFname = Path.Combine(outputDirectory, selectedPackage.Name + ".xml");
            XmlWriter xmlWriter = XmlWriter.Create(exportedFname, settings);
            xmlDocument.Save(xmlWriter);

            xmlWriter.Close();

            customProfiles.Clear();

            //Create XML error log for exported model
            XmlDocument exportDocument = new XmlDocument();
            exportDocument.LoadXml("<exportLog></exportLog>");

            foreach (string logLine in exportLog)
            {
                XmlElement logElement = exportDocument.CreateElement("log");
                logElement.InnerText = logLine;
                exportDocument.DocumentElement.AppendChild(logElement);
            }
            string exportedLname = Path.Combine(outputDirectory, selectedPackage.Name + "_log.xml");
            XmlWriter xmlEWriter = XmlWriter.Create(exportedLname, settings);
            exportDocument.Save(xmlEWriter);
            xmlEWriter.Close();

            MessageBox.Show("XML Export is Complete");

        }
        private void selectOutputDir()
        {
            string outputDir = Tools.PromptForDirectory();
            if (outputDir != null)
            {
                this.outputDirectory = outputDir;
                //startExportButton.Enabled = true;
            }


        }
        public void UnpackagePackage(EA.Package package, XmlDocument xmlDocument, string parentGuid, string parentType, bool isRootNode)
        {
            // Get package SysML type
            XmlElement typeElement = xmlDocument.CreateElement(HUDSConstants.TYPE);
            typeElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
            string packageType = "";
            if (package.IsModel) packageType = SysmlConstants.SYSMLMODEL;
            else packageType = GetPackageType(package);
            typeElement.InnerText = packageType;


            // Get EA id for the package
            string packageGuid = package.PackageGUID.Substring(1, package.PackageGUID.Length - 2);
            
            XmlElement idElement = MTIPCommon.CreateIdElement(xmlDocument, packageGuid);

            // Create attributes element to add to data node
            XmlElement attributesElement = xmlDocument.CreateElement(AttributeConstants.ATTRIBUTES);
            attributesElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);
            CreateHUDSAttribute(xmlDocument, AttributeConstants.NAME, HUDSConstants.STR, package.Name, attributesElement);

            if (package.IsModel != true)
            {
                if (package.Element.Stereotype != "")
                {
                    XmlElement stereotypeAttribute = xmlDocument.CreateElement(AttributeConstants.ATTRIBUTE);
                    stereotypeAttribute.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);
                    stereotypeAttribute.SetAttribute(HUDSConstants.KEY, AttributeConstants.STEREOTYPE);

                    XmlElement stereotypeNameAttribute = xmlDocument.CreateElement(AttributeConstants.ATTRIBUTE);
                    stereotypeNameAttribute.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                    stereotypeNameAttribute.SetAttribute(HUDSConstants.KEY, AttributeConstants.STEREOTYPENAME);
                    stereotypeNameAttribute.InnerText = package.Element.Stereotype;


                    stereotypeAttribute.AppendChild(stereotypeNameAttribute);
                    attributesElement.AppendChild(stereotypeAttribute);
                }
                if (package.Notes != "")
                {
                    string note = Regex.Replace(package.Notes, "<[^>]+>", string.Empty);
                    note = Regex.Replace(note, "&gt;", "greater than");
                    note = Regex.Replace(note, "&lt;", "less than");
                    CreateHUDSAttribute(xmlDocument, AttributeConstants.DOCUMENTATION, HUDSConstants.STR, note, attributesElement);
                }
                if (package.Element.Alias != "") CreateHUDSAttribute(xmlDocument, AttributeConstants.ALIAS, HUDSConstants.STR, package.Element.Alias, attributesElement);
            }

            // Add type, id, and attributes node to data node
            XmlElement dataElement = xmlDocument.CreateElement(HUDSConstants.DATA);
            dataElement.AppendChild(typeElement);
            dataElement.AppendChild(idElement);
            dataElement.AppendChild(attributesElement);

            // Add relationships to the data node
            if (parentGuid != null || isRootNode == false)
            {
                XmlElement relationshipsElement = xmlDocument.CreateElement(RelationshipConstants.RELATIONSHIPS);
                relationshipsElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);

                XmlElement hasParentElement = xmlDocument.CreateElement(RelationshipConstants.HASPARENT);
                hasParentElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);

                XmlElement hasParentTypeElement = xmlDocument.CreateElement(RelationshipConstants.TYPE);
                hasParentTypeElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                hasParentTypeElement.InnerText = parentType;

                XmlElement hasParentIdElement = xmlDocument.CreateElement(RelationshipConstants.ID);
                hasParentIdElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                hasParentIdElement.InnerText = parentGuid;

                XmlElement relMetadataElement = xmlDocument.CreateElement(HUDSConstants.RELATIONSHIPMETADATA);

                hasParentElement.AppendChild(hasParentTypeElement);
                hasParentElement.AppendChild(hasParentIdElement);
                hasParentElement.AppendChild(relMetadataElement);
                relationshipsElement.AppendChild(hasParentElement);
                dataElement.AppendChild(relationshipsElement);
            }

            XmlNode rootNode = xmlDocument.DocumentElement;
            rootNode.AppendChild(dataElement);

            // Check if package has any additional child packages, elements, or diagrams and create XML node for them if they exist
            if (package.Packages != null)
            {
                foreach (EA.Package childPackage in package.Packages)
                {
                    UnpackagePackage(childPackage, xmlDocument, packageGuid, packageType, false);
                }
            }
            if (package.Elements != null)
            {
                foreach (EA.Element element in package.Elements)
                {
                    UnpackageElement(element, xmlDocument, packageGuid, packageType);
                }
            }
            if (package.Diagrams != null)
            {
                foreach (EA.Diagram diagram in package.Diagrams)
                {
                    UnpackageDiagram(diagram, xmlDocument, packageGuid, packageType);
                }
            }

        }
        private void UnpackageElement(EA.Element element, XmlDocument xmlDocument, string parentGuid, string parentType)
        {
            string profile = "";
            XmlElement dataElement = xmlDocument.CreateElement(HUDSConstants.DATA);
            XmlElement attributesElement = xmlDocument.CreateElement(HUDSConstants.ATTRIBUTES);
            attributesElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);
            XmlElement typeElement = xmlDocument.CreateElement(HUDSConstants.TYPE);
            typeElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
            XmlElement relationshipsElement = xmlDocument.CreateElement(HUDSConstants.RELATIONSHIPS);
            relationshipsElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);


            string elementType = GetSysMLType(element.Type, element.Stereotype, element.Subtype, element.MetaType);
            if (elementType == SysmlConstants.SYSMLOBJECT && element.ClassfierID != 0) elementType = SysmlConstants.SYSMLINSTANCESPECIFICATION;
            // Check if Initial Node is from Activity or State Machine
            if (elementType == SysmlConstants.SYSMLINITIALPSEUDOSTATE && parentType == SysmlConstants.SYSMLACTIVITY) elementType = SysmlConstants.SYSMLINITIALNODE;


            // Create id element to be added to data element
            string elementGuid = element.ElementGUID.Substring(1, element.ElementGUID.Length - 2);
            XmlElement idElement = MTIPCommon.CreateIdElement(xmlDocument, elementGuid);

            // Get element profiles and attributes
            if (element.PropertyType != 0)
            {
                XmlElement typedByRelationship = xmlDocument.CreateElement(HUDSConstants.TYPEDBY);
                typedByRelationship.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);

                EA.Element typedByElement = repository.GetElementByID(element.PropertyType);
                string typedByType = GetSysMLType(typedByElement.Type, typedByElement.Stereotype, typedByElement.Subtype, typedByElement.MetaType);
                XmlElement typedbByTypeElement = xmlDocument.CreateElement(HUDSConstants.TYPE);
                typedbByTypeElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                typedbByTypeElement.InnerText = typedByType;
                typedByRelationship.AppendChild(typedbByTypeElement);

                XmlElement typedByIdElement = xmlDocument.CreateElement(HUDSConstants.ID);
                typedByIdElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                typedByIdElement.InnerText = typedByElement.ElementGUID.Substring(1, element.ElementGUID.Length - 2);
                typedByRelationship.AppendChild(typedByIdElement);

                relationshipsElement.AppendChild(typedByRelationship);

            }

            if (elementType == SysmlConstants.SYSMLBOUNDREFERENCE || elementType == SysmlConstants.SYSMLPARTICIPANTPROPERTY ||
                elementType == SysmlConstants.SYSMLCLASSIFIERBEHAVIORPROPERTY)
            {
                profile = "SysML";
            }
            else if (elementType == SysmlConstants.SYSMLFLOWPORT || elementType == SysmlConstants.SYSMLFULLPORT || elementType == SysmlConstants.SYSMLPROXYPORT ||
                elementType == SysmlConstants.SYSMLPORT || elementType == SysmlConstants.SYSMLCONSTRAINTBLOCK || elementType == SysmlConstants.CONSTRAINTPROPERTY || elementType == SysmlConstants.SYSMLPARTPROPERTY)
            {
                if (elementType != SysmlConstants.SYSMLPARTPROPERTY) CreateHUDSAttribute(xmlDocument, AttributeConstants.ISCOMPOSITE, HUDSConstants.STR, element.IsComposite.ToString(), attributesElement);
                if (elementType != SysmlConstants.SYSMLPORT && elementType != SysmlConstants.SYSMLPARTPROPERTY) { profile = "SysML"; }
            }
            else if (elementType == SysmlConstants.SYSMLSTATE && element.ClassfierID != 0)
            {
                EA.Element classifierElement = repository.GetElementByID(element.ClassifierID);
                CreateHUDSAttribute(xmlDocument, AttributeConstants.SUBMACHINE, HUDSConstants.STR, classifierElement.ElementGUID.Substring(1, element.ElementGUID.Length - 2), attributesElement);

            }
            else if (elementType == SysmlConstants.SYSMLTEXT && element.Name.Contains("://{"))
            {
                elementType = SysmlConstants.SYSMLHYPERLINK;
                string[] hyperlinkType = element.Name.Split('{');
                string[] hyperlinkId = hyperlinkType[1].Split('}');
                string[] hyperlinkPointType = element.Name.Remove(0, 1).Split(':');

                XmlElement hyperlinkRelationship = xmlDocument.CreateElement(RelationshipConstants.HYPERLINK);
                hyperlinkRelationship.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);

                XmlElement hyperlinkTypeElement = xmlDocument.CreateElement(RelationshipConstants.TYPE);
                hyperlinkTypeElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                hyperlinkTypeElement.InnerText = hyperlinkPointType[0];
                hyperlinkRelationship.AppendChild(hyperlinkTypeElement);

                XmlElement hyperlinkIdIdElement = xmlDocument.CreateElement(HUDSConstants.ID);
                hyperlinkIdIdElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                hyperlinkIdIdElement.InnerText = hyperlinkId[0];
                hyperlinkRelationship.AppendChild(hyperlinkIdIdElement);

                relationshipsElement.AppendChild(hyperlinkRelationship);
            }
            if (elementType == "")
            {
                exportLog.Add("Element not supported: Name=" + element.Name + " - GUID=" + element.ElementGUID + " - Type=" + element.Type + " - Stereotype=" + element.Stereotype);
                exportLog.Add("     Elements, diagrams, and connectors belonging to this element have not been exported");
            }

            // Create attributes element to add to data element
            if (element.ClassifierID != 0)
            {
                try
                {
                    EA.Element classifierElement = repository.GetElementByID(element.ClassifierID);
                    string classifierType = GetSysMLType(classifierElement.Type, classifierElement.Stereotype, classifierElement.Subtype, classifierElement.MetaType);
                    XmlElement classifiedByRelationship;
                    if (elementType == SysmlConstants.SYSMLACCEPTEVENTACTION || classifierType == SysmlConstants.SYSMLSENDSIGNALACTION) classifiedByRelationship = xmlDocument.CreateElement(RelationshipConstants.TRIGGER);
                    else classifiedByRelationship = xmlDocument.CreateElement(RelationshipConstants.CLASSIFIEDBY);
                    classifiedByRelationship.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);

                    XmlElement hyperlinkTypeElement = xmlDocument.CreateElement(HUDSConstants.TYPE);
                    hyperlinkTypeElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                    hyperlinkTypeElement.InnerText = classifierType;
                    classifiedByRelationship.AppendChild(hyperlinkTypeElement);

                    XmlElement hyperlinkIdIdElement = xmlDocument.CreateElement(HUDSConstants.ID);
                    hyperlinkIdIdElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                    hyperlinkIdIdElement.InnerText = classifierElement.ElementGUID.Substring(1, element.ElementGUID.Length - 2);
                    classifiedByRelationship.AppendChild(hyperlinkIdIdElement);

                    relationshipsElement.AppendChild(classifiedByRelationship);
                }
                catch
                {
                    exportLog.Add("Unable to add classifier to xml: Name=" + element.Name + " - GUID=" + element.ElementGUID + " - Type=" + element.Type + " - Stereotype=" + element.Stereotype);
                }
            }
            if (element.Name != "") CreateHUDSAttribute(xmlDocument, AttributeConstants.NAME, HUDSConstants.STR, element.Name, attributesElement);
            if (element.Notes != "")
            {
                string note = Regex.Replace(element.Notes, "<[^>]+>", string.Empty);
                note = Regex.Replace(note, "&gt;", "greater than");
                note = Regex.Replace(note, "&lt;", "less than");
                CreateHUDSAttribute(xmlDocument, AttributeConstants.DOCUMENTATION, HUDSConstants.STR, note, attributesElement);
            }
            if (element.Alias != "") CreateHUDSAttribute(xmlDocument, AttributeConstants.ALIAS, HUDSConstants.STR, element.Alias, attributesElement);
            if (element.Multiplicity != "") CreateHUDSAttribute(xmlDocument, AttributeConstants.MULTIPLICITY, HUDSConstants.STR, element.Multiplicity, attributesElement);
            if (element.IsComposite && element.CompositeDiagram != null)
            {
                try
                {
                    EA.Diagram diagramElement = repository.GetDiagramByGuid(element.CompositeDiagram.DiagramGUID);
                    string diagramType = GetDiagramSysMLType(diagramElement);
                    XmlElement compositeDiagramRelationship = xmlDocument.CreateElement(RelationshipConstants.COMPOSITEDIAGRAM);
                    compositeDiagramRelationship.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);

                    XmlElement diagramTypeElement = xmlDocument.CreateElement(HUDSConstants.TYPE);
                    diagramTypeElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                    diagramTypeElement.InnerText = diagramType;
                    compositeDiagramRelationship.AppendChild(diagramTypeElement);

                    XmlElement diagramIdElement = xmlDocument.CreateElement(HUDSConstants.ID);
                    diagramIdElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                    diagramIdElement.InnerText = element.CompositeDiagram.DiagramGUID.Substring(1, element.CompositeDiagram.DiagramGUID.Length - 2);
                    compositeDiagramRelationship.AppendChild(diagramIdElement);

                    relationshipsElement.AppendChild(compositeDiagramRelationship);
                }
                catch
                {
                    exportLog.Add("Unable to add composite diagram type tp to xml: Name=" + element.Name + " - GUID=" + element.ElementGUID + " - Type=" + element.Type + " - Stereotype=" + element.Stereotype);
                }

            }
            // Get stereotype if no element has SysML profile type
            if (element.Stereotype != "")
            {
                XmlElement stereotypeAttribute = xmlDocument.CreateElement(AttributeConstants.ATTRIBUTE);
                stereotypeAttribute.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);
                stereotypeAttribute.SetAttribute(HUDSConstants.KEY, AttributeConstants.STEREOTYPE);

                XmlElement stereotypeNameAttribute = xmlDocument.CreateElement(AttributeConstants.ATTRIBUTE);
                stereotypeNameAttribute.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                stereotypeNameAttribute.SetAttribute(HUDSConstants.KEY, AttributeConstants.STEREOTYPENAME);
                stereotypeNameAttribute.InnerText = element.StereotypeEx;
                stereotypeAttribute.AppendChild(stereotypeNameAttribute);

                if (profile != "")
                {
                    XmlElement stereotypeProfileAttribute = xmlDocument.CreateElement(AttributeConstants.ATTRIBUTE);
                    stereotypeProfileAttribute.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                    stereotypeProfileAttribute.SetAttribute(HUDSConstants.KEY, AttributeConstants.PROFILENAME);
                    stereotypeProfileAttribute.InnerText = profile;
                    stereotypeAttribute.AppendChild(stereotypeProfileAttribute);
                }

                attributesElement.AppendChild(stereotypeAttribute);
            }
            if (element.Methods.Count > 0)
            {
                try
                {
                    int key = 0;
                    XmlElement attributesbehaviorElement = xmlDocument.CreateElement(AttributeConstants.ATTRIBUTE);
                    attributesbehaviorElement.SetAttribute(HUDSConstants.KEY, AttributeConstants.BEHAVIOR);
                    attributesbehaviorElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.LIST);
                    foreach (EA.Method method in element.Methods)
                    {
                        bool hasBehavior = false;
                        if (method.Behavior != "")
                        {
                            XmlElement attribute = xmlDocument.CreateElement(AttributeConstants.ATTRIBUTE);
                            attribute.SetAttribute(HUDSConstants.KEY, key.ToString());
                            attribute.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);

                            if (method.Name != "")
                            {
                                XmlElement attributeName = xmlDocument.CreateElement(AttributeConstants.ATTRIBUTE);
                                attributeName.SetAttribute(HUDSConstants.KEY, AttributeConstants.NAME);
                                attributeName.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                                attributeName.InnerText = method.Name;
                                attribute.AppendChild(attributeName);
                                hasBehavior = true;
                            }
                            if (method.Behavior != "")
                            {
                                XmlElement attributeBehavior = xmlDocument.CreateElement(AttributeConstants.ATTRIBUTE);
                                attributeBehavior.SetAttribute(HUDSConstants.KEY, AttributeConstants.VALUE);
                                attributeBehavior.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                                attributeBehavior.InnerText = method.Behavior;
                                attribute.AppendChild(attributeBehavior);
                                hasBehavior = true;
                            }
                            if (method.ReturnType != "")
                            {
                                XmlElement attributeReturnType = xmlDocument.CreateElement(AttributeConstants.ATTRIBUTE);
                                attributeReturnType.SetAttribute(HUDSConstants.KEY, AttributeConstants.TYPE);
                                attributeReturnType.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                                attributeReturnType.InnerText = method.ReturnType;
                                attribute.AppendChild(attributeReturnType);
                                hasBehavior = true;
                            }
                            if (hasBehavior == true)
                            {
                                attributesbehaviorElement.AppendChild(attribute);
                                key += 1;
                            }
                        }
                    }
                    if (key > 0) attributesElement.AppendChild(attributesbehaviorElement);
                }
                catch
                {
                    exportLog.Add("Unable to add behavior to xml: Name=" + element.Name + " - GUID=" + element.ElementGUID + " - Type=" + element.Type + " - Stereotype=" + element.Stereotype);

                }

            }

            // Create XML node for child element
            foreach (EA.Element childElement in element.Elements)
            {
                UnpackageElement(childElement, xmlDocument, elementGuid, elementType);
            }
            // Get element attributes
            if (element.Attributes.Count > 0)
            {
                int key = 0;
                XmlElement attributesAttributeElement = xmlDocument.CreateElement(AttributeConstants.ATTRIBUTE);
                attributesAttributeElement.SetAttribute(HUDSConstants.KEY, AttributeConstants.ATTRIBUTE);
                attributesAttributeElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.LIST);
                foreach (EA.Attribute attribute in element.Attributes)
                {
                    XmlElement attElement = xmlDocument.CreateElement(AttributeConstants.ATTRIBUTE);
                    attElement.SetAttribute(HUDSConstants.KEY, key.ToString());

                    CreateHUDSAttribute(xmlDocument, AttributeConstants.TYPE, HUDSConstants.STR, attribute.Type, attElement);
                    CreateHUDSAttribute(xmlDocument, AttributeConstants.NAME, HUDSConstants.STR, attribute.Name, attElement);
                    if (attribute.Default != "") CreateHUDSAttribute(xmlDocument, AttributeConstants.INITIALVALUE, HUDSConstants.STR, attribute.Default, attElement);
                    if (attribute.Visibility != "") CreateHUDSAttribute(xmlDocument, AttributeConstants.VISIBILITY, HUDSConstants.STR, attribute.Visibility, attElement);
                    if (attribute.Stereotype != "") CreateHUDSAttribute(xmlDocument, AttributeConstants.STEREOTYPE, HUDSConstants.STR, attribute.Stereotype, attElement);
                    if (attribute.Alias != "") CreateHUDSAttribute(xmlDocument, AttributeConstants.ALIAS, HUDSConstants.STR, attribute.Alias, attElement);

                    attributesAttributeElement.AppendChild(attElement);

                    key += 1;
                }
                attributesElement.AppendChild(attributesAttributeElement);

            }
            if (element.ExtensionPoints != "") CreateHUDSAttribute(xmlDocument, AttributeConstants.EXTENSIONPOINT, HUDSConstants.STR, element.ExtensionPoints, attributesElement);

            Dictionary<string, string> taggedValues = new Dictionary<string, string>();
            // Get element tagged values
            foreach (EA.TaggedValue taggedValue in element.TaggedValues)
            {
                if (!taggedValues.ContainsKey(taggedValue.Name) && taggedValue.Value != "")
                {
                    string tagValue = Regex.Replace(taggedValue.Value, "<[^>]+>", string.Empty);
                    tagValue = Regex.Replace(tagValue, "&gt;", "greater than");
                    tagValue = Regex.Replace(tagValue, "&lt;", "less than");
                    taggedValues.Add(taggedValue.Name, tagValue);
                }
                else if (taggedValue.Value != "")
                {
                    string tagValue = Regex.Replace(taggedValue.Value, "<[^>]+>", string.Empty);
                    tagValue = Regex.Replace(tagValue, "&gt;", "greater than");
                    tagValue = Regex.Replace(tagValue, "&lt;", "less than");
                    taggedValues[taggedValue.Name] += ";" + tagValue;
                }
            }

            if (taggedValues.Count > 0)
            {
                XmlElement attributesTagsElement = xmlDocument.CreateElement(AttributeConstants.ATTRIBUTE);
                attributesTagsElement.SetAttribute(HUDSConstants.KEY, AttributeConstants.TAGGEDVALUE);

                attributesTagsElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);
                foreach (KeyValuePair<string, string> taggedValue in taggedValues)
                {
                    CreateHUDSAttribute(xmlDocument, taggedValue.Key, HUDSConstants.STR, taggedValue.Value, attributesTagsElement);
                }
                attributesElement.AppendChild(attributesTagsElement);
            }

            // Add relationships to the data element
            XmlElement hasParentElement = xmlDocument.CreateElement(RelationshipConstants.HASPARENT);
            hasParentElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);

            XmlElement hasParentTypeElement = xmlDocument.CreateElement(HUDSConstants.TYPE);
            hasParentTypeElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
            hasParentTypeElement.InnerText = parentType;

            XmlElement hasParentIdElement = xmlDocument.CreateElement(HUDSConstants.ID);
            hasParentIdElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
            hasParentIdElement.InnerText = parentGuid;

            hasParentElement.AppendChild(hasParentTypeElement);
            hasParentElement.AppendChild(hasParentIdElement);
            relationshipsElement.AppendChild(hasParentElement);

            int numSuppliers = 0;
            if (elementType != "")
            {
                xmlDocument.DocumentElement.AppendChild(dataElement);
                if (element.Diagrams != null)
                {
                    foreach (EA.Diagram diagram in element.Diagrams)
                    {
                        UnpackageDiagram(diagram, xmlDocument, elementGuid, elementType);
                    }
                }
                foreach (EA.Connector connector in element.Connectors)
                {
                    try
                    {
                        if (element.Type == SysmlConstants.SYNCHRONIZATION)
                        {
                            string supplierId = repository.GetElementByID(connector.ClientID).ElementGUID;
                            if (element.ElementGUID == supplierId)
                            {
                                numSuppliers += 1;
                            }
                        }
                        UnpackageConnector(connector, xmlDocument, elementGuid, element.Name, parentGuid, parentType);
                    }
                    catch
                    {
                        exportLog.Add("Unable to add connector to xml: Name=" + connector.Name + " - GUID=" + connector.ConnectorGUID + " - Type=" + connector.Type + " - Stereotype=" + connector.Stereotype);

                    }

                }
                if (element.Type == SysmlConstants.SYNCHRONIZATION && numSuppliers == 1)
                {
                    elementType = SysmlConstants.SYSMLJOINNODE;
                }
                else if (element.Type == SysmlConstants.SYNCHRONIZATION && numSuppliers >= 1)
                {
                    elementType = SysmlConstants.SYSMLFORKNODE;
                }
                typeElement.InnerText = elementType;
                // Get element constraints
                foreach (EA.Constraint constraint in element.Constraints)
                {
                    string constraintGuid = Guid.NewGuid().ToString();
                    string opaqueExpressionGuid = Guid.NewGuid().ToString();

                    CreateConstraintNode(xmlDocument, constraintGuid, opaqueExpressionGuid, elementGuid, elementType);
                    CreateOpaqueNode(xmlDocument, opaqueExpressionGuid, constraint.Name, constraintGuid);
                }

                // Check element SysML compliance
                CheckElementSysMLCompliance(element, elementType, parentType);

                // Add type, id, attributes, and relationships node to data node
            
                dataElement.AppendChild(typeElement);
                dataElement.AppendChild(idElement);
                dataElement.AppendChild(attributesElement);
                dataElement.AppendChild(relationshipsElement);
            }
            else
            {
                exportLog.Add("Unable to add element to XML: Name=" + element.Name + " - GUID=" + element.ElementGUID + " - Type=" + element.Type + " - Stereotype=" + element.Stereotype);
            }



        }
        public void UnpackageDiagram(EA.Diagram diagram, XmlDocument xmlDocument, string parentGuid, string parentType)
        {
            XmlElement dataElement = xmlDocument.CreateElement(HUDSConstants.DATA);
            XmlElement attributesElement = xmlDocument.CreateElement(AttributeConstants.ATTRIBUTES);
            attributesElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);
            XmlElement typeElement = xmlDocument.CreateElement(HUDSConstants.TYPE);
            typeElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
            XmlElement relationshipsElement = xmlDocument.CreateElement(HUDSConstants.RELATIONSHIPS);
            relationshipsElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);
            string diagramGuid = diagram.DiagramGUID.Substring(1, diagram.DiagramGUID.Length - 2);
            string diagramType = GetDiagramSysMLType(diagram);

            if (diagramType == SysmlConstants.SYSMLPACKAGE) CreateHUDSAttribute(xmlDocument, AttributeConstants.DISPLAYAS, HUDSConstants.STR, "Diagram", attributesElement);

            typeElement.InnerText = diagramType;

            // Check SysML compliance of the diagram and log if it isnt compliant
            CheckDiagramSysMLCompliance(diagram, diagramType, parentType);

            // Create id element to be added to data element
      
            XmlElement idElement = MTIPCommon.CreateIdElement(xmlDocument, diagramGuid);
            CreateHUDSAttribute(xmlDocument, AttributeConstants.NAME, HUDSConstants.STR, diagram.Name, attributesElement);

            if (diagramType == SysmlConstants.SYSMLCLASS) CreateHUDSAttribute(xmlDocument, AttributeConstants.DISPLAYAS, HUDSConstants.STR, "Diagram", attributesElement);

            if (diagram.DiagramObjects.Count > 0)
            {
                int key = 0;
                XmlElement elementRelElement = xmlDocument.CreateElement(HUDSConstants.ELEMENT);
                elementRelElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.LIST);

                foreach (EA.DiagramObject diagramObject in diagram.DiagramObjects)
                {
                    try
                    {
                        EA.Element element = repository.GetElementByID(diagramObject.ElementID);
                        string elementType = GetSysMLType(element.Type, element.Stereotype, element.Subtype, element.MetaType);
                        if (elementType != "")
                        {
                            string elementGuid = element.ElementGUID.Substring(1, element.ElementGUID.Length - 2);
                            // Add relationships to the data element
                            XmlElement diagramObjectElement = xmlDocument.CreateElement(HUDSConstants.ELEMENT);
                            diagramObjectElement.SetAttribute(HUDSConstants.KEY, key.ToString());
                            diagramObjectElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);


                            XmlElement objectTypeElement = xmlDocument.CreateElement(RelationshipConstants.TYPE);
                            objectTypeElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                            objectTypeElement.InnerText = elementType;
                            diagramObjectElement.AppendChild(objectTypeElement);

                            XmlElement objectIdElement = xmlDocument.CreateElement(HUDSConstants.ID);
                            objectIdElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                            objectIdElement.InnerText = elementGuid;
                            diagramObjectElement.AppendChild(objectIdElement);

                            XmlElement relMetadataElement = xmlDocument.CreateElement(HUDSConstants.RELATIONSHIPMETADATA);
                            relMetadataElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);

                            XmlElement objectTopElement = xmlDocument.CreateElement(RelationshipConstants.RELMETADATATOP);
                            objectTopElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.INTTYPE);
                            objectTopElement.InnerText = diagramObject.top.ToString();
                            relMetadataElement.AppendChild(objectTopElement);

                            XmlElement objectLeftElement = xmlDocument.CreateElement(RelationshipConstants.RELMETADATALEFT);
                            objectLeftElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.INTTYPE);
                            objectLeftElement.InnerText = diagramObject.left.ToString();
                            relMetadataElement.AppendChild(objectLeftElement);

                            XmlElement objectBottomElement = xmlDocument.CreateElement(RelationshipConstants.RELMETADATABOTTOM);
                            objectBottomElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.INTTYPE);
                            objectBottomElement.InnerText = diagramObject.bottom.ToString();
                            relMetadataElement.AppendChild(objectBottomElement);

                            XmlElement objectRightElement = xmlDocument.CreateElement(RelationshipConstants.RELMETADATARIGHT);
                            objectRightElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.INTTYPE);
                            objectRightElement.InnerText = diagramObject.right.ToString();
                            relMetadataElement.AppendChild(objectRightElement);

                            XmlElement objectSeqElement = xmlDocument.CreateElement(RelationshipConstants.RELMETADATASEQ);
                            objectSeqElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.INTTYPE);
                            objectSeqElement.InnerText = diagramObject.Sequence.ToString();
                            relMetadataElement.AppendChild(objectSeqElement);

                            diagramObjectElement.AppendChild(relMetadataElement);

                            elementRelElement.AppendChild(diagramObjectElement);
                            key += 1;
                        }

                    }
                    catch
                    {
                        exportLog.Add("Unable to export diagram object to XML: Diagram=" + diagram.Name + " - Diagram GUID=" + diagram.DiagramGUID + " - Type=");
                    }
                }
                relationshipsElement.AppendChild(elementRelElement);
            }

            if (diagram.DiagramLinks.Count > 0)
            {
                int key = 0;
                XmlElement diagramLinkRelElement = xmlDocument.CreateElement(RelationshipConstants.DIAGRAMCONNECTOR);
                diagramLinkRelElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.LIST);

                try
                {
                    foreach (EA.DiagramLink diagramLink in diagram.DiagramLinks)
                    {
                        string diagramLinkGuid = repository.GetConnectorByID(diagramLink.ConnectorID).ConnectorGUID;
                        string diagramLinkGuid2 = diagramLinkGuid.Substring(1, diagramLinkGuid.Length - 2);
                        EA.Connector connectorElement = repository.GetConnectorByGuid(diagramLinkGuid);

                        string diagramLinkType = GetConnectorSysMLType(connectorElement);

                        XmlElement diagramLinkElement = xmlDocument.CreateElement(RelationshipConstants.DIAGRAMCONNECTOR);
                        diagramLinkElement.SetAttribute(HUDSConstants.KEY, key.ToString());
                        diagramLinkElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.LIST);

                        // Create diagram connector relationship

                        XmlElement diagramLinkTypeElement = xmlDocument.CreateElement(HUDSConstants.TYPE);
                        diagramLinkTypeElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                        diagramLinkTypeElement.InnerText = diagramLinkType;

                        XmlElement diagramLinkIdElement = xmlDocument.CreateElement(HUDSConstants.ID);
                        diagramLinkIdElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                        diagramLinkIdElement.InnerText = diagramLinkGuid2;

                        if (diagramLinkType == SysmlConstants.SYSMLMESSAGE)
                        {
                            XmlElement relMetadataElement = xmlDocument.CreateElement(RelationshipConstants.RELMETADATA);
                            relMetadataElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);
                            XmlElement sequenceNumElement = xmlDocument.CreateElement(RelationshipConstants.MESSAGENUMBER);
                            sequenceNumElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                            sequenceNumElement.InnerText = connectorElement.SequenceNo.ToString();
                            relMetadataElement.AppendChild(sequenceNumElement);
                            diagramLinkElement.AppendChild(relMetadataElement);
                        }



                        diagramLinkElement.AppendChild(diagramLinkTypeElement);
                        diagramLinkElement.AppendChild(diagramLinkIdElement);
                        diagramLinkRelElement.AppendChild(diagramLinkElement);

                        key += 1;
                    }
                }
                catch
                {
                    exportLog.Add("Unable to diagram connector to XML: Diagram=" + diagram.Name + " - Diagram GUID=" + diagram.DiagramGUID + " - Type=");
                }

                relationshipsElement.AppendChild(diagramLinkRelElement);

            }

            // Create has parent relationship
            XmlElement hasParentElement = xmlDocument.CreateElement(RelationshipConstants.HASPARENT);
            hasParentElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);

            XmlElement hasParentTypeElement = xmlDocument.CreateElement(RelationshipConstants.TYPE);
            hasParentTypeElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
            hasParentTypeElement.InnerText = parentType;

            XmlElement hasParentIdElement = xmlDocument.CreateElement(RelationshipConstants.ID);
            hasParentIdElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
            hasParentIdElement.InnerText = parentGuid;

            hasParentElement.AppendChild(hasParentTypeElement);
            hasParentElement.AppendChild(hasParentIdElement);
            relationshipsElement.AppendChild(hasParentElement);

            dataElement.AppendChild(typeElement);
            dataElement.AppendChild(idElement);
            dataElement.AppendChild(attributesElement);
            dataElement.AppendChild(relationshipsElement);
            if (diagramType != "")
            {
                xmlDocument.DocumentElement.AppendChild(dataElement);
            }

            else
            {
                exportLog.Add("Diagram type is not supported: Diagram=" + diagram.Name + " - Diagram GUID=" + diagram.DiagramGUID + " - Type=");
                //Tools.Log(diagram.Name + " - " + diagram.Type + " - " + diagram.MetaType);
            }


        }
        public void UnpackageConnector(Connector connector, XmlDocument xmlDocument,
                                       string elementGuid, string elementName, string parentGuid, string parentType)
        {
            try
            {
                XmlElement dataElement = xmlDocument.CreateElement(HUDSConstants.DATA);
                XmlElement attributesElement = xmlDocument.CreateElement(HUDSConstants.ATTRIBUTES);
                attributesElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);
                XmlElement typeElement = xmlDocument.CreateElement(HUDSConstants.TYPE);
                typeElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                XmlElement relationshipsElement = xmlDocument.CreateElement(HUDSConstants.RELATIONSHIPS);
                relationshipsElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);

                string connectorGuid = connector.ConnectorGUID.Substring(1, connector.ConnectorGUID.Length - 2);
                string supplierGuid = repository.GetElementByID(connector.SupplierID).ElementGUID;
                supplierGuid = supplierGuid.Substring(1, supplierGuid.Length - 2);
                EA.Element supplierElement = repository.GetElementByID(connector.SupplierID);
                string clientGuid = repository.GetElementByID(connector.ClientID).ElementGUID;
                clientGuid = clientGuid.Substring(1, clientGuid.Length - 2);
                EA.Element clientElement = repository.GetElementByID(connector.ClientID);
                string clientSysmlType = GetSysMLType(clientElement.Type, clientElement.Stereotype, clientElement.Subtype, clientElement.MetaType);
                string supplierSysmlType = GetSysMLType(supplierElement.Type, supplierElement.Stereotype, supplierElement.Subtype, supplierElement.MetaType);
                if (clientSysmlType != "" && supplierSysmlType != "")
                {
                    string connectorType = "";
                    if (elementGuid == supplierGuid)
                    {
                        connectorType = GetConnectorSysMLType(connector);
                        typeElement.InnerText = connectorType;

                        // Create id element to be added to data element
                        XmlElement idElement = MTIPCommon.CreateIdElement(xmlDocument, connectorGuid);
                        if (connector.Name != "") CreateHUDSAttribute(xmlDocument, AttributeConstants.NAME, HUDSConstants.STR, connector.Name, attributesElement);
                        if (connector.Stereotype != "")
                        {

                            XmlElement stereotypeAttribute = xmlDocument.CreateElement(AttributeConstants.ATTRIBUTE);
                            stereotypeAttribute.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);
                            stereotypeAttribute.SetAttribute(HUDSConstants.KEY, AttributeConstants.STEREOTYPE);

                            XmlElement stereotypeNameAttribute = xmlDocument.CreateElement(AttributeConstants.ATTRIBUTE);
                            stereotypeNameAttribute.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                            stereotypeNameAttribute.SetAttribute(HUDSConstants.KEY, AttributeConstants.STEREOTYPENAME);
                            stereotypeNameAttribute.InnerText = connector.Stereotype;

                            stereotypeAttribute.AppendChild(stereotypeNameAttribute);
                            attributesElement.AppendChild(stereotypeAttribute);
                        }
                        if (connector.TransitionAction != "" && (connector.TransitionAction != BlockConstants.SIGNAL || connector.TransitionAction != "Call")) CreateHUDSAttribute(xmlDocument, RelationshipConstants.EFFECT, HUDSConstants.STR, connector.TransitionAction, attributesElement);
                        if (connector.TransitionGuard != "") CreateHUDSAttribute(xmlDocument, RelationshipConstants.GUARD, HUDSConstants.STR, connector.TransitionGuard, attributesElement);
                        if (connector.TransitionAction == BlockConstants.SIGNAL && connector.TransitionEvent == "Asynchronous")
                        {
                            CreateHUDSAttribute(xmlDocument, AttributeConstants.MESSAGESORT, HUDSConstants.STR, RelationshipConstants.ASYNCHSIGNAL, attributesElement);
                            foreach (EA.ConnectorTag tag in connector.TaggedValues)
                            {
                                if (tag.Name == RelationshipConstants.SIGNALGUID && tag.Value != "")
                                {
                                    XmlElement signalRelElement = xmlDocument.CreateElement(RelationshipConstants.SIGNATURE);
                                    signalRelElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);

                                    XmlElement signalTypeElement = xmlDocument.CreateElement(RelationshipConstants.TYPE);
                                    signalTypeElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                                    signalTypeElement.InnerText = SysmlConstants.SYSMLSIGNAL;

                                    XmlElement signalIdElement = xmlDocument.CreateElement(RelationshipConstants.ID);
                                    signalIdElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                                    signalIdElement.InnerText = tag.Value.Substring(1, tag.Value.Length - 2);

                                    signalRelElement.AppendChild(signalTypeElement);
                                    signalRelElement.AppendChild(signalIdElement);
                                    relationshipsElement.AppendChild(signalRelElement);
                                }
                            }
                        }
                        if (connector.TransitionAction == "Call" && connector.TransitionEvent == "Asynchronous") CreateHUDSAttribute(xmlDocument, AttributeConstants.MESSAGESORT, HUDSConstants.STR, RelationshipConstants.ASYNCHCALL, attributesElement);
                        if (connector.TransitionAction == "Call" && connector.TransitionEvent == "Synchronous") CreateHUDSAttribute(xmlDocument, AttributeConstants.MESSAGESORT, HUDSConstants.STR, RelationshipConstants.SYNCHCALL, attributesElement);

                        CreateHUDSAttribute(xmlDocument, "isInclusiveOfBaseClass", HUDSConstants.STR, "false", attributesElement);

                        // Create client relationship
                        string clientType = GetSysMLType(clientElement.Type, clientElement.Stereotype, clientElement.Subtype, clientElement.MetaType);
                        string supplierType = GetSysMLType(supplierElement.Type, supplierElement.Stereotype, clientElement.Subtype, supplierElement.MetaType);
                        XmlElement clientRelElement = xmlDocument.CreateElement(RelationshipConstants.CLIENT);
                        clientRelElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);

                        XmlElement clientTypeElement = xmlDocument.CreateElement(RelationshipConstants.TYPE);
                        clientTypeElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                        clientTypeElement.InnerText = supplierType;

                        XmlElement clientIdElement = xmlDocument.CreateElement(RelationshipConstants.ID);
                        clientIdElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                        clientIdElement.InnerText = supplierGuid;

                        clientRelElement.AppendChild(clientTypeElement);
                        clientRelElement.AppendChild(clientIdElement);
                        relationshipsElement.AppendChild(clientRelElement);

                        // Create supplier relationship
                        XmlElement supplierRelElement = xmlDocument.CreateElement(RelationshipConstants.SUPPLIER);
                        supplierRelElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);

                        XmlElement supplierTypeElement = xmlDocument.CreateElement(RelationshipConstants.TYPE);
                        supplierTypeElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                        supplierTypeElement.InnerText = clientType;

                        XmlElement supplierIdElement = xmlDocument.CreateElement(RelationshipConstants.ID);
                        supplierIdElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                        supplierIdElement.InnerText = clientGuid;

                        supplierRelElement.AppendChild(supplierTypeElement);
                        supplierRelElement.AppendChild(supplierIdElement);
                        relationshipsElement.AppendChild(supplierRelElement);

                        // Create has parent relationship
                        XmlElement hasParentElement = xmlDocument.CreateElement(RelationshipConstants.HASPARENT);
                        hasParentElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);

                        XmlElement hasParentTypeElement = xmlDocument.CreateElement(RelationshipConstants.TYPE);
                        hasParentTypeElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                        hasParentTypeElement.InnerText = parentType;

                        XmlElement hasParentIdElement = xmlDocument.CreateElement(RelationshipConstants.ID);
                        hasParentIdElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
                        hasParentIdElement.InnerText = parentGuid;

                        hasParentElement.AppendChild(hasParentTypeElement);
                        hasParentElement.AppendChild(hasParentIdElement);
                        relationshipsElement.AppendChild(hasParentElement);

                        
                        dataElement.AppendChild(typeElement);
                        dataElement.AppendChild(idElement);
                        dataElement.AppendChild(attributesElement);
                        dataElement.AppendChild(relationshipsElement);
                        if (connectorType != "")
                        {
                            xmlDocument.DocumentElement.AppendChild(dataElement);
                        }
                        else
                        {
                            //Tools.Log(connector.Name + " - " + connector.Type + " - " + " - " + connector.Stereotype + " - " + connector.MetaType + " - " + connector.ConnectorGUID);
                            exportLog.Add("Diagram location is not SysML compliant: Package diagrams must be a child of a model, package, model library or block");
                            exportLog.Add("Could not add connector: " + connector.Name + " - Connector GUID: " + connector.ConnectorGUID);
                        }
                    }
                }
            }
            catch
            {
                exportLog.Add("Could not add connector to XML: " + connector.Name + " - Connector GUID: " + connector.ConnectorGUID);

            }

            
        }

        // Retuen package SysML type
        public string GetPackageType(EA.Package package)
        {
            string packageType = "";
            if (package.IsModel == true)
            {
                packageType = SysmlConstants.SYSMLMODEL;
            }
            else if (package.Element.Stereotype == StereotypeConstants.MODEL)
            {
                packageType = SysmlConstants.SYSMLMODEL;
            }
            else if (package.Element.Stereotype == StereotypeConstants.PROFILE)
            {
                packageType = SysmlConstants.SYSMLPROFILE;
                if (!profilePackages.ContainsKey(package.Name)) profilePackages.Add(package.Name, package);
            }
            else packageType = SysmlConstants.SYSMLPACKAGE;
            return packageType;
        }
        public void CreateHUDSAttribute(XmlDocument xmlDocument, string key, string type, string value, XmlElement parentElement)
        {
            XmlElement attribute = xmlDocument.CreateElement(AttributeConstants.ATTRIBUTE);
            attribute.SetAttribute(HUDSConstants.KEY, key);
            attribute.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);
            XmlElement attributeAttrib = xmlDocument.CreateElement(AttributeConstants.ATTRIBUTE);
            attributeAttrib.SetAttribute(HUDSConstants.KEY, HUDSConstants.VALUE);
            attributeAttrib.SetAttribute(HUDSConstants.DTYPE, type);
            attributeAttrib.InnerText = value;
            attribute.AppendChild(attributeAttrib);
            parentElement.AppendChild(attribute);
        }
        public void CreateConstraintNode(XmlDocument xmlDocument, string constraintGuid, string opaqueExpressionGuid, string parentGuid, string parentType)
        {
            // Create constraint data block
            XmlElement dataElement = xmlDocument.CreateElement(HUDSConstants.DATA);
            XmlElement typeElement = xmlDocument.CreateElement(HUDSConstants.TYPE);
            typeElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
            typeElement.InnerText = SysmlConstants.SYSMLCONSTRAINT;
            XmlElement relationshipsElement = xmlDocument.CreateElement(HUDSConstants.RELATIONSHIPS);
            relationshipsElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);

            // Create id element to be added to constraint data element
            XmlElement idElement = MTIPCommon.CreateIdElement(xmlDocument, constraintGuid);
            // Create constraint relationship

            XmlElement valueSpecificationElement = xmlDocument.CreateElement(RelationshipConstants.VALUESPECIFICATION);
            valueSpecificationElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);

            XmlElement valueSpecTypeElement = xmlDocument.CreateElement(RelationshipConstants.TYPE);
            valueSpecTypeElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
            valueSpecTypeElement.InnerText = SysmlConstants.OPAQUEEXPRESSION;

            XmlElement valueSpecIdElement = xmlDocument.CreateElement(RelationshipConstants.ID);
            valueSpecIdElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
            valueSpecIdElement.InnerText = opaqueExpressionGuid;

            valueSpecificationElement.AppendChild(valueSpecTypeElement);
            valueSpecificationElement.AppendChild(valueSpecIdElement);
            relationshipsElement.AppendChild(valueSpecificationElement);

            XmlElement hasParentElement = xmlDocument.CreateElement(RelationshipConstants.HASPARENT);
            hasParentElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);

            XmlElement hasParentTypeElement = xmlDocument.CreateElement(RelationshipConstants.TYPE);
            hasParentTypeElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
            hasParentTypeElement.InnerText = parentType;

            XmlElement hasParentIdElement = xmlDocument.CreateElement(RelationshipConstants.ID);
            hasParentIdElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
            hasParentIdElement.InnerText = parentGuid;

            hasParentElement.AppendChild(hasParentTypeElement);
            hasParentElement.AppendChild(hasParentIdElement);
            relationshipsElement.AppendChild(hasParentElement);

            // Add type, id, attributes, and relationships node to data node
            dataElement.AppendChild(typeElement);
            dataElement.AppendChild(idElement);
            dataElement.AppendChild(relationshipsElement);

            xmlDocument.DocumentElement.AppendChild(dataElement);
        }
        public void CreateOpaqueNode(XmlDocument xmlDocument, string opaqueGuid, string name, string parentGuid)
        {
            // Create constraint data block
            XmlElement dataElement = xmlDocument.CreateElement(HUDSConstants.DATA);
            XmlElement typeElement = xmlDocument.CreateElement(HUDSConstants.TYPE);
            typeElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
            typeElement.InnerText = SysmlConstants.SYSMLOPAQUEEXPRESSION;
            XmlElement attributesElement = xmlDocument.CreateElement(HUDSConstants.ATTRIBUTES);
            attributesElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);
            XmlElement relationshipsElement = xmlDocument.CreateElement(HUDSConstants.RELATIONSHIPS);
            relationshipsElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);


            // Create id element to be added to opaque expression data element
            XmlElement idElement = MTIPCommon.CreateIdElement(xmlDocument, opaqueGuid);

            // Create opaque expression relationship
            XmlElement hasParentElement = xmlDocument.CreateElement(RelationshipConstants.HASPARENT);
            hasParentElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);

            XmlElement hasParentTypeElement = xmlDocument.CreateElement(HUDSConstants.TYPE);
            hasParentTypeElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
            hasParentTypeElement.InnerText = SysmlConstants.SYSMLCONSTRAINT;

            XmlElement hasParentIdElement = xmlDocument.CreateElement(HUDSConstants.ID);
            hasParentIdElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
            hasParentIdElement.InnerText = parentGuid;

            hasParentElement.AppendChild(hasParentTypeElement);
            hasParentElement.AppendChild(hasParentIdElement);
            relationshipsElement.AppendChild(hasParentElement);

            // Create opaque expression relationship
            CreateHUDSAttribute(xmlDocument, AttributeConstants.BODY, HUDSConstants.STR, name, attributesElement);

            // Add type, id, attributes, and relationships node to data node
            dataElement.AppendChild(typeElement);
            dataElement.AppendChild(idElement);
            dataElement.AppendChild(attributesElement);
            dataElement.AppendChild(relationshipsElement);

            xmlDocument.DocumentElement.AppendChild(dataElement);
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
            else if (type == SysmlConstants.CHANGE) elementType = SysmlConstants.SYSMLCHANGE;
            else if (type == SysmlConstants.COLLABORATION) elementType = SysmlConstants.SYSMLCOLLABORATION;
            else if (type == SysmlConstants.CONSTRAINT) elementType = SysmlConstants.SYSMLCONSTRAINT;
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
            else if (type == SysmlConstants.PART)
            {
                if (stereotype == "") elementType = SysmlConstants.SYSMLPARTPROPERTY;
                else if (stereotype == StereotypeConstants.PARTPROPERTY) elementType = SysmlConstants.SYSMLPARTPROPERTY;
                else if (stereotype == StereotypeConstants.PROPERTY) elementType = SysmlConstants.SYSMLPARTPROPERTY;
                else if (stereotype == StereotypeConstants.CONSTRAINTPROPERTY) elementType = SysmlConstants.SYSMLCONSTRAINTPROPERTY;
                else if (stereotype == StereotypeConstants.CLASSIFICATION) elementType = SysmlConstants.SYSMLCLASSIFICATION;

            }
            else if (type == SysmlConstants.REQUIREDINTERFACE) elementType = SysmlConstants.SYSMLREQUIREDINTERFACE;
            else if (type == SysmlConstants.NOTE) elementType = SysmlConstants.SYSMLNOTE;
            else if (type == SysmlConstants.PACKAGE) elementType = SysmlConstants.SYSMLPACKAGE;
            else if (stereotype == StereotypeConstants.ALLOCATED || type == SysmlConstants.ACTION || type == SysmlConstants.ACTIVITYPARAMETER ||
                            type == SysmlConstants.ACTIONPIN || type == SysmlConstants.EVENT)
            {
                if (stereotype == StereotypeConstants.ALLOCATED) elementType = SysmlConstants.SYSMLALLOCATED;
                else if (type == SysmlConstants.ACTION && metatype == MetatypeConstants.acceptEventAction) elementType = SysmlConstants.SYSMLACCEPTEVENTACTION;
                else if (type == SysmlConstants.ACTION && metatype == MetatypeConstants.callBehaviorAction) elementType = SysmlConstants.SYSMLCALLBEHAVIORACTION;
                else if (type == SysmlConstants.ACTION && metatype == MetatypeConstants.createObjectAction) elementType = SysmlConstants.SYSMLCREATEOBJECTACTION;
                else if (type == SysmlConstants.ACTION && metatype == MetatypeConstants.destroyObjectAction) elementType = SysmlConstants.SYSMLDESTROYOBJECTACTION;
                else if (type == SysmlConstants.ACTION && metatype == MetatypeConstants.callOperationAction) elementType = SysmlConstants.SYSMLCALLOPERATIONACTION;
                else if (type == SysmlConstants.ACTION && metatype == MetatypeConstants.opaqueAction) elementType = SysmlConstants.SYSMLOPAQUEACTION;
                else if (type == SysmlConstants.ACTION && metatype == MetatypeConstants.sendSignalAction) elementType = SysmlConstants.SYSMLSENDSIGNALACTION;
                else if (type == SysmlConstants.ACTION) elementType = SysmlConstants.SYSMLACTION;
                else if (type == SysmlConstants.ACTIVITYPARAMETER) elementType = SysmlConstants.SYSMLACTIVITYPARAMETER;
                else if (type == SysmlConstants.ACTIONPIN && (stereotype == StereotypeConstants.OUTPUT || metatype == MetatypeConstants.outputPin)) elementType = SysmlConstants.SYSMLOUTPUTPIN;
                else if (type == SysmlConstants.ACTIONPIN && (stereotype == StereotypeConstants.INPUT || metatype == MetatypeConstants.inputPin)) elementType = SysmlConstants.SYSMLINPUTPIN;
                else if (type == SysmlConstants.ACTIONPIN) elementType = SysmlConstants.SYSMLACTIONPIN;
                else if (type == SysmlConstants.EVENT)
                {
                    if (subtype == 0) elementType = SysmlConstants.SYSMLSENDSIGNALACTION;
                    if (subtype == 1) elementType = SysmlConstants.SYSMLACCEPTEVENTACTION;
                }

            }
            else if (type == SysmlConstants.MERGENODE) elementType = SysmlConstants.SYSMLMERGENODE;
            else if (type == SysmlConstants.SYNCHRONIZATION)
            {
                if (stereotype == StereotypeConstants.JOIN) elementType = SysmlConstants.SYSMLJOIN;
                else if (stereotype == StereotypeConstants.FORK) elementType = SysmlConstants.SYSMLFORK;
                else elementType = SysmlConstants.SYSMLSYNCHRONIZATION;
            }
            else if (type == SysmlConstants.INTERRUPTIBLEACTIVITYREGION) elementType = SysmlConstants.SYSMLINTERRUPTIBLEACTIVITYREGION;
            else if (stereotype == StereotypeConstants.FLOWSPECIFICATION) elementType = SysmlConstants.SYSMLFLOWSPECIFICATION;
            else if (stereotype == StereotypeConstants.EXTERNAL || stereotype == StereotypeConstants.SUBSYSTEM || stereotype == StereotypeConstants.SYSTEM || stereotype == StereotypeConstants.SYSTEMCONTEXT) elementType = SysmlConstants.SYSMLBLOCK;
            return elementType;
        }
        public string GetDiagramSysMLType(EA.Diagram diagram)
        {
            string diagramType = "";
            if (diagram.MetaType == "SysML1.3::Activity" || diagram.MetaType == "SysML1.4::Activity" || diagram.Type == "Activity") diagramType = "sysml.ActivityDiagram";
            else if (diagram.MetaType == "SysML1.3::BlockDefinition" || diagram.MetaType == "SysML1.4::BlockDefinition") diagramType = "sysml.BlockDefinitionDiagram";
            else if (diagram.Type == "Logical" && diagram.MetaType == "") diagramType = "sysml.Class";
            else if (diagram.MetaType == "SysML1.3::InternalBlock" || diagram.MetaType == "SysML1.4::InternalBlock") diagramType = "sysml.InternalBlockDiagram";
            else if (diagram.MetaType == "SysML1.3::Package" || diagram.MetaType == "SysML1.4::Package" || diagram.Type == "Package") diagramType = "sysml.Package";
            else if (diagram.MetaType == "SysML1.3::Parametric" || diagram.MetaType == "SysML1.4::Parametric") diagramType = "sysml.ParametricDiagram";
            else if (diagram.MetaType == "SysML1.3::Requirement" || diagram.MetaType == "SysML1.4::Requirement" ||
                diagram.MetaType == "Extended::Requirements" || diagram.Type == "Requirement") diagramType = "sysml.RequirementsDiagram";
            else if (diagram.MetaType == "SysML1.3::Sequence" || diagram.MetaType == "SysML1.4::Sequence" || diagram.Type == "Sequence") diagramType = "sysml.SequenceDiagram";
            else if (diagram.MetaType == "SysML1.3::StateMachine" || diagram.MetaType == "SysML1.4::StateMachine" || diagram.Type == "Statechart") diagramType = "sysml.StateMachineDiagram";
            else if (diagram.MetaType == "SysML1.3::UseCase" || diagram.MetaType == "SysML1.4::UseCase" || diagram.Type == "Use Case") diagramType = "sysml.UseCaseDiagram";
            else
            {
                exportLog.Add("Diagram is not supported: Name=" + diagram.Name + " - GUID=" + diagram.DiagramGUID + " - Type=" + diagram.Type);
            }

            return diagramType;
        }
        public string GetConnectorSysMLType(EA.Connector connector)
        {
            string connectorType = "";
            if (connector.Type == SysmlConstants.ABSTRACTION) connectorType = SysmlConstants.SYSMLABSTRACTION;
            else if (connector.Type == SysmlConstants.AGGREGATION)
            {
                if (connector.SupplierEnd.Aggregation == 2) connectorType = SysmlConstants.SYSMLCOMPOSITION;
                else connectorType = SysmlConstants.SYSMLAGGREGATION;
            }

            else if (connector.Type == SysmlConstants.ASSOCIATION)
            {
                if (connector.Stereotype == SysmlConstants.BLOCK) connectorType = SysmlConstants.SYSMLASSOCIATIONBLOCK;
                else connectorType = SysmlConstants.SYSMLASSOCIATION;
            }
            else if (connector.Type == SysmlConstants.CONNECTOR) connectorType = SysmlConstants.SYSMLCONNECTOR;
            else if (connector.Type == SysmlConstants.CONTROLFLOW) connectorType = SysmlConstants.SYSMLCONTROLFLOW;
            else if (connector.Type == SysmlConstants.DEPENDENCY) connectorType = SysmlConstants.SYSMLDEPENDENCY;
            else if (connector.Type == SysmlConstants.EXTENSION) connectorType = SysmlConstants.SYSMLEXTENSION;
            else if (connector.Type == SysmlConstants.GENERALIZATION) connectorType = SysmlConstants.SYSMLGENERALIZATION;
            else if (connector.Type == SysmlConstants.INTERRUPTFLOW) connectorType = SysmlConstants.SYSMLINTERRUPTFLOW;
            else if (connector.Type == SysmlConstants.INFORMATIONFLOW)
            {
                if (connector.Stereotype == SysmlConstants.ITEMFLOW || connector.Stereotype == "itemFlow") connectorType = SysmlConstants.SYSMLITEMFLOW;
                else connectorType = SysmlConstants.SYSMLINFORMATIONFLOW;
            }
            else if (connector.Type == SysmlConstants.ITEMFLOW) connectorType = SysmlConstants.SYSMLINFORMATIONFLOW;
            else if (connector.Type == SysmlConstants.MESSAGE || connector.Type == SysmlConstants.SEQUENCE) connectorType = SysmlConstants.SYSMLMESSAGE;
            else if (connector.Type == SysmlConstants.NESTING) connectorType = SysmlConstants.SYSMLNESTING;
            else if (connector.Type == SysmlConstants.OBJECTFLOW) connectorType = SysmlConstants.SYSMLOBJECTFLOW;
            else if (connector.Type == SysmlConstants.REALISATION) connectorType = SysmlConstants.SYSMLREALIZATION;
            else if (connector.Type == SysmlConstants.STATEFLOW) connectorType = SysmlConstants.SYSMLTRANSITION;
            else if (connector.Type == SysmlConstants.USAGE) connectorType = SysmlConstants.SYSMLUSAGE;
            else if (connector.Type == SysmlConstants.USECASE) connectorType = SysmlConstants.SYSMLUSECASERELATIONSHIP;
            else
            {
                exportLog.Add("Connector is not supported: Type=" + connector.Type + " - GUID=" + connector.ConnectorGUID);
            }
            return connectorType;
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
        public void GetCustomProfiles()
        {
            if (repository.Models.Count > 0)
            {
                foreach (EA.Package rootModel in repository.Models) CheckForCustomProfile(rootModel);
            }
        }
        public void CheckForCustomProfile(EA.Package package)
        {
            foreach (EA.Package childPackage in package.Packages)
            {
                if (childPackage.Element.Stereotype == "profile" && !customProfiles.ContainsKey(childPackage.Name)) customProfiles.Add(childPackage.Name, childPackage.PackageGUID.Substring(1, package.PackageGUID.Length - 2));
                if (childPackage.Packages.Count > 0) CheckForCustomProfile(childPackage);

            }

        }

    }
}
