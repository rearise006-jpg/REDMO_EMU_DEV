using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace DigitalWorldOnline.Commons.XMLAssetsDTO
{
    public static class ItemAssetLoader
    {
        public static ItemListXmlDto LoadFromXml(string xmlPath)
        {
            var serializer = new XmlSerializer(typeof(ItemListXmlDto));
            using var stream = File.OpenRead(xmlPath);
            return (ItemListXmlDto)serializer.Deserialize(stream);
        }
    }
}