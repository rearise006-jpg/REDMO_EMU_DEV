using DigitalWorldOnline.Commons.DTOs.Routine;
using DigitalWorldOnline.Commons.Enums;

namespace DigitalWorldOnline.Commons.Interfaces
{
    public interface IRoutineRepository
    {
        // Quest
        Task ExecuteDailyQuestsAsync(List<short> questIdList);

        // Daily Reward
        Task ExecuteDailyRewardsAsync();

        // Arena (Coliseum)
        Task ExecuteWeeklyColiseumAsync();

        Task ExecuteMonthlyColiseumAsync();

        Task ExecuteSeasonalColiseumAsync();

        // Routines
        Task<List<RoutineDTO>> GetActiveRoutinesAsync();

        Task UpdateRoutineExecutionTimeAsync(RoutineTypeEnum routineType);
    }
}
