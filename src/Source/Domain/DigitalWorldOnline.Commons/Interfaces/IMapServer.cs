using DigitalWorldOnline.Commons.Entities;
using DigitalWorldOnline.Commons.Models;
using DigitalWorldOnline.Commons.Models.Config;
using DigitalWorldOnline.Commons.Models.Summon;

namespace DigitalWorldOnline.Commons.Interfaces
{
    public interface IMapServer
    {
        // Broadcast
        void BroadcastForTamerViewsAndSelf(long tamerId, byte[] packet);
        void BroadcastForTargetTamers(long tamerId, byte[] packet);


        // Handler
        GameClient FindClientByTamerHandle(int handler);
        MobConfigModel? GetMobByHandler(short mapId, int handler, byte channel);
        SummonMobModel? GetMobByHandler(short mapId, int handler, bool summon, byte channel);


        // Attack
        bool MobsAttacking(short mapId, long tamerId, byte channel);
        bool MobsAttacking(short mapId, long tamerId, bool Summon, byte channel);


        // GetMobsNearby
        List<MobConfigModel> GetMobsNearbyPartner(Location location, int range, byte channel);
        List<MobConfigModel> GetMobsNearbyTargetMob(short mapId, int handler, int range, byte channel);
        List<SummonMobModel> GetMobsNearbyPartner(Location location, int range, bool summon, byte channel);
        List<SummonMobModel> GetMobsNearbyTargetMob(short mapId, int handler, int range, bool Summon, byte channel);


        // AddSummons
        void AddSummonMobs(short mapId, SummonMobModel summon, byte channel);


        // Live Channels
        IDictionary<byte, byte> GetLiveChannelsAndPlayerCountsForMap(int mapId);
    }
}