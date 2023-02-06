using MTIP.Constants;
using System.Xml;

namespace MTIP.Translations
{
    internal static class MTIPCommon
    {
        //DS: Not the way I would do this.
        
        public static RelationshipConstants relationshipConstants = new RelationshipConstants();
        public static HUDSConstants hudsConstants =new HUDSConstants();

        internal static XmlElement CreateIdElement(XmlDocument xmlDocument, string eaElementGuid)
        {

            XmlElement idElement = xmlDocument.CreateElement(AttributeConstants.ID);
            idElement.SetAttribute(hudsConstants.dtype, hudsConstants.dict);

            //unified ID shall be the new Vendor independed ID field for cross tool roundtrips.
            XmlElement unifiedIdElement = xmlDocument.CreateElement(hudsConstants.unified);
            unifiedIdElement.SetAttribute(hudsConstants.dtype, hudsConstants.str);
            unifiedIdElement.InnerText = eaElementGuid.ToString();

            idElement.AppendChild(unifiedIdElement);

            //unified ID shall be the new Vendor specific ID field for cross tool roundtrips, best case this is not required going forward
            XmlElement eaIdElement = xmlDocument.CreateElement(hudsConstants.ea);
            eaIdElement.SetAttribute(hudsConstants.dtype, hudsConstants.str);
            eaIdElement.InnerText = eaElementGuid.ToString();

            idElement.AppendChild(eaIdElement);

            return idElement;
        }
    }
}
