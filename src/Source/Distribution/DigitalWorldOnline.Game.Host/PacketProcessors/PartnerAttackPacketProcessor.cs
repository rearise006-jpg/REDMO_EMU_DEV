using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.Map;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Config;
using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Models.Summon;
using DigitalWorldOnline.Commons.Packets.Chat;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Packets.GameServer.Combat;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Game.Managers;
using DigitalWorldOnline.GameHost;
using DigitalWorldOnline.GameHost.EventsServer;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class PartnerAttackPacketProcessor : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.PartnerAttack;

        private readonly MapServer _mapServer;
        private readonly DungeonsServer _dungeonServer;
        private readonly EventServer _eventServer;
        private readonly PvpServer _pvpServer;
        private readonly ILogger _logger;
        private readonly ISender _sender;

        public PartnerAttackPacketProcessor(MapServer mapServer, DungeonsServer dungeonsServer, EventServer eventServer, PvpServer pvpServer,
            ILogger logger, ISender sender)
        {
            _mapServer = mapServer;
            _dungeonServer = dungeonsServer;
            _eventServer = eventServer;
            _pvpServer = pvpServer;
            _logger = logger;
            _sender = sender;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            var attackerHandler = packet.ReadInt();
            var targetHandler = packet.ReadInt();

            #region Remove AFK

            if (client.Tamer.CurrentCondition == ConditionEnum.Away)
            {
                client.Tamer.UpdateCurrentCondition(ConditionEnum.Default);
                _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SyncConditionPacket(client.Tamer.GeneralHandler, client.Tamer.CurrentCondition).Serialize());
                client.Tamer.ResetAfkNotifications();
            }

            #endregion

            #region Attack Logic

            IMapServer server = client.DungeonMap ? _dungeonServer : _mapServer;

            if (client.DungeonMap)
            {
                Action<long, byte[]> broadcastAction = (id, data) => _dungeonServer.BroadcastForTamerViewsAndSelf(id, data);
                Func<long, bool, bool> broadcastMobs = (tamerId, isSummon) => _dungeonServer.IsMobsAttacking(tamerId, isSummon);

                var isNormalMob = _dungeonServer.GetIMobByHandler(targetHandler, client.TamerId);
                var isSummonMob = _dungeonServer.GetIMobByHandler(targetHandler, client.TamerId, true);

                if (isNormalMob != null)
                {
                    HandleMobAttack(client, server, isNormalMob, attackerHandler, targetHandler, broadcastAction, broadcastMobs, false);
                }
                else if (isSummonMob != null)
                {
                    HandleMobAttack(client, server, isSummonMob, attackerHandler, targetHandler, broadcastAction, broadcastMobs, true);
                }
                else
                {
                    //_logger.Error($"Tamer [{client.TamerId}:{client.Tamer.Name}] try to attack a invalid target !! Handler {targetHandler}.");

                    client.Tamer.StopBattle();
                    client.Partner.StopAutoAttack();

                    client.Send(new SetCombatOffPacket(client.Partner.GeneralHandler).Serialize());
                }
            }
            else
            {
                Action<long, byte[]> broadcastAction = (id, data) => _mapServer.BroadcastForTamerViewsAndSelf(id, data);
                Func<long, bool, bool> broadcastMobs = (tamerId, isSummon) => _mapServer.IsMobsAttacking(tamerId, isSummon);

                var isNormalMob = _mapServer.GetIMobByHandler(targetHandler, client.TamerId, false);
                var isSummonMob = _mapServer.GetIMobByHandler(targetHandler, client.TamerId, true);

                var targetPartner = _mapServer.GetEnemyByHandler(client.Tamer.Location.MapId, targetHandler, client.TamerId);

                if (targetPartner == null)
                {
                    if (isNormalMob != null)
                    {
                        HandleMobAttack(client, server, isNormalMob, attackerHandler, targetHandler, broadcastAction, broadcastMobs, false);
                    }
                    else if (isSummonMob != null)
                    {
                        HandleMobAttack(client, server, isSummonMob, attackerHandler, targetHandler, broadcastAction, broadcastMobs, true);
                    }
                    else
                    {
                        _logger.Error($"Tamer [{client.TamerId}:{client.Tamer.Name}] try to attack a invalid target !! Handler {targetHandler}.");

                        client.Tamer.StopBattle();
                        client.Partner.StopAutoAttack();

                        client.Send(new SetCombatOffPacket(client.Partner.GeneralHandler).Serialize());
                    }
                }
                else
                {
                    HandlePlayerAttack(client, targetPartner, attackerHandler, targetHandler);
                }
            }

            #endregion

        }

        // --------------------------------------------------------------------------------------------------------------------
        private void HandlePlayerAttack(GameClient client, DigimonModel targetPartner, int attackerHandler, int targetHandler)
        {
            if (targetPartner == null || client.Partner == null)
                return;

            if (targetPartner.Character.PvpMap == false)
            {
                client.Tamer.StopBattle();
                client.Partner.StopAutoAttack();

                client.Send(new SetCombatOffPacket(client.Partner.GeneralHandler).Serialize());
                client.Send(new SystemMessagePacket($"Tamer {targetPartner.Name} pvp is off !!"));

                return;
            }

            if (client.Tamer.PvpMap && targetPartner.Character.PvpMap && targetPartner != null)
            {
                if (DateTime.Now < client.Partner.LastHitTime.AddMilliseconds(client.Partner.AS))
                {
                    client.Partner.StartAutoAttack();
                }

                if (targetPartner.Alive)
                {
                    if (client.Partner.IsAttacking)
                    {
                        if (client.Tamer.TargetMob?.GeneralHandler != targetPartner.GeneralHandler)
                        {
                            _logger.Information($"Character {client.Tamer.Id} switched target to partner {targetPartner.Id} - {targetPartner.Name}.");

                            client.Tamer.SetHidden(false);
                            client.Tamer.UpdateTarget(targetPartner);
                            client.Partner.StartAutoAttack();
                        }
                    }
                    else
                    {
                        if (DateTime.Now < client.Partner.LastHitTime.AddMilliseconds(client.Partner.AS))
                        {
                            client.Partner.StartAutoAttack();
                            return;
                        }

                        if (!client.Tamer.InBattle)
                        {
                            _logger.Information($"Character {client.Tamer.Id} engaged partner {targetPartner.Id} - {targetPartner.Name}.");
                            client.Tamer.SetHidden(false);

                            _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(attackerHandler).Serialize());
                            client.Tamer.StartBattle(targetPartner);
                        }
                        else
                        {
                            _logger.Information($"Character {client.Tamer.Id} switched to partner {targetPartner.Id} - {targetPartner.Name}.");
                            client.Tamer.SetHidden(false);
                            client.Tamer.UpdateTarget(targetPartner);
                        }

                        if (!targetPartner.Character.InBattle)
                        {
                            _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(targetHandler).Serialize());
                        }

                        targetPartner.Character.StartBattle(client.Partner);

                        client.Tamer.Partner.StartAutoAttack();

                        var missed = false;

                        if (missed)
                        {
                            _logger.Information($"Partner {client.Tamer.Partner.Id} missed hit on {client.Tamer.TargetPartner.Id} - {client.Tamer.TargetPartner.Name}.");
                            _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new MissHitPacket(attackerHandler, targetHandler).Serialize());
                        }
                        else
                        {
                            #region Hit Damage

                            var critBonusMultiplier = 0.00;
                            var blocked = false;
                            var finalDmg = CalculateFinalDamage(client, targetPartner, out critBonusMultiplier, out blocked);

                            if (finalDmg != 0 && !client.Tamer.GodMode)
                            {
                                finalDmg = DebuffReductionDamage(client, finalDmg);
                            }

                            #endregion

                            #region Take Damage

                            if (finalDmg <= 0) finalDmg = 1;

                            var newHp = targetPartner.ReceiveDamage(finalDmg);

                            var hitType = blocked ? 2 : critBonusMultiplier > 0 ? 1 : 0;
                            
                            bool mobDied = newHp <= 0;

                            if (newHp > 0)
                            {
                                _logger.Verbose($"Partner {client.Partner.Id} inflicted {finalDmg} to partner {targetPartner?.Id} - {targetPartner?.Name}.");

                                _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new HitPacket(attackerHandler, targetHandler, finalDmg, targetPartner.HP, newHp, hitType).Serialize());
                            }
                            else
                            {
                                _logger.Verbose($"Partner {client.Partner.Id} killed partner {targetPartner?.Id} - {targetPartner?.Name} with {finalDmg} damage.");

                                _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new KillOnHitPacket(attackerHandler, targetHandler, finalDmg, hitType).Serialize());

                                targetPartner.Character.Die();

                                if (!_mapServer.EnemiesAttacking(client.Tamer.Location.MapId, client.Partner.Id, client.TamerId))
                                {
                                    client.Tamer.StopBattle();

                                    _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOffPacket(attackerHandler).Serialize());
                                }
                            }

                            #endregion

                            client.Partner.StartAutoAttack();
                        }

                        client.Tamer.Partner.UpdateLastHitTime();
                    }
                }
                else
                {
                    if (!_mapServer.EnemiesAttacking(client.Tamer.Location.MapId, client.Partner.Id, client.TamerId))
                    {
                        client.Tamer.StopBattle();

                        _mapServer.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOffPacket(attackerHandler).Serialize());
                    }
                }

            }
        }

        private void HandleMobAttack(GameClient client, IMapServer server, IMob targetMob, int attackerHandler, int targetHandler, Action<long, byte[]> broadcastAction, Func<long, bool, bool> broadcastMobs, bool isSummon)
        {
            if (targetMob == null || client.Partner == null)
                return;

            if (!targetMob.Alive)
            {
                HandleDeadTarget(client, server, attackerHandler);
                return;
            }

            TryStartBattle(client, server, targetMob, attackerHandler, targetHandler);

            if (!client.Partner.CanAutoAttack() || !client.Partner.CanAttack())
            {
                client.Partner.StartAutoAttack();
                return;
            }

            HandlePartnerAttack(client, server, targetMob, attackerHandler, targetHandler);
        }

        private void HandleDeadTarget(GameClient client, IMapServer server, int attackerHandler)
        {
            if (!server.MobsAttacking(client.Tamer.Location.MapId, client.TamerId, client.Tamer.Channel))
            {
                client.Tamer.StopBattle();
                server.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOffPacket(attackerHandler).Serialize());
            }
        }

        private void TryStartBattle(GameClient client, IMapServer server, IMob targetMob, int attackerHandler, int targetHandler)
        {
            client.Partner.SetEndAttacking();

            client.Tamer.SetHidden(false);

            if (!client.Tamer.InBattle)
            {
                server.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(attackerHandler).Serialize());
                client.Tamer.StartBattle(targetMob);
            }
            else
            {
                client.Tamer.UpdateTarget(targetMob);
            }

            if (!targetMob.InBattle)
            {
                server.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOnPacket(targetHandler).Serialize());
                targetMob.StartBattle(client.Tamer);
            }
            else
            {
                targetMob.AddTarget(client.Tamer);
            }
        }

        private void HandlePartnerAttack(GameClient client, IMapServer server, IMob targetMob, int attackerHandler, int targetHandler)
        {
            client.Tamer.Partner.StartAutoAttack();

            bool missed = false;

            if (!client.Tamer.GodMode)
                missed = client.Tamer.TargetSummonMobs.Count > 0 ? client.Tamer.CanMissHit(true) : client.Tamer.CanMissHit();

            if (missed)
            {
                server.BroadcastForTamerViewsAndSelf(client.TamerId, new MissHitPacket(attackerHandler, targetHandler).Serialize());
                return;
            }

            #region Hit Damage

            var critBonusMultiplier = 0.00;

            var blocked = false;
            int hitType = blocked ? 2 : ((client.Partner.CC / 100) > UtilitiesFunctions.RandomDouble()) ? 1 : 0;
            var finalDmg = client.Tamer.GodMode ? targetMob.CurrentHP : AttackManager.CalculateDamage(client, out critBonusMultiplier, out blocked);

            if (finalDmg <= 0) finalDmg = 1;
            if (finalDmg > targetMob.CurrentHP) finalDmg = targetMob.CurrentHP;

            HandleHitResult(client, server, targetMob, attackerHandler, targetHandler, finalDmg, hitType);

            client.Tamer.Partner.UpdateLastHitTime();
            client.Partner.ResetAutoAttackTimer();

            #endregion
        }

        private void HandleHitResult(GameClient client, IMapServer server, IMob targetMob, int attackerHandler, int targetHandler, int finalDmg, int hitType)
        {
            if (client.DungeonMap)
                _dungeonServer.ProcessReturnFireDamageReflection(client, targetMob, finalDmg);
            
            int newHp = targetMob.ReceiveDamage(finalDmg, client.TamerId);

            bool mobDied = newHp <= 0;

            if (mobDied)
            {
                var killPacket = new KillOnHitPacket(attackerHandler, targetHandler, finalDmg, hitType).Serialize();

                server.BroadcastForTamerViewsAndSelf(client.TamerId, killPacket);

                client.Partner.SetEndAttacking(client.Partner.AS * -2);

                targetMob.Die();
                targetMob.UpdateCurrentAction(MobActionEnum.Wait);

                if (!server.MobsAttacking(client.Tamer.Location.MapId, client.TamerId, client.Tamer.Channel))
                {
                    client.Tamer.StopBattle();

                    server.BroadcastForTamerViewsAndSelf(client.TamerId, new SetCombatOffPacket(attackerHandler).Serialize());
                }
            }
            else
            {
                var hitPacket = new HitPacket(attackerHandler, targetHandler, finalDmg, targetMob.HPValue, newHp, hitType).Serialize();

                server.BroadcastForTamerViewsAndSelf(client.TamerId, hitPacket);
            }
        }

        // --------------------------------------------------------------------------------------------------------------------

        private static int DebuffReductionDamage(GameClient client, int finalDmg)
        {
            if (client.Tamer.Partner.DebuffList.ActiveDebuffReductionDamage())
            {
                var debuffInfo = client.Tamer.Partner.DebuffList.ActiveBuffs
                .Where(buff => buff.BuffInfo.SkillInfo.Apply
                    .Any(apply => apply.Attribute == Commons.Enums.SkillCodeApplyAttributeEnum.AttackPowerDown))

                .ToList();

                var totalValue = 0;
                var SomaValue = 0;

                foreach (var debuff in debuffInfo)
                {
                    foreach (var apply in debuff.BuffInfo.SkillInfo.Apply)
                    {

                        switch (apply.Type)
                        {
                            case SkillCodeApplyTypeEnum.Default:
                                totalValue += apply.Value;
                                break;

                            case SkillCodeApplyTypeEnum.AlsoPercent:
                            case SkillCodeApplyTypeEnum.Percent:
                                {

                                    SomaValue += apply.Value + (debuff.TypeN) * apply.IncreaseValue;

                                    double fatorReducao = SomaValue / 100;

                                    // Calculando o novo finalDmg após a redução
                                    finalDmg -= (int)(finalDmg * fatorReducao);

                                }
                                break;

                            case SkillCodeApplyTypeEnum.Unknown200:
                                {

                                    SomaValue += apply.AdditionalValue;

                                    double fatorReducao = SomaValue / 100.0;

                                    // Calculando o novo finalDmg após a redução
                                    finalDmg -= (int)(finalDmg * fatorReducao);

                                }
                                break;

                        }
                    }
                }
            }

            return finalDmg;
        }

        // --------------------------------------------------------------------------------------------------------------------
        

        // Player
        private static int CalculateFinalDamage(GameClient client, DigimonModel? targetPartner, out double critBonusMultiplier, out bool blocked)
        {
            var baseDamage = (client.Tamer.Partner.AT / targetPartner.DE * 150) + UtilitiesFunctions.RandomInt(5, 50);
            if (baseDamage < 0) baseDamage = 0;

            critBonusMultiplier = 0.00;
            double critChance = client.Tamer.Partner.CC / 100;

            if (critChance >= UtilitiesFunctions.RandomDouble())
            {

                var vlrAtual = client.Tamer.Partner.CD;
                var bonusMax = 1.50; //TODO: externalizar?
                var expMax = 10000; //TODO: externalizar?

                critBonusMultiplier = (bonusMax * vlrAtual) / expMax;
            }

            blocked = targetPartner.BL >= UtilitiesFunctions.RandomDouble();

            var levelBonusMultiplier = client.Tamer.Partner.Level > targetPartner.Level ? (0.01f * (client.Tamer.Partner.Level - targetPartner.Level)) : 0;

            // ===============================
            // ATTRIBUTE MULTIPLIER
            // ===============================
            var attributeMultiplier = 0.00;
            var attackerAttr = client.Tamer.Partner.BaseInfo.Attribute;
            var targetAttr   = targetPartner.BaseInfo.Attribute;

            // Unknown vs Unknown = neutral (for now)
            if (attackerAttr == DigimonAttributeEnum.Unknown &&
                targetAttr   == DigimonAttributeEnum.Unknown)
            {
                attributeMultiplier = 0;
            }
            else if (attackerAttr == DigimonAttributeEnum.Unknown &&
                    attackerAttr.HasAttributeAdvantage(targetAttr))
            {
                // Unknown has static + 20% advantage
                attributeMultiplier = 0.20;
            }
            else if (attackerAttr.HasAttributeAdvantage(targetAttr))
            {
                // Atributos normales usan EXP
                var attExp = client.Tamer.Partner.GetAttributeExperience();
                var bonusMax = 1.00;   // TODO externalizar
                var expMax = 10000.0;  // TODO externalizar

                attributeMultiplier = (bonusMax * attExp) / expMax;
            }
            else if (targetAttr.HasAttributeAdvantage(attackerAttr))
            {
                // Desventaja clásica
                attributeMultiplier = -0.25;
            }
            else
            {
                // Neutral
                attributeMultiplier = 0;
            }


            var elementMultiplier = 0.00;
            if (client.Tamer.Partner.BaseInfo.Element.HasElementAdvantage(targetPartner.BaseInfo.Element))
            {
                var vlrAtual = client.Tamer.Partner.GetElementExperience();
                var bonusMax = 0.50; //TODO: externalizar?
                var expMax = 10000; //TODO: externalizar?

                elementMultiplier = (bonusMax * vlrAtual) / expMax;
            }
            else if (targetPartner.BaseInfo.Element.HasElementAdvantage(client.Tamer.Partner.BaseInfo.Element))
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

        // --------------------------------------------------------------------------------------------------------------------
    }
}