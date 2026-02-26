namespace DigitalWorldOnline.Commons.DTOs.Mechanics
{
    public class MastersMatchDTO
    {
        public long Id { get; set; } // Chave primária
        public DateTime LastResetDate { get; set; } // Data/Hora do último reset
        public int TeamADonations { get; set; } // Total de doações do Time A
        public int TeamBDonations { get; set; } // Total de doações do Time B

        // Coleção para os rankers
        public List<MastersMatchRankerDTO> Rankers { get; set; }
    }
}
