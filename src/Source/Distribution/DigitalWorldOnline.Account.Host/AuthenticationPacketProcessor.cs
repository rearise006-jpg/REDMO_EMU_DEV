using AutoMapper;
using DigitalWorldOnline.Application.Separar.Commands.Create;
using DigitalWorldOnline.Application.Separar.Commands.Update;
using DigitalWorldOnline.Application.Separar.Commands.Delete;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Enums.Account;
using DigitalWorldOnline.Commons.Enums.ClientEnums;
using DigitalWorldOnline.Commons.Enums.PacketProcessor;
using DigitalWorldOnline.Commons.Extensions;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Models.Account;
using DigitalWorldOnline.Commons.Models.Servers;
using DigitalWorldOnline.Commons.Packets.AuthenticationServer;
using DigitalWorldOnline.Account.Models.Configuration;
using MediatR;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Text;
using Microsoft.Extensions.Options;

namespace DigitalWorldOnline.Account
{
    public sealed class AuthenticationPacketProcessor : IProcessor, IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;
        private readonly ISender _sender;
        private readonly ILogger _logger;
        private readonly AuthenticationServerConfigurationModel _authenticationServerConfiguration;

        private const string CharacterServerAddress = "CharacterServer:Address";

        private const int HandshakeDegree = 32321;

        public AuthenticationPacketProcessor(IMapper mapper, ILogger logger, ISender sender,
            IConfiguration configuration, IOptions<AuthenticationServerConfigurationModel> authenticationServerConfiguration)
        {
            _configuration = configuration;
            _authenticationServerConfiguration = authenticationServerConfiguration.Value;
            _mapper = mapper;
            _sender = sender;
            _logger = logger;
        }

        /// <summary>
        /// Process the arrived TCP packet, sent from the game client
        /// </summary>
        /// <param name="client">The game client whos sended the packet</param>
        /// <param name="data">The packet bytes array</param>
        public async Task ProcessPacketAsync(GameClient client, byte[] data)
        {
            var packet = new AuthenticationPacketReader(data);

            //_logger.Information("Received packet type {Type} from {Address}", packet.Enum, client.ClientAddress);

            switch (packet.Enum)
            {
                case AuthenticationServerPacketEnum.Connection:
                    {
                        var kind = packet.ReadByte();

                        var handshakeTimestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        var handshake = (short)(client.Handshake ^ HandshakeDegree);

                        client.Send(new ConnectionPacket(handshake, handshakeTimestamp));
                    }
                    break;

                case AuthenticationServerPacketEnum.KeepConnection:
                    break;

                case AuthenticationServerPacketEnum.LoginRequest:
                    {
                        var username = ExtractUsername(packet);
                        var password = ExtractPassword(packet, username);
                        var cpu = ExtractCpu(packet, username, password);
                        var gpu = ExtractGpu(packet, username, password, cpu);

                        try
                        {
                            var account = await _sender.Send(new AccountByUsernameQuery(username));

                            if (account == null)
                            {
                                await LogLoginAttempt(username, client.ClientAddress, LoginTryResultEnum.IncorrectUsername);
                                
                                SendLoginResult(client, LoginResultEnum.AUTH_FAILED, null);
                                break;
                            }

                            client.SetAccountId(account.Id);
                            client.SetAccessLevel(account.AccessLevel);

                            var servers = _mapper.Map<IEnumerable<ServerObject>>(await _sender.Send(new ServersQuery(client.AccessLevel)));

                            if (servers.Any(x => x.Maintenance == true) && client.AccessLevel != AccountAccessLevelEnum.Administrator)
                            {
                                SendLoginResult(client, LoginResultEnum.SERVER_IS_MAINTENANCE, account.SecondaryPassword);
                                return;
                            }

                            if (servers.Any(x => x.Overload == ServerOverloadEnum.Full) && client.AccessLevel != AccountAccessLevelEnum.Administrator)
                            {
                                SendLoginResult(client, LoginResultEnum.SERVER_CONNECT_USER_FULL, account.SecondaryPassword);
                                return;
                            }

                            if (account.AccountBlock != null)
                            {
                                var blockInfo = _mapper.Map<AccountBlockModel>(await _sender.Send(new AccountBlockByIdQuery(account.AccountBlock.Id)));

                                if (blockInfo.EndDate > DateTime.Now)
                                {
                                    TimeSpan timeRemaining = blockInfo.EndDate - DateTime.Now;

                                    uint secondsRemaining = (uint)timeRemaining.TotalSeconds;
                                    _logger.Debug($"Saving {username} login try for blocked account...");

                                    await _sender.Send(new CreateLoginTryCommand(username, client.ClientAddress,
                                        LoginTryResultEnum.AccountBlocked));
                                    client.Send(new LoginRequestBannedAnswerPacket(secondsRemaining, blockInfo.Reason));
                                    break;
                                }
                                else
                                {
                                    await _sender.Send(new DeleteBanCommand(blockInfo.Id));
                                }
                            }

                            if (account.Password != password.Encrypt())
                            {
                                await LogLoginAttempt(username, client.ClientAddress, LoginTryResultEnum.IncorrectPassword);

                                SendLoginResult(client, LoginResultEnum.ERROR_LOGINPASS, account.SecondaryPassword);
                                break;
                            }

                            if (account.IsOnline == 1)
                            {
                                SendLoginResult(client, LoginResultEnum.ALREADY_LOGIN, account.SecondaryPassword);
                                break;
                            }

                            SendLoginResult(client, LoginResultEnum.Success, account.SecondaryPassword);

                            _sender.Send(new UpdateAccountStatusCommand(account.Id, 1));

                            if (_authenticationServerConfiguration.UseHash)
                            {
                                var hashString = await _sender.Send(new ResourcesHashQuery());

                                client.Send(new ResourcesHashPacket(hashString));
                            }

                            if (account.SystemInformation == null)
                            {
                                await _sender.Send(new CreateSystemInformationCommand(account.Id, cpu, gpu, client.ClientAddress));
                            }
                            else
                            {
                                await _sender.Send(new UpdateSystemInformationCommand(account.SystemInformation.Id, account.Id, cpu, gpu, client.ClientAddress));
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[LoginRequest]: {ex}");
                        }
                    }
                    break;

                case AuthenticationServerPacketEnum.SecondaryPasswordRegister:
                    {
                        var securityPassword = packet.ReadZString();

                        await _sender.Send(new CreateOrUpdateSecondaryPasswordCommand(client.AccountId, securityPassword));

                        SendLoginResult(client, LoginResultEnum.Success, "exists");
                    }
                    break;

                case AuthenticationServerPacketEnum.SecondaryPasswordCheck:
                    {
                        var needToCheck = packet.ReadShort() == SecondaryPasswordCheckEnum.Check.GetHashCode();

                        try
                        {
                            var account = await _sender.Send(new AccountByIdQuery(client.AccountId));

                            if (account == null)
                                throw new KeyNotFoundException(nameof(account));

                            if (needToCheck)
                            {
                                var securitycode = packet.ReadZString();
                                var result = account.SecondaryPassword == securitycode
                                    ? SecondaryPasswordCheckEnum.CorrectOrSkipped
                                    : SecondaryPasswordCheckEnum.Incorrect;

                                await LogLoginAttempt(account.Username, client.ClientAddress,
                                    result == SecondaryPasswordCheckEnum.CorrectOrSkipped
                                        ? LoginTryResultEnum.Success
                                        : LoginTryResultEnum.IncorrectSecondaryPassword);

                                client.Send(new SecondaryPasswordCheckResultPacket(result));
                            }
                            else
                            {
                                await LogLoginAttempt(account.Username, client.ClientAddress, LoginTryResultEnum.Success);
                                client.Send(new SecondaryPasswordCheckResultPacket(SecondaryPasswordCheckEnum.CorrectOrSkipped).Serialize());
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[SecondaryPasswordCheck]: {ex}");
                        }
                    }
                    break;

                case AuthenticationServerPacketEnum.SecondaryPasswordChange:
                    {
                        var currentSecurityCode = packet.ReadZString();
                        var newSecurityCode = packet.ReadZString();

                        try
                        {
                            var account = await _sender.Send(new AccountByIdQuery(client.AccountId));

                            if (account == null)
                                throw new KeyNotFoundException(nameof(account));

                            if (account.SecondaryPassword == currentSecurityCode)
                            {
                                await _sender.Send(new CreateOrUpdateSecondaryPasswordCommand(client.AccountId, newSecurityCode));

                                client.Send(new SecondaryPasswordChangeResultPacket(SecondaryPasswordChangeEnum.Changed).Serialize());
                            }
                            else
                            {
                                client.Send(new SecondaryPasswordChangeResultPacket(SecondaryPasswordChangeEnum.IncorretCurrentPassword).Serialize());
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[SecondaryPasswordChange]: {ex}");
                        }
                    }
                    break;

                case AuthenticationServerPacketEnum.LoadServerList:
                    {
                        try
                        {
                            var servers = _mapper.Map<IEnumerable<ServerObject>>(await _sender.Send(new ServersQuery(client.AccessLevel)));

                            var serverObjects = servers.ToList();

                            foreach (var server in serverObjects)
                            {
                                server.UpdateCharacterCount(
                                    await _sender.Send(new CharactersInServerQuery(client.AccountId, server.Id)));
                            }

                            if (client.AccessLevel == AccountAccessLevelEnum.Administrator)
                            {
                                foreach (var server in servers.Where(s => s.Maintenance))
                                    server.Maintenance = false;
                            }

                            client.Send(new ServerListPacket(serverObjects).Serialize());
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[LoadServerList]: {ex}");
                        }
                    }
                    break;

                case AuthenticationServerPacketEnum.ConnectCharacterServer:
                    {
                        var serverId = packet.ReadInt();

                        try
                        {
                            await _sender.Send(new UpdateLastPlayedServerCommand(client.AccountId, serverId));

                            if (_authenticationServerConfiguration.UseHash)
                            {
                                var hashString = await _sender.Send(new ResourcesHashQuery());

                                client.Send(new ResourcesHashPacket(hashString));
                            }

                            var servers = _mapper.Map<IEnumerable<ServerObject>>(await _sender.Send(new ServersQuery(client.AccessLevel)));

                            var targetServer = servers.First(x => x.Id == serverId);

                            client.Send(new ConnectCharacterServerPacket(client.AccountId, _configuration[CharacterServerAddress], targetServer.Port.ToString()));
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[ConnectCharacterServer]: {ex}");
                        }
                    }
                    break;

                default:
                    {
                        _logger.Warning($"Unknown packet. Type: {packet.Type} Length: {packet.Length}.");
                    }
                    break;
            }
        }

        // ===================================================================================

        private static string ExtractGpu(AuthenticationPacketReader packet, string username, string password, string cpu)
        {
            packet.Seek(9 + username.Length + 2 + password.Length + 2 + cpu.Length + 2);

            var gpuSize = packet.ReadByte();

            var gpuArray = new byte[gpuSize];

            for (int i = 0; i < gpuSize; i++)
                gpuArray[i] = packet.ReadByte();

            return Encoding.ASCII.GetString(gpuArray).Trim();
        }

        private static string ExtractCpu(AuthenticationPacketReader packet, string username, string password)
        {
            packet.Seek(9 + username.Length + 2 + password.Length + 2);

            var cpuSize = packet.ReadByte();

            var cpuArray = new byte[cpuSize];

            for (int i = 0; i < cpuSize; i++)
                cpuArray[i] = packet.ReadByte();

            return Encoding.ASCII.GetString(cpuArray).Trim();
        }

        private static string ExtractPassword(AuthenticationPacketReader packet, string username)
        {
            packet.Seek(9 + username.Length + 2);
            var passwordSize = packet.ReadByte();

            var passwordArray = new byte[passwordSize];

            for (int i = 0; i < passwordSize; i++)
                passwordArray[i] = packet.ReadByte();

            return Encoding.ASCII.GetString(passwordArray).Trim();
        }

        private static string ExtractUsername(AuthenticationPacketReader packet)
        {
            packet.Seek(9);
            var usernameSize = packet.ReadByte();
            var usernameArray = new byte[usernameSize];

            for (int i = 0; i < usernameSize; i++)
                usernameArray[i] = packet.ReadByte();

            return Encoding.ASCII.GetString(usernameArray).Trim();
        }

        // ===================================================================================

        private void SendLoginResult(GameClient client, LoginResultEnum result, string? secondaryPassword)
        {
            var screen = string.IsNullOrEmpty(secondaryPassword)
                ? SecondaryPasswordScreenEnum.RequestSetup
                : SecondaryPasswordScreenEnum.RequestInput;

            client.Send(new LoginRequestAnswerPacket(result, screen));
        }

        private async Task LogLoginAttempt(string username, string ip, LoginTryResultEnum result)
        {
            await _sender.Send(new CreateLoginTryCommand(username, ip, result));
        }

        // ===================================================================================

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}