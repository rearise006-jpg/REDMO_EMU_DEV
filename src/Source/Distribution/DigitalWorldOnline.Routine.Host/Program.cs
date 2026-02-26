using DigitalWorldOnline.Application;
using DigitalWorldOnline.Application.Admin.Repositories;
using DigitalWorldOnline.Application.Extensions;
using DigitalWorldOnline.Application.Services;
using DigitalWorldOnline.Commons.Utils;
using DigitalWorldOnline.Commons.Interfaces;
using DigitalWorldOnline.Commons.Repositories.Admin;
using DigitalWorldOnline.Infrastructure;
using DigitalWorldOnline.Infrastructure.Mapping;
using DigitalWorldOnline.Infrastructure.Repositories.Account;
using DigitalWorldOnline.Infrastructure.Repositories.Admin;
using DigitalWorldOnline.Infrastructure.Repositories.Character;
using DigitalWorldOnline.Infrastructure.Repositories.Config;
using DigitalWorldOnline.Infrastructure.Repositories.Routine;
using DigitalWorldOnline.Infrastructure.Repositories.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Globalization;
using System.Reflection;
using MediatR;
using Serilog;

namespace DigitalWorldOnline.Routine
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Run();
        }

        private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine(((Exception)e.ExceptionObject).InnerException);
            if (e.IsTerminating)
            {
                var message = "";
                var exceptionStackTrace = "";

                if (e.ExceptionObject is Exception exception) 
                {
                    message =  exception.Message;
                    exceptionStackTrace = exception.StackTrace;
                }

                Console.WriteLine($"{message}");
                Console.WriteLine($"{exceptionStackTrace}");
                Console.WriteLine("Terminating by unhandled exception...");
            }
            else
                Console.WriteLine("Received unhandled exception.");

            Console.ReadLine();
        }

        public static IHost CreateHostBuilder(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            Console.Title = $"Edge of Infinity :: RoutineServer";

            Moldura();

            var host = Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .UseEnvironment("Development")
                .ConfigureServices((context, services) =>
                {
                    services.AddDbContext<DatabaseContext>();

                    services.AddScoped<IAdminQueriesRepository, AdminQueriesRepository>();
                    services.AddScoped<IAdminCommandsRepository, AdminCommandsRepository>();

                    services.AddScoped<IAccountQueriesRepository, AccountQueriesRepository>();
                    services.AddScoped<IAccountCommandsRepository, AccountCommandsRepository>();

                    services.AddScoped<IServerQueriesRepository, ServerQueriesRepository>();
                    services.AddScoped<IServerCommandsRepository, ServerCommandsRepository>();

                    services.AddScoped<ICharacterQueriesRepository, CharacterQueriesRepository>();
                    services.AddScoped<ICharacterCommandsRepository, CharacterCommandsRepository>();

                    services.AddScoped<IConfigQueriesRepository, ConfigQueriesRepository>();
                    services.AddScoped<IConfigCommandsRepository, ConfigCommandsRepository>();

                    services.AddScoped<IRoutineRepository, RoutineRepository>();

                    AddAutoMapper(services);

                    services.AddSingleton<AssetsLoader>();
                    services.AddSingleton<InfiniteWarRankRewardService>();

                    services.AddSingleton<ISender, ScopedSender<Mediator>>();
                    services.AddSingleton(ConfigureLogger(context.Configuration));
                    services.AddHostedService<RoutineServer>();

                    services.AddTransient<Mediator>();
                    services.AddMediatR(typeof(MediatorApplicationHandlerExtension).GetTypeInfo().Assembly);
                })
                .ConfigureHostConfiguration(hostConfig =>
                {
                    hostConfig.SetBasePath(Directory.GetCurrentDirectory())
                              .AddEnvironmentVariables(Constants.Configuration.EnvironmentPrefix)
                              .AddUserSecrets<Program>();
                })
                .Build();

            var _logger = host.Services.GetRequiredService<ILogger>();

            _logger.Information("Services Loaded !!");
            Console.WriteLine();

            return host;
        }

        private static void AddAutoMapper(IServiceCollection services)
        {
            services.AddAutoMapper(typeof(AccountProfile));
            services.AddAutoMapper(typeof(AssetsProfile));
            services.AddAutoMapper(typeof(CharacterProfile));
            services.AddAutoMapper(typeof(ConfigProfile));
            services.AddAutoMapper(typeof(DigimonProfile));
            services.AddAutoMapper(typeof(GameProfile));
            services.AddAutoMapper(typeof(SecurityProfile));
            services.AddAutoMapper(typeof(ArenaProfile));
            services.AddAutoMapper(typeof(RoutineProfile));
        }

        private static ILogger ConfigureLogger(IConfiguration configuration)
        {
            return new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.RollingFile(configuration["Log:DebugRepository"] ?? "logs\\Debug\\RoutineServer", Serilog.Events.LogEventLevel.Information, retainedFileCountLimit: 10)
                .CreateLogger();
        }

        // ------------------------------------------------------------------------

        private static void Moldura()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"|---------------------------------------------------------------------|");
            Console.WriteLine(@"|  World of Tamers - Edge Infinity");
            Console.WriteLine($"|---------------------------------------------------------------------|");
            Console.WriteLine();
            Console.ResetColor();
        }

        // ------------------------------------------------------------------------
    }
}