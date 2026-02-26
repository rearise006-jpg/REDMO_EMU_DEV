namespace DigitalWorldOnline.Commons.Models.Assets
{
    public sealed class MapRegionAssetModel
    {
        public int MapID { get; set; }
        public int CenterX { get; set; }
        public int CenterY { get; set; }
        public int Radius { get; set; }
        public byte FatigueType { get; set; }
        public byte FatigueDebuff { get; set; }
        public int FatigueStartTime { get; set; }
        public int FatigueAddTime { get; set; }
        public int FatigueAddPoint { get; set; }
    }
}