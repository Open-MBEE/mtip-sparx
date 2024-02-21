using MTIP.Constants;
using System.Xml;

namespace MTIP.Translations
{
    internal static class MTIPCommon
    {
        internal static XmlElement CreateIdElement(XmlDocument xmlDocument, string eaElementGuid)
        {

            XmlElement idElement = xmlDocument.CreateElement(AttributeConstants.ID);
            idElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.DICT);

            //unified ID shall be the new Vendor independed ID field for cross tool roundtrips.
            XmlElement unifiedIdElement = xmlDocument.CreateElement(HUDSConstants.UNIFIED);
            unifiedIdElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
            unifiedIdElement.InnerText = eaElementGuid.ToString();

            idElement.AppendChild(unifiedIdElement);

            //unified ID shall be the new Vendor specific ID field for cross tool roundtrips, best case this is not required going forward
            XmlElement eaIdElement = xmlDocument.CreateElement(HUDSConstants.EA);
            eaIdElement.SetAttribute(HUDSConstants.DTYPE, HUDSConstants.STR);
            eaIdElement.InnerText = eaElementGuid.ToString();

            idElement.AppendChild(eaIdElement);

            return idElement;
        }
    }
}
