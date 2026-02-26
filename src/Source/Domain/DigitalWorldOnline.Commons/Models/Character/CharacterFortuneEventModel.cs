using DigitalWorldOnline.Commons.Models.Character;

namespace DigitalWorldOnline.Commons.DTOs.Character
{
    public class CharacterFortuneEventModel
    {
        public long Id { get; private set; }
        public int DayOfWeek { get; private set; }
        public byte Received { get; private set; } = 0;
        public DateTime LastReceived { get; private set; }
        public void SetFortuneEvent(int dayOfWeek, byte received)
        {
            DayOfWeek = dayOfWeek;
            Received = received;
            LastReceived = DateTime.Now;
        }
        public void SetFortuneReceived(byte value)
        {
            Received = value;
        }
        public CharacterFortuneEventModel()
        {

        }
        public CharacterFortuneEventModel(long id)
        {
            CharacterId = id;
        }

        //References
        public long CharacterId { get; private set; }
        public CharacterModel Character { get; private set; }
    }
}