using DigitalWorldOnline.Commons.DTOs.Character;
using DigitalWorldOnline.Commons.Enums;

namespace DigitalWorldOnline.Commons.DTOs.Mechanics
{
    public class MastersMatchRankerDTO
    {
        public long Id { get; set; } // Chave primária
        public long MastersMatchId { get; set; } // Chave estrangeira para MastersMatchDTO
        public MastersMatchDTO MastersMatch { get; set; } // Propriedade de navegação

        public short Rank { get; set; } // Posição no ranking
        public string TamerName { get; set; } // Nome do domador
        public int Donations { get; set; } // Número de doações do domador
        public MastersMatchTeamEnum Team { get; set; } // Time (0 para A, 1 para B)

        /// <summary>
        /// Tamer member reference.
        /// </summary>
        public CharacterDTO Character { get; set; }

        public long CharacterId { get; set; }
    }
}