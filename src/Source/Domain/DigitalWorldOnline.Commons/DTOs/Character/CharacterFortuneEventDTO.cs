namespace DigitalWorldOnline.Commons.DTOs.Character
{
    public class CharacterFortuneEventDTO
    {
        public long Id { get; set; }
        public int DayOfWeek { get; set; }
        public byte Received { get; set; }
        public DateTime LastReceived { get; set; }

        //References
        public long CharacterId { get; set; }
        public CharacterDTO Character { get; set; }
    }
}