using GameplayV3.Session;

namespace GameplayV3.Network;

public enum RegionPermissionV3
{
    JoinRegion=1,
    ViewRegion=2,
    CommandOwnMercenary=3,
    UseStockpile=4,
    Build=5,
    Demolish=6,
    WithdrawResource=7
}

public sealed class RegionAccessPolicyV3
{
    public bool CanJoin(PlayerCompanyStateV3 company,RegionPersistentStateV3 region)=>
        region.RegionType==RegionTypeV3.SharedNeutral||
        region.RegionType==RegionTypeV3.PrivateEstate&&region.OwnerCompanyId==company.CompanyId;

    public bool CanView(PlayerCompanyStateV3 company,RegionPersistentStateV3 region)=>CanJoin(company,region);

    public bool CanCommandMercenary(PlayerCompanyStateV3 company,RegionPersistentStateV3 region,string mercenaryId)=>
        CanJoin(company,region)&&
        company.TryGetMercenaryPresence(mercenaryId,out MercenaryPresenceStateV3? presence)&&
        presence?.AtRegion==true&&presence.CurrentRegionId==region.RegionId&&
        company.MercenaryProfiles.TryGetState(mercenaryId,out var state)&&state?.CompanyId==company.CompanyId;
}
