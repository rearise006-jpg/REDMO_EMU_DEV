using AutoMapper;
using DigitalWorldOnline.Application.Separar.Commands.Create;
using DigitalWorldOnline.Application.Separar.Commands.Delete;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Enums.Character;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Account;
using DigitalWorldOnline.Commons.Models.Assets;
using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Packets.CharacterServer;
using DigitalWorldOnline.Commons.Utils;
using MediatR;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace DigitalWorldOnline.Character
{
    public sealed class CharacterPacketProcessor : IProcessor, IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly ISender _sender;
        private readonly ILogger _logger;
        private readonly IMapper _mapper;

        private const string GameServerAddress = "GameServer:Address";
        private const string GamerServerPublic = "GameServer:PublicAddress";
        private const string GameServerPort = "GameServer:Port";
        private const int HandshakeDegree = 32321;
        private const int HandshakeStampDegree = 65535;

        public CharacterPacketProcessor(IConfiguration configuration, ISender sender, ILogger logger, IMapper mapper)
        {
            _configuration = configuration;
            _sender = sender;
            _logger = logger;
            _mapper = mapper;
        }

        /// <summary>
        /// Process the arrived TCP packet, sent from the game client
        /// </summary>
        /// <param name="client">The game client whos sent the packet</param>
        /// <param name="data">The packet bytes array</param>
        public async Task ProcessPacketAsync(GameClient client, byte[] data)
        {
            var packet = new CharacterPacketReader(data);

            switch (packet.Enum)
            {
                case CharacterServerPacketEnum.Connection:
                    {
                        var kind = packet.ReadByte();

                        var handshakeTimestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        var handshake = (short)(client.Handshake ^ HandshakeDegree);

                        client.Send(new ConnectionPacket(handshake, handshakeTimestamp).Serialize());
                    }
                    break;

                case CharacterServerPacketEnum.KeepConnection:
                    break;

                case CharacterServerPacketEnum.RequestCharacters:
                    {
                        packet.Seek(8);

                        var accountId = packet.ReadUInt();

                        try
                        {
                            var characters = _mapper.Map<List<CharacterModel>>(await _sender.Send(new CharactersByAccountIdQuery(accountId)));

                            characters.ForEach(character =>
                            {
                                if (character.Partner.CurrentType != character.Partner.BaseType)
                                {
                                    character.Partner.UpdateCurrentType(character.Partner.BaseType);

                                    _sender.Send(new UpdatePartnerCurrentTypeCommand(character.Partner));
                                }
                            });

                            client.Send(new CharacterListPacket(characters));

                            client.SetAccountId(accountId);

                            _sender.Send(new UpdateAccountStatusCommand(client.AccountId, 1));
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[RequestCharacters] :: {ex.Message}");
                        }
                    }
                    break;

                case CharacterServerPacketEnum.CreateCharacter:
                    {
                        var position = packet.ReadByte();
                        var tamerModel = packet.ReadInt();
                        var tamerName = packet.ReadZString();
                        packet.Seek(42);
                        var digimonModel = packet.ReadInt();
                        var digimonName = packet.ReadZString();

                        try
                        {
                            var account = _mapper.Map<AccountModel>(await _sender.Send(new AccountByIdQuery(client.AccountId)));

                            var character = CharacterModel.Create(
                                client.AccountId,
                                tamerName,
                                tamerModel,
                                position,
                                account.LastPlayedServer);

                            var digimon = DigimonModel.Create(
                                digimonName,
                                digimonModel,
                                digimonModel,
                                DigimonHatchGradeEnum.Lv5,
                                UtilitiesFunctions.RandomShort(12000, 12000),
                                0);

                            character.AddDigimon(digimon);

                            var handshakeTimestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                            var handshake = (short)(handshakeTimestamp & HandshakeStampDegree);

                            client.Send(new CharacterCreatedPacket(character, handshake));

                            character.SetBaseStatus(_mapper.Map<CharacterBaseStatusAssetModel>(
                                    await _sender.Send(new TamerBaseStatusQuery(character.Model))));

                            character.SetLevelStatus(_mapper.Map<CharacterLevelStatusAssetModel>(
                                    await _sender.Send(new TamerLevelStatusQuery(character.Model, character.Level))));

                            character.Partner.SetBaseInfo(_mapper.Map<DigimonBaseInfoAssetModel>(
                                    await _sender.Send(new DigimonBaseInfoQuery(character.Partner.CurrentType))));

                            character.Partner.AddEvolutions(await _sender.Send(new DigimonEvolutionAssetsByTypeQuery(digimonModel)));
                            await _sender.Send(new CreateCharacterCommand(character));
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[CreateCharacter] :: {ex.Message}");
                        }
                    }
                    break;

                case CharacterServerPacketEnum.CheckNameDuplicity:
                    {
                        var tamerName = packet.ReadString();

                        try
                        {
                            var account = _mapper.Map<AccountModel>(await _sender.Send(new AccountByIdQuery(client.AccountId)));

                            var existingCharacter = await _sender.Send(new CharacterByNameQuery(tamerName));

                            var availableName = existingCharacter == null;

                            client.Send(new AvailableNamePacket(availableName).Serialize());
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[CheckNameDuplicity] :: {ex.Message}");
                        }
                    }
                    break;

                case CharacterServerPacketEnum.DeleteCharacter:
                    {
                        var position = packet.ReadByte();
                        packet.Skip(3);
                        var validation = packet.ReadString();

                        try
                        {
                            var account = _mapper.Map<AccountModel>(await _sender.Send(new AccountByIdQuery(client.AccountId)));

                            if (account.CharacterDeleteValidation(validation))
                            {
                                var deletedCharacter = await _sender.Send(new DeleteCharacterCommand(client.AccountId, position));

                                client.Send(new CharacterDeletedPacket(deletedCharacter).Serialize());
                            }
                            else
                            {
                                client.Send(new CharacterDeletedPacket(DeleteCharacterResultEnum.ValidationFail).Serialize());
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[DeleteCharacter] :: {ex.Message}");
                        }
                    }
                    break;

                case CharacterServerPacketEnum.GetCharacterPosition:
                    {
                        var position = packet.ReadByte();

                        try
                        {
                            var character = _mapper.Map<CharacterModel>(await _sender.Send(new CharacterByAccountIdAndPositionQuery(client.AccountId, position)));

                            while (character == null)
                            {
                                await Task.Delay(1500);

                                character = _mapper.Map<CharacterModel>(await _sender.Send(new CharacterByAccountIdAndPositionQuery(client.AccountId, position)));
                            }

                            await _sender.Send(new UpdateLastPlayedCharacterCommand(client.AccountId, character.Id));
                            await _sender.Send(new UpdateCharacterChannelCommand(character.Id));
                            await _sender.Send(new UpdateAccountWelcomeFlagCommand(character.AccountId));
                            await _sender.Send(new UpdateCharacterInitialPacketSentOnceSentCommand(character.Id, false));

                            client.Send(new ConnectGameServerInfoPacket(
                                _configuration[GameServerAddress], _configuration[GameServerPort], character.Location.MapId).Serialize());

                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[GetCharacterPosition] :: {ex.Message}");
                        }
                    }
                    break;

                case CharacterServerPacketEnum.ConnectGameServer:
                    {
                        client.Send(new ConnectGameServerPacket().Serialize());
                    }
                    break;

                default:
                    _logger.Warning($"Unknown packet. Type: {packet.Type} Length: {packet.Length}.");
                    break;
            }
        }

        /// <summary>
        /// Disposes the entire object.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}