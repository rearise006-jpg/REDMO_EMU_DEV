namespace DigitalWorldOnline.Admin.ViewModel
{
    public class MapConfigViewModel
    {
        public long Id { get; set; }
        public int MapId { get; set; }
        public string Name { get; set; }
        public int Type { get; set; }
        public short Channels { get; set; }
        public int MapRegionindex { get; set; }

        // 🆕 YENİ ALAN
        public bool MapIsOpen { get; set; } = true;
    }
}