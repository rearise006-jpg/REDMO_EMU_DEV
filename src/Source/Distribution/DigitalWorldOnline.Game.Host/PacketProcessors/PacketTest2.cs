using AutoMapper;
using DigitalWorldOnline.Application.Separar.Commands.Delete;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Packets.GameServer;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Game.Managers;
using MediatR;
using Serilog;


namespace DigitalWorldOnline.Game.PacketProcessors
{
    public class DigimonPacketTest2 : IGamePacketProcessor
    {
        public GameServerPacketEnum Type => GameServerPacketEnum.PacketTest2;

        private readonly StatusManager _statusManager;
        private readonly IMapper _mapper;
        private readonly ILogger _logger;
        private readonly ISender _sender;

        public DigimonPacketTest2(
            StatusManager statusManager,
            IMapper mapper,
            ILogger logger,
            ISender sender)
        {
            _statusManager = statusManager;
            _mapper = mapper;
            _logger = logger;
            _sender = sender;
        }

        public async Task Process(GameClient client, byte[] packetData)
        {
            var packet = new GamePacketReader(packetData);

            try
            {
                //_logger.Information($"Processing packet 3136 for client {client.TamerId}.");

                //// Tente interpretar os dados do pacote com os métodos de leitura apropriados.
                //var field1 = packet.ReadInt();
                //_logger.Information($"Field1 (Int): {field1}");

                //var field2 = packet.ReadByte();
                //_logger.Information($"Field2 (Byte): {field2}");

                var field3 = packet.ReadString();
                //_logger.Information($"Field3 (String): {field3}");
                // Verificação de bytes brutos para entender a string
                var byteArrayForString = packet.ReadBytes(20);  // Supondo que a string tenha no máximo 20 bytes
                                                                //_logger.Information($"Raw bytes for Field3: {BitConverter.ToString(byteArrayForString)}");

                // Insira a lógica específica para lidar com o pacote aqui.
                //_logger.Debug($"Packet 3132 processed successfully for client {client.TamerId}.");

            }
            catch (Exception ex)
            {
                _logger.Error($"Error processing packet 3132 for client {client.TamerId}: {ex.Message}");
            }
        }
    }
}
