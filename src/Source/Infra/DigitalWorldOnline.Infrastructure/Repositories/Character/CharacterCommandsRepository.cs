using AutoMapper;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.Character;
using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Models.Mechanics;
using DigitalWorldOnline.Commons.Models.Events;
using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Commons.Models.Chat;
using DigitalWorldOnline.Commons.DTOs.Character;
using DigitalWorldOnline.Commons.DTOs.Digimon;
using DigitalWorldOnline.Commons.DTOs.Base;
using DigitalWorldOnline.Commons.DTOs.Chat;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using DigitalWorldOnline.Commons.DTOs.Mechanics;

namespace DigitalWorldOnline.Infrastructure.Repositories.Character
{
    public class CharacterCommandsRepository : ICharacterCommandsRepository
    {
        private readonly DatabaseContext _context;
        private readonly IMapper _mapper;

        public CharacterCommandsRepository(DatabaseContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<long> AddCharacterAsync(CharacterModel character)
        {
            var dto = _mapper.Map<CharacterDTO>(character);

            _context.Character.Add(dto);
            await _context.SaveChangesAsync();

            return dto.Id;
        }

        public async Task<DigimonDTO> AddDigimonAsync(DigimonModel digimon)
        {
            var tamerDto = await _context.Character
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
                .SingleOrDefaultAsync(x => x.Id == digimon.CharacterId);

            var dto = _mapper.Map<DigimonDTO>(digimon);

            if (tamerDto == null) return dto;

            tamerDto.Digimons.Add(dto);

            await _context.SaveChangesAsync();

            return dto;
        }
        public async Task<CharacterFriendDTO> AddFriendAsync(CharacterFriendModel friend)
        {
            var tamerDto = await _context.Character
                .Include(x => x.Friends)
                .SingleOrDefaultAsync(x => x.Id == friend.CharacterId);

            var dto = _mapper.Map<CharacterFriendDTO>(friend);

            if (tamerDto == null) return dto;

            tamerDto.Friends.Add(dto);

            await _context.SaveChangesAsync();

            return dto;
        }
        public async Task DeleteDigimonSkillMemoryAsync(long skillId, long evolutionId)
        {
            var dto = await _context.DigimonSkillMemory
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.SkillId == skillId && x.EvolutionId == evolutionId);

            if (dto == null)
                return;

            _context.Remove(dto);
            await _context.SaveChangesAsync();
        }
        public async Task<DeleteCharacterResultEnum> DeleteCharacterByAccountAndPositionAsync(long accountId, byte characterPosition)
        {
            // Get the execution strategy
            var strategy = _context.Database.CreateExecutionStrategy();

            // Execute the operation with retry logic
            return await strategy.ExecuteAsync(async () =>
            {
                // Begin the transaction
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Find the character to delete
                    var dto = await _context.Character
                        .Include(x => x.Incubator)
                        .SingleOrDefaultAsync(x => x.AccountId == accountId && x.Position == characterPosition);

                    if (dto == null)
                        return DeleteCharacterResultEnum.Error;

                    // Remove the character
                    _context.Character.Remove(dto);

                    // Save changes and commit the transaction
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return DeleteCharacterResultEnum.Deleted;
                }
                catch (Exception)
                {
                    // Rollback the transaction on error
                    await transaction.RollbackAsync();
                    throw; // Re-throw the exception to allow retry
                }
            });
        }

        public async Task UpdateCharacterChannelByIdAsync(long characterId, byte channel)
        {
            var character = await _context.Character.FirstOrDefaultAsync(x => x.Id == characterId);
            if (character != null)
            {
                character.Channel = channel;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateCharacterLocationAsync(CharacterLocationModel location)
        {
            var dto = await _context.CharacterLocation.FirstOrDefaultAsync(x => x.Id == location.Id);
            if (dto != null)
            {
                dto.MapId = location.MapId;
                dto.X = location.X;
                dto.Y = location.Y;
                dto.Z = location.Z;

                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateDigimonLocationAsync(DigimonLocationModel location)
        {
            var dto = await _context.DigimonLocation.FirstOrDefaultAsync(x => x.Id == location.Id);
            if (dto != null)
            {
                dto.MapId = location.MapId;
                dto.X = location.X;
                dto.Y = location.Y;
                dto.Z = location.Z;

                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateCharacterResourcesAsync(CharacterModel tamer)
        {
            var tamerDto = await _context.Character
                .Include(x => x.Digimons)
                .FirstOrDefaultAsync(x => x.Id == tamer.Id);

            if (tamerDto != null)
            {
                tamerDto.CurrentHp = tamer.CurrentHp;
                tamerDto.CurrentDs = tamer.CurrentDs;
                tamerDto.Digimons = _mapper.Map<List<DigimonDTO>>(tamer.Digimons);

                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateCharactersStateAsync(CharacterStateEnum state)
        {
            var characters = await _context.Character.ToListAsync();
            foreach (var character in characters)
            {
                character.State = state;
                character.EventState = CharacterEventStateEnum.None;
            }

            await _context.SaveChangesAsync();
        }

        public async Task UpdateCharacterStateByIdAsync(long characterId, CharacterStateEnum state)
        {
            var character = await _context.Character.FirstOrDefaultAsync(x => x.Id == characterId);
            if (character != null)
            {
                character.State = state;
                await _context.SaveChangesAsync();
            }
        }


        public async Task UpdateCharacterExperienceAsync(long tamerId, long currentExperience, byte level)
        {
            var dto = await _context.Character.FirstOrDefaultAsync(x => x.Id == tamerId);

            if (dto != null)
            {
                dto.CurrentExperience = currentExperience;
                dto.Level = level;

                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateDigimonExperienceAsync(DigimonModel digimon)
        {
            var dto = await _context.Digimon
                .Include(x => x.Evolutions)
                .Include(x => x.AttributeExperience)
                .FirstOrDefaultAsync(x => x.Id == digimon.Id);

            if (dto != null)
            {
                dto.CurrentExperience = digimon.CurrentExperience;
                dto.CurrentSkillExperience = digimon.CurrentSkillExperience;
                dto.TranscendenceExperience = digimon.TranscendenceExperience;
                dto.Level = digimon.Level;

                dto.AttributeExperience.Data = digimon.AttributeExperience.Data;
                dto.AttributeExperience.Vaccine = digimon.AttributeExperience.Vaccine;
                dto.AttributeExperience.Virus = digimon.AttributeExperience.Virus;
                dto.AttributeExperience.Ice = digimon.AttributeExperience.Ice;
                dto.AttributeExperience.Water = digimon.AttributeExperience.Water;
                dto.AttributeExperience.Fire = digimon.AttributeExperience.Fire;
                dto.AttributeExperience.Land = digimon.AttributeExperience.Land;
                dto.AttributeExperience.Wind = digimon.AttributeExperience.Wind;
                dto.AttributeExperience.Wood = digimon.AttributeExperience.Wood;
                dto.AttributeExperience.Light = digimon.AttributeExperience.Light;
                dto.AttributeExperience.Dark = digimon.AttributeExperience.Dark;
                dto.AttributeExperience.Thunder = digimon.AttributeExperience.Thunder;
                dto.AttributeExperience.Steel = digimon.AttributeExperience.Steel;

                foreach (var evolutionDto in dto.Evolutions)
                {
                    var evolutionModel = digimon.Evolutions.FirstOrDefault(x => x.Id == evolutionDto.Id);
                    if (evolutionModel != null)
                    {
                        evolutionDto.Type = evolutionModel.Type;
                        evolutionDto.Unlocked = evolutionModel.Unlocked;
                        evolutionDto.SkillPoints = evolutionModel.SkillPoints;
                        evolutionDto.SkillMastery = evolutionModel.SkillMastery;
                        evolutionDto.SkillExperience = evolutionModel.SkillExperience;
                    }
                }

                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateCharacterSealsAsync(CharacterSealListModel sealList)
        {
            var dto = await _context.CharacterSealList
                .AsNoTracking()
                .Include(x => x.Seals)
                .FirstOrDefaultAsync(x => x.Id == sealList.Id);

            if (dto != null)
            {
                dto.SealLeaderId = sealList.SealLeaderId;

                foreach (var seal in sealList.Seals)
                {
                    var dtoSeal = dto.Seals.FirstOrDefault(x => x.Id == seal.Id);
                    if (dtoSeal != null)
                    {
                        dtoSeal.SealId = seal.SealId;
                        dtoSeal.SequentialId = seal.SequentialId;
                        dtoSeal.Favorite = seal.Favorite;
                        dtoSeal.Amount = seal.Amount;
                        _context.Update(dtoSeal);
                    }
                    else
                    {
                        dtoSeal = _mapper.Map<CharacterSealDTO>(seal);
                        dtoSeal.SealListId = sealList.Id;
                        dto.Seals.Add(dtoSeal);
                        _context.Add(dtoSeal);
                    }
                }

                _context.Update(dto);
                _context.SaveChanges();
            }
        }

        public async Task AddChatMessageAsync(ChatMessageModel chatMessage)
        {
            var dto = _mapper.Map<ChatMessageDTO>(chatMessage);
            if (dto != null)
            {
                await _context.AddAsync(dto);
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdatePartnerCurrentTypeAsync(DigimonModel digimon)
        {
            var dto = await _context.Digimon.FirstOrDefaultAsync(x => x.Id == digimon.Id);
            if (dto != null)
            {
                dto.CurrentType = digimon.CurrentType;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateDigicloneAsync(DigimonDigicloneModel digiclone)
        {
            var dto = await _context.DigimonDigiclone
                .Include(x => x.History)
                .FirstOrDefaultAsync(x => x.Id == digiclone.Id || x.DigimonId == digiclone.DigimonId);

            if (dto != null)
            {
                dto.ATLevel = digiclone.ATLevel;
                dto.BLLevel = digiclone.BLLevel;
                dto.CTLevel = digiclone.CTLevel;
                dto.EVLevel = digiclone.EVLevel;
                dto.HPLevel = digiclone.HPLevel;

                dto.ATValue = digiclone.ATValue;
                dto.BLValue = digiclone.BLValue;
                dto.CTValue = digiclone.CTValue;
                dto.EVValue = digiclone.EVValue;
                dto.HPValue = digiclone.HPValue;

                dto.History.ATValues = digiclone.History.ATValues;
                dto.History.BLValues = digiclone.History.BLValues;
                dto.History.CTValues = digiclone.History.CTValues;
                dto.History.EVValues = digiclone.History.EVValues;
                dto.History.HPValues = digiclone.History.HPValues;

                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateCharacterTitleByIdAsync(long characterId, short titleId)
        {
            var dto = await _context.Character.FirstOrDefaultAsync(x => x.Id == characterId);
            if (dto != null)
            {
                dto.CurrentTitle = titleId;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateCharacterProgressCompleteAsync(CharacterProgressModel progress)
        {
            var dto = await _context.CharacterProgress.FirstOrDefaultAsync(x => x.Id == progress.Id);
            if (dto != null)
            {
                dto.CompletedData = progress.CompletedData;
                dto.CompletedDataValue = progress.CompletedDataValue;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateCharacterBuffListAsync(CharacterBuffListModel buffList)
        {
            var dto = await _context.CharacterBuffList
                .AsNoTracking()
                .Include(x => x.Buffs)
                .FirstOrDefaultAsync(x => x.Id == buffList.Id);

            if (dto != null)
            {
                // Remove os buffs em dto que não existem em buffList
                var buffsToRemove = dto.Buffs.Where(dtoBuff => !buffList.Buffs.Any(buff => buff.Id == dtoBuff.Id))
                    .ToList();
                foreach (var buffToRemove in buffsToRemove)
                {
                    dto.Buffs.Remove(buffToRemove);
                    _context.Remove(buffToRemove);
                }

                foreach (var buff in buffList.Buffs.Where(x => !x.Expired))
                {
                    var dtoBuff = dto.Buffs.FirstOrDefault(x => x.Id == buff.Id);
                    if (dtoBuff != null)
                    {
                        dtoBuff.Duration = buff.Duration;
                        dtoBuff.EndDate = buff.EndDate;
                        dtoBuff.SkillId = buff.SkillId;
                        dtoBuff.TypeN = buff.TypeN;
                        _context.Update(dtoBuff);
                    }
                    else
                    {
                        dtoBuff = _mapper.Map<CharacterBuffDTO>(buff);
                        dtoBuff.BuffListId = buffList.Id;
                        dto.Buffs.Add(dtoBuff);
                        _context.Add(dtoBuff);
                    }
                }

                _context.SaveChanges();
            }
        }

        public async Task UpdateDigimonBuffListAsync(DigimonBuffListModel buffList)
        {
            var dto = await _context.DigimonBuffList
                .Include(x => x.Buffs)
                .FirstOrDefaultAsync(x => x.Id == buffList.Id);

            if (dto is null)
                return;

            // Remover buffs que não estão mais na lista recebida
            var buffsToRemove = dto.Buffs
                .Where(existing => !buffList.Buffs.Any(updated => updated.Id == existing.Id))
                .ToList();

            foreach (var buffToRemove in buffsToRemove)
            {
                _context.Remove(buffToRemove); // remove direto do contexto
            }

            foreach (var buff in buffList.Buffs)
            {
                var existing = dto.Buffs.FirstOrDefault(x => x.Id == buff.Id);
                if (existing != null)
                {
                    // Atualiza apenas os campos relevantes
                    existing.Duration = buff.Duration;
                    existing.EndDate = buff.EndDate;
                    existing.SkillId = buff.SkillId;
                    existing.TypeN = buff.TypeN;
                    existing.CoolEndDate = buff.CoolEndDate;
                    existing.Cooldown = buff.Cooldown;
                }
                else
                {
                    var newBuff = _mapper.Map<DigimonBuffDTO>(buff);
                    newBuff.BuffListId = buffList.Id;

                    // Adiciona explicitamente ao contexto e ao DTO
                    _context.Entry(newBuff).State = EntityState.Added;
                    dto.Buffs.Add(newBuff);
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task UpdateDigimonSkillCodeAsync(DigimonEvolutionSkillModel skillModel)
        {
            // Carregar o Digimon Evolution com as skills (não usar AsNoTracking para permitir atualizações)
            var evolution = await _context.DigimonEvolution
                .Include(x => x.Skills)
                .FirstOrDefaultAsync(x => x.Id == skillModel.EvolutionId);

            if (evolution == null)
            {
                throw new Exception($"DigimonEvolution com ID {skillModel.EvolutionId} não encontrado.");
            }

            // Atualizar apenas a skill específica com o mesmo ID
            var skillToUpdate = evolution.Skills.FirstOrDefault(s => s.Id == skillModel.Id);
            if (skillToUpdate != null)
            {
                skillToUpdate.MaxLevel = skillModel.MaxLevel;
            }
            else
            {
                throw new Exception($"Skill com ID {skillModel.Id} não encontrada no DigimonEvolution {skillModel.EvolutionId}.");
            }

            // Salvar alterações no banco de dados
            await _context.SaveChangesAsync();
        }

        
        
        public async Task UpdateDigimonSkillMemoryAsync(DigimonSkillMemoryModel skillMemoryEvolution)
        {
            try
            {
                Console.WriteLine($"[DEBUG] Iniciando UpdateDigimonSkillMemoryAsync");
                Console.WriteLine($"[DEBUG] Valores de entrada - Cooldown: {skillMemoryEvolution.Cooldown}, Duration: {skillMemoryEvolution.Duration}");

                // Asegurar valores predeterminados
                var cooldown = Math.Max(0, skillMemoryEvolution.Cooldown);
                var duration = Math.Max(0, skillMemoryEvolution.Duration);
                var evolutionStatus = (byte)Math.Max(0, (int)skillMemoryEvolution.EvolutionStatus);

                // Asegurar fechas
                var endCooldown = skillMemoryEvolution.EndCooldown == default ? DateTime.Now : skillMemoryEvolution.EndCooldown;
                var endDate = skillMemoryEvolution.EndDate == default ? DateTime.Now : skillMemoryEvolution.EndDate;

                Console.WriteLine($"[DEBUG] Valores finales - Cooldown: {cooldown}, Duration: {duration}, Status: {evolutionStatus}");

                var sql = @"
    IF EXISTS (SELECT 1 FROM Digimon.SkillMemory 
              WHERE EvolutionId = @p0 AND SkillId = @p1)
    BEGIN
        UPDATE Digimon.SkillMemory 
        SET Cooldown = @p2, 
            Duration = @p3, 
            EndCooldown = @p4, 
            EndDate = @p5, 
            EvolutionStatus = @p6, 
            DigimonType = @p7
        WHERE EvolutionId = @p0 AND SkillId = @p1
    END
    ELSE
    BEGIN
        DECLARE @NewId bigint;
        SELECT @NewId = ISNULL(MAX(Id), 0) + 1 FROM Digimon.SkillMemory;
        
        INSERT INTO Digimon.SkillMemory 
            (Id, SkillId, Cooldown, Duration, EndCooldown, EndDate, 
             EvolutionId, EvolutionStatus, DigimonType)
        VALUES 
            (@NewId, @p1, @p2, @p3, @p4, @p5, @p0, @p6, @p7)
            
        SELECT @NewId;
    END";
                var parameters = new object[]
                {
            skillMemoryEvolution.EvolutionId,
            skillMemoryEvolution.SkillId,
            cooldown,
            duration,
            endCooldown,
            endDate,
            evolutionStatus,
            skillMemoryEvolution.DigimonType
                };

                Console.WriteLine($"[DEBUG] Ejecutando SQL con parámetros: {string.Join(", ", parameters)}");

                var result = await _context.Database.ExecuteSqlRawAsync(sql, parameters);
                Console.WriteLine($"[DEBUG] Operación completada. Filas afectadas: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error en UpdateDigimonSkillMemoryAsync: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[ERROR] Inner Exception: {ex.InnerException.Message}");
                    if (ex.InnerException.InnerException != null)
                    {
                        Console.WriteLine($"[ERROR] Inner Inner Exception: {ex.InnerException.InnerException.Message}");
                    }
                }
                throw;
            }
        }

        public async Task UpdateCharacterActiveEvolutionAsync(CharacterActiveEvolutionModel activeEvolution)
        {
            var dto = await _context.CharacterActiveEvolution.SingleOrDefaultAsync(x => x.Id == activeEvolution.Id);
            if (dto != null)
            {
                dto.XgPerSecond = activeEvolution.XgPerSecond;
                dto.DsPerSecond = activeEvolution.DsPerSecond;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateCharacterBasicInfoAsync(CharacterModel character)
        {
            var dto = await _context.Character
                .Include(x => x.Digimons)
                .SingleOrDefaultAsync(x => x.Id == character.Id);

            if (dto != null)
            {
                dto.CurrentHp = character.CurrentHp;
                dto.CurrentDs = character.CurrentDs;
                dto.XGauge = character.XGauge;
                dto.XCrystals = character.XCrystals;

                foreach (var digimonDto in dto.Digimons)
                {
                    var digimonModel = character.Digimons.FirstOrDefault(x => x.Id == digimonDto.Id);
                    if (digimonModel != null)
                    {
                        digimonDto.CurrentHp = digimonModel.CurrentHp;
                        digimonDto.CurrentDs = digimonModel.CurrentDs;
                        digimonDto.CurrentType = digimonModel.CurrentType;
                    }
                }

                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateItemListBitsAsync(long itemListId, long bits)
        {
            var dto = await _context.ItemLists.FirstOrDefaultAsync(x => x.Id == itemListId);
            if (dto != null)
            {
                dto.Bits = bits;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateItemsAsync(List<ItemModel> items)
        {
            await RemoveDeletedItems(items);
            await AddOrUpdateItems(items);
            await _context.SaveChangesAsync();
        }

        private async Task AddOrUpdateItems(List<ItemModel> items)
        {
            if (!items.Any()) return;

            foreach (var item in items.ToList())
            {
                var dto = await _context.Items
                    .Include(x => x.AccessoryStatus)
                    .Include(x => x.SocketStatus)
                    .FirstOrDefaultAsync(x => x.Id == item.Id);

                if (dto != null)
                {
                    dto.Slot = item.Slot;
                    dto.Amount = item.Amount;
                    dto.ItemId = item.ItemId;
                    dto.Duration = item.Duration;
                    dto.EndDate = item.EndDate;
                    dto.FirstExpired = item.FirstExpired;
                    if (item.ItemListId > 0) dto.ItemListId = item.ItemListId;
                    dto.RerollLeft = item.RerollLeft;
                    dto.FamilyType = item.FamilyType;
                    dto.Power = item.Power;
                    dto.TamerShopSellPrice = item.TamerShopSellPrice;

                    foreach (var dtoStatus in dto.AccessoryStatus)
                    {
                        var modelStatus = item.AccessoryStatus.First(x => x.Slot == dtoStatus.Slot);
                        dtoStatus.Type = modelStatus.Type;
                        dtoStatus.Value = modelStatus.Value;
                    }

                    foreach (var dtoStatus in dto.SocketStatus)
                    {
                        var modelStatus = item.SocketStatus.First(x => x.Slot == dtoStatus.Slot);
                        dtoStatus.Type = modelStatus.Type;
                        dtoStatus.AttributeId = modelStatus.AttributeId;
                        dtoStatus.Value = modelStatus.Value;
                    }
                }
                else
                {
                    await _context.AddAsync(_mapper.Map<ItemDTO>(item));
                }
            }
        }

        private async Task RemoveDeletedItems(List<ItemModel> items)
        {
            if (!items.Any()) return;

            var dtoItemsId = await _context.Items
                .Where(x => x.ItemListId == items.First().ItemListId)
                .ToListAsync();

            var itemsToRemove = dtoItemsId.Where(x => !items.Select(y => y.Id).Contains(x.Id)).ToList();

            foreach (var itemToRemove in itemsToRemove)
                _context.Remove(itemToRemove);
        }

        public async Task UpdateItemAccessoryStatusAsync(ItemModel item)
        {
            var dto = await _context.Items
                .Include(x => x.AccessoryStatus)
                .FirstOrDefaultAsync(x => x.Id == item.Id);

            if (dto != null)
            {
                dto.RerollLeft = item.RerollLeft;
                dto.Power = item.Power;

                foreach (var dtoStatus in dto.AccessoryStatus)
                {
                    var modelStatus = item.AccessoryStatus.First(x => x.Slot == dtoStatus.Slot);
                    dtoStatus.Type = modelStatus.Type;
                    dtoStatus.Value = modelStatus.Value;
                }

                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateItemSocketStatusAsync(ItemModel item)
        {
            var dto = await _context.Items
                .Include(x => x.SocketStatus)
                .FirstOrDefaultAsync(x => x.Id == item.Id);

            if (dto != null)
            {
                dto.RerollLeft = item.RerollLeft;
                dto.Power = item.Power;

                foreach (var dtoStatus in dto.SocketStatus)
                {
                    var modelStatus = item.SocketStatus.First(x => x.Slot == dtoStatus.Slot);
                    dtoStatus.Type = modelStatus.Type;
                    dtoStatus.AttributeId = modelStatus.AttributeId;
                    dtoStatus.Value = modelStatus.Value;
                }

                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateItemAsync(ItemModel item)
        {
            var dto = await _context.Items
                .Include(x => x.AccessoryStatus)
                .Include(x => x.SocketStatus)
                .FirstOrDefaultAsync(x => x.Id == item.Id);

            if (dto != null)
            {
                dto.Amount = item.Amount;
                dto.ItemId = item.ItemId;
                dto.Duration = item.Duration;
                dto.EndDate = item.EndDate;
                dto.FirstExpired = item.FirstExpired;
                if (item.ItemListId > 0) dto.ItemListId = item.ItemListId;
                dto.RerollLeft = item.RerollLeft;
                dto.Power = item.Power;
                dto.TamerShopSellPrice = item.TamerShopSellPrice;

                foreach (var dtoStatus in dto.AccessoryStatus)
                {
                    var modelStatus = item.AccessoryStatus.First(x => x.Slot == dtoStatus.Slot);
                    dtoStatus.Type = modelStatus.Type;
                    dtoStatus.Value = modelStatus.Value;
                }

                foreach (var dtoStatus in dto.SocketStatus)
                {
                    var modelStatus = item.SocketStatus.First(x => x.Slot == dtoStatus.Slot);
                    dtoStatus.Type = modelStatus.Type;
                    dtoStatus.AttributeId = modelStatus.AttributeId;
                    dtoStatus.Value = modelStatus.Value;
                }

                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateItemListSizeAsync(long itemListId, byte newSize)
        {
            var dto = await _context.ItemLists.FirstOrDefaultAsync(x => x.Id == itemListId);
            if (dto != null)
            {
                dto.Size = newSize;
                await _context.SaveChangesAsync();
            }
        }

        // Otimizações aplicadas: SaveChanges -> SaveChangesAsync, remoção de AsNoTracking desnecessário onde aplicável

        public async Task AddInventorySlotsAsync(List<ItemModel> items)
        {
            var itemListDto = await _context.ItemLists
                .Include(x => x.Items)
                .ThenInclude(y => y.AccessoryStatus)
                .Include(x => x.Items)
                .ThenInclude(y => y.SocketStatus)
                .FirstOrDefaultAsync(x => x.Id == items.First().ItemListId);

            if (itemListDto != null)
            {
                foreach (var item in items)
                {
                    await _context.AddAsync(_mapper.Map<ItemDTO>(item));
                    itemListDto.Size += 1;
                }
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateCharacterEventStateByIdAsync(long characterId, CharacterEventStateEnum state)
        {
            var dto = await _context.Character.FirstOrDefaultAsync(x => x.Id == characterId);
            if (dto != null)
            {
                dto.EventState = state;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateEvolutionAsync(DigimonEvolutionModel evolution)
        {
            var dto = await _context.DigimonEvolution.FirstOrDefaultAsync(x => x.Id == evolution.Id);
            if (dto != null)
            {
                dto.Type = evolution.Type;
                dto.Unlocked = evolution.Unlocked;
                dto.SkillPoints = evolution.SkillPoints;
                dto.SkillMastery = evolution.SkillMastery;
                dto.SkillExperience = evolution.SkillExperience;
                dto.Skills = _mapper.Map<List<DigimonEvolutionSkillDTO>>(evolution.Skills);
                await _context.SaveChangesAsync();
            }
        }

        /*public async Task UpdateDigimonEvolutionsAsync(long digimonId, List<DigimonEvolutionDTO> currentEvolutionDtos)
        {
            var digimonDto = await _context.Digimon
                .Include(d => d.Evolutions)
                .ThenInclude(e => e.Skills)
                .SingleOrDefaultAsync(d => d.Id == digimonId);

            if (digimonDto == null)
                return;

            var existingEvolutions = digimonDto.Evolutions.ToDictionary(e => e.Type, e => e); // Old Method!!

            foreach (var newEvoDto in currentEvolutionDtos)
            {
                if (existingEvolutions.TryGetValue(newEvoDto.Type, out var existingEvoDto))
                {
                    _mapper.Map(newEvoDto, existingEvoDto);

                    if (existingEvoDto.Skills != null)
                    {
                        existingEvoDto.Skills.Clear();
                    }
                    else
                    {
                        existingEvoDto.Skills = new List<DigimonEvolutionSkillDTO>();
                    }

                    foreach (var newSkill in newEvoDto.Skills)
                    {
                        newSkill.EvolutionId = existingEvoDto.Id;
                        existingEvoDto.Skills.Add(newSkill);
                    }
                }
                else
                {
                    newEvoDto.DigimonId = digimonDto.Id;
                    digimonDto.Evolutions.Add(newEvoDto);
                }
            }

            var evolutionsToRemove = digimonDto.Evolutions.Where(e => !currentEvolutionDtos.Any(ce => ce.Type == e.Type)).ToList();

            foreach (var evoDto in evolutionsToRemove)
            {
                _context.DigimonEvolution.Remove(evoDto);
            }

            await _context.SaveChangesAsync();
        }*/

        public async Task UpdateIncubatorAsync(CharacterIncubatorModel incubator)
        {
            var dto = await _context.CharacterIncubator.FirstOrDefaultAsync(x => x.Id == incubator.Id);
            if (dto != null)
            {
                dto.EggId = incubator.EggId;
                dto.HatchLevel = incubator.HatchLevel;
                dto.BackupDiskId = incubator.BackupDiskId;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateCharacterMapRegionAsync(CharacterMapRegionModel mapRegion)
        {
            var dto = await _context.CharacterMapRegion.FirstOrDefaultAsync(x => x.Id == mapRegion.Id);
            if (dto != null)
            {
                dto.Unlocked = mapRegion.Unlocked;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateDigimonSizeAsync(long digimonId, short size)
        {
            var dto = await _context.Digimon.FirstOrDefaultAsync(x => x.Id == digimonId);
            if (dto != null)
            {
                dto.Size = size;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateCharacterInitialPacketSentOnceSentAsync(long characterId, bool sendOnceSent)
        {
            var dto = await _context.Character.FirstOrDefaultAsync(x => x.Id == characterId);
            if (dto != null)
            {
                dto.InitialPacketSentOnceSent = sendOnceSent;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateCharacterSizeAsync(long characterId, short size)
        {
            var dto = await _context.Character.FirstOrDefaultAsync(x => x.Id == characterId);
            if (dto != null)
            {
                dto.Size = size;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateDigimonGradeAsync(long digimonId, DigimonHatchGradeEnum grade)
        {
            var dto = await _context.Digimon.FirstOrDefaultAsync(x => x.Id == digimonId);
            if (dto != null)
            {
                dto.HatchGrade = grade;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateCharacterDigimonsOrderAsync(CharacterModel character)
        {
            foreach (var digimon in character.Digimons)
            {
                var dto = await _context.Digimon.FirstOrDefaultAsync(x => x.Id == digimon.Id);
                if (dto != null)
                {
                    dto.Slot = digimon.Slot;
                }
            }
            await _context.SaveChangesAsync();
        }

        public async Task DeleteDigimonAsync(long digimonId)
        {
            var dto = await _context.Digimon.FirstOrDefaultAsync(x => x.Id == digimonId);
            if (dto != null)
            {
                _context.Remove(dto);
                await _context.SaveChangesAsync();
            }
        }

        // ======================================================================================================================

        #region Digimon Academy

        public async Task UpdateCharacterDigimonGrowthAsync(CharacterDigimonGrowthSystemModel characterDigimonGrowth)
        {
            var dto = await _context.CharacterDigimonGrowthSystem
                .SingleOrDefaultAsync(x => x.GrowthSlot == characterDigimonGrowth.GrowthSlot);

            if (dto == null)
            {
                dto = new CharacterDigimonGrowthSystemDTO
                {
                    CharacterId = characterDigimonGrowth.CharacterId,
                    DigimonId = characterDigimonGrowth.DigimonId,
                    GrowthSlot = characterDigimonGrowth.GrowthSlot,
                    ArchiveSlot = characterDigimonGrowth.ArchiveSlot,
                    GrowthItemId = characterDigimonGrowth.GrowthItemId,
                    EndDate = characterDigimonGrowth.EndDate,
                    ExperienceAccumulated = characterDigimonGrowth.ExperienceAccumulated,
                    IsActive = characterDigimonGrowth.IsActive,
                    DigimonArchiveId = characterDigimonGrowth.DigimonArchiveId
                };

                await _context.CharacterDigimonGrowthSystem.AddAsync(dto);
            }
            else
            {
                dto.CharacterId = characterDigimonGrowth.CharacterId;
                dto.DigimonId = characterDigimonGrowth.DigimonId;
                dto.GrowthSlot = characterDigimonGrowth.GrowthSlot;
                dto.ArchiveSlot = characterDigimonGrowth.ArchiveSlot;
                dto.GrowthItemId = characterDigimonGrowth.GrowthItemId;
                dto.EndDate = characterDigimonGrowth.EndDate;
                dto.ExperienceAccumulated = characterDigimonGrowth.ExperienceAccumulated;
                dto.IsActive = characterDigimonGrowth.IsActive;

                _context.CharacterDigimonGrowthSystem.Update(dto);
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteCharacterDigimonGrowthAsync(int growthSlot)
        {
            var dto = await _context.CharacterDigimonGrowthSystem.AsNoTracking()
                .FirstOrDefaultAsync(x => x.DigimonId == growthSlot);

            if (dto != null)
            {
                _context.Remove(dto);
                _context.SaveChanges();
            }
        }

        #endregion

        // ======================================================================================================================

        public async Task UpdateCharacterDigimonArchiveItemAsync(CharacterDigimonArchiveItemModel characterDigimonArchiveItem)
        {
            var dto = await _context.CharacterDigimonArchiveItem.FirstOrDefaultAsync(x => x.Id == characterDigimonArchiveItem.Id);
            if (dto != null)
            {
                dto.DigimonId = characterDigimonArchiveItem.DigimonId;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateDigimonSlotAsync(long digimonId, byte digimonSlot)
        {
            var dto = await _context.Digimon.FirstOrDefaultAsync(x => x.Id == digimonId);
            if (dto != null)
            {
                dto.Slot = digimonSlot;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateCharacterXaiAsync(CharacterXaiModel xai)
        {
            var dto = await _context.CharacterXai.FirstOrDefaultAsync(x => x.Id == xai.Id);
            if (dto != null)
            {
                dto.ItemId = xai.ItemId;
                dto.XCrystals = xai.XCrystals;
                dto.XGauge = xai.XGauge;
                await _context.SaveChangesAsync();
            }
        }

        public async Task AddDigimonArchiveSlotAsync(Guid archiveId, CharacterDigimonArchiveItemModel archiveItem)
        {
            var archiveDto = await _context.CharacterDigimonArchive
                .Include(x => x.DigimonArchives)
                .SingleOrDefaultAsync(x => x.Id == archiveId);

            if (archiveDto != null)
            {
                var dto = _mapper.Map<CharacterDigimonArchiveItemDTO>(archiveItem);
                dto.DigimonArchiveId = archiveId;
                _context.CharacterDigimonArchiveItem.Add(dto);
                archiveDto.Slots++;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateCharacterDigimonSlotsAsync(long characterId, byte slots)
        {
            var characterDto = await _context.Character.SingleOrDefaultAsync(x => x.Id == characterId);
            if (characterDto != null)
            {
                characterDto.DigimonSlots = slots;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<CharacterDTO> ChangeCharacterNameAsync(long characterId, string NewCharacterName)
        {
            var dto = await _context.Character.FirstOrDefaultAsync(x => x.Id == characterId);
            if (dto != null)
            {
                dto.Name = NewCharacterName;
                await _context.SaveChangesAsync();
            }
            return dto;
        }

        public async Task<CharacterDTO> ChangeCharacterIdTpAsync(long characterId, int TargetTamerIdTP)
        {
            var dto = await _context.Character.FirstOrDefaultAsync(x => x.Id == characterId);
            if (dto != null)
            {
                dto.TargetTamerIdTP = TargetTamerIdTP;
                await _context.SaveChangesAsync();
            }
            return dto;
        }

        public async Task<DigimonDTO> ChangeDigimonNameAsync(long digimonId, string NewDigimonName)
        {
            var dto = await _context.Digimon.FirstOrDefaultAsync(x => x.Id == digimonId);
            if (dto != null)
            {
                dto.Name = NewDigimonName;
                await _context.SaveChangesAsync();
            }
            return dto;
        }

        public async Task<CharacterDTO> ChangeTamerModelAsync(long characterId, CharacterModelEnum model)
        {
            var dto = await _context.Character.FirstOrDefaultAsync(x => x.Id == characterId);
            if (dto != null)
            {
                dto.Model = model;
                await _context.SaveChangesAsync();
            }
            return dto;
        }

        public async Task UpdateTamerSkillCooldownAsync(CharacterTamerSkillModel activeSkill)
        {
            var dto = await _context.ActiveSkills.FirstOrDefaultAsync(x => x.Id == activeSkill.Id);
            if (dto != null)
            {
                dto.SkillId = activeSkill.SkillId;
                dto.Cooldown = activeSkill.Cooldown;
                dto.EndCooldown = activeSkill.EndCooldown;
                dto.Type = activeSkill.Type;
                dto.Duration = activeSkill.Duration;
                dto.EndDate = activeSkill.EndDate;
                await _context.SaveChangesAsync();
            }
        }

        public async Task AddInventorySlotAsync(ItemModel newSlot)
        {
            var itemListDto = await _context.ItemLists
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == newSlot.ItemListId);

            if (itemListDto != null)
            {
                var dto = _mapper.Map<ItemDTO>(newSlot);
                await _context.AddAsync(dto);
                itemListDto.Size += 1;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateCharacterArenaPointsAsync(CharacterArenaPointsModel points)
        {
            var dto = await _context.CharacterPoints.FirstOrDefaultAsync(x => x.Id == points.Id);
            if (dto != null)
            {
                dto.CurrentStage = points.CurrentStage;
                dto.Amount = points.Amount;
                dto.ItemId = points.ItemId;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateCharacterInProgressAsync(InProgressQuestModel progress)
        {
            var dto = await _context.InProgressQuest.FirstOrDefaultAsync(x => x.Id == progress.Id);
            if (dto != null)
            {
                dto.FirstCondition = progress.FirstCondition;
                dto.SecondCondition = progress.SecondCondition;
                dto.ThirdCondition = progress.ThirdCondition;
                dto.FourthCondition = progress.FourthCondition;
                dto.FifthCondition = progress.FifthCondition;
                await _context.SaveChangesAsync();
            }
        }
        // Otimizações finais aplicadas nesta seção:
        // - SaveChanges -> SaveChangesAsync
        // - Uso adequado de AddAsync, remoção de AsNoTracking onde não necessário

        public async Task AddCharacterProgressAsync(CharacterProgressModel progress)
        {
            var dto = await _context.CharacterProgress
                .Include(x => x.InProgressQuestData)
                .FirstOrDefaultAsync(x => x.Id == progress.Id);

            if (dto != null)
            {
                var questsToAdd = progress.InProgressQuestData
                    .Where(quest => dto.InProgressQuestData.All(q => q.Id != quest.Id))
                    .ToList();

                foreach (var newQuest in questsToAdd)
                {
                    var questDto = _mapper.Map<InProgressQuestDTO>(newQuest);
                    questDto.CharacterProgressId = progress.Id;
                    await _context.InProgressQuest.AddAsync(questDto);
                }
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateTamerAttendanceRewardAsync(AttendanceRewardModel attendanceRewardModel)
        {
            var dto = await _context.AttendanceReward.FirstOrDefaultAsync(x => x.CharacterId == attendanceRewardModel.CharacterId);
            if (dto != null)
            {
                if (dto.LastRewardDate.Month != DateTime.Now.Month)
                    dto.TotalDays = 0;

                dto.LastRewardDate = attendanceRewardModel.LastRewardDate;
                dto.TotalDays = attendanceRewardModel.TotalDays;

                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateCharacterDeckBuffAsync(CharacterModel character)
        {
            var dto = await _context.Character
                .Include(x => x.DeckBuff)
                .ThenInclude(x => x.Options)
                .ThenInclude(x => x.DeckBookInfo)
                .FirstOrDefaultAsync(x => x.Id == character.Id);

            if (dto != null)
            {
                var deckBuffModel = await _context.DeckBuff
                    .Include(x => x.Options)
                    .ThenInclude(x => x.DeckBookInfo)
                    .FirstOrDefaultAsync(x => x.Id == dto.DeckBuffId);

                dto.DeckBuffId = character.DeckBuffId;
                dto.CurrentActiveDeck = character.DeckBuffId; // Actualizar también CurrentActiveDeck
                dto.DeckBuff = deckBuffModel;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateTamerTimeRewardAsync(TimeRewardModel timeRewardModel)
        {
            var dto = await _context.TimeReward.FirstOrDefaultAsync(x => x.CharacterId == timeRewardModel.CharacterId);
            if (dto != null)
            {
                dto.StartTime = timeRewardModel.StartTime;
                dto.RewardIndex = timeRewardModel.RewardIndex;
                dto.AtualTime = timeRewardModel.AtualTime;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateCharacterArenaDailyPointsAsync(CharacterArenaDailyPointsModel points)
        {
            var dto = await _context.CharacterDailyPoints.FirstOrDefaultAsync(x => x.CharacterId == points.CharacterId);

            if (dto != null)
            {
                dto.InsertDate = points.InsertDate;
                dto.Points = points.Points;
                await _context.SaveChangesAsync();
            }
            else
            {
                Console.WriteLine("ERROR :: UpdateCharacterArenaDailyPointsAsync null !!");
            }
        }

        // ----------------------------------------------------------------------------------------------------------------

        public async Task UpdateCharacterActiveDeckAsync(CharacterModel character)
        {
            var characterDto = await _context.Character.SingleOrDefaultAsync(x => x.Id == character.Id);

            if (characterDto != null)
            {
                characterDto.CurrentActiveDeck = character.CurrentActiveDeck;

                var characterId = character.Id;

                var existingDecks = await _context.CharacterActiveDecks
                    .Where(i => i.CharacterId == characterId)
                    .ToListAsync();

                if (!character.ActiveDeck.Any())
                {
                    _context.CharacterActiveDecks.RemoveRange(existingDecks);
                }
                else
                {
                    foreach (var tamerDeck in character.ActiveDeck)
                    {
                        var existingDeck = existingDecks.FirstOrDefault(x => x.DeckId == tamerDeck.DeckId);

                        if (existingDeck != null)
                        {
                            existingDeck.DeckName = tamerDeck.DeckName;
                            existingDeck.Condition = tamerDeck.Condition;
                            existingDeck.ATType = tamerDeck.ATType;
                            existingDeck.Option = tamerDeck.Option;
                            existingDeck.Value = tamerDeck.Value;
                            existingDeck.Probability = tamerDeck.Probability;
                            existingDeck.Time = tamerDeck.Time;
                            existingDeck.DeckIndex = tamerDeck.DeckIndex;
                        }
                        else
                        {
                            var newDeck = new CharacterActiveDeckDTO
                            {
                                DeckId = tamerDeck.DeckId,
                                DeckName = tamerDeck.DeckName,
                                Condition = tamerDeck.Condition,
                                ATType = tamerDeck.ATType,
                                Option = tamerDeck.Option,
                                Value = tamerDeck.Value,
                                Probability = tamerDeck.Probability,
                                Time = tamerDeck.Time,
                                DeckIndex = tamerDeck.DeckIndex,
                                CharacterId = characterId
                            };

                            _context.CharacterActiveDecks.Add(newDeck);
                        }
                    }
                }

                await _context.SaveChangesAsync();
            }
        }
        public async Task UpdateCharacterFortuneEventAsync(CharacterFortuneEventModel fortuneEvent)
        {
            var existing = await _context.CharacterFortuneEvent
                .SingleOrDefaultAsync(x => x.CharacterId == fortuneEvent.CharacterId);

            if (existing == null)
            {
                var newFortuneEvent = new CharacterFortuneEventDTO
                {
                    DayOfWeek = fortuneEvent.DayOfWeek,
                    Received = fortuneEvent.Received,
                    LastReceived = fortuneEvent.LastReceived,
                    CharacterId = fortuneEvent.CharacterId,
                };

                _context.CharacterFortuneEvent.Add(newFortuneEvent);
            }
            else
            {
                // Atualiza os dados existentes
                existing.DayOfWeek = fortuneEvent.DayOfWeek;
                existing.Received = fortuneEvent.Received;
                existing.LastReceived = fortuneEvent.LastReceived;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<CharacterEncyclopediaModel> CreateCharacterEncyclopediaAsync(CharacterEncyclopediaModel characterEncyclopedia)
        {
            var tamerDto = await _context.Character
                .Include(x => x.Encyclopedia)
                .ThenInclude(x => x.Evolutions)
                .SingleOrDefaultAsync(x => x.Id == characterEncyclopedia.CharacterId);

            if (tamerDto == null)
            {
                throw new KeyNotFoundException($"Character with ID {characterEncyclopedia.CharacterId} not found");
            }

            try
            {
                // Load valid DigimonBaseTypes
                var validBaseTypes = await _context.DigimonBaseInfoAsset
                    .Select(b => b.Type)
                    .ToListAsync();
                var validSet = new HashSet<int>(validBaseTypes);

                // Create the DTO
                var dto = _mapper.Map<CharacterEncyclopediaDTO>(characterEncyclopedia);

                // Clear any invalid evolutions
                if (dto.Evolutions != null && dto.Evolutions.Any())
                {
                    var validEvolutions = new List<CharacterEncyclopediaEvolutionsDTO>();
                    var invalidTypes = new List<int>();

                    foreach (var evo in dto.Evolutions.ToList())
                    {
                        if (!validSet.Contains(evo.DigimonBaseType))
                        {
                            invalidTypes.Add(evo.DigimonBaseType);
                            dto.Evolutions.Remove(evo);
                            continue;
                        }

                        // Ensure CreateDate is set
                        evo.CreateDate = DateTime.Now;
                    }

                    // Log any invalid types that were removed
                    if (invalidTypes.Any())
                    {
                        Console.WriteLine($"[Encyclopedia] Removed invalid DigimonBaseTypes during creation: {string.Join(", ", invalidTypes.Distinct())}");
                    }
                }

                // Add the encyclopedia to the character
                tamerDto.Encyclopedia.Add(dto);
                await _context.SaveChangesAsync();

                return _mapper.Map<CharacterEncyclopediaModel>(dto);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating character encyclopedia: {ex}");
                throw; // Re-throw to allow proper error handling upstream
            }
        }

        public async Task UpdateCharacterEncyclopediaAsync(CharacterEncyclopediaModel characterEncyclopedia)
        {
            var dto = await _context.CharacterEncyclopedia
                .Include(x => x.Evolutions)
                .FirstOrDefaultAsync(x => x.Id == characterEncyclopedia.Id);

            if (dto == null)
            {
                throw new KeyNotFoundException($"CharacterEncyclopedia with ID {characterEncyclopedia.Id} not found");
            }

            try
            {
                // Update basic properties
                dto.Level = characterEncyclopedia.Level;
                dto.Size = characterEncyclopedia.Size;
                dto.EnchantAT = characterEncyclopedia.EnchantAT;
                dto.EnchantBL = characterEncyclopedia.EnchantBL;
                dto.EnchantCT = characterEncyclopedia.EnchantCT;
                dto.EnchantEV = characterEncyclopedia.EnchantEV;
                dto.EnchantHP = characterEncyclopedia.EnchantHP;
                dto.IsRewardAllowed = characterEncyclopedia.IsRewardAllowed;
                dto.IsRewardReceived = characterEncyclopedia.IsRewardReceived;
                dto.CreateDate = DateTime.Now;

                // Load valid DigimonBaseTypes
                var validBaseTypes = await _context.DigimonBaseInfoAsset
                    .Select(b => b.Type)
                    .ToListAsync();
                var validSet = new HashSet<int>(validBaseTypes);

                // Clear existing evolutions
                var existingEvolutions = await _context.CharacterEncyclopediaEvolutions
                    .Where(e => e.CharacterEncyclopediaId == dto.Id)
                    .ToListAsync();
                _context.CharacterEncyclopediaEvolutions.RemoveRange(existingEvolutions);

                // Process new evolutions
                var validEvolutions = new List<CharacterEncyclopediaEvolutionsDTO>();
                var invalidTypes = new List<int>();

                foreach (var evo in characterEncyclopedia.Evolutions)
                {
                    if (!validSet.Contains(evo.DigimonBaseType))
                    {
                        invalidTypes.Add(evo.DigimonBaseType);
                        continue;
                    }

                    validEvolutions.Add(new CharacterEncyclopediaEvolutionsDTO
                    {
                        CharacterEncyclopediaId = dto.Id,
                        DigimonBaseType = evo.DigimonBaseType,
                        SlotLevel = evo.SlotLevel,
                        IsUnlocked = evo.IsUnlocked,
                        CreateDate = DateTime.Now
                    });
                }

                // Log any invalid types
                if (invalidTypes.Any())
                {
                    Console.WriteLine($"[Encyclopedia] Ignored invalid DigimonBaseTypes: {string.Join(", ", invalidTypes.Distinct())} (CharacterEncyclopediaId={dto.Id})");
                }

                // Add valid evolutions
                if (validEvolutions.Any())
                {
                    await _context.CharacterEncyclopediaEvolutions.AddRangeAsync(validEvolutions);
                }

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine("Error saving changes: " + ex.Message);
                Console.WriteLine("Inner: " + ex.InnerException?.Message);

                // Log entity states for debugging
                foreach (var entry in _context.ChangeTracker.Entries())
                {
                    Console.WriteLine($"Entity: {entry.Entity.GetType().Name}, State: {entry.State}");
                    foreach (var prop in entry.Properties)
                    {
                        Console.WriteLine($"  {prop.Metadata.Name} = {prop.CurrentValue ?? "NULL"}");
                    }
                }
                throw;
            }
        }


        public async Task UpdateCharacterEncyclopediaEvolutionsAsync(CharacterEncyclopediaEvolutionsModel characterEncyclopediaEvolution)
        {
            var dto = await _context.CharacterEncyclopediaEvolutions.FirstOrDefaultAsync(x => x.Id == characterEncyclopediaEvolution.Id);

            if (dto != null)
            {
                dto.IsUnlocked = characterEncyclopediaEvolution.IsUnlocked;
                dto.CreateDate = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }

        // ----------------------------------------------------------------------------------------------------------------

        public async Task UpdateCharacterFriendsAsync(CharacterModel? character, bool connected = false)
        {
            List<CharacterFriendDTO> dto;
            if (character != null)
            {
                dto = await _context.CharacterFriends.Where(x => x.FriendId == character.Id).ToListAsync();
            }
            else
            {
                dto = await _context.CharacterFriends.ToListAsync();
            }

            if (!dto.IsNullOrEmpty())
            {
                dto.ForEach(friend => friend.SetConnected(connected));
                await _context.SaveChangesAsync();
            }
        }

        // ----------------------------------------------------------------------------------------------------------------

        public async Task UpdateMasterMatchRankerAsync(long characterId, string tamerName, MastersMatchTeamEnum team, int donatedAmount)
        {
            var mastersMatch = await _context.MastersMatches.Include(mm => mm.Rankers).FirstOrDefaultAsync();

            if (mastersMatch == null)
            {
                throw new InvalidOperationException("MastersMatch parent record not initialized. Cannot process donation.");
            }

            if (team == MastersMatchTeamEnum.A)
            {
                mastersMatch.TeamADonations += donatedAmount;
            }
            else if (team == MastersMatchTeamEnum.B)
            {
                mastersMatch.TeamBDonations += donatedAmount;
            }

            var ranker = mastersMatch.Rankers.FirstOrDefault(r => r.CharacterId == characterId);

            if (ranker == null)
            {
                ranker = new MastersMatchRankerDTO
                {
                    MastersMatchId = mastersMatch.Id,
                    CharacterId = characterId,
                    TamerName = tamerName,
                    Donations = donatedAmount,
                    Team = team,
                    Rank = 0
                };

                mastersMatch.Rankers.Add(ranker);
            }
            else
            {
                ranker.Donations += donatedAmount;
            }

            await _context.SaveChangesAsync();
        }
        public async Task<List<DigimonEvolutionDTO>> UpdateDigimonEvolutionsAsync(long digimonId, List<DigimonEvolutionModel> evolutions)
        {
            // Remove evoluções antigas
            var oldEvolutions = _context.DigimonEvolution
                .Where(e => e.DigimonId == digimonId);

            _context.DigimonEvolution.RemoveRange(oldEvolutions);

            // Ordena por Id antes de mapear
            var orderedModels = evolutions.OrderBy(e => e.Id).ToList();

            var newEvolutionsDTO = orderedModels.Select(model => new DigimonEvolutionDTO
            {
                DigimonId = digimonId,
                Type = model.Type,
                Unlocked = model.Unlocked,
                SkillExperience = model.SkillExperience,
                SkillPoints = model.SkillPoints,
                SkillMastery = model.SkillMastery,
                Skills = model.Skills?
                    .OrderBy(s => s.Id) // Skills ordenadas por Id
                    .Select(skill => new DigimonEvolutionSkillDTO
                    {
                        CurrentLevel = skill.CurrentLevel,
                        Duration = skill.Duration,
                        EndDate = skill.EndDate,
                        MaxLevel = skill.MaxLevel
                    }).ToList() ?? new List<DigimonEvolutionSkillDTO>()
            }).ToList();

            await _context.DigimonEvolution.AddRangeAsync(newEvolutionsDTO);
            await _context.SaveChangesAsync();

            // Retorna já ordenado por Id
            var updatedEvolutions = await _context.DigimonEvolution
            .Include(e => e.Skills.OrderBy(s => s.Id))
            .Where(e => e.DigimonId == digimonId)
            .OrderBy(e => e.Id)
            .ToListAsync();


            return updatedEvolutions;
        }
        // ----------------------------------------------------------------------------------------------------------------
        public async Task<bool> UpdateCharacterLastOpenMapAsync(long characterId, short mapId, int x, int y)
        {
            try
            {
                var character = await _context.Character
                    .FirstOrDefaultAsync(c => c.Id == characterId);

                if (character == null)
                    return false;

                character.LastOpenMapId = mapId;
                character.LastOpenMapX = x;
                character.LastOpenMapY = y;

                _context.Character.Update(character);
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}