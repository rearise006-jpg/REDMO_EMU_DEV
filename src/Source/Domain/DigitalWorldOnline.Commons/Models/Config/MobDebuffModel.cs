namespace DigitalWorldOnline.Commons.Models.Config
{ 
    public sealed partial class MobDebuffModel
    {
        public Guid Id { get; set; }

        public long BuffListId { get; set; }

        public DateTime? LastDotTick { get; set; }

        public MobDebuffModel()
        {
            Id = Guid.NewGuid();
        }
    }
}