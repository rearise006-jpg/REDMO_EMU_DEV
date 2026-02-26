using DigitalWorldOnline.Commons.DTOs.Mechanics;
using DigitalWorldOnline.Commons.Enums;
using MediatR;

namespace DigitalWorldOnline.Application.Separar.Commands.Create
{
    public class CreateMasterMatchPlayerCommand : IRequest<MastersMatchRankerDTO>
    {
        public long CharacterId { get; set; }
        public string TamerName { get; set; }
        public MastersMatchTeamEnum AssignedTeam { get; set; }

        /// <summary>
        /// Inicializa uma nova instância do comando CreateMasterMatchPlayerCommand.
        /// </summary>
        /// <param name="characterId">O ID do personagem do jogador.</param>
        /// <param name="tamerName">O nome do domador do jogador.</param>
        /// <param name="assignedTeam">O time atribuído ao jogador.</param>
        public CreateMasterMatchPlayerCommand(long characterId, string tamerName, MastersMatchTeamEnum assignedTeam)
        {
            CharacterId = characterId;
            TamerName = tamerName;
            AssignedTeam = assignedTeam;
        }
    }
}