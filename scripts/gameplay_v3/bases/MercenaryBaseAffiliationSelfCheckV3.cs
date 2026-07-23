using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;
using GameplayV3.Company;
using GameplayV3.Mercenary;
using GameplayV3.Needs;
using WorldV2;

namespace GameplayV3.Bases;

public static class MercenaryBaseAffiliationSelfCheckV3
{
    public static string LastPerformanceSummary{get;private set;}="not run";

    public static bool TryValidate(out string reason)
    {
        try
        {
            using Fixture f=new(101,2);
            string companyA=f.Companies[0],companyB=f.Companies[1];
            var mercA=f.AddMercenary(companyA,new(200,200));
            f.Affiliations.ProcessUntilIdle();
            Require(f.Affiliations.TryGetMercenaryBase(mercA.MercenaryId,out var initial)&&initial?.State==MercenaryBaseAffiliationStateV3.Unassigned,"A: Mercenary without a base was not Unassigned.");

            AddBed(f.Bases,"bed_a",companyA,new(0,0));f.Rebuild(companyA);
            mercA.TrySetCurrentCell(new(new Vector2I(0,0)),out _);f.Affiliations.RequestReevaluation(mercA.MercenaryId,"EnteredBaseCore");f.Affiliations.ProcessUntilIdle();
            bool coreAssigned=Assigned(f,mercA.MercenaryId,out string baseA);f.Affiliations.TryGetMercenaryBase(mercA.MercenaryId,out var core);BaseAreaV3 debugArea=f.Bases.Areas.GetForCompany(companyA)[0];Require(coreAssigned&&core?.AssignmentSource==MercenaryBaseAssignmentSourceV3.InsideBaseCore,$"C: Core assignment failed state={core?.State} source={core?.AssignmentSource} base={core?.BaseAreaId} cell={mercA.CurrentCell} areas={f.Bases.Areas.GetForCompany(companyA).Count} ranges={f.Facilities.GetBaseAreasWhoseActivityRangeContains(companyA,mercA.CurrentCell).Count} bounds={debugArea.Bounds} contains={debugArea.Contains(mercA.CurrentCell.Value)} cells={debugArea.InfluenceCellCount}.");
            RestFacilitySlotV3 slotA=Slot("bed_a",new(0,0));Require(f.Needs.Assignments.TryAssign(mercA.MercenaryId,slotA,out reason),reason);f.Affiliations.ProcessUntilIdle();
            Require(f.Affiliations.TryGetMercenaryBase(mercA.MercenaryId,out var bed)&&bed?.BaseAreaId==baseA&&bed.AssignmentSource==MercenaryBaseAssignmentSourceV3.AssignedBed,"B: Assigned bed did not win affiliation priority.");

            var rangeMerc=f.AddMercenary(companyA,new(40,0));f.Affiliations.ProcessUntilIdle();Require(f.Affiliations.TryGetMercenaryBase(rangeMerc.MercenaryId,out var range)&&range?.BaseAreaId==baseA&&range.AssignmentSource==MercenaryBaseAssignmentSourceV3.InsideActivityRange,"D: Activity range assignment failed.");
            AddBed(f.Bases,"bed_b",companyA,new(80,0));f.Rebuild(companyA);var mercB=f.AddMercenary(companyA,new(80,0));f.Needs.Assignments.TryAssign(mercB.MercenaryId,Slot("bed_b",new(80,0)),out _);f.Affiliations.ProcessUntilIdle();Require(Assigned(f,mercB.MercenaryId,out string baseB)&&baseB!=baseA,"E: Two-base bed assignments were not isolated.");

            int remapEvents=0;f.Affiliations.MercenaryBaseRemapped+=_=>remapEvents++;string[] bridge=Enumerable.Range(1,9).Select(i=>$"bridge_{i}").ToArray();for(int i=0;i<bridge.Length;i++)AddBed(f.Bases,bridge[i],companyA,new((i+1)*8,0));f.Rebuild(companyA);Require(f.Bases.Areas.GetForCompany(companyA).Count==1,"F: Merge did not produce one base.");Require(Assigned(f,mercA.MercenaryId,out string survivor)&&Assigned(f,mercB.MercenaryId,out string mergedB)&&survivor==mergedB&&remapEvents>0,"F: Mercenary merge remap failed.");

            foreach(string id in bridge)f.Bases.RemoveSource(id);f.Rebuild(companyA);Require(f.Bases.Areas.GetForCompany(companyA).Count==2,"G: Split did not restore two bases.");bool hasSplitA=Assigned(f,mercA.MercenaryId,out string splitA),hasSplitB=Assigned(f,mercB.MercenaryId,out string splitB);Require(hasSplitA&&hasSplitB&&splitA!=splitB,"G: Split ignored assigned bed/source membership.");

            f.Bases.RemoveSource("bed_a");f.Rebuild(companyA);Require(Assigned(f,mercA.MercenaryId,out string reassigned)&&reassigned==splitB,"H: Removed base did not choose the remaining company base.");f.Bases.RemoveSource("bed_b");f.Rebuild(companyA);Require(f.Affiliations.TryGetMercenaryBase(mercA.MercenaryId,out var removed)&&removed?.State==MercenaryBaseAffiliationStateV3.Unassigned&&removed.BaseAreaId==null,"H: Last-base removal retained a retired id.");

            AddBed(f.Bases,"company_b_bed",companyB,new(300,0));f.Rebuild(companyB);f.Mercenaries.Registry.TryGetProfile(rangeMerc.MercenaryId,out var profile);Require(f.Mercenaries.Registry.TryRemoveMercenary(rangeMerc.MercenaryId,out reason),reason);DateTime created=profile!.CreatedUtc;Require(MercenaryStateV3.TryCreate(profile.MercenaryId,companyB,new(new Vector2I(300,0)),MercenaryActivityStateV3.Idle,created,out var moved,out reason),reason);Require(f.Mercenaries.Registry.TryRegisterMercenary(profile,moved,out reason),reason);f.Affiliations.ProcessUntilIdle();Require(Assigned(f,rangeMerc.MercenaryId,out string companyChanged)&&f.Bases.Areas.TryGet(companyChanged,out var changedBase)&&changedBase?.CompanyId==companyB,"I: Company change retained the previous-company base.");

            AddBed(f.Bases,"stable_a",companyA,new(0,200));AddBed(f.Bases,"stable_b",companyA,new(120,200));f.Rebuild(companyA);var stable=f.AddMercenary(companyA,new(0,200));f.Affiliations.ProcessUntilIdle();Require(Assigned(f,stable.MercenaryId,out string stableId),"J: Stability setup failed.");foreach(int x in new[]{47,49,47,49}){stable.TrySetCurrentCell(new(new Vector2I(x,200)),out _);f.Affiliations.RequestReevaluation(stable.MercenaryId,"BoundaryProbe");f.Affiliations.ProcessUntilIdle();Require(f.Affiliations.IsMercenaryAssignedToBase(stable.MercenaryId,stableId),"J: Activity boundary jitter changed the base.");}

            Require(f.Affiliations.Diagnostics.MercenaryBaseCartesianComparisonCount==0&&f.Affiliations.Diagnostics.PerMercenaryBaseNodeCount==0&&f.Affiliations.Diagnostics.PerMercenaryBaseProcessCount==0&&f.Affiliations.Diagnostics.MercenaryBaseTriggeredChunkGenerationCount==0,"Forbidden mercenary-base architecture was used.");
            reason=string.Empty;return true;
        }
        catch(Exception exception){reason=exception.Message;return false;}
    }

    public static string RunPerformanceFixture()
    {
        Stopwatch watch=Stopwatch.StartNew();using Fixture f=new(202,4);const int baseCount=100,mercenaryCount=1000;List<(string Id,string Company,Vector2I Cell)> anchors=new();
        for(int i=0;i<baseCount;i++){string company=i<40?f.Companies[(i/2)%4]:f.Companies[i%4];Vector2I cell=i<40?new((i/2)*512+(i%2)*16,((i/2)%4)*4096):new(12000+(i%20)*256,(i/20)*256+(i%4)*4096);string id=$"perf_bed_{i}";AddBed(f.Bases,id,company,cell);anchors.Add((id,company,cell));}
        foreach(string company in f.Companies)f.Rebuild(company,false);f.Facilities.ProcessUntilIdle();
        for(int i=0;i<mercenaryCount;i++){var anchor=anchors[i%baseCount];Vector2I cell=(i%4) switch{0=>anchor.Cell,1=>anchor.Cell+new Vector2I(1,0),2=>anchor.Cell+new Vector2I(40,0),_=>anchor.Cell+new Vector2I(90,0)};var state=f.AddMercenary(anchor.Company,cell);if(i%4==0)f.Needs.Assignments.TryAssign(state.MercenaryId,Slot(anchor.Id,anchor.Cell),out _);}
        f.Affiliations.ProcessUntilIdle();Require(f.Affiliations.Count==mercenaryCount,"Performance fixture lost mercenary affiliations.");
        for(int cycle=0;cycle<20;cycle++){int pair=cycle;string company=anchors[pair*2].Company;string bridge=$"perf_bridge_{cycle}";AddBed(f.Bases,bridge,company,anchors[pair*2].Cell+new Vector2I(8,0));f.Rebuild(company);f.Bases.RemoveSource(bridge);f.Rebuild(company);}
        for(int i=0;i<20;i++){var anchor=anchors[80+i];f.Bases.RemoveSource(anchor.Id);f.Rebuild(anchor.Company);}
        f.Affiliations.ProcessUntilIdle();foreach(string id in f.Mercenaries.Registry.GetAllMercenaryIds()){Require(f.Affiliations.TryGetMercenaryBase(id,out var affiliation)&&affiliation!=null,"Performance affiliation missing.");if(affiliation?.BaseAreaId!=null)Require(f.Bases.Areas.TryGet(affiliation.BaseAreaId,out _),"Retired BaseAreaId survived churn.");}
        var d=f.Affiliations.Diagnostics;Require(d.MercenaryBaseCartesianComparisonCount==0&&d.PerMercenaryBaseNodeCount==0&&d.PerMercenaryBaseProcessCount==0&&d.MercenaryBaseTriggeredChunkGenerationCount==0,"Performance fixture used forbidden architecture.");watch.Stop();LastPerformanceSummary=$"companies=4 initialBases={baseCount} mercenaries={mercenaryCount} merge/split/remove=20/20/20 dirtyMax={MercenaryBaseAffiliationSettingsV3.MaxMercenaryReevaluationsPerTick} remaps={d.RemappedCount} retiredRefs=0 elapsed={watch.Elapsed.TotalMilliseconds:0.0}ms";return LastPerformanceSummary;
    }

    private sealed class Fixture:IDisposable
    {
        public Fixture(long revision,int companyCount){CompaniesSession=new();Require(CompaniesSession.TryInitializeLocalSinglePlayer(out _,out string reason),reason);Companies.Add(CompaniesSession.LocalContext.LocalCompanyId!);for(int i=1;i<companyCount;i++){string player=CompanyIdFactoryV3.CreatePlayerId();Require(CompaniesSession.CompanyRegistry.TryCreateCompany(player,$"Company {i}",out var company,out reason),reason);Companies.Add(company!.CompanyId);}Mercenaries=new(CompaniesSession.CompanyRegistry);Needs=new(revision);Bases=new(revision);Bases.SetWorldBounds(new(-1000,-1000,50000,50000));Facilities=new(revision,Bases);Affiliations=new(revision,Mercenaries,Needs,Bases,Facilities);}
        public CompanySessionV3 CompaniesSession{get;}public List<string> Companies{get;}=new();public MercenarySessionV3 Mercenaries{get;}public MercenaryNeedsSessionV3 Needs{get;}public BaseAreaSessionV3 Bases{get;}public FacilityAffiliationSessionV3 Facilities{get;}public MercenaryBaseAffiliationSessionV3 Affiliations{get;}
        public MercenaryStateV3 AddMercenary(string company,Vector2I cell){string id=MercenaryIdFactoryV3.CreateMercenaryId();DateTime now=DateTime.UtcNow;MercenaryAttributeSetV3.TryCreate(10,10,10,10,10,out var attributes,out _);MercenaryWorkSkillSetV3.TryCreate(8,8,8,8,8,8,8,out var skills,out _);MercenaryProfileV3.TryCreate(id,$"Merc {Mercenaries.Registry.Count}","placeholder",attributes,skills,now,out var profile,out _);MercenaryStateV3.TryCreate(id,company,new(cell),MercenaryActivityStateV3.Idle,now,out var state,out _);Require(Mercenaries.Registry.TryRegisterMercenary(profile,state,out string reason),reason);return state!;}
        public void Rebuild(string company,bool process=true){Bases.RebuildCompanyNow(company);if(process){Facilities.ProcessUntilIdle();Affiliations.ProcessUntilIdle();}}
        public void Dispose(){Affiliations.Dispose();Facilities.Dispose();}
    }

    private static bool Assigned(Fixture fixture,string id,out string baseId){baseId=string.Empty;if(!fixture.Affiliations.TryGetMercenaryBase(id,out var value)||value?.State!=MercenaryBaseAffiliationStateV3.Assigned||value.BaseAreaId==null)return false;baseId=value.BaseAreaId;return true;}
    private static RestFacilitySlotV3 Slot(string structureId,Vector2I cell)=>new($"slot_{structureId}",structureId,0,new(cell),1f,"Test");
    private static void AddBed(BaseAreaSessionV3 bases,string id,string company,Vector2I cell)=>bases.UpsertSource(new(id,company,BaseSpatialSourceKindV3.Bed,BaseSpatialSourceRoleV3.Anchor,new[]{new GlobalCellCoord(cell)},4,bases.NextSourceCreationOrder(),1));
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
}
