using System.Collections.Generic;
using System.Xml.Serialization;

namespace DigitalWorldOnline.Commons.XMLAssetsDTO
{
    public class ItemInfoXmlDto
    {
        [XmlElement("s_dwItemID")]
        public int ItemId { get; set; }

        [XmlElement("s_szName")]
        public string Name { get; set; }

        [XmlElement("s_nClass")]
        public int Class { get; set; }

        [XmlElement("s_nType_L")]
        public int Type { get; set; }

        [XmlElement("s_nType_S")]
        public int TypeN { get; set; }

        [XmlElement("s_btApplyRateMin")]
        public short ApplyValueMin { get; set; }

        [XmlElement("s_btApplyRateMax")]
        public short ApplyValueMax { get; set; }

        [XmlElement("s_btApplyElement")]
        public short ApplyElement { get; set; }

        [XmlElement("s_nSection")]
        public int Section { get; set; }

        [XmlElement("s_nSellType")]
        public int SellType { get; set; }

        [XmlElement("s_nBelonging")]
        public int BoundType { get; set; }

        [XmlElement("s_bUseTimeType")]
        public int UseTimeType { get; set; }

        [XmlElement("s_dwSkill")]
        public long SkillCode { get; set; }

        [XmlElement("s_nTamerReqMinLevel")]
        public byte TamerMinLevel { get; set; }

        [XmlElement("s_nTamerReqMaxLevel")]
        public byte TamerMaxLevel { get; set; }

        [XmlElement("s_nDigimonReqMinLevel")]
        public byte DigimonMinLevel { get; set; }

        [XmlElement("s_nDigimonReqMaxLevel")]
        public byte DigimonMaxLevel { get; set; }

        [XmlElement("s_dwSale")]
        public long SellPrice { get; set; }

        [XmlElement("s_dwScanPrice")]
        public int ScanPrice { get; set; }

        [XmlElement("s_dwDigiCorePrice")]
        public int DigicorePrice { get; set; }

        [XmlElement("s_dwEventItemPrice")]
        public int EventPriceAmount { get; set; }

        [XmlElement("s_nEventItemType")]
        public int EventItemType { get; set; }

        [XmlElement("s_nUseTime_Min")]
        public int UsageTimeMinutes { get; set; }

        [XmlElement("s_nOverlap")]
        public short Overlap { get; set; }

        [XmlElement("s_nUseCharacter")]
        public int Target { get; set; }
    }
}
