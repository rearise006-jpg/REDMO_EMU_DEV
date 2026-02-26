namespace DigitalWorldOnline.Commons.Models.Config
{
    public sealed partial class MobLocationConfigModel : Location
    {
        /// <summary>
        /// Unique sequential identifier.
        /// </summary>
        public long Id { get; private set; }

        public static readonly (int X, int Y)[] SpecialMobSpawnPositions = new[]
        {
            (22998, 11996),
            (39410, 14074),
            (54561, 18250),
            (35818, 30920),
            (8751, 27486)
        };
    }
}