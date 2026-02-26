using DigitalWorldOnline.Commons.Enums;
using DigitalWorldOnline.Commons.Utils;
using System.Drawing;

namespace DigitalWorldOnline.Commons.Models.Character
{
    public sealed partial class CharacterIncubatorModel
    {
        /// <summary>
        /// Inserts a new egg into the incubator.
        /// </summary>
        public void InsertEgg(int eggId)
        { 
            EggId = eggId;
            HatchLevel = 0;
        }

        /// <summary>
        /// Removes the current egg from the incubator.
        /// </summary>
        public void RemoveEgg()
        { 
            EggId = 0;
            HatchLevel = 0;
        }
        
        /// <summary>
        /// Inserts a new backup disk into the incubator.
        /// </summary>
        public void InsertBackupDisk(int backupDiskId) => BackupDiskId = backupDiskId;

        /// <summary>
        /// Removes the current backup disk from the incubator.
        /// </summary>
        public void RemoveBackupDisk() => BackupDiskId = 0;
        
        /// <summary>
        /// Increases the current incubator egg level.
        /// </summary>
        public void IncreaseLevel() => HatchLevel += 1;

        // <summary>
        /// ✅ NEW: Increases hatch level by specified amount
        /// </summary>
        public void IncreaseLevelBy(int amount)
        {
            // ✅ FIXED: Explicit cast from int to byte
            int newLevel = HatchLevel + amount;
            HatchLevel = (byte)Math.Min(newLevel, 100);
        }

        /// <summary>
        /// ✅ NEW: Sets hatch level with max cap
        /// </summary>
        public void SetHatchLevel(int newLevel)
        {
            // ✅ FIXED: Explicit cast from int to byte
            HatchLevel = (byte)Math.Min(Math.Max(newLevel, 0), 100);
        }


        /// <summary>
        /// Returns the hatch size based on current egg level;
        /// </summary>
        public short GetLevelSize()
        {
            return HatchLevel switch
            {
                3 => UtilitiesFunctions.RandomShort(8200, 10000),
                4 => UtilitiesFunctions.RandomShort(11000, 12500),
                5 => UtilitiesFunctions.RandomShort(11800, 13000),
                _ => 0,
            };
        }

        /// <summary>
        /// Returns the flag for perfect size based on the hatch grade.
        /// </summary>
        /// <param name="grade">Hatch grade enumeration</param>
        /// <param name="size">Hatch size</param>
        public bool PerfectSize(DigimonHatchGradeEnum grade, short size)
        {
            return grade switch
            {
                DigimonHatchGradeEnum.Lv3 => size == 10000,
                DigimonHatchGradeEnum.Lv4 => size == 12500,
                DigimonHatchGradeEnum.Lv5 => size == 13000,
                _ => false
            };
        }

        /// <summary>
        /// Flag for recent inserted egg.
        /// </summary>
        public bool NotDevelopedEgg => EggId > 0 && HatchLevel == 0;

        /// <summary>
        /// Adds success rate bonus from mini-game.
        /// </summary>
        /// <summary>
        /// ✅ FIXED: CurrentSuccessRate'i decimal olarak güncelle
        /// </summary>
        public void AddSuccessBonus(double bonusPercentage)
        {
            // ✅ FIXED: Double → Decimal dönüşümü
            decimal bonus = (decimal)bonusPercentage;
            CurrentSuccessRate = Math.Min(CurrentSuccessRate + bonus, 100m);
        }

        /// <summary>
        /// ✅ FIXED: Hatch seviyesini artır ve başarı oranı kontrol et
        /// </summary>
        public void IncreaseLevelWithBonus(int levelIncrease, double bonusPercentage)
        {
            // Hatch level'i artır
            HatchLevel = (byte)Math.Min(HatchLevel + levelIncrease, 100);

            // ✅ FIXED: Başarı oranını decimal olarak güncelle
            decimal bonus = (decimal)bonusPercentage;
            CurrentSuccessRate = Math.Min(CurrentSuccessRate + bonus, 100m);

            // ✅ FIXED: Güncelleme zamanını kaydet
            LastHatchTime = DateTime.UtcNow;
        }

        /// <summary>
        /// ✅ FIXED: Başarı oranını sıfırla
        /// </summary>
        public void ResetSuccessRate()
        {
            CurrentSuccessRate = 0m;
        }

        /// <summary>
        /// Başarı oranı yüzdesini al
        /// </summary>
        public decimal GetSuccessRatePercentage()
        {
            return Math.Min(Math.Max(CurrentSuccessRate, 0m), 100m);
        }

        /// <summary>
        /// Başarı oranına göre bonusu hesapla
        /// </summary>
        public int CalculateBonusFromSuccessRate()
        {
            if (CurrentSuccessRate >= 80m) return 15;
            if (CurrentSuccessRate >= 60m) return 10;
            if (CurrentSuccessRate >= 40m) return 5;
            if (CurrentSuccessRate >= 20m) return 0;
            return -5;
        }

        /// <summary>
        /// ✅ ADDED: Final score calculation
        /// </summary>
        public double CalculateFinalScore()
        {
            // ✅ FIXED: Decimal → Double dönüşümü
            double scoreFromRate = (double)CurrentSuccessRate * 1.5;
            double scoreFromLevel = HatchLevel * 10.0;
            double scoreFromMiniGames = MiniGamesPlayed * 5.0;

            FinalScore = Math.Round(scoreFromRate + scoreFromLevel + scoreFromMiniGames, 2);
            return FinalScore;
        }
    }
}
