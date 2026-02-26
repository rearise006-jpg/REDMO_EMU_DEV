using DigitalWorldOnline.Commons.DTOs.Character;
using DigitalWorldOnline.Commons.DTOs.Character;  // ✅ Buradan import et
using DigitalWorldOnline.Commons.DTOs.Digimon;

namespace DigitalWorldOnline.Commons.Interfaces
{
    public interface ICharacterQueriesRepository
    {
        Task<IList<CharacterDTO>> GetCharactersByAccountIdAsync(long accountId);
        
        Task<CharacterDTO?> GetCharacterByAccountIdAndPositionAsync(long accountId, byte position);

        Task<CharacterDTO?> GetCharacterByIdAsync(long characterId);

        Task<IDictionary<byte, byte>> GetChannelsByMapIdAsync(short mapId);

        Task<CharacterDTO?> GetCharacterAndItemsByIdAsync(long characterId);

        Task<CharacterDTO?> GetCharacterByNameAsync(string characterName);

        Task<DigimonDTO?> GetDigimonByIdAsync(long digimonId);
        
        Task<List<DigimonDTO>> GetAllDigimonsAsync();

        Task<List<DigimonDTO>> GetDigimonsByIdsAsync(List<long> digimonIds);

        Task<(string TamerName, string GuildName)> GetCharacterNameAndGuildByIdQAsync(long characterId);

        Task<List<CharacterDigimonGrowthSystemDTO>> GetCharacterDigimonGrowthAsync(long characterId);

        Task<IList<CharacterEncyclopediaDTO>> GetCharacterEncyclopediaByCharacterIdAsync(long characterId);

        Task<List<CharacterActiveDeckDTO>> GetCharacterDecksByIdAsync(long characterId);

        Task<List<DigimonSkillMemoryDTO>> GetDigimonSkillMemoryAsync(long evolutionId);
        Task<LastOpenMapDTO> GetCharacterLastOpenMapAsync(long characterId);
    }
}
