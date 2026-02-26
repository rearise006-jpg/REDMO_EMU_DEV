
namespace DigitalWorldOnline.Commons.Models.Character
{
    public sealed class CharacterActiveDeckModel
    {
        /// <summary>
        /// Identificador único da entrada do deck ativo.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Identificador do deck (referência ao tipo de deck configurado).
        /// </summary>
        public int DeckId { get; set; }

        /// <summary>
        /// Nome do deck atribuído ao personagem.
        /// </summary>
        public string DeckName { get; set; }

        /// <summary>
        /// Condições de ativação:
        /// 0 = Desativado,
        /// 1 = Passiva,
        /// 2 = Chance,
        /// 3 = Chance + Tempo.
        /// </summary>
        public int Condition { get; set; }

        /// <summary>
        /// Tipo de verificação:
        /// 0 = Sempre ativo,
        /// 1 = Ataque normal (auto attack),
        /// 2 = Ataque por skill.
        /// </summary>
        public int ATType { get; set; }

        /// <summary>
        /// Tipo da opção:
        /// 1 = Ataque (AT),
        /// 2 = Dano de skill (SkillDmg),
        /// 3 = Crítico (Crit),
        /// 4 = Resetar cooldown (Reset CD),
        /// 5 = HP,
        /// 6 = Velocidade de ataque (AtkSpeed).
        /// </summary>
        public int Option { get; set; }

        /// <summary>
        /// Valor absoluto do efeito aplicado.
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// Probabilidade de ativação do efeito (em centésimos de %, ex: 5000 = 50%).
        /// </summary>
        public int Probability { get; set; }

        /// <summary>
        /// Índice do slot no deck ativo (ex: 0 a 2).
        /// </summary>
        public byte DeckIndex { get; set; }

        /// <summary>
        /// Duração do efeito (em milissegundos).
        /// </summary>
        public int Time { get; set; }

        /// <summary>
        /// Identificador do personagem dono deste deck.
        /// </summary>
        public long CharacterId { get; set; }
    }
}
