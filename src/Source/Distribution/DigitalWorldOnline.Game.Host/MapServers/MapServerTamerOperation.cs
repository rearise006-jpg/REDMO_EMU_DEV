using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.Character;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Models.Assets;
using DigitalWorldOnline.Commons.Models.Base;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Models.Config;
using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Models.Map;
using DigitalWorldOnline.Commons.Models.Summon;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Packets.GameServer.Combat;
using DigitalWorldOnline.Commons.Packets.Items;
using DigitalWorldOnline.Commons.Packets.MapServer;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Game.Managers;
using System.Diagnostics;
using System.Text;


namespace DigitalWorldOnline.GameHost
{
    public sealed partial class MapServer
    {
        private readonly Dictionary<long, CancellationTokenSource> _verdandiDebuffTokens = new();

        const int combatTimeoutSeconds = 10;

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

                ProcessDebuffs(client);
                ProcessVisibility(map, tamer, client);
                ProcessAttacks(client, map, tamer);
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
                Console.WriteLine($"TamersOperation ({map.ConnectedTamers.Count}): {sw.Elapsed.TotalMilliseconds}ms");
        }


        // 1) Debuffs e afins
        private void ProcessDebuffs(GameClient client)
        {
            CheckLocationDebuff(client);
            CheckAndHandleVerdandiDebuff(client);
            CheckVerdandiBlessBuff(client);


            if (client.Tamer.Location.MapId == 105)
            {
                AutomaticTeleportYokohama(client); // Add this line to call our new function
                AutomaticTeleportYokohama2(client); // Add this line to call our new function
                AutomaticTeleportYokohama3(client); // Add this line to call our new function
                AutomaticTeleportYokohama4(client); // Add this line to call our new function
                AutomaticTeleportYokohama5(client); // Add this line to call our new function
                AutomaticTeleportYokohama6(client); // Add this line to call our new function
                AutomaticTeleportYokohama7(client); // Add this line to call our new function
                AutomaticTeleportYokohama8ToDats(client); // Add this line to call our new function
            }
            else if (client.Tamer.Location.MapId == 3)
            {
                DatsToSilverLake(client); // Add this line to call our new function
            }
            else if (client.Tamer.Location.MapId == 1301)
            {
                DatsToSilverLake(client); // Add this line to call our new function
                SilverLakeQuest1(client);
                SilverLakeQuest2(client);
                SilverLakeQuest3(client);
                SilverLakeQuest4(client);
                SilverLakeQuest5(client);
                SilverLakeToSilentForest(client);
            }
            else if (client.Tamer.Location.MapId == 1302)
            {
                SilentQuest1(client);
                SilentQuest2(client);
                //SilentQuest3(client);
                SilentQuest4(client);
                SilentQuest5(client);
            }
            else if (client.Tamer.Location.MapId == 1303)
            {
                LostHistoricQuest1(client);
                //LostHistoricQuest2(client);
                LostHistoricQuest3(client);
                LostHistoricQuest4(client);
                LostHistoricQuest5(client);
                LostHistoricToFileIsland(client);
            }
            else if (client.Tamer.Location.MapId == 1305)
            {
                FileQuest1(client);
            }
            else if (client.Tamer.Location.MapId == 1400)
            {
                DesertQuest1(client);
                DesertQuest2(client);
                DesertQuest3(client);
                DesertQuest4(client);
                DesertQuest5(client);
                DesertQuest6(client);
                DesertQuest7(client);
                DesertQuest8(client);
                //DesertToContinent(client);
            }
            else
            {
                return;
            }

            // MapBuff(client);
            // client.Send(new LoadInventoryPacket(client.Tamer.Inventory, InventoryTypeEnum.Inventory));
        }

        // 2) Visibilidade de mobs, tamer e shop
        private void ProcessVisibility(GameMap map, CharacterModel tamer, GameClient client)
        {
            GetInViewMobs(map, tamer);
            GetInViewMobs(map, tamer, true);
            ShowOrHideTamer(map, tamer);
            ShowOrHideConsignedShop(map, tamer);
        }

        // ====================================================================================================

        private void ProcessAttacks(GameClient client, GameMap map, CharacterModel tamer)
        {
            var currentTargetMob = GetCurrentTargetMob(tamer, map.Mobs);
            var currentTargetMobSummon = GetCurrentTargetMobSummon(tamer, map.SummonMobs);

            if (currentTargetMob == null && currentTargetMobSummon == null && !tamer.SetCombatOff && tamer.TargetPartner == null)
            {
                client.Send(new SetCombatOffPacket(tamer.Partner.GeneralHandler));
                tamer.SetCombatOff = true;
            }

            if (currentTargetMob != null || currentTargetMobSummon != null)
                PartnerAutoAttackMob(client, tamer);

            if (tamer.TargetPartner != null)
                PartnerAutoAttackPlayer(client, tamer);
        }

        public MobConfigModel GetCurrentTargetMob(CharacterModel tamer, List<MobConfigModel> mobs)
        {
            return mobs.FirstOrDefault(m => m.TargetTamers.Contains(tamer) && m.Alive);
        }

        public SummonMobModel GetCurrentTargetMobSummon(CharacterModel tamer, List<SummonMobModel> mobs)
        {
            return mobs.FirstOrDefault(m => m.TargetTamers.Contains(tamer) && m.Alive);
        }

        // ====================================================================================================

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

        // ====================================================================================================

        // 5) Itens expirados em todas as abas
        private void ProcessExpiredItems(GameClient client, CharacterModel tamer)
        {
            if (!tamer.CheckExpiredItemsTime) return;

            tamer.SetLastExpiredItemsCheck();

            //_logger.Information($"Checking Expired Items !!");

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

                }
             }
        }




        // Create a public method that can be called from skill processors
        public void OnPartnerSkillUsed(CharacterModel tamer, int skillId)
        {
            // Check if this is the party buff skill and apply it if needed
            CheckAndApplyPartySkillBuff(tamer, skillId);
        }




        // summary>


       private void ProcessSyncResources(GameMap map, CharacterModel tamer, GameClient client)
        {
            // 1) Control de tiempo de sincronización
            if (!tamer.SyncResourcesTime)
                return;

            tamer.UpdateSyncResourcesTime();

            // 2) Crear snapshot del estado actual
            var state = new SyncResourceState(
                Hp: (short)tamer.CurrentHp,
                Ds: (short)tamer.CurrentDs,
                Condition: (int)tamer.CurrentCondition,
                ShopName: tamer.ShopName,
                XGauge: (short)tamer.XGauge,
                XCrystals: (short)tamer.XCrystals
            );

            // 3) Si es igual al último estado → no se envía nada (FIX DEL BUG)
            if (state == tamer.LastSyncState)
                return;

            // Actualizar el snapshot
            tamer.LastSyncState = state;

            // -----------------------------------------------------------------------
            //                      ENVÍO AL PROPIO JUGADOR
            // -----------------------------------------------------------------------
            // Stats básicos
            client.Send(new UpdateCurrentResourcesPacket(
                tamer.GeneralHandler,
                state.Hp,
                state.Ds,
                0 // Fatigue no se usa
            ));

            // Status completo (incluye Partner)
            client.Send(new UpdateStatusPacket(tamer));

            // Recursos XAIs
            client.Send(new TamerXaiResourcesPacket(state.XGauge, state.XCrystals));

            // -----------------------------------------------------------------------
            //                  ENVÍO A LOS QUE LO TIENEN EN VIEW
            // -----------------------------------------------------------------------
            // Solo se manda información necesaria
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
                    // Eliminar visualmente el party para este tamer
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
                    // Actualizar miembro en party
                    party.UpdateMember(party[tamer.Id], tamer);

                    // Actualizar visual a todos los miembros
                    map.BroadcastForTargetTamers(
                        party.GetMembersIdList(),
                        new PartyMemberInfoPacket(party[tamer.Id]).Serialize()
                    );

                    // Si cambió el líder, actualizar
                    var leaderEntry = party.GetMemberById(party.LeaderId);
                    if (leaderEntry != null)
                    {
                        party.LeaderSlot = leaderEntry.Value.Key;

                        BroadcastForTargetTamers(
                            party.GetMembersIdList(),
                            new PartyLeaderChangedPacket(
                                (int)leaderEntry.Value.Key
                            ).Serialize()
                        );
                    }

                    // Mandar lista completa al jugador
                    client.Send(new PartyMemberListPacket(party, tamer.Id));
                }
            }
        }


        // 8) SaveResourcesTime
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

        // 9) Reset de daily quests
        private void ProcessDailyQuestReset(GameClient client, CharacterModel tamer)
        {
            if (!tamer.ResetDailyQuestsTime)
                return;

            tamer.UpdateDailyQuestsSyncTime();

            var resetTask = _sender.Send(new DailyQuestResetTimeQuery());

            if (DateTime.Now >= resetTask.Result)
                client.Send(new QuestDailyUpdatePacket());
        }
        
        // ================================================================================================

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

            // Adicionar e remover os IDs de Mob na lista tamer.MobsInView após a iteração
            mobsToAdd.ForEach(id => tamer.MobsInView.Add(id));
            mobsToRemove.ForEach(id => tamer.MobsInView.Remove(id));
        }

        private void GetInViewMobs(GameMap map, CharacterModel tamer, bool Summon)
        {
            List<long> mobsToAdd = new List<long>();
            List<long> mobsToRemove = new List<long>();

            // Criar uma cópia da lista de Mobs
            List<SummonMobModel> mobsCopy = new List<SummonMobModel>(map.SummonMobs);

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

            // Adicionar e remover os IDs de Mob na lista tamer.MobsInView após a iteração
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

        // -----------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Swaps the digimons current handler.
        /// </summary>
        /// <param name="mapId">Target map handler manager</param>
        /// <param name="oldPartnerId">Old partner identifier</param>
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

        private async void CheckLocationDebuff(GameClient client)
        {
            if (client.Tamer.DebuffTime)
            {
                client.Tamer.UpdateDebuffTime();

                // Verification for Dark Tower
                if (client.Tamer.Location.MapId == 1109)
                {
                    var mapDebuff = client.Partner.DebuffList.ActiveBuffs.Where(x => x.BuffId == 50101);
                    var evolutionType = _assets.DigimonBaseInfo.First(x => x.Type == client.Partner.CurrentType).EvolutionType;

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

                    if (mapDebuff != null)
                    {

                        foreach (var teste in mapDebuff)
                        {
                        }

                    }

                }

                // Verifica Buff do PvpMap
                var buff1 = client.Tamer.BuffList.ActiveBuffs.FirstOrDefault(x => x.BuffId == 40345);

                if (buff1 != null)
                {
                    client.Tamer.BuffList.Buffs.Remove(buff1);

                    client.Send(new UpdateStatusPacket(client.Tamer));
                    client.Send(new RemoveBuffPacket(client.Tamer.GeneralHandler, buff1.BuffId).Serialize());
                }

                await _sender.Send(new UpdateCharacterBuffListCommand(client.Tamer.BuffList));
            }
        }



        private async void MapBuff(GameClient client)
        {
            var buff = _assets.BuffInfo.Where(x => x.BuffId == 40327 || x.BuffId == 40350).ToList();

            if (buff != null)
            {
                buff.ForEach(buffAsset =>
                {
                    if (!client.Tamer.BuffList.Buffs.Any(x => x.BuffId == buffAsset.BuffId))
                    {
                        var newCharacterBuff = CharacterBuffModel.Create(buffAsset.BuffId, buffAsset.SkillId, 2592000, 0);

                        newCharacterBuff.SetBuffInfo(buffAsset);

                        client.Tamer.BuffList.Buffs.Add(newCharacterBuff);

                        BroadcastForTamerViewsAndSelf(client.TamerId, new AddBuffPacket(client.Tamer.GeneralHandler, buffAsset, 0, 0).Serialize());
                    }
                });

                await _sender.Send(new UpdateCharacterBuffListCommand(client.Tamer.BuffList));
            }
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
                    item?.SetItemInfo(_assets.ItemInfo.GetValueOrDefault(item.ItemId)); // item.Id != null ? item.ItemId : null););

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

        private void PartnerAutoAttackPlayer(GameClient client, CharacterModel tamer)
        {
            if (!tamer.Partner.AutoAttack || tamer.Partner.HP < 1)
                return;

            if (!tamer.Partner.IsAttacking && tamer.TargetPartner != null && tamer.TargetPartner.Alive)
            {
                tamer.Partner.SetEndAttacking(tamer.Partner.AS);
                tamer.SetHidden(false);

                if (!tamer.InBattle)
                {

                    BroadcastForTamerViewsAndSelf(tamer.Id, new SetCombatOnPacket(tamer.Partner.GeneralHandler).Serialize());

                    tamer.StartBattle(tamer.TargetPartner);
                }

                if (!tamer.TargetPartner.Character.InBattle)
                {
                    BroadcastForTamerViewsAndSelf(tamer.Id, new SetCombatOnPacket(tamer.TargetPartner.Character.GeneralHandler).Serialize());

                    tamer.TargetPartner.Character.StartBattle(tamer.Partner);
                }

                var missed = false;

                if (missed)
                {
                    _logger.Warning(
                        $"Partner {tamer.Partner.Id} missed hit on partner {tamer.TargetPartner.Id} - {tamer.TargetPartner.Name}.");
                    BroadcastForTamerViewsAndSelf(tamer.Id,
                        new MissHitPacket(tamer.Partner.GeneralHandler, tamer.TargetPartner.GeneralHandler)
                            .Serialize());
                }
                else
                {
                    #region Hit Damage

                    var critBonusMultiplier = 0.00;
                    var blocked = false;
                    var finalDmg = CalculateDamagePlayer(tamer, out critBonusMultiplier, out blocked);

                    #endregion

                    if (finalDmg <= 0) finalDmg = 1;
                    if (finalDmg > tamer.TargetPartner.CurrentHp) finalDmg = tamer.TargetPartner.CurrentHp;

                    var newHp = tamer.TargetPartner.ReceiveDamage(finalDmg);

                    var hitType = blocked ? 2 : critBonusMultiplier > 0 ? 1 : 0;

                    if (newHp > 0)
                    {
                        _logger.Warning(
                            $"Partner {tamer.Partner.Id} inflicted {finalDmg} to partner {tamer.TargetPartner?.Id} - {tamer.TargetPartner?.Name}.");

                        BroadcastForTamerViewsAndSelf(
                            tamer.Id,
                            new HitPacket(
                                tamer.Partner.GeneralHandler,
                                tamer.TargetPartner.GeneralHandler,
                                finalDmg,
                                tamer.TargetPartner.HP,
                                newHp,
                                hitType).Serialize());
                    }
                    else
                    {
                        _logger.Warning(
                            $"Partner {tamer.Partner.Id} killed partner {tamer.TargetPartner?.Id} - {tamer.TargetPartner?.Name} with {finalDmg} damage.");

                        BroadcastForTamerViewsAndSelf(
                            tamer.Id,
                            new KillOnHitPacket(
                                tamer.Partner.GeneralHandler,
                                tamer.TargetPartner.GeneralHandler,
                                finalDmg,
                                hitType).Serialize());

                        tamer.TargetPartner.Character.Die();

                        if (!EnemiesAttacking(tamer.Location.MapId, tamer.Partner.Id, tamer.Id))
                        {
                            tamer.StopBattle();

                            BroadcastForTamerViewsAndSelf(
                                tamer.Id,
                                new SetCombatOffPacket(tamer.Partner.GeneralHandler).Serialize());
                        }
                    }
                }

                tamer.Partner.UpdateLastHitTime();
            }

            bool StopAttackPlayer = tamer.TargetPartner == null || !tamer.TargetPartner.Alive || tamer.Partner.HP < 1;

            if (StopAttackPlayer) tamer.Partner?.StopAutoAttack();
        }

        public void PartnerAutoAttackMob(GameClient client, CharacterModel tamer)
        {
            if (!tamer.Partner.AutoAttack || tamer.TargetIMob == null)
                return;

            if (tamer.Partner.CanAutoAttack() && tamer.Partner.CanAttack() && tamer.TargetIMob != null && tamer.TargetIMob.Alive & tamer.Partner.Alive)
            {
                tamer.Partner.SetEndAttacking();
                tamer.SetHidden(false);

                if (!tamer.InBattle)
                {
                    BroadcastForTamerViewsAndSelf(tamer.Id, new SetCombatOnPacket(tamer.Partner.GeneralHandler).Serialize());
                    tamer.StartBattle(tamer.TargetIMob);
                }

                if (!tamer.TargetIMob.InBattle)
                {
                    BroadcastForTamerViewsAndSelf(tamer.Id, new SetCombatOnPacket(tamer.TargetIMob.GeneralHandler).Serialize());
                    tamer.TargetIMob.StartBattle(tamer);
                }

                var missed = false;

                if (!tamer.GodMode)
                    missed = tamer.CanMissHit();

                if (missed)
                {
                    BroadcastForTamerViewsAndSelf(tamer.Id, new MissHitPacket(tamer.Partner.GeneralHandler, tamer.TargetIMob.GeneralHandler).Serialize());
                }
                else
                {
                    var critBonusMultiplier = 0.00;

                    var blocked = false;
                    int finalDamage = tamer.GodMode ? tamer.TargetIMob.CurrentHP : AttackManager.CalculateDamage(client, out critBonusMultiplier, out blocked);

                    finalDamage = Math.Clamp(finalDamage, 1, tamer.TargetIMob.CurrentHP);

                    int newHp = tamer.TargetIMob.ReceiveDamage(finalDamage, tamer.Id);
                    int hitType = blocked ? 2 : critBonusMultiplier > 0 ? 1 : 0;

                    if (newHp > 0)
                    {
                        BroadcastForTamerViewsAndSelf(tamer.Id,
                            new HitPacket(tamer.Partner.GeneralHandler, tamer.TargetIMob.GeneralHandler, finalDamage, tamer.TargetIMob.HPValue, newHp, hitType).Serialize());
                    }
                    else
                    {
                        BroadcastForTamerViewsAndSelf(tamer.Id,
                            new KillOnHitPacket(tamer.Partner.GeneralHandler, tamer.TargetIMob.GeneralHandler, finalDamage, hitType).Serialize());

                        tamer.TargetIMob?.Die();
                        tamer.TargetIMob?.UpdateCurrentAction(Commons.Enums.Map.MobActionEnum.Wait);

                        if (!MobsAttacking(tamer.Location.MapId, tamer.Id, tamer.Channel))
                        {
                            tamer.StopBattle();

                            BroadcastForTamerViewsAndSelf(tamer.Id,
                                new SetCombatOffPacket(tamer.Partner.GeneralHandler).Serialize());
                        }
                    }
                }

                tamer.Partner.UpdateLastHitTime();
            }

            bool StopAttackMob = tamer.TargetIMob == null || tamer.TargetIMob.Dead;

            if (StopAttackMob)
                tamer.SetCombatOff = false;

            if (StopAttackMob)
                tamer.Partner?.StopAutoAttack();
        }

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

        private void CheckAndHandleVerdandiDebuff(GameClient client)
        {
            if (client.Tamer.Location.MapId != 1700)
            {
                RemoveVerdandiDebuff(client);
                return;
            }

            var map = Maps.FirstOrDefault(x => x.MapId == 1700 && x.Channel == client.Tamer.Channel);
            var mob = map?.Mobs.FirstOrDefault(m => m.Type == 72100);

            if (mob == null)
            {
                RemoveVerdandiDebuff(client);
                return;
            }

            var evolutionType = _assets.DigimonBaseInfo.First(x => x.Type == client.Partner.CurrentType).EvolutionType;

            if (evolutionType is (int)EvolutionRankEnum.RookieX or (int)EvolutionRankEnum.ChampionX or
                (int)EvolutionRankEnum.UltimateX or (int)EvolutionRankEnum.MegaX or
                (int)EvolutionRankEnum.BurstModeX or (int)EvolutionRankEnum.JogressX)
            {
                RemoveVerdandiDebuff(client);
                return;
            }

            if (client.Partner.BuffList.ActiveBuffs.Any(x => x.BuffId == 64001))
            {
                RemoveVerdandiDebuff(client);
                return;
            }

            if (mob.Dead)
            {
                if (mob.DeathTime.HasValue)
                {
                    var timeSinceDeath = DateTime.UtcNow - mob.DeathTime.Value;
                    if (timeSinceDeath < TimeSpan.FromHours(2))
                    {
                        RemoveVerdandiDebuff(client);
                        return;
                    }
                    else
                    {
                        ApplyVerdandiDebuff(client);
                        return;
                    }
                }
                else
                {
                    RemoveVerdandiDebuff(client);
                    return;
                }
            }

            ApplyVerdandiDebuff(client);
        }


        private void ApplyVerdandiDebuff(GameClient client)
        {
            if (client.Tamer.Location.MapId != 1700)
                return;

            if (client.Partner.BuffList.ActiveBuffs.Any(x => x.BuffId == 64001))
                return; // X-Protector evita aplicação

            var debuffAsset = _assets.BuffInfo.FirstOrDefault(x => x.BuffId == 64000);
            if (debuffAsset == null)
                return;

            var hasDebuff = client.Partner.DebuffList.ActiveBuffs.Any(x => x.BuffId == 64000);
            if (!hasDebuff)
            {
                var newDebuff = DigimonDebuffModel.Create(
                    debuffAsset.BuffId,
                    debuffAsset.SkillId,
                    0,
                    debuffAsset.TimeType
                );
                newDebuff.SetBuffInfo(debuffAsset);

                client.Partner.DebuffList.Add(newDebuff);
                client.Send(new AddBuffPacket(client.Partner.GeneralHandler, debuffAsset, 0, (uint)debuffAsset.TimeType).Serialize());

                StartVerdandiDebuffEffect(client);
            }
        }


        private void StartVerdandiDebuffEffect(GameClient client)
        {
            if (_verdandiDebuffTokens.TryGetValue(client.TamerId, out var oldToken))
            {
                oldToken.Cancel();
                _verdandiDebuffTokens.Remove(client.TamerId);
            }

            var cts = new CancellationTokenSource();
            _verdandiDebuffTokens[client.TamerId] = cts;

            _ = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    if (client.Tamer == null || client.Partner == null || client.Tamer.Location.MapId != 1700)
                    {
                        RemoveVerdandiDebuff(client);
                        break;
                    }

                    if (client.Partner.BuffList.ActiveBuffs.Any(x => x.BuffId == 64001))
                    {
                        RemoveVerdandiDebuff(client);
                        await Task.Delay(2000, cts.Token);
                        continue;
                    }

                    var map = Maps.FirstOrDefault(x => x.MapId == 1700 && x.Channel == client.Tamer.Channel);
                    var mob = map?.Mobs.FirstOrDefault(m => m.Type == 72100);

                    if (mob == null)
                    {
                        RemoveVerdandiDebuff(client);
                        await Task.Delay(5000, cts.Token);
                        continue;
                    }

                    if (mob.Dead)
                    {
                        if (mob.DeathTime.HasValue)
                        {
                            var timeSinceDeath = DateTime.UtcNow - mob.DeathTime.Value;
                            if (timeSinceDeath < TimeSpan.FromHours(2))
                            {
                                RemoveVerdandiDebuff(client);
                                await Task.Delay(10000, cts.Token);
                                continue;
                            }
                            else
                            {
                                RemoveVerdandiDebuff(client); // <- ALTERAÇÃO: remove mesmo depois das 2 horas
                                await Task.Delay(10000, cts.Token);
                                continue;
                            }
                        }
                        else
                        {
                            RemoveVerdandiDebuff(client);
                            await Task.Delay(5000, cts.Token);
                            continue;
                        }
                    }

                    // Se chegou aqui, o mob está vivo — aí sim pode aplicar o debuff se necessário
                    if (!client.Partner.DebuffList.ActiveBuffs.Any(x => x.BuffId == 64000))
                        ApplyVerdandiDebuff(client);

                    await _sender.Send(new UpdateDigimonBuffListCommand(client.Partner.BuffList));
                    client.Send(new UpdateStatusPacket(client.Tamer));

                    // Dano contínuo
                    if (client.Partner.NextHpLossTime <= DateTime.UtcNow)
                    {
                        int currentHp = client.Partner.CurrentHp;
                        if (currentHp > 1)
                        {
                            int damageToApply = Math.Min(100, currentHp - 1);
                            client.Partner.ReceiveDamage(damageToApply);
                            client.Partner.NextHpLossTime = DateTime.UtcNow.AddSeconds(10);
                            client.Send(new UpdateStatusPacket(client.Tamer));
                        }
                    }

                    await Task.Delay(1000, cts.Token);
                }
            }, cts.Token);
        }



        private async void RemoveVerdandiDebuff(GameClient client)
        {
            try
            {
                var tamer = client?.Tamer;
                var partner = client?.Partner ?? tamer?.Partner;
                if (tamer == null || partner == null)
                    return;

                var debuff = partner.DebuffList?.ActiveBuffs?.FirstOrDefault(x => x.BuffId == 64000);
                if (debuff != null)
                {
                    try
                    {
                        partner.DebuffList.Remove(64000);
                        client.Send(new RemoveBuffPacket(partner.GeneralHandler, 64000).Serialize());
                        await _sender.Send(new UpdateDigimonBuffListCommand(partner.BuffList))
                                     .ConfigureAwait(false);
                    }
                    catch (Exception exUpd)
                    {
                        _logger.Error(exUpd, "Falha ao remover Verdandi Debuff 64000 (tamerId={TamerId})", tamer.Id);
                    }
                }

                if (_verdandiDebuffTokens.TryGetValue(tamer.Id, out var token))
                {
                    token.Cancel();
                    _verdandiDebuffTokens.Remove(tamer.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "RemoveVerdandiDebuff fault (tamerId={TamerId})", client?.TamerId);
            }
        }



        private async void CheckVerdandiBlessBuff(GameClient client)
        {
            try
            {
                var tamer = client?.Tamer;
                var partner = client?.Partner ?? tamer?.Partner;
                if (tamer == null || partner == null)
                    return;

                if (tamer.Location.MapId != 1700)
                    return;

                // lógica centralizada (mantida)
                try { CheckAndHandleVerdandiDebuff(client); }
                catch (Exception exChk)
                {
                    _logger.Error(exChk, "CheckAndHandleVerdandiDebuff falhou (tamerId={TamerId})", tamer.Id);
                }

                // remove Bless (64002) se o mob-alvo estiver vivo (map 1700, mob 72100)
                var map = Maps?.FirstOrDefault(x => x.MapId == 1700 && x.Channel == tamer.Channel);
                var mob = map?.Mobs?.FirstOrDefault(m => m.Type == 72100);

                if (mob != null && !mob.Dead)
                {
                    var blessBuff = partner.BuffList?.ActiveBuffs?.FirstOrDefault(x => x.BuffId == 64002);
                    if (blessBuff != null)
                    {
                        try
                        {
                            partner.BuffList.Remove(64002);
                            client.Send(new RemoveBuffPacket(partner.GeneralHandler, 64002).Serialize());
                            await _sender.Send(new UpdateDigimonBuffListCommand(partner.BuffList))
                                         .ConfigureAwait(false);
                        }
                        catch (Exception exRem)
                        {
                            _logger.Error(exRem, "Falha ao remover Bless 64002 (tamerId={TamerId})", tamer.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "CheckVerdandiBlessBuff fault (tamerId={TamerId})", client?.TamerId);
            }
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

        private static int CalculateDamagePlayer(CharacterModel tamer, out double critBonusMultiplier, out bool blocked)
        {
            var baseDamage = (tamer.Partner.AT / tamer.TargetPartner.DE * 150) + UtilitiesFunctions.RandomInt(5, 50);
            if (baseDamage < 0) baseDamage = 0;

            critBonusMultiplier = 0.00;
            double critChance = tamer.Partner.CC / 100;

            if (critChance >= UtilitiesFunctions.RandomDouble())
            {
                var vlrAtual = tamer.Partner.CD;
                var bonusMax = 1.00; //TODO: externalizar?
                var expMax = 10000; //TODO: externalizar?

                critBonusMultiplier = (bonusMax * vlrAtual) / expMax;
            }

            blocked = tamer.TargetPartner.BL >= UtilitiesFunctions.RandomDouble();

            var levelBonusMultiplier = tamer.Partner.Level > tamer.TargetPartner.Level
                ? (0.01f * (tamer.Partner.Level - tamer.TargetPartner.Level))
                : 0; //TODO: externalizar no portal

            var attributeMultiplier = 0.00;

            if (tamer.Partner.BaseInfo.Attribute.HasAttributeAdvantage(tamer.TargetPartner.BaseInfo.Attribute))
            {
                var vlrAtual = tamer.Partner.GetAttributeExperience();
                var bonusMax = 1.00; //TODO: externalizar?
                var expMax = 10000; //TODO: externalizar?

                attributeMultiplier = (bonusMax * vlrAtual) / expMax;
            }
            else if (tamer.TargetPartner.BaseInfo.Attribute.HasAttributeAdvantage(tamer.Partner.BaseInfo.Attribute))
            {
                attributeMultiplier = -0.25;
            }

            var elementMultiplier = 0.00;

            if (tamer.Partner.BaseInfo.Element.HasElementAdvantage(tamer.TargetPartner.BaseInfo.Element))
            {
                var vlrAtual = tamer.Partner.GetElementExperience();
                var bonusMax = 0.5; //TODO: externalizar?
                var expMax = 10000; //TODO: externalizar?

                elementMultiplier = (bonusMax * vlrAtual) / expMax;
            }
            else if (tamer.TargetPartner.BaseInfo.Element.HasElementAdvantage(tamer.Partner.BaseInfo.Element))
            {
                elementMultiplier = -0.50;
            }

            baseDamage /= blocked ? 2 : 1;

            return (int)Math.Floor(baseDamage +
                                   (baseDamage * critBonusMultiplier) +
                                   (baseDamage * levelBonusMultiplier) +
                                   (baseDamage * attributeMultiplier) +
                                   (baseDamage * elementMultiplier));
        }

        // -----------------------------------------------------------------------------------------------------------------------

        private async void CheckTimeReward(GameClient client)
        {
            var tr = client.Tamer.TimeReward;

            if (tr.ReedemRewards)
            {
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

        // Yokohama Teleports
        private async void AutomaticTeleportYokohama(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 105)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 4024);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 4024 / 8;
            int questBit = 4024 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 4024);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 32512;
            int destY = 29692;

            // Update tamer location
            client.Tamer.NewLocation(105, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(105, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(4024, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 4024);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }
        private async void AutomaticTeleportYokohama2(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 105)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 4027);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 4027 / 8;
            int questBit = 4027 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 4027);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 29217;
            int destY = 34227;

            // Update tamer location
            client.Tamer.NewLocation(105, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(105, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(4027, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 4027);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }
        private async void AutomaticTeleportYokohama3(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 105)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 4036);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 4036 / 8;
            int questBit = 4036 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 4036);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 31567;
            int destY = 11448;

            // Update tamer location
            client.Tamer.NewLocation(105, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(105, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(4036, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 4036);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }
        private async void AutomaticTeleportYokohama4(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 105)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 4039);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 4039 / 8;
            int questBit = 4039 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 4039);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 21197;
            int destY = 7787;

            // Update tamer location
            client.Tamer.NewLocation(105, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(105, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(4039, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 4039);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }
        private async void AutomaticTeleportYokohama5(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 105)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 4042);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 4042 / 8;
            int questBit = 4042 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 4042);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 13211;
            int destY = 3258;

            // Update tamer location
            client.Tamer.NewLocation(105, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(105, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(4042, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 4042);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }
        private async void AutomaticTeleportYokohama6(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 105)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 4045);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 4045 / 8;
            int questBit = 4045 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 4045);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 40488;
            int destY = 4920;

            // Update tamer location
            client.Tamer.NewLocation(105, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(105, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(4045, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 4045);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }
        private async void AutomaticTeleportYokohama7(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 105)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 4050);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 4050 / 8;
            int questBit = 4050 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 4050);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 45644;
            int destY = 3376;

            // Update tamer location
            client.Tamer.NewLocation(105, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(105, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(4050, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 4050);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }
        private async void AutomaticTeleportYokohama8ToDats(GameClient client) // Loading Teleport
        {
            try
            {
                // Check if client is valid
                if (client?.Tamer == null)
                    return;

                // Target map is DATS HQ - Map ID 3
                const int targetMapId = 3;

                // Check if the tamer has the specific quest
                var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 4051);
                if (!hasYokohamaQuest)
                    return;

                // Check if the quest is already completed
                int questIndex = 4051 / 8;
                int questBit = 4051 % 8;
                if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                    (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
                {
                    // Quest already completed, don't continue with teleport
                    return;
                }

                // Check if this tamer has already been teleported
                var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 4051);
                if (questData == null || questData.GetGoalValue(0) == 1)
                    return; // Already teleported or quest data is missing

                // Set up the teleport coordinates for DATS HQ
                int destX = 20053;
                int destY = 38099;

                // Set the tamer's channel to 0 (default channel for DATS)
                client.Tamer.SetCurrentChannel(0);

                // Remove client from current map
                if (client.DungeonMap && _dungeonServer != null)
                {
                    _dungeonServer.RemoveClient(client);
                }
                else if (_mapServer != null) // If not in dungeon map, use regular map server
                {
                    _mapServer.RemoveClient(client);
                }

                // Update tamer location to the new map
                client.Tamer.NewLocation(targetMapId, destX, destY);
                await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

                // Update partner location to the new map
                client.Tamer.Partner.NewLocation(targetMapId, destX, destY);
                await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

                // Set the character state to loading
                client.Tamer.UpdateState(CharacterStateEnum.Loading);
                await _sender.Send(new UpdateCharacterStateCommand(client.TamerId, CharacterStateEnum.Loading));

                // Ensure the client isn't marked as quitting
                client.SetGameQuit(false);

                // Get server address and port from configuration
                string serverAddress = "127.0.0.1"; // Default fallback value
                string serverPort = "7607";       // Default fallback value

                if (_configuration != null)
                {
                    serverAddress = _configuration[GamerServerPublic] ?? serverAddress;
                    serverPort = _configuration[GameServerPort] ?? serverPort;
                }

                // Send the map swap packet for cross-map teleportation
                client.Send(new MapSwapPacket(
                    serverAddress,
                    serverPort,
                    targetMapId,
                    destX,
                    destY
                ));

                // Mark that this tamer has been teleported by setting goal value to 1
                client.Tamer.Progress.UpdateQuestInProgress(4051, 0, 1);

                // Update the quest progress
                var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 4051);
                if (inProgressQuest != null)
                {
                    await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
                }

                // Log the teleport
                _logger.Debug($"Automatically teleported tamer {client.TamerId} from Yokohama to DATS HQ at X:{destX}, Y:{destY}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error during teleport to DATS HQ: {ex.Message}");
                _logger.Error(ex.StackTrace); // Log stack trace for debugging
            }
        }

        // Dats Teleports
        private async void DatsToSilverLake(GameClient client) // Loading Teleport
        {
            try
            {
                // Check if client is valid
                if (client?.Tamer == null)
                    return;

                // Only proceed if the player is in DATS (Map ID 3)
                if (client.Tamer.Location.MapId != 3)
                    return;

                // Target map is Silver Lake - Map ID 1301
                const int targetMapId = 1301;

                // Check if the tamer has the specific quest
                var hasDatsToSilverLakeQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 4053);
                if (!hasDatsToSilverLakeQuest)
                    return;

                // Check if the quest is already completed
                int questIndex = 4053 / 8;
                int questBit = 4053 % 8;
                if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                    (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
                {
                    // Quest already completed, don't continue with teleport
                    return;
                }

                // Check if this tamer has already been teleported
                var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 4053);
                if (questData == null || questData.GetGoalValue(0) == 1)
                    return; // Already teleported or quest data is missing

                // Set up the teleport coordinates for Silver Lake
                int destX = 10261;
                int destY = 47261;

                // Set the tamer's channel to 0 (default channel)
                client.Tamer.SetCurrentChannel(0);

                // Remove client from current map
                if (client.DungeonMap && _dungeonServer != null)
                {
                    _dungeonServer.RemoveClient(client);
                }
                else if (_mapServer != null)
                {
                    _mapServer.RemoveClient(client);
                }

                // Update tamer location to the new map
                client.Tamer.NewLocation(targetMapId, destX, destY);
                await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

                // Update partner location to the new map
                client.Tamer.Partner.NewLocation(targetMapId, destX, destY);
                await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

                // Set the character state to loading
                client.Tamer.UpdateState(CharacterStateEnum.Loading);
                await _sender.Send(new UpdateCharacterStateCommand(client.TamerId, CharacterStateEnum.Loading));

                // Ensure the client isn't marked as quitting
                client.SetGameQuit(false);

                // Get server address and port from configuration
                string serverAddress = "127.0.0.1"; // Default fallback value
                string serverPort = "7607";       // Default fallback value

                if (_configuration != null)
                {
                    serverAddress = _configuration[GamerServerPublic] ?? serverAddress;
                    serverPort = _configuration[GameServerPort] ?? serverPort;
                }

                // Send the map swap packet for cross-map teleportation
                client.Send(new MapSwapPacket(
                    serverAddress,
                    serverPort,
                    targetMapId,
                    destX,
                    destY
                ));

                // Mark that this tamer has been teleported by setting goal value to 1
                client.Tamer.Progress.UpdateQuestInProgress(4053, 0, 1);

                // Update the quest progress
                var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 4053);
                if (inProgressQuest != null)
                {
                    await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
                }

                // Log the teleport
                _logger.Debug($"Automatically teleported tamer {client.TamerId} from Dats to SilverLake HQ at X:{destX}, Y:{destY}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error during teleport to SilverLake HQ: {ex.Message}");
                _logger.Error(ex.StackTrace); // Log stack trace for debugging
            }
        }

        // Silver Lake Teleports
        private async void SilverLakeQuest1(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 1301)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 2886);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 2886 / 8;
            int questBit = 2886 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 2886);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 40229;
            int destY = 36308;

            // Update tamer location
            client.Tamer.NewLocation(1301, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(1301, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(2886, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 2886);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }
        private async void SilverLakeQuest2(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 1301)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 2893);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 2893 / 8;
            int questBit = 2893 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 2893);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 44067;
            int destY = 24500;

            // Update tamer location
            client.Tamer.NewLocation(1301, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(1301, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(2893, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 2893);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }
        private async void SilverLakeQuest3(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 1301)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 2910);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 2910 / 8;
            int questBit = 2910 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 2910);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 25381;
            int destY = 29364;

            // Update tamer location
            client.Tamer.NewLocation(1301, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(1301, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(2910, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 2910);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }
        private async void SilverLakeQuest4(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 1301)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 2912);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 2912 / 8;
            int questBit = 2912 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 2912);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 11129;
            int destY = 33791;

            // Update tamer location
            client.Tamer.NewLocation(1301, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(1301, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(2912, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 2912);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }
        private async void SilverLakeQuest5(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 1301)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 2919);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 2919 / 8;
            int questBit = 2919 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 2919);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 31093;
            int destY = 13109;

            // Update tamer location
            client.Tamer.NewLocation(1301, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(1301, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(2919, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 2919);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }
        private async void SilverLakeToSilentForest(GameClient client) // Loading Teleport
        {
            try
            {
                // Check if client is valid
                if (client?.Tamer == null)
                    return;

                // Target map is SilentForest HQ - Map ID 3
                const int targetMapId = 1302;

                // Check if the tamer has the specific quest
                var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 2922);
                if (!hasYokohamaQuest)
                    return;

                // Check if the quest is already completed
                int questIndex = 2922 / 8;
                int questBit = 2922 % 8;
                if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                    (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
                {
                    // Quest already completed, don't continue with teleport
                    return;
                }

                // Check if this tamer has already been teleported
                var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 2922);
                if (questData == null || questData.GetGoalValue(0) == 1)
                    return; // Already teleported or quest data is missing

                // Set up the teleport coordinates for DATS HQ
                int destX = 22759;
                int destY = 10623;

                // Set the tamer's channel to 0 (default channel for DATS)
                client.Tamer.SetCurrentChannel(0);

                // Remove client from current map
                if (client.DungeonMap && _dungeonServer != null)
                {
                    _dungeonServer.RemoveClient(client);
                }
                else if (_mapServer != null) // If not in dungeon map, use regular map server
                {
                    _mapServer.RemoveClient(client);
                }

                // Update tamer location to the new map
                client.Tamer.NewLocation(targetMapId, destX, destY);
                await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

                // Update partner location to the new map
                client.Tamer.Partner.NewLocation(targetMapId, destX, destY);
                await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

                // Set the character state to loading
                client.Tamer.UpdateState(CharacterStateEnum.Loading);
                await _sender.Send(new UpdateCharacterStateCommand(client.TamerId, CharacterStateEnum.Loading));

                // Ensure the client isn't marked as quitting
                client.SetGameQuit(false);

                // Get server address and port from configuration
                string serverAddress = "127.0.0.1"; // Default fallback value
                string serverPort = "7607";       // Default fallback value

                if (_configuration != null)
                {
                    serverAddress = _configuration[GamerServerPublic] ?? serverAddress;
                    serverPort = _configuration[GameServerPort] ?? serverPort;
                }

                // Send the map swap packet for cross-map teleportation
                client.Send(new MapSwapPacket(
                    serverAddress,
                    serverPort,
                    targetMapId,
                    destX,
                    destY
                ));

                // Mark that this tamer has been teleported by setting goal value to 1
                client.Tamer.Progress.UpdateQuestInProgress(2922, 0, 1);

                // Update the quest progress
                var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 2922);
                if (inProgressQuest != null)
                {
                    await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
                }

                // Log the teleport
                _logger.Debug($"Automatically teleported tamer {client.TamerId} from Yokohama to DATS HQ at X:{destX}, Y:{destY}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error during teleport to DATS HQ: {ex.Message}");
                _logger.Error(ex.StackTrace); // Log stack trace for debugging
            }
        }

        // Silent forest Teleports
        private async void SilentQuest1(GameClient client) // 2 x quests checking Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 1302)
                return;

            // Check if the tamer has BOTH specific quests (2992 and 2991)
            var hasQuest2992 = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 2992);
            var hasQuest2991 = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 2991);

            // Only proceed if both quests are active
            if (!hasQuest2992 || !hasQuest2991)
                return;

            // Check if either quest is already completed
            int questIndex2992 = 2992 / 8;
            int questBit2992 = 2992 % 8;
            bool isQuest2992Completed = questIndex2992 < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex2992] & (1 << questBit2992)) != 0;

            int questIndex2991 = 2991 / 8;
            int questBit2991 = 2991 % 8;
            bool isQuest2991Completed = questIndex2991 < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex2991] & (1 << questBit2991)) != 0;

            // If either quest is completed, don't teleport
            if (isQuest2992Completed || isQuest2991Completed)
                return;

            // Check if this tamer has already been teleported by looking at quest 2992's goal value
            var questData2992 = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 2992);
            if (questData2992 == null || questData2992.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 33744;
            int destY = 48519;

            // Update tamer location
            client.Tamer.NewLocation(1302, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(1302, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1 for both quests
            client.Tamer.Progress.UpdateQuestInProgress(2992, 0, 1);
            client.Tamer.Progress.UpdateQuestInProgress(2991, 0, 1);

            // Update the quest progress for both quests
            var inProgressQuest2992 = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 2992);
            if (inProgressQuest2992 != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest2992));
            }

            var inProgressQuest2991 = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 2991);
            if (inProgressQuest2991 != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest2991));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Silent Forest coordinates X:{destX}, Y:{destY} - Has quests 2992 and 2991");
        }
        private async void SilentQuest2(GameClient client) // 2x times Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 1302)
                return;

            // Check if the tamer has the specific quest
            var hasQuest3004 = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 3004);
            if (!hasQuest3004)
                return;

            // Check if the quest is already completed
            int questIndex = 3004 / 8;
            int questBit = 3004 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Get the quest data to check teleport state
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 3004);
            if (questData == null)
                return; // Quest data is missing

            // Check teleport state using goal value
            // 0 = not teleported yet
            // 1 = teleported to first location
            // 2 = teleported back to second location - complete
            byte teleportState = questData.GetGoalValue(0);

            if (teleportState >= 2)
                return; // Teleport sequence already completed

            try
            {
                // Set teleport coordinates based on state
                int destX, destY;

                if (teleportState == 0)
                {
                    // First teleport - to target location
                    destX = 60009;
                    destY = 56318;

                    // Update teleport state
                    client.Tamer.Progress.UpdateQuestInProgress(3004, 0, 1);
                }
                else // teleportState == 1
                {
                    // Second teleport - back to origin
                    destX = 33640;
                    destY = 48638;

                    // Update teleport state
                    client.Tamer.Progress.UpdateQuestInProgress(3004, 0, 2);
                }

                // Update tamer location
                client.Tamer.NewLocation(1302, destX, destY);
                await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

                // Update partner location
                client.Tamer.Partner.NewLocation(1302, destX, destY);
                await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

                // Send local teleport packet (no loading screen)
                client.Send(new LocalMapSwapPacket(
                    client.Tamer.GeneralHandler,
                    client.Tamer.Partner.GeneralHandler,
                    destX, destY,
                    destX, destY
                ));

                // Update the quest progress
                var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 3004);
                if (inProgressQuest != null)
                {
                    await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
                }

                // Log the teleport
                _logger.Debug($"Automatically teleported tamer {client.TamerId} in Silent Forest to coordinates X:{destX}, Y:{destY} (State: {teleportState + 1}/2)");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error during teleport in Silent Forest: {ex.Message}");
                _logger.Error(ex.StackTrace); // Log stack trace for debugging
            }
        }
        //private async void SilentQuest3(GameClient client) // Local Teleport
        //{
        //    // Check if client is valid and in the correct map
        //    if (client?.Tamer?.Location?.MapId != 1302)
        //        return;

        //    // Check if the tamer has the specific quest
        //    var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 3005);
        //    if (!hasYokohamaQuest)
        //        return;

        //    // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
        //    // Each quest ID bit is stored in the CompletedData byte array
        //    int questIndex = 3005 / 8;
        //    int questBit = 3005 % 8;
        //    if (questIndex < client.Tamer.Progress.CompletedData.Length &&
        //        (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
        //    {
        //        // Quest already completed, don't continue with teleport
        //        return;
        //    }

        //    // Check if this tamer has already been teleported by looking at a special quest flag
        //    // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
        //    var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 3005);
        //    if (questData == null || questData.GetGoalValue(0) == 1)
        //        return; // Already teleported or quest data is missing, don't teleport again

        //    // Set up the teleport coordinates
        //    int destX = 33640;
        //    int destY = 48638;

        //    // Update tamer location
        //    client.Tamer.NewLocation(1302, destX, destY);
        //    await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

        //    // Update partner location
        //    client.Tamer.Partner.NewLocation(1302, destX, destY);
        //    await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

        //    // Send local teleport packet (no loading screen)
        //    client.Send(new LocalMapSwapPacket(
        //        client.Tamer.GeneralHandler,
        //        client.Tamer.Partner.GeneralHandler,
        //        destX, destY,
        //        destX, destY
        //    ));

        //    // Mark that this tamer has been teleported by setting goal value to 1
        //    client.Tamer.Progress.UpdateQuestInProgress(3005, 0, 1);

        //    // Use the correct command with the correct parameter
        //    var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 3005);
        //    if (inProgressQuest != null)
        //    {
        //        await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
        //    }

        //    // Log the teleport for debugging purposes
        //    _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        //}
        private async void SilentQuest4(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 1302)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 3009);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 3009 / 8;
            int questBit = 3009 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 3009);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 71099;
            int destY = 38017;

            // Update tamer location
            client.Tamer.NewLocation(1302, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(1302, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(3009, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 3009);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }
        private async void SilentQuest5(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 1302)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 3008);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 3008 / 8;
            int questBit = 3008 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 3008);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 71099;
            int destY = 38017;

            // Update tamer location
            client.Tamer.NewLocation(1302, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(1302, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(3008, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 3008);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }

        // Lost historic Teleports
        private async void LostHistoricQuest1(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 1303)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 951);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 951 / 8;
            int questBit = 951 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 951);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 25046;
            int destY = 44634;

            // Update tamer location
            client.Tamer.NewLocation(1303, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(1303, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(951, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 951);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }
        //private async void LostHistoricQuest2(GameClient client) // Local Teleport
        //{
            // Check if client is valid and in the correct map
            //if (client?.Tamer?.Location?.MapId != 1303)
                //return;

            // Check if the tamer has the specific quest
            //var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 1030);
            //if (!hasYokohamaQuest)
                //return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            //int questIndex = 1030 / 8;
            //int questBit = 1030 % 8;
            //if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                //(client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            //{
                // Quest already completed, don't continue with teleport
                //return;
            //}

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            //var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 1030);
            //if (questData == null || questData.GetGoalValue(0) == 1)
                //return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            //int destX = 27057;
            //int destY = 27164;

            // Update tamer location
            //client.Tamer.NewLocation(1303, destX, destY);
            //await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            //client.Tamer.Partner.NewLocation(1303, destX, destY);
            //await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            //client.Send(new LocalMapSwapPacket(
                //client.Tamer.GeneralHandler,
                //client.Tamer.Partner.GeneralHandler,
                //destX, destY,
                //destX, destY
            //));

            // Mark that this tamer has been teleported by setting goal value to 1
            //client.Tamer.Progress.UpdateQuestInProgress(1030, 0, 1);

            // Use the correct command with the correct parameter
            //var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 1030);
            //if (inProgressQuest != null)
            //{
                //await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            //}

            // Log the teleport for debugging purposes
            //_logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        //}
        private async void LostHistoricQuest3(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 1303)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 954);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 954 / 8;
            int questBit = 954 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 954);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 14245;
            int destY = 14418;

            // Update tamer location
            client.Tamer.NewLocation(1303, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(1303, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(954, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 954);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }
        private async void LostHistoricQuest4(GameClient client) // Local Teleport
        {
            try
            {
                // Check if client is valid and in the correct map
                if (client?.Tamer?.Location?.MapId != 1303)
                    return;

                // Check if the tamer has quest ID 957
                var quest957 = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 957);
                if (quest957 == null)
                    return; // Quest not found, don't teleport

                // Check if this tamer has already been teleported (using goal index 0 as tracking)
                byte teleportState = quest957.GetGoalValue(0);
                if (teleportState == 1)
                    return; // Already teleported for this quest, don't teleport again

                // Check if the quest is already completed (we don't teleport if it's completed)
                int questIndex = 957 / 8;
                int questBit = 957 % 8;
                bool questCompleted = questIndex < client.Tamer.Progress.CompletedData.Length &&
                    (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0;
                if (questCompleted)
                    return; // Quest completed, don't teleport

                // Set up the teleport coordinates
                int destX = 46377;
                int destY = 24291;

                // Log the teleport attempt
                _logger.Debug($"Teleporting tamer {client.TamerId} to coordinates X:{destX}, Y:{destY} for quest 957");

                // Update tamer location
                client.Tamer.NewLocation(1303, destX, destY);
                await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

                // Update partner location
                client.Tamer.Partner.NewLocation(1303, destX, destY);
                await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

                // Send local teleport packet (no loading screen)
                client.Send(new LocalMapSwapPacket(
                    client.Tamer.GeneralHandler,
                    client.Tamer.Partner.GeneralHandler,
                    destX, destY,
                    destX, destY
                ));

                // Mark that this tamer has been teleported by setting goal value to 1
                // This prevents future teleports for this quest
                client.Tamer.Progress.UpdateQuestInProgress(957, 0, 1);

                // Persist the change to database
                await _sender.Send(new UpdateCharacterInProgressCommand(quest957));

                // Log the successful teleport
                _logger.Debug($"Successfully teleported tamer {client.TamerId} to coordinates X:{destX}, Y:{destY} for quest 957");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error teleporting for quest 957: {ex.Message}");
            }
        }
        private async void LostHistoricQuest5(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 1303)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 960);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 960 / 8;
            int questBit = 960 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 960);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 63571;
            int destY = 36345;

            // Update tamer location
            client.Tamer.NewLocation(1303, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(1303, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(960, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 960);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }
        private async void LostHistoricToFileIsland(GameClient client) // Loading Teleport
        {
            try
            {
                // Check if client is valid
                if (client?.Tamer == null)
                    return;

                // Target map is SilentForest HQ - Map ID 3
                const int targetMapId = 1305;

                // Check if the tamer has the specific quest
                var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 967);
                if (!hasYokohamaQuest)
                    return;

                // Check if the quest is already completed
                int questIndex = 967 / 8;
                int questBit = 967 % 8;
                if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                    (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
                {
                    // Quest already completed, don't continue with teleport
                    return;
                }

                // Check if this tamer has already been teleported
                var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 967);
                if (questData == null || questData.GetGoalValue(0) == 1)
                    return; // Already teleported or quest data is missing

                // Set up the teleport coordinates for DATS HQ
                int destX = 52925;
                int destY = 27497;

                // Set the tamer's channel to 0 (default channel for DATS)
                client.Tamer.SetCurrentChannel(0);

                // Remove client from current map
                if (client.DungeonMap && _dungeonServer != null)
                {
                    _dungeonServer.RemoveClient(client);
                }
                else if (_mapServer != null) // If not in dungeon map, use regular map server
                {
                    _mapServer.RemoveClient(client);
                }

                // Update tamer location to the new map
                client.Tamer.NewLocation(targetMapId, destX, destY);
                await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

                // Update partner location to the new map
                client.Tamer.Partner.NewLocation(targetMapId, destX, destY);
                await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

                // Set the character state to loading
                client.Tamer.UpdateState(CharacterStateEnum.Loading);
                await _sender.Send(new UpdateCharacterStateCommand(client.TamerId, CharacterStateEnum.Loading));

                // Ensure the client isn't marked as quitting
                client.SetGameQuit(false);

                // Get server address and port from configuration
                string serverAddress = "127.0.0.1"; // Default fallback value
                string serverPort = "7607";       // Default fallback value

                if (_configuration != null)
                {
                    serverAddress = _configuration[GamerServerPublic] ?? serverAddress;
                    serverPort = _configuration[GameServerPort] ?? serverPort;
                }

                // Send the map swap packet for cross-map teleportation
                client.Send(new MapSwapPacket(
                    serverAddress,
                    serverPort,
                    targetMapId,
                    destX,
                    destY
                ));

                // Mark that this tamer has been teleported by setting goal value to 1
                client.Tamer.Progress.UpdateQuestInProgress(967, 0, 1);

                // Update the quest progress
                var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 967);
                if (inProgressQuest != null)
                {
                    await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
                }

                // Log the teleport
                _logger.Debug($"Automatically teleported tamer {client.TamerId} from Yokohama to DATS HQ at X:{destX}, Y:{destY}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error during teleport to DATS HQ: {ex.Message}");
                _logger.Error(ex.StackTrace); // Log stack trace for debugging
            }
        }

        // File Island Teleports
        private async void FileQuest1(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 1305)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 2954);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 2954 / 8;
            int questBit = 2954 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 2954);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 34713;
            int destY = 30040;

            // Update tamer location
            client.Tamer.NewLocation(1305, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(1305, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(2954, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 2954);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }

        // Desert Teleports
        private async void DesertQuest1(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 1400)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 1267);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 1267 / 8;
            int questBit = 1267 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 1267);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 21724;
            int destY = 37603;

            // Update tamer location
            client.Tamer.NewLocation(1400, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(1400, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(1267, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 1267);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }
        private async void DesertQuest2(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 1400)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 1272);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 1272 / 8;
            int questBit = 1272 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 1272);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 26375;
            int destY = 32861;

            // Update tamer location
            client.Tamer.NewLocation(1400, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(1400, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(1272, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 1272);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }
        private async void DesertQuest3(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 1400)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 1276);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 1276 / 8;
            int questBit = 1276 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 1276);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 25279;
            int destY = 20141;

            // Update tamer location
            client.Tamer.NewLocation(1400, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(1400, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(1276, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 1276);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }
        private async void DesertQuest4(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 1400)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 1279);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 1279 / 8;
            int questBit = 1279 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 1279);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 20058;
            int destY = 24313;

            // Update tamer location
            client.Tamer.NewLocation(1400, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(1400, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(1279, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 1279);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }
        private async void DesertQuest5(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 1400)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 1280);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 1280 / 8;
            int questBit = 1280 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 1280);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 11168;
            int destY = 23650;

            // Update tamer location
            client.Tamer.NewLocation(1400, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(1400, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(1280, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 1280);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }
        private async void DesertQuest6(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 1400)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 1281);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 1281 / 8;
            int questBit = 1281 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 1281);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 25211;
            int destY = 19766;

            // Update tamer location
            client.Tamer.NewLocation(1400, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(1400, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(1281, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 1281);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }
        private async void DesertQuest7(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 1400)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 1293);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 1293 / 8;
            int questBit = 1293 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 1293);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 60518;
            int destY = 39334;

            // Update tamer location
            client.Tamer.NewLocation(1400, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(1400, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(1293, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 1293);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }
        private async void DesertQuest8(GameClient client) // Local Teleport
        {
            // Check if client is valid and in the correct map
            if (client?.Tamer?.Location?.MapId != 1400)
                return;

            // Check if the tamer has the specific quest
            var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 1301);
            if (!hasYokohamaQuest)
                return;

            // NEW: Check if the quest is already completed (completed quests should be in CompletedData)
            // Each quest ID bit is stored in the CompletedData byte array
            int questIndex = 1301 / 8;
            int questBit = 1301 % 8;
            if (questIndex < client.Tamer.Progress.CompletedData.Length &&
                (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
            {
                // Quest already completed, don't continue with teleport
                return;
            }

            // Check if this tamer has already been teleported by looking at a special quest flag
            // Using goal index 0 as a tracking mechanism (0 = not teleported, 1 = already teleported)
            var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 1301);
            if (questData == null || questData.GetGoalValue(0) == 1)
                return; // Already teleported or quest data is missing, don't teleport again

            // Set up the teleport coordinates
            int destX = 74611;
            int destY = 30343;

            // Update tamer location
            client.Tamer.NewLocation(1400, destX, destY);
            await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

            // Update partner location
            client.Tamer.Partner.NewLocation(1400, destX, destY);
            await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

            // Send local teleport packet (no loading screen)
            client.Send(new LocalMapSwapPacket(
                client.Tamer.GeneralHandler,
                client.Tamer.Partner.GeneralHandler,
                destX, destY,
                destX, destY
            ));

            // Mark that this tamer has been teleported by setting goal value to 1
            client.Tamer.Progress.UpdateQuestInProgress(1301, 0, 1);

            // Use the correct command with the correct parameter
            var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 1301);
            if (inProgressQuest != null)
            {
                await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
            }

            // Log the teleport for debugging purposes
            _logger.Debug($"Automatically teleported tamer {client.TamerId} to Yokohama coordinates X:{destX}, Y:{destY}");
        }
        //private async void DesertToContinent(GameClient client) // Loading Teleport
        //{
        //    try
        //    {
        //        // Check if client is valid
        //        if (client?.Tamer == null)
        //            return;

        //        // Target map is SilentForest HQ - Map ID 3
        //        const int targetMapId = 140;

        //        // Check if the tamer has the specific quest
        //        var hasYokohamaQuest = client.Tamer.Progress.InProgressQuestData.Any(q => q.QuestId == 1370);
        //        if (!hasYokohamaQuest)
        //            return;

        //        // Check if the quest is already completed
        //        int questIndex = 1370 / 8;
        //        int questBit = 1370 % 8;
        //        if (questIndex < client.Tamer.Progress.CompletedData.Length &&
        //            (client.Tamer.Progress.CompletedData[questIndex] & (1 << questBit)) != 0)
        //        {
        //            // Quest already completed, don't continue with teleport
        //            return;
        //        }

        //        // Check if this tamer has already been teleported
        //        var questData = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 1370);
        //        if (questData == null || questData.GetGoalValue(0) == 1)
        //            return; // Already teleported or quest data is missing

        //        // Set up the teleport coordinates for DATS HQ
        //        int destX = 35199;
        //        int destY = 31674;

        //        // Set the tamer's channel to 0 (default channel for DATS)
        //        client.Tamer.SetCurrentChannel(0);

        //        // Remove client from current map
        //        if (client.DungeonMap && _dungeonServer != null)
        //        {
        //            _dungeonServer.RemoveClient(client);
        //        }
        //        else if (_mapServer != null) // If not in dungeon map, use regular map server
        //        {
        //            _mapServer.RemoveClient(client);
        //        }

        //        // Update tamer location to the new map
        //        client.Tamer.NewLocation(targetMapId, destX, destY);
        //        await _sender.Send(new UpdateCharacterLocationCommand(client.Tamer.Location));

        //        // Update partner location to the new map
        //        client.Tamer.Partner.NewLocation(targetMapId, destX, destY);
        //        await _sender.Send(new UpdateDigimonLocationCommand(client.Tamer.Partner.Location));

        //        // Set the character state to loading
        //        client.Tamer.UpdateState(CharacterStateEnum.Loading);
        //        await _sender.Send(new UpdateCharacterStateCommand(client.TamerId, CharacterStateEnum.Loading));

        //        // Ensure the client isn't marked as quitting
        //        client.SetGameQuit(false);

        //        // Get server address and port from configuration
        //        string serverAddress = "127.0.0.1"; // Default fallback value
        //        string serverPort = "7607";       // Default fallback value

        //        if (_configuration != null)
        //        {
        //            serverAddress = _configuration[GamerServerPublic] ?? serverAddress;
        //            serverPort = _configuration[GameServerPort] ?? serverPort;
        //        }

        //        // Send the map swap packet for cross-map teleportation
        //        client.Send(new MapSwapPacket(
        //            serverAddress,
        //            serverPort,
        //            targetMapId,
        //            destX,
        //            destY
        //        ));

        //        // Mark that this tamer has been teleported by setting goal value to 1
        //        client.Tamer.Progress.UpdateQuestInProgress(1370, 0, 1);

        //        // Update the quest progress
        //        var inProgressQuest = client.Tamer.Progress.InProgressQuestData.FirstOrDefault(q => q.QuestId == 1370);
        //        if (inProgressQuest != null)
        //        {
        //            await _sender.Send(new UpdateCharacterInProgressCommand(inProgressQuest));
        //        }

        //        // Log the teleport
        //        _logger.Debug($"Automatically teleported tamer {client.TamerId} from Yokohama to DATS HQ at X:{destX}, Y:{destY}");
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.Error($"Error during teleport to DATS HQ: {ex.Message}");
        //        _logger.Error(ex.StackTrace); // Log stack trace for debugging
        //    }
        //}

        private void CheckAndApplyPartySkillBuff(CharacterModel tamer, int usedSkillId)
        {
            if (usedSkillId != 6700391)
                return;

            var party = _partyManager.FindParty(tamer.Id);
            if (party == null || party.Members.Count <= 1)
                return;

            var buffInfo = _assets.BuffInfo.FirstOrDefault(x => x.BuffId == 64100);
            if (buffInfo == null)
                return;

            const int durationSeconds = 240;

            Console.WriteLine($"[PartyBuff] Triggered by skill {usedSkillId}. Buff={buffInfo.BuffId}, Duration={durationSeconds}s");

            foreach (var memberId in party.GetMembersIdList())
            {
                var memberClient = GetClientByTamerId(memberId);
                if (memberClient?.IsConnected != true || memberClient.Partner == null)
                    continue;

                var partner = memberClient.Partner;

                // Remover buff existente
                var existing = partner.BuffList.ActiveBuffs.FirstOrDefault(b => b.BuffId == 64100);
                if (existing != null)
                {
                    partner.BuffList.Remove(existing.BuffId);

                    BroadcastRemoveBuff(memberClient, partner.GeneralHandler, existing.BuffId);

                    Console.WriteLine($"[PartyBuff] Removed old buff {existing.BuffId} from {partner.Id}");
                }

                // Crear nuevo buff
                var newBuff = DigimonBuffModel.Create(64100, usedSkillId, 0, durationSeconds);
                newBuff.SetBuffInfo(buffInfo);
                newBuff.SetEndDate(DateTime.Now.AddSeconds(durationSeconds));
                partner.BuffList.Add(newBuff);

                // Solo enviar el AddBuff visual (no recalculamos stats manualmente)
                BroadcastAddBuff(memberClient, partner.GeneralHandler, buffInfo, 0, durationSeconds);

                Console.WriteLine($"[PartyBuff] Applied buff {buffInfo.BuffId} to partner {partner.Id}");

                // Persistencia
                _sender.Send(new UpdateDigimonBuffListCommand(partner.BuffList));
            }
        }

            private void BroadcastAddBuff(
            GameClient client,
            int handler,
            BuffInfoAssetModel buff,
            short buffLevel,
            int duration)
        {
            var packet = new AddBuffPacket(handler, buff, buffLevel, duration).Serialize();

            if (client.DungeonMap)
            {
                _dungeonServer.BroadcastForTamerViewsAndSelf(client.TamerId, packet);
                return;
            }

            if (client.EventMap)
            {
                _eventServer.BroadcastForTamerViewsAndSelf(client.TamerId, packet);
                return;
            }

            if (client.PvpMap)
            {
                _pvpServer.BroadcastForTamerViewsAndSelf(client.TamerId, packet);
                return;
            }

            // Default → mapas normales
            _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, packet);
        }


        private void BroadcastRemoveBuff(GameClient client, int handler, int buffId)
        {
            var packet = new RemoveBuffPacket(handler, buffId).Serialize();

            if (client.DungeonMap)
            {
                _dungeonServer.BroadcastForTamerViewsAndSelf(client.TamerId, packet);
                return;
            }

            if (client.EventMap)
            {
                _eventServer.BroadcastForTamerViewsAndSelf(client.TamerId, packet);
                return;
            }

            if (client.PvpMap)
            {
                _pvpServer.BroadcastForTamerViewsAndSelf(client.TamerId, packet);
                return;
            }

            _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, packet);
        }


        // Helper method to get a client by tamer ID
        private GameClient GetClientByTamerId(long tamerId)
        {
            foreach (var map in Maps)
            {
                var client = map.Clients.FirstOrDefault(c => c.TamerId == tamerId);
                if (client != null)
                    return client;
            }
            return null;
        }

        private async void ReedemTimeReward(GameClient client)
        {
            var tr = client.Tamer.TimeReward;

            var drops = _assets.TimeRewardAssets.Where(d => d.CurrentReward == (int)tr.RewardIndex);

            foreach (var drop in drops)
            {
                var reward = new ItemModel();

                reward.SetItemInfo(_assets.ItemInfo.GetValueOrDefault(drop.ItemId));

                reward.ItemId = drop.ItemId;
                reward.Amount = drop.ItemCount;

                if (reward.IsTemporary)
                    reward.SetRemainingTime((uint)reward.ItemInfo.UsageTimeMinutes);

                if (client.Tamer.Inventory.AddItem(reward))
                {
                    client.Send(new ReceiveItemPacket(reward, InventoryTypeEnum.Inventory));
                    await _sender.Send(new UpdateItemsCommand(client.Tamer.Inventory));
                }
            }
        }

    }
}