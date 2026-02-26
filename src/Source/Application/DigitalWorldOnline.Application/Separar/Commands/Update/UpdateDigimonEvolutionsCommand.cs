using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.DTOs.Digimon;
using MediatR;
using System.Collections.Generic;

namespace DigitalWorldOnline.Application.Separar.Commands.Update
{
    public class UpdateDigimonEvolutionsCommand : IRequest<List<DigimonEvolutionDTO>>
    {
        public long DigimonId { get; set; }
        public List<DigimonEvolutionModel> Evolutions { get; set; }

        public UpdateDigimonEvolutionsCommand(long digimonId, List<DigimonEvolutionModel> evolutions)
        {
            DigimonId = digimonId;
            Evolutions = evolutions;
        }
    }
}
