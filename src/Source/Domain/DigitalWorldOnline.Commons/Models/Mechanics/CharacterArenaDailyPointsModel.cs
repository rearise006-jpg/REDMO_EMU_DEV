
namespace DigitalWorldOnline.Commons.Models.Mechanics
{
    public class CharacterArenaDailyPointsModel
    {
        public Guid Id { get; set; }
        public DateTime InsertDate { get; set; }
        public int Points { get; set; }
        public long CharacterId { get; private set; }

        public void AddPoints(int points)
        {
            Points += points;
        }
     
        public CharacterArenaDailyPointsModel(DateTime insertDate, int points, long characterId)
        {
            Id = Guid.NewGuid();
            InsertDate = insertDate;
            Points = points;
            CharacterId = characterId;
        }
    }

}