namespace DigitalWorldOnline.Commons.Enums.ClientEnums
{
    /// <summary>
    /// Hatch Minigame Result Enum
    /// Used for minigame button click results
    /// </summary>
    public enum HatchMiniGameResultEnum
    {
        /// <summary>
        /// Success - Bar fully charged and clicked in time
        /// </summary>
        Success = 0,

        /// <summary>
        /// Fail - Clicked too early or too late
        /// </summary>
        Fail = 1,

        /// <summary>
        /// Break - Bar timer exceeded, no click
        /// </summary>
        Break = 2
    }

    /// <summary>
    /// Hatch Minigame Type Enum
    /// Identifies which type of minigame is being played
    /// </summary>
    public enum HatchMiniGameTypeEnum
    {
        /// <summary>
        /// Hatch Egg minigame (DIGMON) - 7 bars
        /// </summary>
        Hatch = 1,

        /// <summary>
        /// Enchant minigame
        /// </summary>
        Enchant = 2,

        /// <summary>
        /// None - Invalid
        /// </summary>
        None = 0
    }

    /// <summary>
    /// Hatch Minigame Error Codes
    /// </summary>
    public enum HatchMiniGameErrorEnum
    {
        /// <summary>
        /// Previous minigame not finished/reset
        /// </summary>
        PreviousGameNotReset = 1,

        /// <summary>
        /// Hatch rate at 0% or max level reached
        /// </summary>
        InvalidHatchRate = 2,

        /// <summary>
        /// Egg and probability 100% - can't play
        /// </summary>
        MaxProbability = 3
    }
}