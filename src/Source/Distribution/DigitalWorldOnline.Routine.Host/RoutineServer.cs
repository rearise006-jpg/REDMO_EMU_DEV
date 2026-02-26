using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Services;
using DigitalWorldOnline.Application.Routines.Commands;
using DigitalWorldOnline.Application.Routines.Queries;
using DigitalWorldOnline.Application.Separar.Queries;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Models.Mechanics;
using DigitalWorldOnline.Models.DTOs.Routine;
using Microsoft.Extensions.Hosting;
using AutoMapper;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Routine
{
    public sealed class RoutineServer : IHostedService
    {
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly AssetsLoader _assets;
        private readonly ILogger _logger;
        private readonly ISender _sender;
        private readonly IMapper _mapper;
        private readonly InfiniteWarRankRewardService _infiniteWarRankRewardService;

        public RoutineServer(IHostApplicationLifetime hostApplicationLifetime, AssetsLoader assets,
            ILogger logger, ISender sender, IMapper mapper, InfiniteWarRankRewardService infiniteWarRankRewardService)
        {
            _hostApplicationLifetime = hostApplicationLifetime;
            _assets = assets.Load();
            _logger = logger;
            _sender = sender;
            _mapper = mapper;
            _infiniteWarRankRewardService = infiniteWarRankRewardService;
        }

        /// <summary>
        /// The default hosted service "starting" method.
        /// </summary>
        /// <param name="cancellationToken">Control token for the operation</param>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            while (_assets.Loading)
                await Task.Delay(1000, cancellationToken);

            _hostApplicationLifetime.ApplicationStarted.Register(OnStarted);
            _hostApplicationLifetime.ApplicationStopping.Register(OnStopping);
            _hostApplicationLifetime.ApplicationStopped.Register(OnStopped);

            _infiniteWarRankRewardService.InitializeRankRewardData();

            LogTitleMessage(ConsoleColor.Green, "ROUTINE SERVER STARTED");

            while (!cancellationToken.IsCancellationRequested)
            {
                var routines = _mapper.Map<List<RoutineModel>>(await _sender.Send(new GetActiveRoutinesQuery(), cancellationToken));

                foreach (var routine in routines.Where(x => x.ExecutionTime))
                {
                    _logger.Information($"Executing routine [{routine.Name}] ...");

                    switch (routine.Type)
                    {
                        case RoutineTypeEnum.DailyQuests:
                            {
                                await _sender.Send(new ExecuteDailyQuestsRoutineCommand(_assets.DailyQuestList), cancellationToken);
                            }
                            break;

                        case RoutineTypeEnum.DailyReward:
                            {
                                await _sender.Send(new ExecuteDailyRewardRoutineCommand(), cancellationToken);
                            }
                            break;

                        case RoutineTypeEnum.DailyColiseum:
                            {

                            }
                            break;

                        case RoutineTypeEnum.WeeklyColiseum:
                            {
                                var rankingWeeklyInfo = _mapper.Map<ArenaRankingModel>(await _sender.Send(new GetLastArenaRankingQuery(ArenaRankingEnum.Weekly)));

                                if (rankingWeeklyInfo != null)
                                {
                                    // FIX: Use UtcNow instead of Now for UTC date comparison
                                    if (rankingWeeklyInfo.EndDate < DateTime.UtcNow)
                                    {
                                        _logger.Information($"Weekly Rank ended, creating new Weekly Rank !!");
                                        await _sender.Send(new ExecuteWeeklyColiseumRoutineCommand(), cancellationToken);

                                        _logger.Information($"Verifying Weekly Rank to send reward !!");
                                        await _infiniteWarRankRewardService.DistributeWeeklyRankRewards(rankingWeeklyInfo);
                                    }
                                    else
                                        _logger.Information($"Weekly Rank not ended yet. Current: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}, Ends: {rankingWeeklyInfo.EndDate:yyyy-MM-dd HH:mm:ss}");
                                }
                                else
                                {
                                    _logger.Information($"Weekly Rank not found, creating new Weekly Rank !!");
                                    await _sender.Send(new ExecuteWeeklyColiseumRoutineCommand(), cancellationToken);
                                }
                            }
                            break;

                        case RoutineTypeEnum.MonthlyColiseum:
                            {
                                var rankingMonthlyInfo = _mapper.Map<ArenaRankingModel>(await _sender.Send(new GetLastArenaRankingQuery(ArenaRankingEnum.Monthly)));

                                if (rankingMonthlyInfo != null)
                                {
                                    // FIX: Use UtcNow instead of Now for UTC date comparison
                                    if (rankingMonthlyInfo.EndDate < DateTime.UtcNow)
                                    {
                                        _logger.Information($"Month Rank ended, creating new Month Rank !!");
                                        await _sender.Send(new ExecuteMonthlyColiseumRoutineCommand(), cancellationToken);

                                        _logger.Information($"Verifying Month Rank to send reward !!");
                                        await _infiniteWarRankRewardService.DistributeMonthRankRewards(rankingMonthlyInfo);
                                    }
                                    else
                                        _logger.Information($"Month Rank not ended yet. Current: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}, Ends: {rankingMonthlyInfo.EndDate:yyyy-MM-dd HH:mm:ss}");
                                }
                                else
                                {
                                    _logger.Information($"Month Rank not found, creating new Month Rank !!");
                                    await _sender.Send(new ExecuteMonthlyColiseumRoutineCommand(), cancellationToken);
                                }
                            }
                            break;

                        case RoutineTypeEnum.SeasonalColiseum:
                            {
                                var rankingSeasonalInfo = _mapper.Map<ArenaRankingModel>(await _sender.Send(new GetLastArenaRankingQuery(ArenaRankingEnum.Seasonal)));

                                if (rankingSeasonalInfo != null)
                                {
                                    // FIX: Use UtcNow instead of Now for UTC date comparison
                                    if (rankingSeasonalInfo.EndDate < DateTime.UtcNow)
                                    {
                                        _logger.Information($"Seasonal Rank ended, creating new Seasonal Rank !!");
                                        await _sender.Send(new ExecuteSeasonalColiseumRoutineCommand(), cancellationToken);

                                        _logger.Information($"Verifying Seasonal Rank to send reward !!");
                                        await _infiniteWarRankRewardService.DistributeSeasonalRankRewards(rankingSeasonalInfo);
                                    }
                                    else
                                        _logger.Information($"Seasonal Rank not ended yet. Current: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}, Ends: {rankingSeasonalInfo.EndDate:yyyy-MM-dd HH:mm:ss}");
                                }
                                else
                                {
                                    _logger.Information($"Seasonal Rank not found, creating new Seasonal Rank !!");
                                    await _sender.Send(new ExecuteSeasonalColiseumRoutineCommand(), cancellationToken);
                                }
                            }
                            break;

                        case RoutineTypeEnum.DailySpinMachine:
                        case RoutineTypeEnum.WeeklySpinMachine:
                        case RoutineTypeEnum.EventRanking:
                        case RoutineTypeEnum.PvpRanking:
                            _logger.Error($"Routine not implemented {routine.Name}.");
                            break;
                    }

                    _logger.Information($"Routine executed !! Updating {routine.Name} execution time on database ...");

                    await Task.Delay(10000, cancellationToken);
                    await _sender.Send(new UpdateRoutineExecutionTimeCommand(routine.Type), cancellationToken);
                }

                var nextRun = 300000;
                var minutes = (nextRun / 60) / 1000;

                LogTitleMessage(ConsoleColor.Green, $"ROUTINES UPDATED !! NEXT RUN IN {minutes} MINUTES");

                await Task.Delay(nextRun, cancellationToken);
            }
        }

        /// <summary>
        /// The default hosted service "stopping" method
        /// </summary>
        /// <param name="cancellationToken">Control token for the operation</param>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <summary>
        /// The default hosted service "started" method action
        /// </summary>
        private void OnStarted()
        {
            LogTitleMessage(ConsoleColor.Green, "ROUTINE SERVER STARTED");
        }

        /// <summary>
        /// The default hosted service "stopping" method action
        /// </summary>
        private void OnStopping()
        {
            LogTitleMessage(ConsoleColor.Red, "STOPPING ROUTINE SERVER");
        }

        /// <summary>
        /// The default hosted service "stopped" method action
        /// </summary>
        private void OnStopped()
        {
            LogTitleMessage(ConsoleColor.Red, "ROUTINE SERVER STOPPED");
        }

        // ------------------------------------------------------------------------

        private void LogTitleMessage(ConsoleColor color, string message)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"|----------------------------------------------------|");
            Console.WriteLine($"|---------  {message.ToUpper()}");
            Console.WriteLine($"|----------------------------------------------------|");
            Console.ResetColor();
        }

        // ------------------------------------------------------------------------

    }
}