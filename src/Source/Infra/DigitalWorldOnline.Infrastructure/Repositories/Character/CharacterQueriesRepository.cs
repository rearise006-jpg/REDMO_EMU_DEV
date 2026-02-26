using AutoMapper;
using DigitalWorldOnline.Commons.DTOs.Character;
using DigitalWorldOnline.Commons.DTOs.Character;  // ✅ Bu import var mı kontrol et
using DigitalWorldOnline.Commons.DTOs.Digimon;
using DigitalWorldOnline.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DigitalWorldOnline.Infrastructure.Repositories.Character
{
    public class CharacterQueriesRepository : ICharacterQueriesRepository
    {
        private readonly DatabaseContext _context;
        private readonly IMapper _mapper;
        private readonly ILogger<CharacterQueriesRepository> _logger;

        public CharacterQueriesRepository(
            DatabaseContext context,
            IMapper mapper,
            ILogger<CharacterQueriesRepository> logger)
        {
            _context = context;
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private const int MaxActiveDigimonSlot = 8; // 0 a 8 → 9 slots

        //TODO: migrate to the server repository
        public async Task<IDictionary<byte, byte>> GetChannelsByMapIdAsync(short mapId)
        {
            // 📌 Database'den map config'i al
            var mapConfig = await _context.MapConfig
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.MapId == mapId);

            var channels = new Dictionary<byte, byte>();

            // 📌 Database'den channel sayısını al, fallback olarak 3 kullan
            int channelCount = mapConfig?.Channels ?? 3;

            // Tüm kanalları initialize et
            for (byte i = 0; i < channelCount; i++)
            {
                channels.Add(i, 0);
            }

            // Database'den bu map'taki tüm character'ları al
            var tamersChannel = await _context.Character
                .AsNoTracking()
                .Where(x => x.Location.MapId == mapId &&
                            x.Channel != byte.MaxValue)
                .Select(x => x.Channel)
                .ToListAsync();

            // Oyuncu sayılarını say
            foreach (var tamerChannel in tamersChannel)
            {
                if (!channels.ContainsKey(tamerChannel))
                    channels.Add(tamerChannel, 1);
                else
                    channels[tamerChannel]++;
            }

            return channels;
        }

        public async Task<CharacterDTO?> GetCharacterAndItemsByIdAsync(long characterId)
        {
            var dto = await _context.Character
                .AsNoTracking()
                .Include(x => x.ItemList)
                .ThenInclude(y => y.Items)
                .ThenInclude(z => z.SocketStatus)
                .Include(x => x.ItemList)
                .ThenInclude(y => y.Items)
                .ThenInclude(z => z.AccessoryStatus) // Including AccessoryStatus within Items
                .Include(x => x.ItemList)
                .ThenInclude(y => y.Items)
                .ThenInclude(z => z.SocketStatus) // Including SocketStatus within Items
                .FirstOrDefaultAsync(x => x.Id == characterId);

            dto?.ItemList.ForEach(itemList => itemList.Items = itemList.Items.OrderBy(x => x.Slot).ToList());

            return dto;
        }

        public async Task<CharacterDTO?> GetCharacterByAccountIdAndPositionAsync(long accountId, byte position)
        {
            return await _context.Character
                .AsNoTracking()
                .Include(x => x.Location)
                .Include(x => x.Digimons)
                .FirstOrDefaultAsync(x => x.AccountId == accountId &&
                                          x.Position == position);
        }

            public async Task<CharacterDTO?> GetCharacterByIdAsync(long characterId)
        {
            try
            {
                var character = await _context.Character
                    .AsSplitQuery()
                    .AsNoTracking()
                    .Include(x => x.ActiveEvolution)
                    .Include(x => x.Incubator)
                    .Include(x => x.Location)
                    .Include(x => x.Xai)
                    .Include(x => x.TimeReward)
                    .Include(x => x.AttendanceReward)
                    .Include(x => x.ActiveSkill)
                    .Include(x => x.ActiveDeck)
                    .Include(x => x.DailyPoints)
                    .Include(x => x.Friends)
                    .ThenInclude(y => y.Friend)
                    .Include(x => x.Friended)
                    .ThenInclude(y => y.Character)
                    .Include(x => x.Foes)
                    .ThenInclude(y => y.Foe)
                    .Include(x => x.Foed)
                    .ThenInclude(y => y.Character)
                    .Include(x => x.Encyclopedia)
                    .ThenInclude(y => y.Evolutions)
                    .Include(x => x.Encyclopedia)
                    .ThenInclude(y => y.EvolutionAsset)
                    .Include(x => x.ConsignedShop)
                    .ThenInclude(y => y.Location)
                    .Include(x => x.Progress)
                    .ThenInclude(x => x.InProgressQuestData)
                    .Include(y => y.MapRegions)
                    .Include(x => x.Points)
                    .Include(x => x.BuffList)
                    .ThenInclude(y => y.Buffs)
                    .Include(x => x.SealList)
                    .ThenInclude(y => y.Seals)
                    .Include(x => x.DigimonArchive)
                    .ThenInclude(y => y.DigimonArchives)
                    .Include(x => x.ItemList)
                    .ThenInclude(y => y.Items)
                    .ThenInclude(z => z.SocketStatus)
                    .Include(x => x.ItemList)
                    .ThenInclude(y => y.Items)
                    .ThenInclude(z => z.AccessoryStatus)
                    .Include(x => x.Digimons)
                    .ThenInclude(y => y.Digiclone)
                    .ThenInclude(z => z.History)
                    .Include(x => x.Digimons)
                    .ThenInclude(y => y.AttributeExperience)
                    .Include(x => x.Digimons)
                    .ThenInclude(y => y.Location)
                    .Include(x => x.Digimons)
                    .ThenInclude(y => y.BuffList)
                    .ThenInclude(z => z.Buffs)
                    .Include(x => x.Digimons)
                    .ThenInclude(y => y.Evolutions)
                    .ThenInclude(z => z.Skills)
                    .Include(x => x.Digimons)
                    .ThenInclude(y => y.Evolutions)
                    .ThenInclude(z => z.SkillsMemory)
                    .Include(x => x.DeckBuff)
                    .ThenInclude(y => y.Options)
                    .ThenInclude(z => z.DeckBookInfo)
                    .SingleOrDefaultAsync(x => x.Id == characterId);

                if (character != null)
                {
                    character.ItemList.ForEach(itemList =>
                        itemList.Items = itemList.Items.OrderBy(x => x.Slot).ToList());

                    character.Digimons = character.Digimons
                        .Where(x => x.Slot <= MaxActiveDigimonSlot)
                        .OrderBy(x => x.Slot)
                        .ToList();
                }

                return character;
            }
            catch (InvalidCastException ex)
            {
                // Specific cast error — most likely Byte ↔ Short mismatch
                Console.WriteLine($"❌ InvalidCastException: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                throw; // optional: rethrow to see in full logs
            }
            catch (Exception ex)
            {
                // Catch all other issues (null refs, SQL timeout, etc.)
                Console.WriteLine($"❌ Exception in GetCharacterByIdAsync: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                throw;
            }
        }


        //public async Task<CharacterDTO?> GetCharacterByNameAsync(string characterName)
        //{
        //    return await _context.Character
        //        .AsNoTracking().FirstOrDefaultAsync(x => x.Name == characterName);
        //}

        public async Task<CharacterDTO?> GetCharacterByNameAsync(string characterName)
        {
            var lowerCharacterName = characterName.ToLower();

            return await _context.Character
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Name.ToLower() == lowerCharacterName);
        }

        public async Task<DigimonDTO?> GetDigimonByIdAsync(long digimonId)
        {
            return await _context.Digimon
                .AsNoTracking()
                .Include(x => x.Digiclone)
                .ThenInclude(x => x.History)
                .Include(x => x.AttributeExperience)
                .Include(x => x.Evolutions)
                .ThenInclude(y => y.Skills)
                .Include(x => x.BuffList)
                .ThenInclude(x => x.Buffs)
                .SingleOrDefaultAsync(x => x.Id == digimonId);
        }

        public async Task<List<DigimonDTO>> GetAllDigimonsAsync()
        {
            try
            {
                // Guardar el timeout actual
                var currentTimeout = _context.Database.GetCommandTimeout();

                // Establecer un timeout mayor para esta consulta (5 minutos)
                _context.Database.SetCommandTimeout(300);

                var result = await _context.Digimon
                    .AsNoTracking()
                    .Include(x => x.Character)
                    .ThenInclude(y => y.Encyclopedia)
                    .ThenInclude(y => y.Evolutions)
                    .Include(x => x.Digiclone)
                    .ThenInclude(x => x.History)
                    .Include(x => x.AttributeExperience)
                    .Include(x => x.Location)
                    .Include(x => x.Evolutions)
                    .ThenInclude(y => y.Skills)
                    .Include(x => x.BuffList)
                    .ThenInclude(x => x.Buffs)
                    .ToListAsync();

                // Restaurar el timeout original
                _context.Database.SetCommandTimeout(currentTimeout);

                return _mapper.Map<List<DigimonDTO>>(result);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error al obtener todos los digimons");
                throw;
            }
        }

        public async Task<List<DigimonSkillMemoryDTO>> GetDigimonSkillMemoryAsync(long evolutionId)
        {
            return await _context.DigimonSkillMemory
                .AsNoTracking()
                .Where(x => x.EvolutionId == evolutionId)
                .ToListAsync();
        }

        public async Task<List<DigimonDTO>> GetDigimonsByIdsAsync(List<long> digimonIds)
        {
            if (digimonIds == null || digimonIds.Count == 0)
                return new List<DigimonDTO>();

            const int batchSize = 1000;
            var result = new List<DigimonDTO>();

            foreach (var batch in digimonIds.Chunk(batchSize))
            {
                var digimons = await _context.Digimon
                    .AsNoTracking()
                    .Include(x => x.Digiclone)
                    .Include(x => x.AttributeExperience)
                    .Include(x => x.Evolutions)
                        .ThenInclude(y => y.Skills)
                    .Include(x => x.Evolutions)
                        .ThenInclude(y => y.SkillsMemory)
                    .Include(x => x.BuffList)
                        .ThenInclude(x => x.Buffs)
                    .Where(x => batch.Contains(x.Id))
                    .ToListAsync();

                result.AddRange(digimons);
            }

            return result;
        }

        public async Task<IList<CharacterDTO>> GetCharactersByAccountIdAsync(long accountId)
        {
            // TODO: verify the need for improvement in response time
            var characters = await _context.Character
                .AsSplitQuery()
                .AsNoTracking()
                .Include(x => x.Location)
                .Include(x => x.Xai)
                .Include(x => x.SealList)
                .ThenInclude(y => y.Seals)
                .Include(x => x.ItemList)
                .ThenInclude(y => y.Items)
                .Include(x => x.Digimons)
                .Where(x => x.AccountId == accountId)
                .ToListAsync();

            characters.ForEach(character =>
            {
                if (character != null)
                {
                    character.ItemList.ForEach(
                        itemList => itemList.Items = itemList.Items.OrderBy(x => x.Slot).ToList());
                    character.Digimons = character.Digimons.Where(x => x.Slot <= MaxActiveDigimonSlot).OrderBy(x => x.Slot).ToList();
                }
            });

            return characters;
        }

        public async Task<(string TamerName, string GuildName)> GetCharacterNameAndGuildByIdQAsync(long characterId)
        {
            var dto = await _context.Character
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == characterId);

            var dtoGuild = await _context.Guild
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Members.Any(m => m.CharacterId == characterId));


            if (dto != null && dtoGuild == null)
            {
                // Assuming your Character object has properties TamerName and GuildName.
                return (dto.Name, string.Empty);
            }

            if (dto != null && dtoGuild != null)
            {
                return (dto.Name, dtoGuild.Name);
            }

            return (string.Empty, string.Empty);
        }

        public async Task<List<CharacterDigimonGrowthSystemDTO>> GetCharacterDigimonGrowthAsync(long characterId)
        {
            return await _context.CharacterDigimonGrowthSystem.AsNoTracking().Where(x => x.CharacterId == characterId).ToListAsync();
        }

        public async Task<List<CharacterActiveDeckDTO>> GetCharacterDecksByIdAsync(long characterId)
        {
            return await _context.CharacterActiveDecks
                .AsNoTracking()
                .Where(x => x.CharacterId == characterId)
                .ToListAsync();
        }

        public async Task<IList<CharacterEncyclopediaDTO>> GetCharacterEncyclopediaByCharacterIdAsync(long characterId)
        {
            // TODO: verify the need for improvement in response time
            var characters = await _context.CharacterEncyclopedia
                .AsNoTracking()
                .Include(x => x.Evolutions)
                .Include(x => x.EvolutionAsset)
                .Where(x => x.CharacterId == characterId)
                .ToListAsync();

            return characters;


        }
        // CharacterQueriesRepository sınıfına bu metodu ekle
        public async Task<LastOpenMapDTO> GetCharacterLastOpenMapAsync(long characterId)
        {
            try
            {
                var character = await _context.Character
                    .FirstOrDefaultAsync(c => c.Id == characterId);

                if (character == null)
                    return null;

                return new LastOpenMapDTO
                {
                    MapId = (short)character.LastOpenMapId,
                    X = character.LastOpenMapX,
                    Y = character.LastOpenMapY
                };
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}