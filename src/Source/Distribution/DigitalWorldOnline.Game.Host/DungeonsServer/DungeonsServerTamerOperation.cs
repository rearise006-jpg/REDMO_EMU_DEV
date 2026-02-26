using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Logger;
using DigitalWorldOnline.Commons.Models.Assets.XML.MapObject;
using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Models.Config;
using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Models.Map;
using DigitalWorldOnline.Commons.Models.Summon;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Packets.GameServer.Combat;
using DigitalWorldOnline.Commons.Packets.Items;
using DigitalWorldOnline.Commons.Packets.MapServer;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Game.Managers;
using System;
using System.Diagnostics;
using System.Text;

namespace DigitalWorldOnline.GameHost
{
    public sealed partial class DungeonsServer
    {
        public void TamerOperation(GameMap map)
        {
            if (!map.ConnectedTamers.Any())
            {
                map.SetNoTamers();
                return;
            }

            var sw = Stopwatch.StartNew();

            foreach (var tamer in map.ConnectedTamers)
            {
                var client = map.Clients.FirstOrDefault(x => x.TamerId == tamer.Id);
                if (client?.IsConnected != true || client.Partner == null)
                    continue;

                DungeonDoors(map);
                ProcessDebuffs(client);
                ProcessMap1702Debuff(map, tamer, client); // Add this line to process the Map 1702 debuff
                ProcessVisibility(map, tamer, client);
                ProcessAttacks(tamer, client);
                CheckTimeReward(client);
                ProcessRegen(map, tamer, client);
                ProcessExpiredItems(client, tamer);
                ProcessBuffs(map, tamer, client);
                ProcessSyncResources(map, tamer, client);
                ProcessSaveResources(tamer);
                ProcessDailyQuestReset(client, tamer);
            }

            sw.Stop();
            if (sw.ElapsedMilliseconds >= 1000)
                Console.WriteLine(
                    $"TamersOperation ({map.ConnectedTamers.Count}): {sw.Elapsed.TotalMilliseconds}ms"
                );
        }

        // -- Objects Verification (Doors)
        // Dictionary to track door states across different maps
        private Dictionary<int, (int currentOpenDoorId, int deadMobsCount)> _doorStates = new Dictionary<int, (int, int)>();

        private void DungeonDoors(GameMap map)
        {
            if (map == null)
            {
                _logger.Error("DungeonDoors called with null map");
                return;
            }

            var mapObject = _assets?.MapObjects?.FirstOrDefault(mo => mo != null && mo.MapId == map.MapId);

            if (mapObject == null)
            {
                return;
            }

            // Process the standard door logic for non-special doors
            foreach (var mapSourceObject in mapObject.MapSourceObjects ?? new List<MapSourceObjectModel>())
            {
                if (mapSourceObject == null) continue;

                var factorId = mapSourceObject.ObjectId;

                foreach (var orderObjects in mapSourceObject.OrderObjects ?? new List<OrderObjectModel>())
                {
                    if (orderObjects == null) continue;

                    foreach (var objects in orderObjects.Objects ?? new List<ObjectModel>())
                    {
                        if (objects == null) continue;

                        var mobType = objects.Factor;
                        var mob = map.Mobs?.FirstOrDefault(x => x != null && x.Type == mobType);

                        if (mob != null)
                        {
                            byte doorState = mob.Alive ? (byte)0 : (byte)1;
                            map.BroadcastForMap(new DoorObjectOpenPacket(factorId, doorState).Serialize());
                        }
                    }
                }
            }

            // Special dungeon progression logic for different maps
            switch (map.MapId)
            {
                case 1701:
                    ProcessMap1701Doors(map);
                    break;
                case 1702:
                    ProcessMap1702Doors(map);
                    break;
            }
        }

        private void ProcessMap1701Doors(GameMap map)
        {
            // Special dungeon progression logic for map 1701
            var Ulforce = map.Mobs?.FirstOrDefault(x => x != null && x.Type == 51132); // Door id = 10071
            var LordKnightmon = map.Mobs?.FirstOrDefault(x => x != null && x.Type == 51120); // Door id = 10067
            var Craniamon = map.Mobs?.FirstOrDefault(x => x != null && x.Type == 51144); // Door id = 10070
            var Examon = map.Mobs?.FirstOrDefault(x => x != null && x.Type == 51141); // Door id = 10066
            var Sleipmon = map.Mobs?.FirstOrDefault(x => x != null && x.Type == 51126); // Door id = 10061
            var Dynasmon = map.Mobs?.FirstOrDefault(x => x != null && x.Type == 51185); // Door id = 10063 after kill open door id = 10074

            // Dictionary to track the status of mobs and their associated doors
            var mobDoors = new Dictionary<string, (int doorId, bool isDead, int mobType)>
            {
                { "LordKnightmon", (10067, LordKnightmon == null, 51120) },
                { "Craniamon", (10070, Craniamon == null, 51144) },
                { "Examon", (10066, Examon == null, 51141) },
                { "Sleipmon", (10061, Sleipmon == null, 51126) },
                { "Dynasmon", (10063, Dynasmon == null, 51185) }
            };

            // Ensure _doorStates is initialized
            if (_doorStates == null)
            {
                _doorStates = new Dictionary<int, (int, int)>();
            }

            // Initialize state storage if needed
            if (!_doorStates.ContainsKey(map.MapId))
            {
                _doorStates[map.MapId] = (0, 0);
            }

            var currentState = _doorStates[map.MapId];
            int currentOpenDoorId = currentState.currentOpenDoorId;
            int deadMobsCount = currentState.deadMobsCount;

            // Always keep Ulforce door open until Ulforce is dead
            if (Ulforce != null)
            {
                map.BroadcastForMap(new DoorObjectOpenPacket(10071, 1).Serialize());
            }
            else
            {
                // Close Ulforce door when Ulforce is dead
                map.BroadcastForMap(new DoorObjectOpenPacket(10071, 0).Serialize());

                // Count how many mobs are dead
                int currentDeadMobsCount = mobDoors.Count(pair => pair.Value.isDead);

                // If the dead count has changed, update our tracking
                if (currentDeadMobsCount != deadMobsCount)
                {
                    deadMobsCount = currentDeadMobsCount;
                    _doorStates[map.MapId] = (0, deadMobsCount); // Reset current door when mob count changes
                    currentOpenDoorId = 0;
                }

                // If we have at least one dead boss but less than 3
                if (deadMobsCount < 3)
                {
                    // If we don't have a door open, pick one randomly
                    if (currentOpenDoorId == 0)
                    {
                        // First, close all doors
                        foreach (var door in mobDoors.Values)
                        {
                            map.BroadcastForMap(new DoorObjectOpenPacket(door.doorId, 0).Serialize());
                        }

                        // Randomly select a door to open among the doors with alive mobs
                        var availableDoors = mobDoors.Where(pair => !pair.Value.isDead).ToList();
                        if (availableDoors.Any())
                        {
                            var random = new Random();
                            var selectedDoor = availableDoors[random.Next(availableDoors.Count)];
                            currentOpenDoorId = selectedDoor.Value.doorId;
                            _doorStates[map.MapId] = (currentOpenDoorId, deadMobsCount);

                            // Open the selected door
                            map.BroadcastForMap(new DoorObjectOpenPacket(currentOpenDoorId, 1).Serialize());
                        }
                    }
                    else
                    {
                        // Check if the mob behind the current open door is still alive
                        var currentDoorMobInfo = mobDoors.FirstOrDefault(d => d.Value.doorId == currentOpenDoorId);

                        // If we found the door and its mob is now dead, we need to pick a new door
                        if (!string.IsNullOrEmpty(currentDoorMobInfo.Key) && currentDoorMobInfo.Value.isDead)
                        {
                            // Close the current door since the mob is dead
                            map.BroadcastForMap(new DoorObjectOpenPacket(currentOpenDoorId, 0).Serialize());

                            // Reset to pick a new door next time
                            _doorStates[map.MapId] = (0, deadMobsCount);
                        }
                        else
                        {
                            // Keep the current door open
                            map.BroadcastForMap(new DoorObjectOpenPacket(currentOpenDoorId, 1).Serialize());
                        }
                    }
                }
                // If 3 or more mobs are dead, open the final door
                else if (deadMobsCount >= 3)
                {
                    // Close all other doors first
                    foreach (var door in mobDoors.Values)
                    {
                        map.BroadcastForMap(new DoorObjectOpenPacket(door.doorId, 0).Serialize());
                    }

                    // Open the final door
                    map.BroadcastForMap(new DoorObjectOpenPacket(10074, 1).Serialize());
                }
            }
        }

        private void ProcessMap1702Doors(GameMap map)
        {
            // For map 1702, check if the mobs with types 51134 and 51212 exist in the map
            var mobType51134Exists = map.Mobs?.Any(x => x != null && x.Type == 51134) ?? false;
            var mobType51212Exists = map.Mobs?.Any(x => x != null && x.Type == 51212) ?? false;

            // Door states: 0 = closed, 1 = open
            byte doorState51134 = mobType51134Exists ? (byte)0 : (byte)1; // Door opens when mob doesn't exist
            byte doorState51212 = mobType51212Exists ? (byte)0 : (byte)1; // Door opens when mob doesn't exist

            // Control the doors related to mob type 51134
            map.BroadcastForMap(new DoorObjectOpenPacket(10067, doorState51134).Serialize());
            map.BroadcastForMap(new DoorObjectOpenPacket(10068, doorState51134).Serialize());

            // Control the doors related to mob type 51212
            map.BroadcastForMap(new DoorObjectOpenPacket(10071, doorState51212).Serialize());
            map.BroadcastForMap(new DoorObjectOpenPacket(10073, doorState51212).Serialize());
        }


        // 1) Debuffs e afins
        private void ProcessDebuffs(GameClient client)
        {
            CheckLocationDebuff(client);
            // MapBuff(client);
            // client.Send(new LoadInventoryPacket(client.Tamer.Inventory, InventoryTypeEnum.Inventory));
        }

        // 2) Visibilidade de mobs, tamer e shop
        private void ProcessVisibility(GameMap map, CharacterModel tamer, GameClient client)
        {
            GetInViewMobs(map, tamer);
            GetInViewMobs(map, tamer, true);
            ShowOrHideTamer(map, tamer);
        }


        // 3) Ataques automáticos
        const int combatTimeoutSeconds = 10;
        private void ProcessAttacks(CharacterModel tamer, GameClient client)
        {
            if (client.Tamer.InBattle && (DateTime.Now - client.Tamer.LastCombatInteractionTime).TotalSeconds > combatTimeoutSeconds)
            {
                // Verifica se realmente não há mais mobs atacando o Tamer
                if (!IsMobsAttacking(tamer.Id, false) && !IsMobsAttacking(tamer.Id, true))
                {
                    //_logger.Information($"Saindo da batalha !!");

                    client.Tamer.StopBattle();
                    client.Partner.StopAutoAttack();

                    client.Send(new SetCombatOffPacket(client.Partner.GeneralHandler).Serialize());
                }
            }

            if (tamer.TargetIMobs.Count > 0)
                PartnerAutoAttackMob(client);

            if (tamer.TargetPartner != null)
            {
                tamer.StopBattle();
                tamer.Partner?.StopAutoAttack();
            }
        }

        // simple regen process
    

        private void ProcessRegen(GameMap map, CharacterModel tamer, GameClient client)
        {
            // Regeneración normal del tamer
            tamer.AutoRegen();
            tamer.ActiveEvolutionReduction();

            // Enviar XAI si corresponde
            if (tamer.HasXai)
            {
                client.Send(new XaiInfoPacket(tamer.Xai));
                client.Send(new TamerXaiResourcesPacket(tamer.XGauge, tamer.XCrystals));
            }
        }

        // Dictionary to track partner debuff timers for map 1702
        private Dictionary<long, DateTime> _map1702DebuffTimers = new Dictionary<long, DateTime>();

        // Constants for Map 1702 debuff
        private const int MAP_1702_DEBUFF_BUFF_ID = 64000;
        private const int MAP_1702_DEBUFF_SKILL_ID = 64000;
        private const int MAP_1702_MOB_TYPE_FOR_DEBUFF_REMOVAL = 51211;
        private const int MAP_1702_DEBUFF_REMOVAL_SECONDS = 30;
        private const int MAP_1702_DAMAGE_INTERVAL_SECONDS = 10;
        private const int MAP_1702_DAMAGE_AMOUNT = 100;

        // Dictionary to track last damage application time for each partner in map 1702
        private Dictionary<long, DateTime> _lastDamageTimeByTamer = new Dictionary<long, DateTime>();

        // Add this to ProcessDebuffs or create a new method call in TamerOperation
        private void ProcessMap1702Debuff(GameMap map, CharacterModel tamer, GameClient client)
        {
            // Null checking for all parameters
            if (map == null || tamer == null || client == null)
            {
                _logger.Error("ProcessMap1702Debuff called with null parameters");
                return;
            }

            // Only process for map 1702
            if (map.MapId != 1702)
                return;

            // Check if tamer's partner exists
            if (tamer.Partner == null)
            {
                _logger.Warning($"ProcessMap1702Debuff: Tamer {tamer.Id} has no partner");
                return;
            }

            // Check if debuff list exists
            if (tamer.Partner.DebuffList == null)
            {
                _logger.Warning($"ProcessMap1702Debuff: Partner of Tamer {tamer.Id} has no DebuffList");
                return;
            }

            // Get current time
            var currentTime = DateTime.Now;

            try
            {
                // Check if partner is still immune from debuff due to recent kill
                if (_map1702DebuffTimers.TryGetValue(tamer.Id, out var immuneUntil) && currentTime < immuneUntil)
                {
                    // Partner is immune, make sure debuff is removed
                    var existingDebuff = tamer.Partner.DebuffList.ActiveBuffs?.FirstOrDefault(x => x != null && x.BuffId == MAP_1702_DEBUFF_BUFF_ID);
                    if (existingDebuff != null)
                    {
                        // Remove the debuff if it exists
                        tamer.Partner.DebuffList.Remove(existingDebuff.BuffId);
                        map.BroadcastForTamerViewsAndSelf(tamer.Id,
                            new RemoveBuffPacket(tamer.Partner.GeneralHandler, existingDebuff.BuffId).Serialize());

                        // Send a message to the client that they're temporarily immune
                        client.Send(new SystemMessagePacket($"Temporary immunity from the dungeon's curse: {(int)(immuneUntil - currentTime).TotalSeconds}s remaining"));
                    }
                    return;
                }

                // Check if the partner already has the debuff
                var debuff = tamer.Partner.DebuffList.ActiveBuffs?.FirstOrDefault(x => x != null && x.BuffId == MAP_1702_DEBUFF_BUFF_ID);

                if (debuff == null)
                {
                    // Partner doesn't have the debuff, apply it
                    var buffInfo = _assets?.BuffInfo?.FirstOrDefault(x => x != null && x.BuffId == MAP_1702_DEBUFF_BUFF_ID);
                    if (buffInfo != null)
                    {
                        try
                        {
                            // Create new debuff
                            var newDebuff = DigimonDebuffModel.Create(
                                buffId: MAP_1702_DEBUFF_BUFF_ID,
                                skillId: MAP_1702_DEBUFF_SKILL_ID,
                                TypeN: 0,
                                duration: 0); // Permanent until removed

                            if (newDebuff != null)
                            {
                                newDebuff.SetBuffInfo(buffInfo);
                                tamer.Partner.DebuffList.Buffs.Add(newDebuff);

                                // Broadcast the debuff to the player and others
                                map.BroadcastForTamerViewsAndSelf(
                                    tamer.Id,
                                    new AddBuffPacket(
                                        tamer.Partner.GeneralHandler,
                                        buffInfo,
                                        0,
                                        0xFFFFFFFF).Serialize()); // Infinite duration

                                // Send message about the debuff
                                client.Send(new SystemMessagePacket("Your partner has been affected by the dungeon's curse! Kill special enemies to temporarily remove it."));
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"Error applying debuff in map 1702: {ex.Message}");
                        }
                    }
                }
                else
                {
                    // Apply periodic damage if the debuff is active
                    if (!_lastDamageTimeByTamer.TryGetValue(tamer.Id, out var lastDamageTime) ||
                        (currentTime - lastDamageTime).TotalSeconds >= MAP_1702_DAMAGE_INTERVAL_SECONDS)
                    {
                        // Apply damage every 10 seconds
                        int damage = MAP_1702_DAMAGE_AMOUNT;

                        // Ensure partner has current HP
                        if (tamer.Partner.CurrentHp <= 1)
                        {
                            _logger.Warning($"Partner of Tamer {tamer.Id} has very low HP ({tamer.Partner.CurrentHp}), skipping damage application");
                            return;
                        }

                        // Apply damage to partner (ensure it doesn't kill them)
                        int finalDamage = Math.Min(damage, Math.Max(1, tamer.Partner.CurrentHp - 1));
                        tamer.Partner.ReceiveDamage(finalDamage);

                        // Update client about damage
                        client.Send(new UpdateCurrentResourcesPacket(
                            tamer.Partner.GeneralHandler,
                            (short)tamer.Partner.CurrentHp,
                            (short)tamer.Partner.CurrentDs,
                            0));

                        // Update HP rate for others to see
                        map.BroadcastForTargetTamers(
                            tamer.Id,
                            new UpdateCurrentHPRatePacket(
                                tamer.Partner.GeneralHandler,
                                tamer.Partner.HpRate
                            ).Serialize());

                        // Show damage effect
                        map.BroadcastForTamerViewsAndSelf(
                            tamer.Id,
                            new AddBuffPacket.AddDotDebuffPacket(
                                0, // No hitter
                                tamer.Partner.GeneralHandler,
                                MAP_1702_DEBUFF_BUFF_ID,
                                (byte)tamer.Partner.HpRate,
                                finalDamage,
                                0 // Not dead
                            ).Serialize());

                        // Update last damage time
                        _lastDamageTimeByTamer[tamer.Id] = currentTime;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Exception in ProcessMap1702Debuff: {ex.Message}\n{ex.StackTrace}");
            }
        }
        // 6) Itens expirados em todas as abas
        private void ProcessExpiredItems(GameClient client, CharacterModel tamer)
        {
            if (!tamer.CheckExpiredItemsTime) return;

            tamer.SetLastExpiredItemsCheck();

            void Expire(IEnumerable<ItemModel> items, InventorySlotTypeEnum slotType, bool removeOnExpire)
            {
                foreach (var item in items)
                {
                    // [IsTemporary] = UseTimeType > 0
                    // [Expired] = UseTimeType > 0, FirstExpired = 0, RemainingMinutes() = 0
                    if (item.ItemInfo != null && item.IsTemporary && item.Expired)
                    {
                        if (item.ItemInfo.UseTimeType == 2 || item.ItemInfo.UseTimeType == 3 || item.ItemInfo.UseTimeType == 4)
                        {
                            item.SetFirstExpired(false);
                            client.Send(new ItemExpiredPacket(slotType, item.Slot, item.ItemId, ExpiredTypeEnum.Quit));
                        }
                        else if (removeOnExpire)
                        {
                            client.Send(new ItemExpiredPacket(slotType, item.Slot, item.ItemId, ExpiredTypeEnum.Remove));

                            var container = slotType switch
                            {
                                InventorySlotTypeEnum.TabInven => tamer.Inventory,
                                InventorySlotTypeEnum.TabWarehouse => tamer.Warehouse,
                                InventorySlotTypeEnum.TabShareStash => tamer.AccountWarehouse!,
                                InventorySlotTypeEnum.TabEquip => tamer.Equipment,
                                InventorySlotTypeEnum.TabChipset => tamer.ChipSets,
                                _ => null
                            };

                            container?.RemoveOrReduceItem(item, item.Amount);
                        }

                        _sender.Send(new UpdateItemCommand(item));
                    }
                }
            }

            Expire(tamer.Inventory.EquippedItems, InventorySlotTypeEnum.TabInven, true);
            Expire(tamer.Warehouse.EquippedItems, InventorySlotTypeEnum.TabWarehouse, true);
            if (tamer.AccountWarehouse != null) Expire(tamer.AccountWarehouse.EquippedItems, InventorySlotTypeEnum.TabShareStash, true);
            Expire(tamer.Equipment.EquippedItems, InventorySlotTypeEnum.TabEquip, true);
            Expire(tamer.ChipSets.EquippedItems, InventorySlotTypeEnum.TabChipset, true);
        }

        // 7) Buffs do tamer e parceiro, mais skills em cash expirados
        // 6) remove expired buff
  
        private void ProcessBuffs(GameMap map, CharacterModel tamer, GameClient client)
        {
            if (!tamer.CheckBuffsTime)
                return;

            tamer.UpdateBuffsCheckTime();

            bool updatedTamerBuffs = false;
            bool updatedPartnerBuffs = false;

            // -------------------------------------------------------
            //       1) Remover BUFFS del TAMER expirados
            // -------------------------------------------------------
            var expiredTamerBuffs = tamer.BuffList.Buffs.Where(x => x.Expired).ToList();
            foreach (var buff in expiredTamerBuffs)
            {
                tamer.BuffList.Remove(buff.BuffId);

                // Solo mostrar visual del buff removido
                map.BroadcastForTamerViewsAndSelf(
                    tamer.Id,
                    new RemoveBuffPacket(tamer.GeneralHandler, buff.BuffId).Serialize()
                );

                updatedTamerBuffs = true;
            }

            // -------------------------------------------------------
            //       2) Remove expired partner buffs   
            // -------------------------------------------------------
            var expiredPartnerBuffs = tamer.Partner.BuffList.Buffs.Where(x => x.Expired).ToList();
            foreach (var buff in expiredPartnerBuffs)
            {
                tamer.Partner.BuffList.Remove(buff.BuffId);

                // Solo visual del buff removido
                map.BroadcastForTamerViewsAndSelf(
                    tamer.Id,
                    new RemoveBuffPacket(tamer.Partner.GeneralHandler, buff.BuffId).Serialize()
                );

                updatedPartnerBuffs = true;
            }

            // -------------------------------------------------------
            // 3) save to db if something changed
            // -------------------------------------------------------
            if (updatedTamerBuffs)
                _sender.Send(new UpdateCharacterBuffListCommand(tamer.BuffList));

            if (updatedPartnerBuffs)
                _sender.Send(new UpdateDigimonBuffListCommand(tamer.Partner.BuffList));

            // -------------------------------------------------------
            // 4) Cash Skill expired - looks well
            // -------------------------------------------------------
            if (tamer.HaveActiveCashSkill)
            {
                var expCash = tamer.ActiveSkill
                    .Where(x => x.Expired && x.SkillId > 0 && x.Type == TamerSkillTypeEnum.Cash)
                    .ToList();

                foreach (var skill in expCash)
                {
                    var active = tamer.ActiveSkill.First(x => x.Id == skill.Id);
                    active.SetTamerSkill(0, 0, TamerSkillTypeEnum.Normal);

                    client.Send(new ActiveTamerCashSkillExpire(active.SkillId));
                    _sender.Send(new UpdateTamerSkillCooldownByIdCommand(active));
                }
            }
        }

        private void ProcessSyncResources(GameMap map, CharacterModel tamer, GameClient client)
        {
            // 1) Control de tiempo
            if (!tamer.SyncResourcesTime)
                return;

            tamer.UpdateSyncResourcesTime();

            // 2) Crear snapshot actual
            var state = new SyncResourceState(
                Hp: (short)tamer.CurrentHp,
                Ds: (short)tamer.CurrentDs,
                Condition: (int)tamer.CurrentCondition,
                ShopName: tamer.ShopName,
                XGauge: (short)tamer.XGauge,
                XCrystals: (short)tamer.XCrystals
            );

            // 3) Si no hubo cambios → NO enviamos nada
            if (state == tamer.LastSyncState)
                return;

            tamer.LastSyncState = state;

            // -----------------------------------------------------------------------
            //                      ENVÍO AL PROPIO JUGADOR
            // -----------------------------------------------------------------------
            client.Send(new UpdateCurrentResourcesPacket(
                tamer.GeneralHandler,
                state.Hp,
                state.Ds,
                0
            ));

            client.Send(new UpdateStatusPacket(tamer));

            client.Send(new TamerXaiResourcesPacket(
                state.XGauge,
                state.XCrystals
            ));

            // -----------------------------------------------------------------------
            //                  ENVÍO A OTROS JUGADORES EN VIEW
            // -----------------------------------------------------------------------
            map.BroadcastForTargetTamers(
                tamer.Id,
                new UpdateCurrentHPRatePacket(
                    tamer.GeneralHandler,
                    tamer.HpRate
                ).Serialize()
            );

            map.BroadcastForTargetTamers(
                tamer.Id,
                new UpdateCurrentHPRatePacket(
                    tamer.Partner.GeneralHandler,
                    tamer.Partner.HpRate
                ).Serialize()
            );

            map.BroadcastForTamerViewsAndSelf(
                tamer.Id,
                new SyncConditionPacket(
                    tamer.GeneralHandler,
                    tamer.CurrentCondition,
                    tamer.ShopName
                ).Serialize()
            );

            // -----------------------------------------------------------------------
            //                      SINCRONIZACIÓN DEL PARTY
            // -----------------------------------------------------------------------
            var party = _partyManager.FindParty(tamer.Id);
            if (party != null)
            {
                if (party.Members.Count == 1)
                {
                    for (int slot = 0; slot < 4; slot++)
                    {
                        BroadcastForTargetTamers(
                            tamer.Id,
                            new PartyMemberKickPacket((byte)slot).Serialize()
                        );
                    }

                    _partyManager.RemoveParty(party.Id);
                }
                else
                {
                    party.UpdateMember(party[tamer.Id], tamer);

                    map.BroadcastForTargetTamers(
                        party.GetMembersIdList(),
                        new PartyMemberInfoPacket(party[tamer.Id]).Serialize()
                    );

                    var leaderEntry = party.GetMemberById(party.LeaderId);
                    if (leaderEntry != null)
                    {
                        party.LeaderSlot = leaderEntry.Value.Key;

                        BroadcastForTargetTamers(
                            party.GetMembersIdList(),
                            new PartyLeaderChangedPacket((int)leaderEntry.Value.Key).Serialize()
                        );
                    }

                    client.Send(new PartyMemberListPacket(party, tamer.Id));
                }
            }
        }


        // 9) SaveResourcesTime
        private void ProcessSaveResources(CharacterModel tamer)
        {
            if (!tamer.SaveResourcesTime)
                return;

            tamer.UpdateSaveResourcesTime();
            var sw = Stopwatch.StartNew();

            _sender.Send(new UpdateCharacterBasicInfoCommand(tamer));
            _sender.Send(new UpdateEvolutionCommand(tamer.Partner.CurrentEvolution));

            sw.Stop();
            if (sw.ElapsedMilliseconds >= 1500)
                Console.WriteLine($"Save resources elapsed time: {sw.ElapsedMilliseconds}ms");
        }

        // 10) Reset de daily quests
        private void ProcessDailyQuestReset(GameClient client, CharacterModel tamer)
        {
            if (!tamer.ResetDailyQuestsTime)
                return;

            tamer.UpdateDailyQuestsSyncTime();

            var resetTask = _sender.Send(new DailyQuestResetTimeQuery());

            if (DateTime.Now >= resetTask.Result)
                client.Send(new QuestDailyUpdatePacket());
        }

        // -----------------------------------------------------------------------------------------------------------------------

        private async void CheckLocationDebuff(GameClient client)
        {
            if (client.Tamer.DebuffTime)
            {
                client.Tamer.UpdateDebuffTime();

                // Verification for Shadow Labyrint
                if (client.Tamer.Location.MapId == 2001 || client.Tamer.Location.MapId == 2002)
                {
                    var debuff = client.Tamer.Partner.DebuffList.ActiveBuffs.FirstOrDefault(x => x.BuffId == 63000);
                    var evolutionType = _assets.DigimonBaseInfo.First(x => x.Type == client.Partner.CurrentType).EvolutionType;

                    if (debuff == null)
                    {
                        if ((EvolutionRankEnum)evolutionType == EvolutionRankEnum.Jogress || (EvolutionRankEnum)evolutionType == EvolutionRankEnum.JogressX)
                        {
                            var duration = 0xffffffff;

                            var buffInfo = _assets.BuffInfo.FirstOrDefault(x => x.BuffId == 63000);

                            var newDigimonDebuff = DigimonDebuffModel.Create(buffInfo.BuffId, buffInfo.SkillCode, 0, 0);
                            newDigimonDebuff.SetBuffInfo(buffInfo);
                            client.Tamer.Partner.DebuffList.Buffs.Add(newDigimonDebuff);

                            BroadcastForTamerViewsAndSelf(client.TamerId,
                                new AddBuffPacket(client.Tamer.Partner.GeneralHandler, buffInfo, (short)0, duration)
                                    .Serialize());
                        }
                    }
                    else if ((EvolutionRankEnum)evolutionType != EvolutionRankEnum.Jogress &&
                             (EvolutionRankEnum)evolutionType != EvolutionRankEnum.JogressX)
                    {
                        BroadcastForTamerViewsAndSelf(client.TamerId,
                            new RemoveBuffPacket(client.Tamer.Partner.GeneralHandler, debuff.BuffId).Serialize());
                        client.Tamer.Partner.DebuffList.Buffs.Remove(debuff);
                    }
                }

                // Verification for Kaiser Lab
                if (client.Tamer.Location.MapId >= 1110 && client.Tamer.Location.MapId <= 1112)
                {
                    //var debuff = client.Partner.DebuffList.ActiveBuffs.FirstOrDefault(x => x.BuffId == 50101);
                    var evolutionType = _assets.DigimonBaseInfo.First(x => x.Type == client.Partner.CurrentType).EvolutionType;

                    // Break Digimon evolution
                    if ((EvolutionRankEnum)evolutionType != EvolutionRankEnum.Rookie && (EvolutionRankEnum)evolutionType != EvolutionRankEnum.Capsule &&
                        (EvolutionRankEnum)evolutionType != EvolutionRankEnum.Spirit)
                    {

                        await Task.Delay(1000);
                        client.Tamer.IsSpecialMapActive = true;

                        client.Tamer.ActiveEvolution.SetDs(0);
                        client.Tamer.ActiveEvolution.SetXg(0);
                    }
                    else
                    {
                        client.Tamer.IsSpecialMapActive = false;
                    }
                }
            }
        }

        // -----------------------------------------------------------------------------------------------------------------------

        private void GetInViewMobs(GameMap map, CharacterModel tamer)
        {
            List<long> mobsToAdd = new List<long>();
            List<long> mobsToRemove = new List<long>();

            // Criar uma cópia da lista de Mobs
            List<MobConfigModel> mobsCopy = new List<MobConfigModel>(map.Mobs);

            // Iterar sobre a cópia da lista
            mobsCopy.ForEach(mob =>
            {
                if (tamer.TempShowFullMap)
                {
                    if (!tamer.MobsInView.Contains(mob.Id))
                        mobsToAdd.Add(mob.Id);
                }
                else
                {
                    if (mob != null)
                    {
                        var distanceDifference = UtilitiesFunctions.CalculateDistance(
                            tamer.Location.X,
                            mob.CurrentLocation.X,
                            tamer.Location.Y,
                            mob.CurrentLocation.Y);

                        if (distanceDifference <= _startToSee && !tamer.MobsInView.Contains(mob.Id))
                            mobsToAdd.Add(mob.Id);

                        if (distanceDifference >= _stopSeeing && tamer.MobsInView.Contains(mob.Id))
                            mobsToRemove.Add(mob.Id);
                    }
                }
            });

            // Adicionar e remover os IDs de Mob na lista tamer.MobsInView após a iteração
            mobsToAdd.ForEach(id => tamer.MobsInView.Add(id));
            mobsToRemove.ForEach(id => tamer.MobsInView.Remove(id));
        }

        private void GetInViewMobs(GameMap map, CharacterModel tamer, bool Summon)
        {
            List<long> mobsToAdd = new List<long>();
            List<long> mobsToRemove = new List<long>();

            List<SummonMobModel> mobsCopy = new List<SummonMobModel>(map.SummonMobs);

            mobsCopy.ForEach(mob =>
            {
                if (mob == null || mob.CurrentLocation == null || tamer.Location == null)
                    return;

                if (tamer.TempShowFullMap)
                {
                    if (!tamer.MobsInView.Contains(mob.Id))
                        mobsToAdd.Add(mob.Id);
                }
                else
                {
                    var distanceDifference = UtilitiesFunctions.CalculateDistance(
                        tamer.Location.X,
                        mob.CurrentLocation.X,
                        tamer.Location.Y,
                        mob.CurrentLocation.Y);

                    if (distanceDifference <= _startToSee && !tamer.MobsInView.Contains(mob.Id))
                        mobsToAdd.Add(mob.Id);

                    if (distanceDifference >= _stopSeeing && tamer.MobsInView.Contains(mob.Id))
                        mobsToRemove.Add(mob.Id);
                }
            });

            mobsToAdd.ForEach(id => tamer.MobsInView.Add(id));
            mobsToRemove.ForEach(id => tamer.MobsInView.Remove(id));
        }

        /// <summary>
        /// Updates the current partners handler values;
        /// </summary>
        /// <param name="mapId">Current map id</param>
        /// <param name="digimons">Current digimons</param>
        public void SetDigimonHandlers(int mapId, List<DigimonModel> digimons)
        {
            Maps.FirstOrDefault(x => x.MapId == mapId)?.SetDigimonHandlers(digimons);
        }

        /// <summary>
        /// Swaps the digimons current handler.
        /// </summary>
        /// <param name="mapId">Target map handler manager</param>
        /// <param name="oldPartner">Old partner identifier</param>
        /// <param name="newPartner">New partner</param>
        public void SwapDigimonHandlers(int mapId, DigimonModel oldPartner, DigimonModel newPartner)
        {
            Maps.FirstOrDefault(x => x.MapId == mapId)?.SwapDigimonHandlers(oldPartner, newPartner);
        }

        public void SwapDigimonHandlers(int mapId, int channel, DigimonModel oldPartner, DigimonModel newPartner)
        {
            Maps.FirstOrDefault(x => x.MapId == mapId && x.Channel == channel)?.SwapDigimonHandlers(oldPartner, newPartner);
        }

        // -----------------------------------------------------------------------------------------------------------------------

        private void ShowOrHideTamer(GameMap map, CharacterModel tamer)
        {
            foreach (var connectedTamer in map.ConnectedTamers.Where(x => x.Id != tamer.Id))
            {
                var distanceDifference = UtilitiesFunctions.CalculateDistance(
                    tamer.Location.X,
                    connectedTamer.Location.X,
                    tamer.Location.Y,
                    connectedTamer.Location.Y);

                if (distanceDifference <= _startToSee)
                    ShowTamer(map, tamer, connectedTamer.Id);
                else if (distanceDifference >= _stopSeeing)
                    HideTamer(map, tamer, connectedTamer.Id);
            }
        }

        private void ShowTamer(GameMap map, CharacterModel tamerToShow, long tamerToSeeId)
        {
            if (!map.ViewingTamer(tamerToShow.Id, tamerToSeeId))
            {
                foreach (var item in tamerToShow.Equipment.EquippedItems.Where(x => x.ItemInfo == null))
                    item?.SetItemInfo(_assets.ItemInfo.GetValueOrDefault(item.ItemId));

                map.ShowTamer(tamerToShow.Id, tamerToSeeId);

                var targetClient = map.Clients.FirstOrDefault(x => x.TamerId == tamerToSeeId);
                if (targetClient != null)
                {
                    targetClient.Send(new LoadTamerPacket(tamerToShow));
                    targetClient.Send(new LoadBuffsPacket(tamerToShow));
                    if (tamerToShow.InBattle)
                    {
                        targetClient.Send(new SetCombatOnPacket(tamerToShow.GeneralHandler));
                        targetClient.Send(new SetCombatOnPacket(tamerToShow.Partner.GeneralHandler));
                    }
#if DEBUG
                    var serialized = SerializeShowTamer(tamerToShow);
                    //File.WriteAllText($"Shows\\Show{tamerToShow.Id}To{tamerToSeeId}_{DateTime.Now:dd_MM_yy_HH_mm_ss}.temp", serialized);
#endif
                }
            }
        }

        private void HideTamer(GameMap map, CharacterModel tamerToHide, long tamerToBlindId)
        {
            if (map.ViewingTamer(tamerToHide.Id, tamerToBlindId))
            {
                map.HideTamer(tamerToHide.Id, tamerToBlindId);

                var targetClient = map.Clients.FirstOrDefault(x => x.TamerId == tamerToBlindId);

                if (targetClient != null)
                {
                    targetClient.Send(new UnloadTamerPacket(tamerToHide));

#if DEBUG
                    var serialized = SerializeHideTamer(tamerToHide);
                    //File.WriteAllText($"Hides\\Hide{tamerToHide.Id}To{tamerToBlindId}_{DateTime.Now:dd_MM_yy_HH_mm_ss}.temp", serialized);
#endif
                }
            }
        }

        // -----------------------------------------------------------------------------------------------------------------------

        private static string SerializeHideTamer(CharacterModel tamer)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Tamer{tamer.Id}{tamer.Name}");
            sb.AppendLine($"TamerHandler {tamer.GeneralHandler.ToString()}");
            sb.AppendLine($"TamerLocation {tamer.Location.X.ToString()}");
            sb.AppendLine($"TamerLocation {tamer.Location.Y.ToString()}");

            sb.AppendLine($"Partner{tamer.Partner.Id}{tamer.Partner.Name}");
            sb.AppendLine($"PartnerHandler {tamer.Partner.GeneralHandler.ToString()}");
            sb.AppendLine($"PartnerLocation {tamer.Partner.Location.X.ToString()}");
            sb.AppendLine($"PartnerLocation {tamer.Partner.Location.Y.ToString()}");

            return sb.ToString();
        }

        private static string SerializeShowTamer(CharacterModel tamer)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Partner{tamer.Partner.Id}");
            sb.AppendLine($"PartnerName {tamer.Partner.Name}");
            sb.AppendLine($"PartnerLocation {tamer.Partner.Location.X.ToString()}");
            sb.AppendLine($"PartnerLocation {tamer.Partner.Location.Y.ToString()}");
            sb.AppendLine($"PartnerHandler {tamer.Partner.GeneralHandler.ToString()}");
            sb.AppendLine($"PartnerCurrentType {tamer.Partner.CurrentType.ToString()}");
            sb.AppendLine($"PartnerSize {tamer.Partner.Size.ToString()}");
            sb.AppendLine($"PartnerLevel {tamer.Partner.Level.ToString()}");
            sb.AppendLine($"PartnerModel {tamer.Partner.Model.ToString()}");
            sb.AppendLine($"PartnerMS {tamer.Partner.MS.ToString()}");
            sb.AppendLine($"PartnerAS {tamer.Partner.AS.ToString()}");
            sb.AppendLine($"PartnerHPRate {tamer.Partner.HpRate.ToString()}");
            sb.AppendLine($"PartnerCloneTotalLv {tamer.Partner.Digiclone.CloneLevel.ToString()}");
            sb.AppendLine($"PartnerCloneAtLv {tamer.Partner.Digiclone.ATLevel.ToString()}");
            sb.AppendLine($"PartnerCloneBlLv {tamer.Partner.Digiclone.BLLevel.ToString()}");
            sb.AppendLine($"PartnerCloneCtLv {tamer.Partner.Digiclone.CTLevel.ToString()}");
            sb.AppendLine($"PartnerCloneEvLv {tamer.Partner.Digiclone.EVLevel.ToString()}");
            sb.AppendLine($"PartnerCloneHpLv {tamer.Partner.Digiclone.HPLevel.ToString()}");

            sb.AppendLine($"Tamer{tamer.Id}");
            sb.AppendLine($"TamerName {tamer.Name.ToString()}");
            sb.AppendLine($"TamerLocation {tamer.Location.X.ToString()}");
            sb.AppendLine($"TamerLocation {tamer.Location.Y.ToString()}");
            sb.AppendLine($"TamerHandler {tamer.GeneralHandler.ToString()}");
            sb.AppendLine($"TamerModel {tamer.Model.ToString()}");
            sb.AppendLine($"TamerLevel {tamer.Level.ToString()}");
            sb.AppendLine($"TamerMS {tamer.MS.ToString()}");
            sb.AppendLine($"TamerHpRate {tamer.HpRate.ToString()}");
            sb.AppendLine($"TamerEquipment {tamer.Equipment.ToString()}");
            sb.AppendLine($"TamerDigivice {tamer.Digivice.ToString()}");
            sb.AppendLine($"TamerCurrentCondition {tamer.CurrentCondition.ToString()}");
            sb.AppendLine($"TamerSize {tamer.Size.ToString()}");
            sb.AppendLine($"TamerCurrentTitle {tamer.CurrentTitle.ToString()}");
            sb.AppendLine($"TamerSealLeaderId {tamer.SealList.SealLeaderId.ToString()}");

            return sb.ToString();
        }

        // -----------------------------------------------------------------------------------------------------------------------

        /*private void WhyRYouGae(GameClient client)
        {
            var inventoryCopy = client.Tamer.Inventory.Items.ToList();  // Clone the list

            for (int itemSlot = 0; itemSlot < inventoryCopy.Count; itemSlot++)
            {
                var targetItem = inventoryCopy[itemSlot];
                if (targetItem == null || targetItem.ItemId == 0) continue;

                var targetItemTrue = _assets.ItemInfo.FirstOrDefault(x => x.ItemId == targetItem.ItemId);
                if (targetItemTrue == null)
                {
                    _logger.Warning($"Item with ID {targetItem.ItemId} does not exist in assets.");
                    client.Send(new SystemMessagePacket("Invalid item data."));
                    return;
                }
                if (targetItem.Amount > 3000)
                {
                    var banProcessor = SingletonResolver.GetService<BanForCheating>();
                    var banMessage = banProcessor.BanAccountWithMessage(client.AccountId, client.Tamer.Name,
                   AccountBlockEnum.Permanent, "Items over the limits", client,
                   "Why are you trying to cheat? Happy ban with video providing cheating.");

                    var chatPacket = new NoticeMessagePacket(banMessage).Serialize();
                    client.SendToAll(chatPacket);
                    return;
                }
            }
        }*/

        // -----------------------------------------------------------------------------------------------------------------------

        public void PartnerAutoAttackMob(GameClient client)
        {
            try
            {
                var tamer = client.Tamer;
                var partner = tamer.Partner;
                var targetMob = tamer.TargetIMob;

                if (!partner.AutoAttack || partner.IsAttacking || targetMob == null || !partner.Alive || !targetMob.Alive)
                    return;

                if (partner.NextHitTime > DateTime.UtcNow)
                    return;

                #region Verification to Attack / Preparação para o ataque

                partner.SetEndAttacking(partner.AS);
                tamer.SetHidden(false);

                if (!tamer.InBattle)
                {
                    BroadcastCombatOn(tamer.Id, partner.GeneralHandler);
                    tamer.StartBattle(targetMob);
                    partner.StartAutoAttack();
                }

                if (!targetMob.InBattle)
                {
                    BroadcastCombatOn(tamer.Id, (ushort)targetMob.GeneralHandler);
                    targetMob.StartBattle(tamer);
                    partner.StartAutoAttack();
                }

                #endregion

                #region Miss Calculation / Cálculo de acerto

                if (tamer.CanMissHit())
                {
                    BroadcastForTamerViewsAndSelf(tamer.Id, new MissHitPacket(partner.GeneralHandler, targetMob.GeneralHandler).Serialize());

                    partner.UpdateLastHitTime();
                    return;
                }

                #endregion

                #region Damage Calculation / Cálculo de dano

                var critBonusMultiplier = 0.00;
                var blocked = false;

                int finalDamage = tamer.GodMode
                    ? targetMob.CurrentHP
                    : AttackManager.CalculateDamage(client, out critBonusMultiplier, out blocked);
                // Apply map 1702 damage reduction if applicable
                float damageMultiplier = GetMap1702DamageMultiplier(client);

                finalDamage = Math.Clamp(finalDamage, 1, targetMob.CurrentHP);

                // Call ProcessReturnFireDamageReflection here - after damage calculation but before attack feedback
                ProcessReturnFireDamageReflection(client, targetMob, finalDamage);

                int newHp = targetMob.ReceiveDamage(finalDamage, tamer.Id);
                int hitType = blocked ? 2 : critBonusMultiplier > 0 ? 1 : 0;

                #endregion

                #region Attack Feedback / Feedback de ataque

                if (newHp > 0)
                {
                    BroadcastForTamerViewsAndSelf(tamer.Id,
                        new HitPacket(partner.GeneralHandler, targetMob.GeneralHandler, finalDamage, targetMob.HPValue, newHp, hitType).Serialize());
                }
                else
                {
                    BroadcastForTamerViewsAndSelf(tamer.Id,
                        new KillOnHitPacket(partner.GeneralHandler, targetMob.GeneralHandler, finalDamage, hitType).Serialize());

                    targetMob.Die();

                    // Add this line to check for the special mob kill
                    CheckAndGrantDebuffImmunity(client, targetMob);

                    if (!IsMobsAttacking(tamer.Id))
                    {
                        tamer.StopBattle();
                        BroadcastCombatOff(tamer.Id, partner.GeneralHandler);
                    }
                }

                #endregion

                partner.UpdateLastHitTime();

                #region Verification to stop autoAttack / Verificação para encerrar ataque automático 

                if (targetMob == null || targetMob.Dead)
                    partner.StopAutoAttack();

                #endregion
            }
            catch (Exception ex)
            {
                _logger.Error($"[DungeonPartnerAutoAttackMob] :: {ex.Message}");
            }
        }


        // Add this to PartnerAutoAttackMob after targetMob.Die() section
        private void CheckAndGrantDebuffImmunity(GameClient client, IMob targetMob)
        {
            try
            {
                // Validate parameters
                if (client == null || targetMob == null)
                {
                    _logger.Warning("CheckAndGrantDebuffImmunity called with null parameters");
                    return;
                }

                // Validate client.Tamer
                if (client.Tamer == null || client.Tamer.Location == null || client.Tamer.Partner == null)
                {
                    _logger.Warning("CheckAndGrantDebuffImmunity: Tamer, location, or partner is null");
                    return;
                }

                // Check if we're in map 1702 and if the mob is the special type
                if (client.Tamer.Location.MapId == 1702 && targetMob.Type == MAP_1702_MOB_TYPE_FOR_DEBUFF_REMOVAL)
                {
                    // Grant immunity for 30 seconds
                    _map1702DebuffTimers[client.TamerId] = DateTime.Now.AddSeconds(MAP_1702_DEBUFF_REMOVAL_SECONDS);

                    // Inform player
                    client.Send(new SystemMessagePacket($"You've gained temporary immunity from the dungeon's curse for {MAP_1702_DEBUFF_REMOVAL_SECONDS} seconds!"));

                    // Ensure partner and debuffList exist
                    if (client.Tamer.Partner.DebuffList != null)
                    {
                        // Remove the debuff if it exists
                        var debuff = client.Tamer.Partner.DebuffList.ActiveBuffs?.FirstOrDefault(x => x != null && x.BuffId == MAP_1702_DEBUFF_BUFF_ID);
                        if (debuff != null)
                        {
                            // Remove the debuff from the data model
                            client.Tamer.Partner.DebuffList.Remove(debuff.BuffId);

                            // Find the current map
                            var currentMap = Maps.FirstOrDefault(m => m.MapId == client.Tamer.Location.MapId);

                            // Create the packet for removing the visual buff effect
                            var removeBuffPacket = new RemoveBuffPacket(client.Tamer.Partner.GeneralHandler, debuff.BuffId).Serialize();

                            // Send to the client first to ensure they see it immediately
                            client.Send(removeBuffPacket);

                            if (currentMap != null)
                            {
                                // Broadcast to all players in the map who can see this tamer
                                currentMap.BroadcastForTamerViewsAndSelf(client.TamerId, removeBuffPacket);
                            }
                            else
                            {
                                // Fallback broadcast method
                                BroadcastForTamerViews(client.TamerId, removeBuffPacket);
                            }

                            // Log what we did
                            _logger.Debug($"Removed debuff {MAP_1702_DEBUFF_BUFF_ID} from Tamer {client.TamerId}'s partner");

                            // Update the database with the changed debuff list
                            // Use UpdateDigimonDebuffListCommand instead of UpdateDigimonBuffListCommand
                            _sender.Send(new UpdateDigimonBuffListCommand(client.Tamer.Partner.BuffList));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Exception in CheckAndGrantDebuffImmunity: {ex.Message}");
            }
        }

        // -----------------------------------------------------------------------------------------------------------------------

        private void BroadcastCombatOn(long tamerId, ushort handler)
        {
            BroadcastForTamerViewsAndSelf(tamerId, new SetCombatOnPacket(handler).Serialize());
        }

        private void BroadcastCombatOff(long tamerId, ushort handler)
        {
            BroadcastForTamerViewsAndSelf(tamerId, new SetCombatOffPacket(handler).Serialize());
        }

        // -----------------------------------------------------------------------------------------------------------------------

        private ReceiveExpResult ReceiveBonusTamerExp(GameClient client, CharacterModel tamer, long totalTamerExp)
        {
            var tamerResult = _expManager.ReceiveTamerExperience(totalTamerExp, tamer);

            if (tamerResult.LevelGain > 0)
            {
                BroadcastForTamerViewsAndSelf(tamer.Id, new LevelUpPacket(tamer.GeneralHandler, tamer.Level).Serialize());

                tamer.SetLevelStatus(_statusManager.GetTamerLevelStatus(tamer.Model, tamer.Level));

                tamer.FullHeal();

                client.Send(new UpdateStatusPacket(tamer));
            }

            return tamerResult;
        }
        private ReceiveExpResult ReceiveTamerExp(GameClient client, CharacterModel tamer, long tamerExpToReceive)
        {
            var tamerResult = _expManager.ReceiveTamerExperience(tamerExpToReceive, tamer);

            if (tamerResult.LevelGain > 0)
            {
                BroadcastForTamerViewsAndSelf(tamer.Id, new LevelUpPacket(tamer.GeneralHandler, tamer.Level).Serialize());

                tamer.SetLevelStatus(_statusManager.GetTamerLevelStatus(tamer.Model, tamer.Level));

                tamer.FullHeal();

                client.Send(new UpdateStatusPacket(tamer));
            }

            return tamerResult;
        }

        private ReceiveExpResult ReceiveBonusPartnerExp(GameClient client, DigimonModel partner, MobConfigModel targetMob, long totalPartnerExp)
        {
            var partnerResult = _expManager.ReceiveDigimonExperience(totalPartnerExp, partner);

            if (partnerResult.LevelGain > 0)
            {
                partner.SetBaseStatus(
                    _statusManager.GetDigimonBaseStatus(partner.CurrentType, partner.Level, partner.Size));

                BroadcastForTamerViewsAndSelf(
                    partner.Character.Id, new LevelUpPacket(partner.GeneralHandler, partner.Level).Serialize());

                partner.FullHeal();

                client.Send(new UpdateStatusPacket(client.Tamer));
            }
            return partnerResult;
        }

        private ReceiveExpResult ReceivePartnerExp(GameClient client, DigimonModel partner, MobConfigModel targetMob, long partnerExpToReceive)
        {
            var attributeExp = partner.GetAttributeExperience();
            var elementExp = partner.GetElementExperience();
            var partnerResult = _expManager.ReceiveDigimonExperience(partnerExpToReceive, partner);

            if (attributeExp < 10000) _expManager.ReceiveAttributeExperience(client, partner, targetMob.Attribute, targetMob.ExpReward);
            if (elementExp < 10000) _expManager.ReceiveElementExperience(client, partner, targetMob.Element, targetMob.ExpReward);

            partner.ReceiveSkillExp(targetMob.ExpReward.SkillExperience);

            if (partnerResult.LevelGain > 0)
            {
                partner.SetBaseStatus(
                    _statusManager.GetDigimonBaseStatus(partner.CurrentType, partner.Level, partner.Size));

                BroadcastForTamerViewsAndSelf(
                    partner.Character.Id, new LevelUpPacket(partner.GeneralHandler, partner.Level).Serialize());

                partner.FullHeal();

                client.Send(new UpdateStatusPacket(client.Tamer));
            }

            return partnerResult;
        }

        // In a suitable location in the class, add this method to modify damage calculations

        // Call this before calculating damage in PartnerAutoAttackMob
        private float GetMap1702DamageMultiplier(GameClient client)
        {
            // Check if in map 1702 and has the debuff
            if (client.Tamer.Location.MapId == 1702)
            {
                var debuff = client.Tamer.Partner.DebuffList.ActiveBuffs.FirstOrDefault(x => x.BuffId == MAP_1702_DEBUFF_BUFF_ID);
                if (debuff != null)
                {
                    // Reduce damage dealt by 50%
                    return 0.5f;
                }
            }
            return 1.0f; // Normal damage
        }

        // Call this when a partner receives damage in map 1702
        private float GetMap1702IncomingDamageMultiplier(GameClient client)
        {
            // Check if in map 1702 and has the debuff
            if (client.Tamer.Location.MapId == 1702)
            {
                var debuff = client.Tamer.Partner.DebuffList.ActiveBuffs.FirstOrDefault(x => x.BuffId == MAP_1702_DEBUFF_BUFF_ID);
                if (debuff != null)
                {
                    // Increase damage received by 50%
                    return 1.5f;
                }
            }
            return 1.0f; // Normal damage
        }

        private ReceiveExpResult ReceivePartnerExp(GameClient client, DigimonModel partner, SummonMobModel targetMob, long partnerExpToReceive)
        {
            var attributeExp = partner.GetAttributeExperience();
            var elementExp = partner.GetElementExperience();
            var partnerResult = _expManager.ReceiveDigimonExperience(partnerExpToReceive, partner);

            if (attributeExp < 10000) _expManager.ReceiveAttributeExperience(client, partner, targetMob.Attribute, targetMob.ExpReward);
            if (elementExp < 10000) _expManager.ReceiveElementExperience(client, partner, targetMob.Element, targetMob.ExpReward);

            partner.ReceiveSkillExp(targetMob.ExpReward.SkillExperience);

            if (partnerResult.LevelGain > 0)
            {
                partner.SetBaseStatus(
                    _statusManager.GetDigimonBaseStatus(partner.CurrentType, partner.Level, partner.Size));

                BroadcastForTamerViewsAndSelf(
                    partner.Character.Id, new LevelUpPacket(partner.GeneralHandler, partner.Level).Serialize());

                partner.FullHeal();

                client.Send(new UpdateStatusPacket(client.Tamer));
            }

            return partnerResult;
        }
        private ReceiveExpResult ReceiveBonusPartnerExp(GameClient client, DigimonModel partner, SummonMobModel targetMob, long totalPartnerExp)
        {
            var partnerResult = _expManager.ReceiveDigimonExperience(totalPartnerExp, partner);

            if (partnerResult.LevelGain > 0)
            {
                partner.SetBaseStatus(
                    _statusManager.GetDigimonBaseStatus(partner.CurrentType, partner.Level, partner.Size));

                BroadcastForTamerViewsAndSelf(
                    partner.Character.Id, new LevelUpPacket(partner.GeneralHandler, partner.Level).Serialize());

                partner.FullHeal();

                client.Send(new UpdateStatusPacket(client.Tamer));
            }
            return partnerResult;
        }

        // -----------------------------------------------------------------------------------------------------------------------

        private async void CheckTimeReward(GameClient client)
        {
            var tr = client.Tamer.TimeReward;

            if (tr.ReedemRewards)
            {
                _logger.Debug($"Reward Index: {tr.RewardIndex}");
                tr.SetStartTime();
                await _sender.Send(new UpdateTamerAttendanceTimeRewardCommand(tr));
            }

            if (tr.RewardIndex > TimeRewardIndexEnum.Fourth)
            {
                tr.RewardIndex = TimeRewardIndexEnum.Ended;
                await _sender.Send(new UpdateTamerAttendanceTimeRewardCommand(tr));
                return;
            }

            if (tr.CurrentTime == 0)
                tr.CurrentTime = tr.AtualTime;

            if (DateTime.Now < tr.LastTimeRewardUpdate)
            {
                tr.SetLastTimeRewardDate();
                return;
            }

            tr.CurrentTime++;
            tr.UpdateCounter++;
            tr.SetAtualTime();

            if (tr.TimeCompleted())
            {
                ReedemTimeReward(client);
                tr.RewardIndex++;
                tr.CurrentTime = 0;
                tr.SetAtualTime();
                await _sender.Send(new UpdateTamerAttendanceTimeRewardCommand(tr));
            }
            else if (tr.UpdateCounter >= 60)
            {
                await _sender.Send(new UpdateTamerAttendanceTimeRewardCommand(tr));
                tr.UpdateCounter = 0;
            }

            if ((DateTime.Now - tr.LastPacketSent).TotalSeconds >= 60)
            {
                client.Send(new TimeRewardPacket(tr));
                tr.LastPacketSent = DateTime.Now;
            }

            tr.SetLastTimeRewardDate();
        }

        private void ReedemTimeReward(GameClient client)
        {
            var tr = client.Tamer.TimeReward;
            var drops = _assets.TimeRewardAssets
                            .Where(d => d.CurrentReward == (int)tr.RewardIndex);

            foreach (var drop in drops)
            {
                var reward = new ItemModel();
                reward.SetItemInfo(
                    _assets.ItemInfo.GetValueOrDefault(drop.ItemId)
                );
                reward.ItemId = drop.ItemId;
                reward.Amount = drop.ItemCount;

                if (reward.IsTemporary)
                    reward.SetRemainingTime((uint)reward.ItemInfo.UsageTimeMinutes);

                if (client.Tamer.Inventory.AddItem(reward))
                {
                    client.Send(new ReceiveItemPacket(reward, InventoryTypeEnum.Inventory));
                    _sender.Send(new UpdateItemCommand(reward));
                }
            }
        }

        // -----------------------------------------------------------------------------------------------------------------------
    }
}