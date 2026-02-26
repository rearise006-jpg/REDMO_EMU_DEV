using System.Collections.Generic;
using System.Xml.Serialization;

namespace DigitalWorldOnline.Commons.XMLAssetsDTO
{
    [XmlRoot("ITEM")]
    public class ItemListXmlDto
    {
        [XmlElement("icount")]
        public int ICount { get; set; }

        [XmlElement("index")]
        public ItemListIndexXmlDto Index { get; set; }
    }

    public class ItemListIndexXmlDto
    {
        [XmlElement("sINFO")]
        public List<ItemInfoXmlDto> Items { get; set; }
    }
}