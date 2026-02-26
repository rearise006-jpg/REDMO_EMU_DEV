using DigitalWorldOnline.Commons.DTOs.Events;
using DigitalWorldOnline.Commons.DTOs.Routine;
using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;

namespace DigitalWorldOnline.Infrastructure.Repositories.Routine
{
    public class RoutineRepository : IRoutineRepository
    {
        private readonly DatabaseContext _context;

        public RoutineRepository(DatabaseContext context)
        {
            _context = context;
        }

        public class BitwiseOperations
        {
            public static int GetBitValue(int[] array, int x)
            {
                int arrIDX = x / 32;
                int bitPosition = x % 32;

                // Return 0 (incomplete) if index is out of bounds instead of throwing
                if (arrIDX >= array.Length || arrIDX < 0)
                    return 0;

                int value = array[arrIDX];
                return (value >> bitPosition) & 1;
            }

            public static void SetBitValue(ref int[] array, int x, int bitValue)
            {
                int arrIDX = x / 32;
                int bitPosition = x % 32;

                // Resize array if needed instead of throwing
                if (arrIDX >= array.Length)
                {
                    int newSize = arrIDX + 1;
                    Array.Resize(ref array, newSize);
                }

                if (arrIDX < 0)
                    throw new ArgumentOutOfRangeException("Invalid array index: negative index");

                if (bitValue != 0 && bitValue != 1)
                    throw new ArgumentException("Invalid bit value. Only 0 or 1 are allowed.");

                int value = array[arrIDX];
                int mask = 1 << bitPosition;

                if (bitValue == 1)
                    array[arrIDX] = value | mask;
                else
                    array[arrIDX] = value & ~mask;
            }
        }

        // Quest

        public async Task ExecuteDailyQuestsAsync(List<short> questIdList)
        {
            var progressList = await _context.CharacterProgress
                .ToListAsync();

            foreach (var progress in progressList)
            {
                foreach (var questId in questIdList)
                {
                    progress.CompletedDataValue = MarkQuestIncomplete(questId, progress.CompletedDataValue);
                }

                _context.Update(progress);
            }

            await _context.SaveChangesAsync();
        }

        public int[] MarkQuestIncomplete(int qIDX, int[] CompleteDataInt)
        {
            // Ensure array is not null
            if (CompleteDataInt == null)
            {
                // Initialize with reasonable size (e.g., 200 ints = 6400 quest bits)
                CompleteDataInt = new int[200];
            }

            int bitValue = BitwiseOperations.GetBitValue(CompleteDataInt, qIDX - 1);

            if (bitValue == 1)
            {
                BitwiseOperations.SetBitValue(ref CompleteDataInt, qIDX - 1, 0);
            }

            return CompleteDataInt;
        }

        // DailyReward

        public async Task ExecuteDailyRewardsAsync()
        {
            try
            {
                var accounts = await _context.Account.ToListAsync();

                foreach (var account in accounts)
                {
                    account.DailyRewardClaimed = false;
                    _context.Update(account);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to execute [ExecuteDailyRewardsAsync]: {ex.Message}");
                throw;
            }
        }

        // Coliseum

        public async Task ExecuteWeeklyColiseumAsync()
        {
            try
            {
                DateTime now = DateTime.UtcNow;

                // Check if there's already an active weekly ranking
                var existingWeekly = await _context.ArenaRanking
                    .Where(x => x.Type == ArenaRankingEnum.Weekly && x.EndDate > now)
                    .FirstOrDefaultAsync();

                // Only create a new period if there's no active one
                if (existingWeekly == null)
                {
                    // Find the start of week (Sunday at 00:00:00)
                    int daysUntilSunday = (int)now.DayOfWeek;

                    DateTime startDateOfWeek = now.Date.AddDays(-daysUntilSunday);
                    startDateOfWeek = new DateTime(startDateOfWeek.Year, startDateOfWeek.Month, startDateOfWeek.Day, 0, 0, 0, DateTimeKind.Utc);

                    // Find the end of week (Saturday at 23:59:59)
                    DateTime endOfWeek = startDateOfWeek.AddDays(7).AddSeconds(-1);

                    var newWeekly = new ArenaRankingDTO
                    {
                        Id = Guid.NewGuid(),
                        Type = ArenaRankingEnum.Weekly,
                        StartDate = startDateOfWeek,
                        EndDate = endOfWeek
                    };

                    _context.Add(newWeekly);
                    await _context.SaveChangesAsync();

                    Console.WriteLine($"Created new Weekly Arena period: {startDateOfWeek} to {endOfWeek}");
                }

                // Update the routine to schedule next execution
                await UpdateRoutineExecutionTimeAsync(RoutineTypeEnum.WeeklyColiseum);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to execute [ExecuteWeeklyColiseumAsync]: {ex.Message}");
                throw;
            }
        }

        public async Task ExecuteMonthlyColiseumAsync()
        {
            try
            {
                DateTime now = DateTime.UtcNow;

                // Check if there's already an active monthly ranking
                var existingMonthly = await _context.ArenaRanking
                    .Where(x => x.Type == ArenaRankingEnum.Monthly && x.EndDate > now)
                    .FirstOrDefaultAsync();

                // Only create a new period if there's no active one
                if (existingMonthly == null)
                {
                    DateTime firstDayOfThisMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    DateTime firstDayOfNextMonth = firstDayOfThisMonth.AddMonths(1);
                    DateTime lastDayOfThisMonth = firstDayOfNextMonth.AddSeconds(-1);

                    var newMonthly = new ArenaRankingDTO
                    {
                        Id = Guid.NewGuid(),
                        Type = ArenaRankingEnum.Monthly,
                        StartDate = firstDayOfThisMonth,
                        EndDate = lastDayOfThisMonth
                    };

                    _context.Add(newMonthly);
                    await _context.SaveChangesAsync();

                    Console.WriteLine($"Created new Monthly Arena period: {firstDayOfThisMonth} to {lastDayOfThisMonth}");
                }

                // Update the routine to schedule next execution
                await UpdateRoutineExecutionTimeAsync(RoutineTypeEnum.MonthlyColiseum);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to execute [ExecuteMonthlyColiseumAsync]: {ex.Message}");
                throw;
            }
        }

        public async Task ExecuteSeasonalColiseumAsync()
        {
            try
            {
                DateTime now = DateTime.UtcNow;

                // Check if there's already an active seasonal ranking
                var existingSeasonal = await _context.ArenaRanking
                    .Where(x => x.Type == ArenaRankingEnum.Seasonal && x.EndDate > now)
                    .FirstOrDefaultAsync();

                // Only create a new period if there's no active one
                if (existingSeasonal == null)
                {
                    int currentMonth = now.Month;
                    int firstMonthOfQuarter = ((currentMonth - 1) / 3) * 3 + 1;

                    DateTime startDateOfQuarter = new DateTime(now.Year, firstMonthOfQuarter, 1, 0, 0, 0, DateTimeKind.Utc);
                    DateTime endDateOfQuarter = startDateOfQuarter.AddMonths(3).AddSeconds(-1);

                    var newSeasonal = new ArenaRankingDTO
                    {
                        Id = Guid.NewGuid(),
                        Type = ArenaRankingEnum.Seasonal,
                        StartDate = startDateOfQuarter,
                        EndDate = endDateOfQuarter
                    };

                    _context.Add(newSeasonal);
                    await _context.SaveChangesAsync();

                    Console.WriteLine($"Created new Seasonal Arena period: {startDateOfQuarter} to {endDateOfQuarter}");
                }

                // Update the routine to schedule next execution
                await UpdateRoutineExecutionTimeAsync(RoutineTypeEnum.SeasonalColiseum);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error to execute arena Seasonal routine: {ex.Message}");
                throw;
            }
        }

        // Routine

        public async Task<List<RoutineDTO>> GetActiveRoutinesAsync()
        {
            return await _context.Routine
                .AsNoTracking()
                .Where(x => x.Active)
                .ToListAsync();
        }

        public async Task UpdateRoutineExecutionTimeAsync(RoutineTypeEnum routineType)
        {
            // Remove AsNoTracking to allow EF Core to properly track changes
            var dto = await _context.Routine
                .FirstOrDefaultAsync(x => x.Type == routineType);

            if (dto != null)
            {
                // Use UtcNow for consistency with other methods and calculate next run time
                dto.NextRunTime = DateTime.UtcNow.AddDays(dto.Interval);

                _context.Update(dto);
                await _context.SaveChangesAsync();
            }
        }
    }
}