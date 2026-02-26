using DigitalWorldOnline.Commons.Enums;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands
{
    public class UpdateMastersMatchRankerCommand : IRequest
    {
        public long CharacterId { get; set; }
        public string TamerName { get; set; }
        public MastersMatchTeamEnum Team { get; set; }
        public int DonatedAmount { get; set; }

        public UpdateMastersMatchRankerCommand(long characterId, string tamerName, MastersMatchTeamEnum team, int donatedAmount)
        {
            CharacterId = characterId;
            TamerName = tamerName;
            Team = team;
            DonatedAmount = donatedAmount;
        }
    }
}