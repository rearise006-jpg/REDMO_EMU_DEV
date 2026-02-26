using DigitalWorldOnline.Commons.Models.Character;
using DigitalWorldOnline.Commons.Models.Digimon;
using DigitalWorldOnline.Commons.Writers;

namespace DigitalWorldOnline.Commons.Packets.Chat
{
    public class EncyclopediaDeckEffectPacket : PacketWriter
    {
        private const int PacketNumber = 3237;

        /// <summary>
        /// Construtor para enviar um buff ativo no deck.
        /// </summary>
        /// <param name="deckOpt">Opção do Deck Ex: 1 = atack speed, 2 = critup.</param>
        /// <param name="deckBuffEndTime">Tempo para finalizar o buff</param>
        public EncyclopediaDeckEffectPacket(short deckOpt, int deckBuffEndTime)
        {
            Type(PacketNumber); // Define o tipo do pacote
            WriteShort(deckOpt);  
            WriteInt(deckBuffEndTime);

        }
    }

}