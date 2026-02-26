
namespace DigitalWorldOnline.Commons.Models.Assets
{
    public sealed class DeckOptionModel
    {
        public short GroupIdx { get; set; }

        /// <summary>
        /// Nome do grupo (máx. 64 wchar_t → 128 bytes).
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// Descrição do grupo (máx. 512 wchar_t → 1024 bytes).
        /// </summary>
        public string Explain { get; set; } = string.Empty;

        /// <summary>
        /// Condições de ativação:
        /// 0 = Desativado,
        /// 1 = Passiva,
        /// 2 = Chance,
        /// 3 = Chance + Tempo.
        /// </summary>
        public short[] Condition { get; set; } = new short[3];

        /// <summary>
        /// Tipo de verificação:
        /// 0 = Sempre ativo,
        /// 1 = Ataque normal (auto attack),
        /// 2 = Ataque por skill.
        /// </summary>
        public short[] AT_Type { get; set; } = new short[3];

        /// <summary>
        /// Tipo da opção:
        /// 1 = Ataque (AT),
        /// 2 = Dano de skill (SkillDmg),
        /// 3 = Crítico (Crit),
        /// 4 = Resetar cooldown (Reset CD),
        /// 5 = HP,
        /// 6 = Velocidade de ataque (AtkSpeed).
        /// </summary>
        public short[] Option { get; set; } = new short[3];

        /// <summary>
        /// Valor numérico da opção aplicada.
        /// </summary>
        public short[] Val { get; set; } = new short[3];

        /// <summary>
        /// Probabilidade de ativação (em centésimos de % — ex: 5000 = 50%).
        /// </summary>
        public int[] Prob { get; set; } = new int[3];

        /// <summary>
        /// Duração da ativação (em milissegundos ou segundos, dependendo da implementação).
        /// </summary>
        public int[] Time { get; set; } = new int[3];
    }
}
