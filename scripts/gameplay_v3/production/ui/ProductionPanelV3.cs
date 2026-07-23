using System;
using System.Collections.Generic;
using System.Linq;
using GameplayV3.Construction;
using GameplayV3.Equipment;
using GameplayV3.Mercenary;
using GameplayV3.Session;
using GameplayV3.UI;
using Godot;
using WorldV2;

namespace GameplayV3.Production.UI;

public partial class ProductionPanelV3:Godot.Control
{
    private const int QueuePoolSize=20;
    private sealed class BillRow{public required VBoxContainer Root;public required Label Summary;public required Button Minus;public required Button Plus;public required Button PlusFive;public required Button Cancel;public string OrderId="";}
    private ProductionSessionV3? _session;private WorldManagerV2? _manager;private string _facilityId="",_recipeId="";private int _batches=1;private Label? _title,_facilityStatus,_detail,_batch,_status;private VBoxContainer? _recipeList,_queueList;private readonly List<Button> _recipeButtons=new();private readonly List<BillRow> _queueRows=new();private bool _initialized,_refreshQueued;private double _progressCredit;
    public int RecipeButtonCount=>_recipeButtons.Count;public int QueueRowCount=>_queueRows.Count;public int ActiveBillRowCount=>_queueRows.Count(x=>x.Root.Visible);public int RootCount=>_initialized?1:0;public int WorldInputLeakCount{get;private set;}public int EffectRefreshCount{get;private set;}public int SnapshotBuildCount{get;private set;}public int FullRefreshCount{get;private set;}public int ProgressRefreshCount{get;private set;}public int HiddenQueryCount{get;private set;}
    public event Action? PanelOpened;

    public void Initialize(WorldManagerV2 manager){if(_initialized)return;_initialized=true;_manager=manager;MouseFilter=MouseFilterEnum.Ignore;SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);BuildUi();BindSession();GameplaySessionV3.SessionBegan+=OnSessionBegan;Visible=false;SetProcess(true);}
    public bool TryOpenAt(InputEvent e,WorldV2GridRenderer grid){if(e is not InputEventMouseButton b||!b.Pressed||b.ButtonIndex!=MouseButton.Right||_manager==null)return false;Vector2I raw=grid.WorldToCell(GetViewport().GetCanvasTransform().AffineInverse()*b.Position);if(!_manager.TryGetConstructionSession(out ConstructionSessionV3? construction)||construction==null||!construction.Structures.TryGetStructureAtCell(new(raw),out var structure)||structure==null||structure.CompanyId!=_manager.LocalCompanyId||_session==null||!_session.TryGetFacility(structure.StructureId,out _))return false;Open(structure.StructureId);return true;}
    public void Open(string facilityId){if(_session==null||!_session.TryGetFacility(facilityId,out _))return;_facilityId=facilityId;Visible=true;if(_status!=null)_status.Text="";PanelOpened?.Invoke();RefreshAll();}
    public void Close(){Visible=false;_facilityId="";_recipeId="";}public bool HandleEscape(){if(!Visible)return false;Close();return true;}public void SetWorldMapBlocked(bool blocked){if(blocked)Close();}
    public override void _Process(double delta){if(!Visible)return;_progressCredit+=delta;if(_progressCredit<.25)return;_progressCredit%=.25;RefreshProgress();}
    private void BindSession(){if(_session!=null)_session.Changed-=OnChanged;GameplaySessionV3.TryGetProductionSession(out _session);if(_session!=null)_session.Changed+=OnChanged;}
    private void OnSessionBegan(){Close();BindSession();}private void OnChanged(ProductionEventV3 e){if(!Visible||e.FacilityId!=_facilityId||_refreshQueued)return;_refreshQueued=true;CallDeferred(MethodName.RefreshDeferred);}private void RefreshDeferred(){_refreshQueued=false;if(Visible)RefreshAll();}

    private void BuildUi()
    {
        PanelContainer panel=new(){Name="ProductionSidePanel"};GameplayUiShellV3.ConfigureSide(panel);AddChild(panel);
        MarginContainer margin=new();foreach(string side in new[]{"margin_left","margin_right","margin_top","margin_bottom"})margin.AddThemeConstantOverride(side,12);panel.AddChild(margin);
        VBoxContainer root=new(){Name="ProductionBillsBody"};margin.AddChild(root);
        HBoxContainer header=new();_title=new Label{SizeFlagsHorizontal=SizeFlags.ExpandFill};_title.AddThemeFontSizeOverride("font_size",18);header.AddChild(_title);Button close=new(){Text="\uB2EB\uAE30",MouseFilter=MouseFilterEnum.Stop};close.Pressed+=Close;header.AddChild(close);root.AddChild(header);
        _facilityStatus=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};root.AddChild(_facilityStatus);
        ScrollContainer scroll=new(){Name="BillsScroll",HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,VerticalScrollMode=ScrollContainer.ScrollMode.Auto,SizeFlagsHorizontal=SizeFlags.ExpandFill,SizeFlagsVertical=SizeFlags.ExpandFill,MouseFilter=MouseFilterEnum.Stop};VBoxContainer content=new(){SizeFlagsHorizontal=SizeFlags.ExpandFill};scroll.AddChild(content);root.AddChild(scroll);
        content.AddChild(new Label{Text="\uC81C\uC791 \uBA85\uB839 \uBAA9\uB85D"});_queueList=new VBoxContainer();content.AddChild(_queueList);for(int i=0;i<QueuePoolSize;i++)CreateBillRow(i);
        content.AddChild(new HSeparator());content.AddChild(new Label{Text="\uC0C8 \uC81C\uC791 \uBA85\uB839"});_recipeList=new VBoxContainer();content.AddChild(_recipeList);
        _detail=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};content.AddChild(_detail);HBoxContainer quantity=new();Button minus=new(){Text="-1"};minus.Pressed+=()=>SetBatches(_batches-1);quantity.AddChild(minus);_batch=new Label{CustomMinimumSize=new(90,0),HorizontalAlignment=HorizontalAlignment.Center};quantity.AddChild(_batch);Button plus=new(){Text="+1"};plus.Pressed+=()=>SetBatches(_batches+1);quantity.AddChild(plus);Button plus5=new(){Text="+5"};plus5.Pressed+=()=>SetBatches(_batches+5);quantity.AddChild(plus5);content.AddChild(quantity);Button add=new(){Text="\uC81C\uC791 \uBA85\uB839 \uCD94\uAC00"};add.Pressed+=AddOrder;content.AddChild(add);
        _status=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart};root.AddChild(_status);root.AddChild(new Label{Text="\uC7AC\uB8CC\uAC00 \uBD80\uC871\uD574\uB3C4 \uBA85\uB839\uC744 \uB4F1\uB85D\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4."});
    }
    private void CreateBillRow(int index){VBoxContainer root=new(){Visible=false,SizeFlagsHorizontal=SizeFlags.ExpandFill,CustomMinimumSize=new(300,0)};Label summary=new(){SizeFlagsHorizontal=SizeFlags.ExpandFill,AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new(280,0)};root.AddChild(summary);HBoxContainer commands=new(){SizeFlagsHorizontal=SizeFlags.ExpandFill};Button minus=new(){Text="-1"},plus=new(){Text="+1"},plusFive=new(){Text="+5"},cancel=new(){Text="\uBA85\uB839 \uCDE8\uC18C",SizeFlagsHorizontal=SizeFlags.ExpandFill};BillRow row=new(){Root=root,Summary=summary,Minus=minus,Plus=plus,PlusFive=plusFive,Cancel=cancel};minus.Pressed+=()=>ChangeOrder(row,-1);plus.Pressed+=()=>ChangeOrder(row,1);plusFive.Pressed+=()=>ChangeOrder(row,5);cancel.Pressed+=()=>CancelOrder(row);commands.AddChild(minus);commands.AddChild(plus);commands.AddChild(plusFive);commands.AddChild(cancel);root.AddChild(commands);_queueList!.AddChild(root);_queueRows.Add(row);}

    private void RefreshAll()
    {
        if(!Visible)return;if(_session==null||!_session.TryGetFacility(_facilityId,out var facility)||facility==null){Close();return;}FullRefreshCount++;SnapshotBuildCount++;
        SetText(_title,$"{FacilityName(facility.FacilityKind)} \u00B7 \uC81C\uC791 \uBA85\uB839");SetText(_facilityStatus,GetFacilitySummary(facility));
        var recipes=_session.GetAvailableRecipes(_facilityId);while(_recipeButtons.Count<recipes.Count){Button button=new(){ToggleMode=true,MouseFilter=MouseFilterEnum.Stop,CustomMinimumSize=new(0,44)};int index=_recipeButtons.Count;button.Pressed+=()=>SelectRecipe(index);_recipeList!.AddChild(button);_recipeButtons.Add(button);}for(int i=0;i<_recipeButtons.Count;i++){bool used=i<recipes.Count;_recipeButtons[i].Visible=used;if(!used)continue;var recipe=recipes[i];SetText(_recipeButtons[i],$"{recipe.DisplayName}\n{FormatInputs(recipe)} \u2192 {OutputName(recipe)} {recipe.OutputAmount}");_recipeButtons[i].ButtonPressed=recipe.RecipeId==_recipeId;}if(string.IsNullOrEmpty(_recipeId)||!recipes.Any(x=>x.RecipeId==_recipeId))_recipeId=recipes.FirstOrDefault()?.RecipeId??"";RefreshDetail();RefreshQueue(facility);
    }
    private void RefreshProgress(){if(_session==null||!_session.TryGetFacility(_facilityId,out var facility)||facility==null){Close();return;}ProgressRefreshCount++;SetText(_facilityStatus,GetFacilitySummary(facility));RefreshQueue(facility);}
    private void SelectRecipe(int index){var recipes=_session!.GetAvailableRecipes(_facilityId);if(index>=recipes.Count)return;_recipeId=recipes[index].RecipeId;RefreshAll();}
    private void RefreshDetail(){if(!Visible||_session==null||!_session.TryGetRecipe(_recipeId,out var recipe)||recipe==null)return;List<string> rows=new();int possible=int.MaxValue;foreach(var input in recipe.Inputs){int need=input.RequiredAmount*_batches,have=_session.GetResourceAvailability(_manager!.LocalCompanyId,input.ResourceType),missing=Math.Max(0,need-have);possible=Math.Min(possible,have/input.RequiredAmount);rows.Add($"{(missing==0?"[\uCDA9\uBD84]":"[\uBD80\uC871]")} {StarterProcessingContentV3.GetResourceDisplayName(input.ResourceType)} {need} / \uBCF4\uC720 {have}{(missing>0?$" \u00B7 {missing}\uAC1C \uBD80\uC871":"")}");}string usage=recipe.OutputResource is { } outputResource?StarterProcessingContentV3.GetUsageText(outputResource):string.Empty;SetText(_detail,$"{recipe.DisplayName}\n{recipe.ShortDescription}\n\n\uD604\uC7AC \uAC00\uB2A5  {Math.Max(0,possible)}\uD68C\n\uC608\uC0C1 \uACB0\uACFC  {OutputName(recipe)} {recipe.OutputAmount*_batches}\uAC1C\n\n\uCD1D \uD544\uC694\n{string.Join('\n',rows)}\n\n\uD544\uC694 \uB2A5\uB825  \uC81C\uC791\n\uAE30\uBCF8 \uC791\uC5C5\uB7C9  Batch\uB2F9 {recipe.BaseWorkSeconds:0.#}\uCD08{(usage.Length>0?$"\n\n\uC6A9\uB3C4\u00B7\uD6A8\uACFC\n{usage}":"")}");SetText(_batch,$"{_batches}\uD68C");EffectRefreshCount++;}
    private void RefreshQueue(ProductionFacilitySnapshotV3 facility){for(int i=0;i<_queueRows.Count;i++){BillRow row=_queueRows[i];bool used=i<facility.Queue.Count;row.Root.Visible=used;if(!used){row.OrderId="";continue;}ProductionOrderSnapshotV3 order=facility.Queue[i];row.OrderId=order.OrderId;_session!.TryGetRecipe(order.RecipeId,out var recipe);float progress=recipe==null||recipe.BaseWorkSeconds<=0?0:Math.Clamp(order.WorkProgressSeconds/recipe.BaseWorkSeconds*100,0,100);string worker=WorkerText(order.AssignedMercenaryId);string blocked=OrderReason(facility,order,recipe);SetText(row.Summary,$"{i+1}. {recipe?.DisplayName??order.RecipeId}\n\uC9C4\uD589 {order.CompletedBatches} / {order.RequestedBatches}\uD68C \u00B7 {StateText(order.State)}{(order.State==ProductionOrderStateV3.Producing?$" {progress:0}%":"")}\n\uC791\uC5C5\uC790 {worker}{(string.IsNullOrEmpty(blocked)?"":$"\n{blocked}")}");bool producing=order.State==ProductionOrderStateV3.Producing;row.Minus.Disabled=producing||order.RemainingBatches<=1;row.Cancel.Disabled=producing;row.Cancel.TooltipText=producing?"\uD604\uC7AC Batch\uAC00 \uB05D\uB09C \uD6C4 \uB0A8\uC740 \uBA85\uB839\uC744 \uCDE8\uC18C\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.":"\uB0A8\uC740 \uC81C\uC791 \uBA85\uB839\uC744 \uCDE8\uC18C\uD569\uB2C8\uB2E4.";}}
    private string GetFacilitySummary(ProductionFacilitySnapshotV3 facility){if(facility.Queue.Count==0)return "\uBA85\uB839\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";ProductionOrderSnapshotV3 order=facility.Queue[0];_session!.TryGetRecipe(order.RecipeId,out var recipe);float progress=recipe==null||recipe.BaseWorkSeconds<=0?0:Math.Clamp(order.WorkProgressSeconds/recipe.BaseWorkSeconds*100,0,100);return $"\uD604\uC7AC \uC0C1\uD0DC  {StateText(order.State)}\n\uD604\uC7AC \uBA85\uB839  {recipe?.DisplayName??order.RecipeId} {order.CompletedBatches}/{order.RequestedBatches}\uD68C\n\uD604\uC7AC \uC791\uC5C5\uC790  {WorkerText(order.AssignedMercenaryId)}\n\uC9C4\uD589\uB960  {progress:0}%\n\uB300\uAE30 \uBA85\uB839  {Math.Max(0,facility.Queue.Count-1)}\uAC74\n{OrderReason(facility,order,recipe)}";}
    private string WorkerText(string? id){if(string.IsNullOrEmpty(id))return "\uC5C6\uC74C";if(GameplaySessionV3.TryGetMercenarySession(out var mercenaries)&&mercenaries!=null&&mercenaries.Registry.TryGetMercenary(id,out var profile,out _)&&profile!=null)return $"{profile.DisplayName} \u00B7 \uC81C\uC791 {profile.WorkSkills.Production}";return id;}
    private string OrderReason(ProductionFacilitySnapshotV3 facility,ProductionOrderSnapshotV3 order,ProductionRecipeDefinitionV3? recipe){if(order.State==ProductionOrderStateV3.OutputBlocked)return order.LastFailureReason=="EquipmentOutputBufferFull"?"\uC644\uC131\uB41C \uC7A5\uBE44\uB97C \uBCF4\uAD00\uD560 \uACF5\uAC04\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.":"\uACB0\uACFC\uBB3C\uC744 \uB193\uC744 \uACF5\uAC04\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";if(order.State==ProductionOrderStateV3.Producing)return "\uC81C\uC791 \uC911";if(order.State==ProductionOrderStateV3.Ready)return string.IsNullOrEmpty(order.AssignedMercenaryId)?"\uC791\uC5C5\uC790 \uBC30\uC815 \uB300\uAE30":"\uC81C\uC791 \uC900\uBE44 \uC644\uB8CC";if(order.State==ProductionOrderStateV3.WaitingMaterials&&recipe!=null){Dictionary<GameplayV3.Resources.ResourceTypeV3,int> delivered=facility.MaterialBuffer.ToDictionary(x=>x.ResourceType,x=>x.RequiredAmount);List<string> missing=recipe.Inputs.Select(x=>(x.ResourceType,Amount:Math.Max(0,x.RequiredAmount-delivered.GetValueOrDefault(x.ResourceType)))).Where(x=>x.Amount>0).Select(x=>$"{StarterProcessingContentV3.GetResourceDisplayName(x.ResourceType)} {x.Amount}\uAC1C \uBD80\uC871").ToList();return missing.Count>0?string.Join(", ",missing):"\uC7AC\uB8CC \uC6B4\uBC18 \uC911";}return string.IsNullOrEmpty(order.LastFailureReason)?StateText(order.State):ReasonText(order.LastFailureReason);}
    private void SetBatches(int value){int next=Math.Clamp(value,1,20);if(next==_batches)return;_batches=next;RefreshDetail();}
    private void AddOrder(){if(_session==null)return;SetText(_status,_session.TryAddOrder(_manager!.LocalCompanyId,_facilityId,_recipeId,_batches,out string result)?$"{_batches}\uD68C \uC81C\uC791 \uBA85\uB839\uC744 \uCD94\uAC00\uD588\uC2B5\uB2C8\uB2E4.":ReasonText(result));RefreshAll();}
    private void ChangeOrder(BillRow row,int amount){if(_session==null||string.IsNullOrEmpty(row.OrderId))return;bool ok=amount>0?_session.TryIncreaseOrder(_manager!.LocalCompanyId,row.OrderId,amount,out string result):_session.TryDecreaseOrder(_manager!.LocalCompanyId,row.OrderId,-amount,out result);SetText(_status,ok?"\uC81C\uC791 \uBA85\uB839 \uC218\uB7C9\uC744 \uBCC0\uACBD\uD588\uC2B5\uB2C8\uB2E4.":ReasonText(result));RefreshAll();}
    private void CancelOrder(BillRow row){if(_session==null||string.IsNullOrEmpty(row.OrderId))return;SetText(_status,_session.TryCancelOrder(_manager!.LocalCompanyId,_facilityId,row.OrderId,out string result)?"\uC81C\uC791 \uBA85\uB839\uC744 \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4.":ReasonText(result));RefreshAll();}
    private static void SetText(Label? label,string value){if(label!=null&&label.Text!=value)label.Text=value;}private static void SetText(Button button,string value){if(button.Text!=value)button.Text=value;}
    private static string FacilityName(ProductionFacilityKindV3 kind)=>kind switch{ProductionFacilityKindV3.ProcessingWorkbench=>"\uAC00\uACF5 \uC791\uC5C5\uB300",ProductionFacilityKindV3.BasicFurnace=>"\uC6A9\uAD11\uB85C",ProductionFacilityKindV3.FieldKitchen=>"\uC57C\uC804 \uCDE8\uC0AC\uB300",_=>"\uC57D\uC81C \uC791\uC5C5\uB300"};
    private static string StateText(ProductionOrderStateV3 state)=>state switch{ProductionOrderStateV3.Queued=>"\uB300\uAE30",ProductionOrderStateV3.WaitingMaterials=>"\uC7AC\uB8CC \uC6B4\uBC18 \uC911",ProductionOrderStateV3.Ready=>"\uC81C\uC791 \uC900\uBE44 \uC644\uB8CC",ProductionOrderStateV3.Producing=>"\uC81C\uC791 \uC911",ProductionOrderStateV3.OutputBlocked=>"\uACB0\uACFC\uBB3C \uBC30\uCD9C \uB300\uAE30",ProductionOrderStateV3.Completed=>"\uC644\uB8CC",ProductionOrderStateV3.Cancelled=>"\uCDE8\uC18C\uB428",_=>"\uC2E4\uD328"};
    private static string ReasonText(string reason)=>reason switch{"InvalidFacility"=>"\uC120\uD0DD\uD55C \uC2DC\uC124\uC744 \uCC3E\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.","UnsupportedRecipe"=>"\uC774 \uC2DC\uC124\uC5D0\uC11C\uB294 \uD574\uB2F9 \uD488\uBAA9\uC744 \uB9CC\uB4E4 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.","OrderQueueLimitExceeded" or "OrderBatchLimitExceeded" or "InvalidOrderChange"=>"\uC81C\uC791 \uBA85\uB839 \uD55C\uB3C4\uB97C \uD655\uC778\uD574 \uC8FC\uC138\uC694.","NoEligibleWorker"=>"\uC720\uD6A8\uD55C \uC81C\uC791 \uC791\uC5C5\uC790\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.","NoApproachCells"=>"\uC2DC\uC124\uAE4C\uC9C0 \uC774\uB3D9\uD560 \uACBD\uB85C\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.",_=>"\uBA85\uB839\uC744 \uCC98\uB9AC\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4."};
    private static string FormatInputs(ProductionRecipeDefinitionV3 recipe)=>string.Join(" + ",recipe.Inputs.Select(x=>$"{StarterProcessingContentV3.GetResourceDisplayName(x.ResourceType)} {x.RequiredAmount}"));
    private static string OutputName(ProductionRecipeDefinitionV3 recipe){if(recipe.OutputResource is { } resource)return StarterProcessingContentV3.GetResourceDisplayName(resource);if(recipe.OutputEquipmentDefinitionId is { } id&&GameplaySessionV3.TryGetEquipmentDefinitions(out var definitions)&&definitions?.TryGetDefinition(id,out var definition)==true&&definition!=null)return definition.DisplayName;return recipe.DisplayName;}
    public override void _ExitTree(){GameplaySessionV3.SessionBegan-=OnSessionBegan;if(_session!=null)_session.Changed-=OnChanged;}
}

public static class StarterProductionUiSelfCheckV3
{
    public static bool TryValidate(out string reason)
    {
        if(GameplayUiThemeV3.ThemeInstanceCount!=1||GameplayUiThemeV3.SharedStyleBoxCount!=5)
        {
            reason="Shared theme diagnostics are invalid.";
            return false;
        }

        Dictionary<ProductionFacilityKindV3,HashSet<string>> requiredByFacility=new()
        {
            [ProductionFacilityKindV3.ProcessingWorkbench]=new(StringComparer.Ordinal)
            {
                "process_wood_plank","process_stone_block","weave_cloth","grind_herb_powder",
                "craft_iron_axe","craft_iron_hammer","craft_iron_pickaxe","craft_padded_armor","craft_iron_sword"
            },
            [ProductionFacilityKindV3.BasicFurnace]=new(StringComparer.Ordinal){"smelt_iron_ingot","smelt_copper_ingot","fire_brick"},
            [ProductionFacilityKindV3.FieldKitchen]=new(StringComparer.Ordinal){"cook_roasted_potato","cook_potato_stew","cook_dried_potato"},
            [ProductionFacilityKindV3.ApothecaryTable]=new(StringComparer.Ordinal){"craft_bandage","craft_simple_medicine"}
        };
        Dictionary<string,ProductionFacilityKindV3> expectedFacilityByRecipe=new(StringComparer.Ordinal);
        foreach(var pair in requiredByFacility)
            foreach(string recipeId in pair.Value)
                expectedFacilityByRecipe.Add(recipeId,pair.Key);

        HashSet<string> seenRecipeIds=new(StringComparer.Ordinal);
        EquipmentDefinitionRegistryV3 equipmentDefinitions=StarterEquipmentContentV3.CreateRegistry();
        foreach(ProductionRecipeDefinitionV3 recipe in StarterProcessingContentV3.GetAll())
        {
            if(!seenRecipeIds.Add(recipe.RecipeId))
            {
                reason=$"Duplicate production RecipeId: {recipe.RecipeId}";
                return false;
            }
            if(!Enum.IsDefined(recipe.FacilityKind))
            {
                reason=$"Recipe has an invalid RequiredFacility: {recipe.RecipeId}";
                return false;
            }
            if(expectedFacilityByRecipe.TryGetValue(recipe.RecipeId,out ProductionFacilityKindV3 expectedFacility)&&recipe.FacilityKind!=expectedFacility)
            {
                reason=$"Recipe is registered to the wrong facility: {recipe.RecipeId}";
                return false;
            }
            if(recipe.OutputKind==ProductionOutputKindV3.Resource)
            {
                if(recipe.OutputResource==null||recipe.OutputEquipmentDefinitionId!=null||recipe.Output.ResourceQuantity<1||recipe.Output.EquipmentQuantity!=0)
                {
                    reason=$"Resource recipe output is invalid: {recipe.RecipeId}";
                    return false;
                }
            }
            else if(recipe.OutputKind==ProductionOutputKindV3.Equipment)
            {
                string? equipmentId=recipe.OutputEquipmentDefinitionId;
                if(recipe.OutputResource!=null||string.IsNullOrWhiteSpace(equipmentId)||recipe.Output.EquipmentQuantity!=1||!equipmentDefinitions.Contains(equipmentId))
                {
                    reason=$"Equipment recipe output is invalid: {recipe.RecipeId}";
                    return false;
                }
            }
            else
            {
                reason=$"Recipe output kind is invalid: {recipe.RecipeId}";
                return false;
            }
        }

        foreach(var pair in requiredByFacility)
            foreach(string recipeId in pair.Value)
            {
                if(!StarterProcessingContentV3.TryGet(recipeId,out ProductionRecipeDefinitionV3? recipe)||recipe==null)
                {
                    reason=$"Required recipe is missing: {recipeId}";
                    return false;
                }
                if(recipe.FacilityKind!=pair.Key)
                {
                    reason=$"Required recipe has the wrong facility: {recipeId}";
                    return false;
                }
            }

        foreach(string equipmentRecipeId in new[]{"craft_iron_pickaxe","craft_padded_armor","craft_iron_sword"})
        {
            if(!StarterProcessingContentV3.TryGet(equipmentRecipeId,out ProductionRecipeDefinitionV3? recipe)||recipe==null||recipe.OutputKind!=ProductionOutputKindV3.Equipment||recipe.OutputEquipmentDefinitionId==null||recipe.Output.EquipmentQuantity!=1||!equipmentDefinitions.Contains(recipe.OutputEquipmentDefinitionId))
            {
                reason=$"Required equipment recipe is invalid: {equipmentRecipeId}";
                return false;
            }
        }

        reason="";
        return true;
    }
}
