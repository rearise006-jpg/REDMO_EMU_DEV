namespace DigitalWorldOnline.Commons.Constants
{
    public static class DungeonServerConstants
    {
        // Variáveis de controle de tempo em segundos
        // Variables for time control in seconds
        public const int MapsSearchIntervalSeconds = 10;
        public const int MobsSearchIntervalSeconds = 10;
        public const int ConsignedShopsSearchIntervalSeconds = 15;

        // Distancia para visao de mobs
        // Distance to see mobs
        public const int StartToSeeMob = 18000;
        public const int StopToSeeMob = 18001;

        // Tempo em horas para fechar um mapa sem tamers
        // Time in hours to close map without tamers
        public const int InactiveMapCloseDelayHours = 2;

        // ID's para RoyalBase
        // RoyalBase ID's
        public static readonly int[] RoyalBaseMapIds = { 1701, 1702, 1703 };

        // ID's para ShadowLab
        // ShadowLab ID's
        public static readonly int[] ShadowLabMapIds = { 2001, 2002 };

        public static readonly int[] GankoomonTraining = { 50 };
    }
}
