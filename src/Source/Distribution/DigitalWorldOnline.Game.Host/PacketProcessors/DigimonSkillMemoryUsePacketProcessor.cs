using AutoMapper;
using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.Items;
using DigitalWorldOnline.Game.Managers;
using DigitalWorldOnline.GameHost;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Game.PacketProcessors;

public class DigimonSkillMemoryUsePacketProcessor : IGamePacketProcessor
{
    public GameServerPacketEnum Type => GameServerPacketEnum.UseSkillMemory;

    private readonly StatusManager _statusManager;
    private readonly IMapper _mapper;
    private readonly ILogger _logger;
    private readonly ISender _sender;
    private readonly MapServer _mapServer;
    private readonly DungeonsServer _dungeonServer;
    private readonly AssetsLoader _assets;
    private readonly PartyManager _partyManager;
    private readonly IMemorySkillService _memorySkillService;

    public DigimonSkillMemoryUsePacketProcessor(StatusManager statusManager,
        IMapper mapper,
        ILogger logger,
        ISender sender,
        MapServer mapServer,
        DungeonsServer dungeonServer,
        AssetsLoader assets,
        PartyManager partyManager,
        IMemorySkillService memorySkillService)
    {
        _statusManager = statusManager;
        _mapper = mapper;
        _logger = logger;
        _sender = sender;
        _mapServer = mapServer;
        _dungeonServer = dungeonServer;
        _assets = assets;
        _partyManager = partyManager;
        _memorySkillService = memorySkillService;
    }

    public async Task Process(GameClient client, byte[] packetData)
    {
        // Responsibility: minimal parsing + input validation + delegate to service
        var packet = new GamePacketReader(packetData);
        var digimonUid = packet.ReadInt();
        var evoStep = packet.ReadByte();
        var skillCode = packet.ReadInt();
        var targetHandler = packet.ReadInt();

        IMapServer server = client.DungeonMap ? _dungeonServer : _mapServer;

        // Locate assets (minimal and readonly lookups)
        var buffInfo = _assets.BuffInfo.FirstOrDefault(x => x.SkillCode == skillCode && x.Class != 450);
        var skillInfo = _assets.SkillInfo.FirstOrDefault(x => x.SkillId == skillCode);

        // Check / consume required item (skill consumable). Keep inventory responsibility in packet processor
        var item = client.Tamer.Inventory.FindItemById(20000);
        int quantityToConsume = skillInfo?.RequiredItem ?? 1;
        if (item == null || item.Amount < quantityToConsume)
            return;

        client.Tamer.Inventory.RemoveOrReduceItem(item, quantityToConsume);
        await _sender.Send(new UpdateItemCommand(item));

        // Delegate full memory skill handling to the dedicated service
        await _memorySkillService.HandleMemorySkillUseAsync(client, server, buffInfo, skillInfo, skillCode, targetHandler);

        // Keep final inventory update here so it's obvious this file touches inventory only
        client.Send(new LoadInventoryPacket(client.Tamer.Inventory, InventoryTypeEnum.Inventory));
    }
}
