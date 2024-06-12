using EA;
using MTIP.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace MTIP.Translations
{
    internal static class MTIPCommon
    {
        //DS: Not the way I would do this.
        public static AttributeConstants attributeConstants = new AttributeConstants();
        public static RelationshipConstants relationshipConstants = new RelationshipConstants();
        public static HUDSConstants hudsConstants = new HUDSConstants();

        internal static Package GetOrAddPackage(Repository repository, Package package, string guid, string name)
        {
            Package localPackage = null;
            Guid localGuid;
            if (Guid.TryParse(guid, out localGuid))
            {
                localPackage = repository.GetPackageByGuid(localGuid.ToString("B"));
                if (localPackage != null)
                {
                    //package found so let's update the name
                    localPackage.Name = name;
                    localPackage.Update();
                    return localPackage;
                }
            }
            //we couldn't find a package - so let's make a new one.
            localPackage = package.Packages.AddNew(name,"");
            localPackage.Update();
            localPackage = ResetGuidPackage(repository, localPackage,localGuid);

            return localPackage;
        }

        private static Package ResetGuidPackage(Repository repository, Package newPackage, Guid localGuid)
        {
            Package localPackage = null;

            string newGuid = newPackage.PackageGUID;
            string updateGuid = localGuid.ToString("B");

            repository.SQLQuery($"update t_package set ea_guid = '{updateGuid}' where ea_guid = '{newGuid}'");
            repository.SQLQuery($"update t_object set ea_guid = '{updateGuid}' where ea_guid = '{newGuid}'");


            localPackage = repository.GetPackageByGuid(updateGuid);
            if (localPackage != null)
            {
                return localPackage;
            }

            return localPackage;
        }

        internal static XmlElement CreateIdElement(XmlDocument xmlDocument, string eaElementGuid)
        {

            XmlElement idElement = xmlDocument.CreateElement(attributeConstants.id);
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
