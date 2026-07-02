using System;
using System.Collections.Generic;
using Godot;

public partial class WorldBuildPanel : Control
{
	public event Action<TileBuildType>? BuildModeSelected;
	public event Action<BuildMaterialType>? BuildMaterialSelected;
	public event Action<RoomType, bool>? RoomDesignationSelected;
	public event Action<MercenaryController>? MercenarySelected;
	public event Action<MercenaryController>? MercenaryWorkSettingsChanged;
	public event Action<Vector2I>? StorageSelected;
	public event Action<bool>? StockpileZoneDesignationSelected;

	private const string MainTabBuild = "\uAC74\uC124";
	private const string MainTabMercenaryManagement = "\uC6A9\uBCD1 \uAD00\uB9AC";
	private const string MainTabBaseManagement = "\uAE30\uC9C0 \uAD00\uB9AC";
	private const string MainTabStorage = "\uBCF4\uAD00\uD568";
	private const string StructureCategory = "\uAD6C\uC870\uBB3C";
	private const string LifeCategory = "\uC0DD\uD65C";
	private const string DiningCategory = "\uC2DD\uB2F9/\uC8FC\uBC29";
	private const string StorageCategory = "\uBCF4\uAD00";
	private const string ProductionCategory = "\uC0DD\uC0B0";
	private const string TrainingCategory = "\uD6C8\uB828";
	private const string MedicalCategory = "\uC758\uB8CC";
	private const string DecorCategory = "\uC7A5\uC2DD";
	private const string SpecialCategory = "\uD2B9\uC218";
	private const string RoomDesignationCategory = "\uBC29 \uC9C0\uC815";
	private const string ZoneCategory = "\uAD6C\uC5ED";
	private const string StoragePolicyCategory = "\uCC3D\uACE0 \uC815\uCC45";
	private const string GuardZoneCategory = "\uACBD\uBE44 \uAD6C\uC5ED";
	private const string RoomDeleteName = "\uBC29 \uC0AD\uC81C";
	private const string MercenaryFilterAll = "\uC804\uCCB4";
	private const string MercenaryFilterCombat = "\uC804\uD22C";
	private const string MercenaryFilterHaul = "\uC6B4\uBC18";
	private const string MercenaryFilterFarm = "\uB18D\uC0AC";
	private const string MercenaryFilterMedical = "\uC758\uB8CC";
	private const string MercenaryFilterCraft = "\uC81C\uC791";
	private const string StorageOverviewCategory = "\uC804\uCCB4 \uBCF4\uAD00";
	private const string ComingSoon = "\uC900\uBE44 \uC911";
	private const string NoSelection = "\uC120\uD0DD \uC5C6\uC74C";
	private static readonly bool ShowStockpileZoneControls = false;
	private const float ManagementPanelLeftMargin = 12.0f;
	private const float ManagementPanelRightMargin = 12.0f;
	private const float ManagementPanelRightReservedWidth = 240.0f;
	private const float ManagementPanelBottomReservedHeight = 176.0f;
	private const float ManagementPanelBottomMargin = 12.0f;
	private const float ManagementPanelTopMargin = 12.0f;
	private const float ManagementPanelMinHeight = 180.0f;
	private const float ManagementPanelPreferredHeight = 260.0f;
	private const float ManagementPanelMaxViewportRatio = 0.48f;
	private const float ManagementPanelMinWidth = 520.0f;

	private readonly List<Button> _mainTabButtons = new();
	private readonly Dictionary<string, Button> _categoryButtons = new();
	private readonly Dictionary<TileBuildType, Button> _itemButtons = new();
	private readonly Dictionary<BuildMaterialType, Button> _materialButtons = new();
	private readonly Dictionary<string, Button> _roomButtons = new();
	private readonly Dictionary<ulong, Button> _mercenaryButtons = new();
	private readonly Dictionary<Vector2I, Button> _storageButtons = new();
	private readonly Dictionary<string, List<BuildPanelItem>> _itemsByCategory = new();
	private readonly Dictionary<string, List<RoomPanelItem>> _roomItemsByCategory = new();
	private BaseBuildManager? _buildManager;
	private PanelContainer? _bottomPanel;
	private HBoxContainer? _categoryRow;
	private HBoxContainer? _itemRow;
	private HBoxContainer? _materialRow;
	private PanelContainer? _infoPanel;
	private Label? _selectionLabel;
	private Label? _infoLabel;
	private GridContainer? _inventoryGrid;
	private string _activeMainTab = "";
	private string _activeCategory = StructureCategory;
	private PanelMode _activePanelMode = PanelMode.None;
	private TileBuildType _selectedBuildType = TileBuildType.None;
	private BuildMaterialType _selectedBuildMaterialType = BuildMaterialType.Wood;
	private RoomType _selectedRoomType = RoomType.None;
	private MercenaryController? _selectedMercenary;
	private Vector2I? _selectedStorageCell;
	private bool _selectedRoomRemoveMode;
	private float _inventoryRefreshTimer;
	private float _mercenaryListRefreshTimer;
	private float _storageListRefreshTimer;

	public bool IsBaseManagementPanelActive => _activePanelMode == PanelMode.BaseManagement && _activeMainTab == MainTabBaseManagement;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsPreset(LayoutPreset.FullRect);
		BuildData();
		BuildMiniInventoryPanel();
		BuildInfoPanel();
		BuildRightTabs();
		BuildBottomPanel();
		SetSelectedBuildType(TileBuildType.None);
		ShowBuildItemInfo(null);
		RefreshInventory();
	}

	public override void _Process(double delta)
	{
		_inventoryRefreshTimer -= (float)delta;

		if (_inventoryRefreshTimer <= 0.0f)
		{
			_inventoryRefreshTimer = 0.35f;
			RefreshInventory();
		}

		if (_buildManager != null && _buildManager.CurrentBuildMode != _selectedBuildType)
		{
			SetSelectedBuildType(_buildManager.CurrentBuildMode);
		}

		if (_activePanelMode == PanelMode.MercenaryManagement)
		{
			_mercenaryListRefreshTimer -= (float)delta;

			if (_mercenaryListRefreshTimer <= 0.0f)
			{
				_mercenaryListRefreshTimer = 1.0f;
				PopulateMercenaryItems(_activeCategory);
			}
		}

		if (_activePanelMode == PanelMode.StorageOverview)
		{
			_storageListRefreshTimer -= (float)delta;

			if (_storageListRefreshTimer <= 0.0f)
			{
				_storageListRefreshTimer = 1.0f;
				RefreshStorageOverviewText();
			}
		}

		UpdateManagementPanelLayout();
	}

	public void SetBuildManager(BaseBuildManager? buildManager)
	{
		_buildManager = buildManager;
		_selectedBuildMaterialType = buildManager?.CurrentBuildMaterialType ?? BuildMaterialType.Wood;
		RebuildMaterialButtons();
		RefreshInventory();
		ShowBuildItemInfo(GetSelectedItem());
	}

	public void SetSelectedBuildType(TileBuildType buildType)
	{
		_selectedBuildType = buildType;
		_selectedBuildMaterialType = BuildStructureDefinitions.NormalizeMaterial(buildType, _selectedBuildMaterialType);

		if (buildType != TileBuildType.None)
		{
			_selectedRoomType = RoomType.None;
			_selectedRoomRemoveMode = false;
			_selectedMercenary = null;
			_selectedStorageCell = null;
		}

		RebuildMaterialButtons();
		UpdateSelectionLabel();
		UpdateItemButtonStyles();
		UpdateMaterialButtonStyles();
		UpdateRoomButtonStyles();
		UpdateStorageButtonStyles();
	}

	public void SetSelectedBuildMaterialType(BuildMaterialType materialType)
	{
		_selectedBuildMaterialType = BuildStructureDefinitions.NormalizeMaterial(_selectedBuildType, materialType);
		RebuildMaterialButtons();
		UpdateSelectionLabel();
		UpdateMaterialButtonStyles();
		ShowBuildItemInfo(GetSelectedItem());
	}

	public void SetSelectedRoomDesignation(RoomType roomType, bool removeMode)
	{
		_selectedRoomType = roomType;
		_selectedRoomRemoveMode = removeMode;
		_selectedBuildType = TileBuildType.None;
		_selectedMercenary = null;
		_selectedStorageCell = null;
		RebuildMaterialButtons();
		UpdateSelectionLabel();
		UpdateItemButtonStyles();
		UpdateMaterialButtonStyles();
		UpdateRoomButtonStyles();
		UpdateStorageButtonStyles();
	}

	public void ClearUiSelection()
	{
		_activeMainTab = "";
		_activeCategory = StructureCategory;
		_activePanelMode = PanelMode.None;
		_selectedBuildType = TileBuildType.None;
		_selectedRoomType = RoomType.None;
		_selectedMercenary = null;
		_selectedStorageCell = null;
		_selectedRoomRemoveMode = false;
		RebuildMaterialButtons();

		if (_bottomPanel != null)
		{
			_bottomPanel.Visible = false;
		}

		RebuildCategoryButtons(GetBuildCategories());
		PopulateBuildItems(StructureCategory);
		ShowBuildItemInfo(null);
		UpdateSelectionLabel();
		UpdateMainTabButtonStyles();
		UpdateCategoryButtonStyles();
		UpdateItemButtonStyles();
		UpdateMaterialButtonStyles();
		UpdateRoomButtonStyles();
		UpdateMercenaryButtonStyles();
		UpdateStorageButtonStyles();
	}

	public void ShowRoomInfo(BaseRoom room)
	{
		if (_infoLabel == null)
		{
			return;
		}

		ShowInfoPanel();
		List<string> furnitureParts = new();
		foreach (KeyValuePair<FurnitureTag, int> entry in room.FurnitureCounts)
		{
			if (entry.Value > 0)
			{
				furnitureParts.Add($"{BaseRoomManager.GetFurnitureTagDisplayName(entry.Key)} {entry.Value}");
			}
		}

		List<string> missingParts = new();
		foreach (string requirement in room.MissingRequirements)
		{
			missingParts.Add(BaseRoomManager.GetRequirementDisplayName(requirement));
		}

		string stateText = room.IsValid ? "\uC720\uD6A8" : "\uBBF8\uC644\uC131";
		string furnitureText = furnitureParts.Count == 0 ? "\uC5C6\uC74C" : string.Join(", ", furnitureParts);
		string missingText = missingParts.Count == 0 ? "\uC5C6\uC74C" : string.Join(", ", missingParts);

		_infoLabel.Text = $"{room.DisplayName}\n"
			+ $"\uD06C\uAE30: {room.Cells.Count}\uCE78\n"
			+ $"\uC0C1\uD0DC: {stateText}\n"
			+ $"\uD488\uC9C8: {room.QualityScore}\n"
			+ $"\uAC00\uAD6C: {furnitureText}\n"
			+ $"\uBD80\uC871: {missingText}\n"
			+ $"\uD6A8\uACFC: {BaseRoomManager.GetRoomEffectText(room.RoomType)}";
	}

	public void ShowStorageInfo(Vector2I storageCell)
	{
		if (_infoLabel == null || _buildManager == null)
		{
			return;
		}

		if (!_buildManager.TryResolveStorageOriginCell(storageCell, out Vector2I originCell))
		{
			HideInfoPanel();
			return;
		}

		ShowInfoPanel();
		_selectedStorageCell = originCell;
		_selectedBuildType = TileBuildType.None;
		_selectedRoomType = RoomType.None;
		_selectedRoomRemoveMode = false;
		_selectedMercenary = null;

		_buildManager.TryGetObjectInfoAtCell(originCell, out _, out _, out Vector2I objectSize);
		_buildManager.TryGetStorageContents(originCell, out Dictionary<BaseResourceType, int> contents);

		int usedWeight = _buildManager.GetStorageUsedWeight(originCell);
		int capacity = _buildManager.GetStorageWeightCapacity(originCell);
		int freeWeight = _buildManager.GetStorageFreeWeight(originCell);
		string contentsText = BuildStorageContentsText(contents);
		string useText = _buildManager.GetStorageInteractionUserLabel(originCell);
		string sizeText = objectSize == Vector2I.Zero ? "\uC800\uC7A5 \uAD6C\uC5ED" : $"{objectSize.X}x{objectSize.Y}";

		_infoLabel.Text = $"{_buildManager.GetStorageDisplayName(originCell)}\n"
			+ $"\uD06C\uAE30: {sizeText}\n"
			+ $"\uC800\uC7A5 \uBB34\uAC8C: {usedWeight} / {capacity}\n"
			+ $"\uB0A8\uC740 \uBB34\uAC8C: {freeWeight}\n\n"
			+ $"\uC815\uCC45: {_buildManager.GetStoragePolicySummary(originCell)}\n"
			+ $"{_buildManager.GetStorageLimitText(originCell)}\n\n"
			+ $"\uB0B4\uC6A9\uBB3C:\n{contentsText}\n\n"
			+ $"\uC0AC\uC6A9 \uC911: {useText}\n\n"
			+ "\uC0C1\uC138 \uC815\uCC45\uC740 \uBCF4\uAD00\uD568 \uD0ED\uC758 \uC800\uC7A5\uACE0 \uAD00\uB9AC\uC5D0\uC11C \uC218\uC815\uD569\uB2C8\uB2E4.";

		UpdateSelectionLabel();
		UpdateStorageButtonStyles();
	}

	public void ShowConstructionSiteInfo(ConstructionSite site)
	{
		if (_infoLabel == null)
		{
			return;
		}

		ShowInfoPanel();
		_selectedStorageCell = null;
		_selectedBuildType = TileBuildType.None;
		_selectedRoomType = RoomType.None;
		_selectedRoomRemoveMode = false;
		_selectedMercenary = null;

		List<string> materialLines = new();

		foreach (ConstructionRequirement requirement in site.Requirements.Values)
		{
			materialLines.Add($"{BaseBuildManager.GetResourceDisplayName(requirement.ResourceType)} {requirement.DeliveredAmount} / {requirement.RequiredAmount}");
		}

		string materialText = materialLines.Count == 0 ? "\uC5C6\uC74C" : string.Join("\n", materialLines);
		string stateText = site.State switch
		{
			ConstructionSiteState.WaitingForMaterials => "\uC7AC\uB8CC \uB300\uAE30",
			ConstructionSiteState.ReadyToBuild => "\uAC74\uC124 \uB300\uAE30",
			ConstructionSiteState.Building => "\uAC74\uC124 \uC911",
			ConstructionSiteState.Completed => "\uC644\uB8CC",
			ConstructionSiteState.Cancelled => "\uCDE8\uC18C",
			_ => site.State.ToString()
		};
		string deliveryWorker = site.ReservedDeliveryWorker != null ? site.ReservedDeliveryWorker.MercenaryName : "\uC5C6\uC74C";
		string buildWorker = site.ReservedBuildWorker != null ? site.ReservedBuildWorker.MercenaryName : "\uC5C6\uC74C";

		_infoLabel.Text = $"{site.DisplayName} \uAC74\uC124 \uC608\uC815\uC9C0\n"
			+ $"\uD06C\uAE30: {site.Size.X}x{site.Size.Y}\n"
			+ $"\uC0C1\uD0DC: {stateText}\n\n"
			+ $"\uC7AC\uB8CC:\n{materialText}\n\n"
			+ $"\uC9C4\uD589: {(int)(site.GetProgressRatio() * 100.0f)}%\n"
			+ $"\uC6B4\uBC18\uC790: {deliveryWorker}\n"
			+ $"\uC791\uC5C5\uC790: {buildWorker}\n"
			+ $"\uCCA0\uAC70 \uBAA8\uB4DC\uB85C \uCDE8\uC18C \uAC00\uB2A5";

		UpdateSelectionLabel();
	}

	public void ClearInfoPanel()
	{
		HideInfoPanel();
	}

	private void BuildData()
	{
		AddItem(StructureCategory, TileBuildType.Erase, "\uCCA0\uAC70", "\uAE30\uC874 \uAC74\uCD95\uBB3C\uC744 \uC81C\uAC70\uD569\uB2C8\uB2E4.", Vector2I.One);
		AddItem(StructureCategory, TileBuildType.Floor, "\uBC14\uB2E5", "\uC774\uB3D9 \uAC00\uB2A5\uD55C \uAE30\uBCF8 \uBC14\uB2E5\uC785\uB2C8\uB2E4.", Vector2I.One);
		AddItem(StructureCategory, TileBuildType.Wall, "\uBCBD", "\uC774\uB3D9\uC744 \uB9C9\uB294 \uAE30\uBCF8 \uBCBD\uC785\uB2C8\uB2E4.", Vector2I.One);
		AddItem(StructureCategory, TileBuildType.Door, "\uBB38", "\uBCBD\uC744 \uD1B5\uACFC \uAC00\uB2A5\uD55C \uCD9C\uC785\uAD6C\uB85C \uBC14\uAFC9\uB2C8\uB2E4.", Vector2I.One);
		AddItem(StructureCategory, TileBuildType.Bed, "\uCE68\uB300", "\uC6A9\uBCD1\uC774 \uC7A0\uC744 \uC790\uB294 \uC2DC\uC124\uC785\uB2C8\uB2E4.", new Vector2I(1, 2));
		AddItem(StructureCategory, TileBuildType.Storage, "\uCC3D\uACE0", "\uC77C\uBC18 \uC790\uC6D0\uC744 \uBCF4\uAD00\uD569\uB2C8\uB2E4.", new Vector2I(2, 2));
		AddItem(StructureCategory, TileBuildType.GuardPost, "\uACBD\uBE44\uCD08\uC18C", "\uACBD\uACC4\uC640 \uAC10\uC9C0 \uC791\uC5C5\uC5D0 \uC0AC\uC6A9\uD558\uB294 \uCD08\uC18C\uC785\uB2C8\uB2E4.", Vector2I.One);

		AddItem(LifeCategory, TileBuildType.ImprovisedBed, "\uAE09\uC870 \uCE68\uB300", "\uC784\uC2DC\uB85C \uB9CC\uB4E0 \uB0AE\uC740 \uD488\uC9C8\uC758 \uCE68\uB300\uC785\uB2C8\uB2E4.", Vector2I.One);
		AddItem(LifeCategory, TileBuildType.Bed, "\uAE30\uBCF8 \uCE68\uB300", "\uC77C\uBC18\uC801\uC778 \uCE68\uB300\uC785\uB2C8\uB2E4.", new Vector2I(1, 2));
		AddItem(LifeCategory, TileBuildType.LuxuryBed, "\uACE0\uAE09 \uCE68\uB300", "\uACE0\uAE09 \uC6A9\uBCD1\uC774 \uC120\uD638\uD558\uB294 \uB113\uC740 \uCE68\uB300\uC785\uB2C8\uB2E4.", new Vector2I(2, 2));
		AddItem(LifeCategory, TileBuildType.SmallCabinet, "\uC791\uC740 \uC218\uB0A9\uC7A5", "\uAC1C\uC778\uC2E4\uC5D0 \uB450\uAE30 \uC88B\uC740 \uC791\uC740 \uC218\uB0A9\uC7A5\uC785\uB2C8\uB2E4.", Vector2I.One);
		AddItem(LifeCategory, TileBuildType.SmallDesk, "\uC791\uC740 \uCC45\uC0C1", "\uAC1C\uC778\uC2E4\uC774\uB098 \uC0AC\uBB34 \uACF5\uAC04\uC5D0 \uC5B4\uC6B8\uB9AC\uB294 \uCC45\uC0C1\uC785\uB2C8\uB2E4.", new Vector2I(2, 1));
		AddItem(LifeCategory, TileBuildType.Lamp, "\uB7A8\uD504", "\uBC29\uC744 \uBC1D\uD600\uC8FC\uB294 \uC791\uC740 \uC870\uBA85\uC785\uB2C8\uB2E4.", Vector2I.One);

		AddItem(DiningCategory, TileBuildType.Chair, "\uC758\uC790", "\uC2DD\uD0C1\uC774\uB098 \uCC45\uC0C1 \uC606\uC5D0 \uB193\uB294 \uAE30\uBCF8 \uC758\uC790\uC785\uB2C8\uB2E4.", Vector2I.One);
		AddItem(DiningCategory, TileBuildType.SmallDiningTable, "\uC791\uC740 \uC2DD\uD0C1", "\uC18C\uC218 \uC778\uC6D0\uC774 \uC2DD\uC0AC\uD560 \uC218 \uC788\uB294 \uC2DD\uD0C1\uC785\uB2C8\uB2E4.", new Vector2I(2, 1));
		AddItem(DiningCategory, TileBuildType.LongDiningTable, "\uAE34 \uC2DD\uD0C1", "\uC5EC\uB7EC \uC6A9\uBCD1\uC774 \uD568\uAED8 \uC549\uC744 \uC218 \uC788\uB294 \uAE34 \uC2DD\uD0C1\uC785\uB2C8\uB2E4.", new Vector2I(3, 1));
		AddItem(DiningCategory, TileBuildType.ServingCounter, "\uBC30\uC2DD\uB300", "\uC74C\uC2DD\uC744 \uB098\uB220\uC8FC\uB294 \uC2DD\uB2F9\uC6A9 \uAC00\uAD6C\uC785\uB2C8\uB2E4.", new Vector2I(2, 1));
		AddItem(DiningCategory, TileBuildType.KitchenCounter, "\uC870\uB9AC\uB300", "\uC2DD\uC7AC\uB8CC\uB97C \uC190\uC9C8\uD558\uB294 \uC8FC\uBC29 \uAC00\uAD6C\uC785\uB2C8\uB2E4.", new Vector2I(2, 1));
		AddItem(DiningCategory, TileBuildType.Hearth, "\uD654\uB355", "\uC74C\uC2DD\uC744 \uC870\uB9AC\uD558\uB294 \uC8FC\uBC29 \uC2DC\uC124\uC785\uB2C8\uB2E4.", new Vector2I(2, 2));
		AddItem(DiningCategory, TileBuildType.IngredientCrate, "\uC2DD\uC7AC\uB8CC \uC0C1\uC790", "\uC8FC\uBC29 \uADFC\uCC98\uC5D0 \uB450\uB294 \uC2DD\uC7AC\uB8CC \uBCF4\uAD00 \uC0C1\uC790\uC785\uB2C8\uB2E4.", Vector2I.One);

		AddItem(StorageCategory, TileBuildType.SmallChest, "\uC791\uC740 \uC0C1\uC790", "\uC801\uC740 \uC591\uC758 \uBB3C\uD488\uC744 \uBCF4\uAD00\uD558\uB294 \uC0C1\uC790\uC785\uB2C8\uB2E4.", Vector2I.One);
		AddItem(StorageCategory, TileBuildType.Storage, "\uAE30\uBCF8 \uCC3D\uACE0", "\uC77C\uBC18\uC801\uC778 \uC790\uC6D0 \uBCF4\uAD00 \uC2DC\uC124\uC785\uB2C8\uB2E4.", new Vector2I(2, 2));
		AddItem(StorageCategory, TileBuildType.LargeStorage, "\uB300\uD615 \uCC3D\uACE0", "\uB9CE\uC740 \uC790\uC6D0\uC744 \uBCF4\uAD00\uD560 \uC218 \uC788\uB294 \uB300\uD615 \uBCF4\uAD00 \uC2DC\uC124\uC785\uB2C8\uB2E4.", new Vector2I(3, 2));
		AddItem(StorageCategory, TileBuildType.MaterialShelf, "\uC790\uC7AC \uC120\uBC18", "\uC790\uC7AC\uB97C \uC815\uB9AC\uD574 \uB450\uB294 \uC120\uBC18\uC785\uB2C8\uB2E4.", new Vector2I(2, 1));
		AddItem(StorageCategory, TileBuildType.WeaponRack, "\uBB34\uAE30 \uAC70\uCE58\uB300", "\uBB34\uAE30\uB97C \uAC78\uC5B4\uB450\uB294 \uAC70\uCE58\uB300\uC785\uB2C8\uB2E4.", new Vector2I(2, 1));
		AddItem(StorageCategory, TileBuildType.MedicineShelf, "\uC57D\uD488 \uC120\uBC18", "\uC57D\uD488\uC744 \uC815\uB9AC\uD574 \uB450\uB294 \uC120\uBC18\uC785\uB2C8\uB2E4.", Vector2I.One);

		AddItem(ProductionCategory, TileBuildType.Workbench, "\uC791\uC5C5\uB300", "\uAC04\uB2E8\uD55C \uC81C\uC791 \uC791\uC5C5\uC744 \uD560 \uC218 \uC788\uB294 \uC791\uC5C5\uB300\uC785\uB2C8\uB2E4.", new Vector2I(2, 1));
		AddItem(ProductionCategory, TileBuildType.LargeWorkbench, "\uD070 \uC791\uC5C5\uB300", "\uBCF8\uACA9\uC801\uC778 \uC81C\uC791\uC744 \uC704\uD55C \uD070 \uC791\uC5C5\uB300\uC785\uB2C8\uB2E4.", new Vector2I(3, 2));
		AddItem(ProductionCategory, TileBuildType.RepairBench, "\uC218\uB9AC\uB300", "\uC7A5\uBE44\uB97C \uC218\uB9AC\uD558\uAE30 \uC704\uD55C \uC791\uC5C5\uB300\uC785\uB2C8\uB2E4.", new Vector2I(2, 1));
		AddItem(ProductionCategory, TileBuildType.Forge, "\uB300\uC7A5\uAC04 \uD654\uB85C", "\uAE08\uC18D \uC7A5\uBE44 \uC81C\uC791\uC5D0 \uC4F8 \uC218 \uC788\uB294 \uD654\uB85C\uC785\uB2C8\uB2E4.", new Vector2I(2, 2));
		AddItem(ProductionCategory, TileBuildType.AlchemyBench, "\uC5F0\uAE08\uB300", "\uC57D\uD488\uACFC \uD2B9\uC218 \uC7AC\uB8CC\uB97C \uB2E4\uB8E8\uB294 \uC791\uC5C5\uB300\uC785\uB2C8\uB2E4.", new Vector2I(2, 1));

		AddItem(TrainingCategory, TileBuildType.TrainingDummy, "\uD6C8\uB828 \uD5C8\uC218\uC544\uBE44", "\uAE30\uBCF8 \uC804\uD22C \uD6C8\uB828\uC6A9 \uD5C8\uC218\uC544\uBE44\uC785\uB2C8\uB2E4.", Vector2I.One);
		AddItem(TrainingCategory, TileBuildType.Sandbag, "\uBAA8\uB798\uC8FC\uBA38\uB2C8", "\uD0C0\uACA9 \uD6C8\uB828\uC6A9 \uBAA8\uB798\uC8FC\uBA38\uB2C8\uC785\uB2C8\uB2E4.", Vector2I.One);
		AddItem(TrainingCategory, TileBuildType.TrainingMat, "\uD6C8\uB828 \uB9E4\uD2B8", "\uAE30\uCD08 \uCCB4\uB825 \uD6C8\uB828\uC744 \uC704\uD55C \uB9E4\uD2B8\uC785\uB2C8\uB2E4.", new Vector2I(2, 2));
		AddItem(TrainingCategory, TileBuildType.WeaponTrainingRack, "\uBB34\uAE30 \uD6C8\uB828\uB300", "\uBB34\uAE30 \uC0AC\uC6A9 \uD6C8\uB828\uC744 \uC704\uD55C \uAC70\uCE58\uB300\uC785\uB2C8\uB2E4.", new Vector2I(2, 1));
		AddItem(TrainingCategory, TileBuildType.TrainingRing, "\uD6C8\uB828 \uB9C1", "\uC6A9\uBCD1\uB4E4\uC774 \uB300\uB828\uD560 \uC218 \uC788\uB294 \uB113\uC740 \uD6C8\uB828 \uACF5\uAC04\uC785\uB2C8\uB2E4.", new Vector2I(4, 2));

		AddItem(MedicalCategory, TileBuildType.ImprovisedMedicalBed, "\uAC04\uC774 \uCE58\uB8CC \uCE68\uB300", "\uAE09\uC870\uD55C \uCE58\uB8CC\uC6A9 \uCE68\uC0C1\uC785\uB2C8\uB2E4.", Vector2I.One);
		AddItem(MedicalCategory, TileBuildType.MedicalBed, "\uCE58\uB8CC \uCE68\uB300", "\uBD80\uC0C1\uC790\uB97C \uB20C\uD600 \uCE58\uB8CC\uD560 \uC218 \uC788\uB294 \uCE68\uB300\uC785\uB2C8\uB2E4.", new Vector2I(1, 2));
		AddItem(MedicalCategory, TileBuildType.LuxuryMedicalBed, "\uACE0\uAE09 \uCE58\uB8CC \uCE68\uB300", "\uC911\uC0C1\uC790\uB97C \uCE58\uB8CC\uD558\uAE30 \uC704\uD55C \uACE0\uAE09 \uCE68\uB300\uC785\uB2C8\uB2E4.", new Vector2I(2, 2));
		AddItem(MedicalCategory, TileBuildType.MedicalTable, "\uC9C4\uB8CC\uB300", "\uC9C4\uB8CC\uC640 \uCC98\uCE58\uB97C \uC704\uD55C \uC758\uB8CC\uC6A9 \uC791\uC5C5\uB300\uC785\uB2C8\uB2E4.", new Vector2I(2, 1));
		AddItem(MedicalCategory, TileBuildType.MedicineCabinet, "\uC57D\uD488\uC7A5", "\uC57D\uD488\uC744 \uBCF4\uAD00\uD558\uB294 \uC758\uB8CC \uAC00\uAD6C\uC785\uB2C8\uB2E4.", Vector2I.One);

		AddItem(DecorCategory, TileBuildType.PlantPot, "\uD654\uBD84", "\uBC29 \uBD84\uC704\uAE30\uB97C \uC870\uAE08 \uC88B\uAC8C \uB9CC\uB4DC\uB294 \uC7A5\uC2DD\uC785\uB2C8\uB2E4.", Vector2I.One);
		AddItem(DecorCategory, TileBuildType.SmallRug, "\uC791\uC740 \uAE54\uAC1C", "\uBC14\uB2E5\uC744 \uAFB8\uBBF8\uB294 \uC791\uC740 \uAE54\uAC1C\uC785\uB2C8\uB2E4.", Vector2I.One);
		AddItem(DecorCategory, TileBuildType.LargeRug, "\uD070 \uAE54\uAC1C", "\uBC29 \uC911\uC559\uC5D0 \uB193\uAE30 \uC88B\uC740 \uD070 \uAE54\uAC1C\uC785\uB2C8\uB2E4.", new Vector2I(2, 2));
		AddItem(DecorCategory, TileBuildType.WallBanner, "\uBCBD\uAC78\uC774 \uAE43\uBC1C", "\uC6A9\uBCD1\uB2E8 \uBD84\uC704\uAE30\uB97C \uB0B4\uB294 \uAE43\uBC1C\uC785\uB2C8\uB2E4.", Vector2I.One);
		AddItem(DecorCategory, TileBuildType.TrophyDisplay, "\uC804\uB9AC\uD488 \uC7A5\uC2DD\uB300", "\uC758\uB8B0\uC5D0\uC11C \uC5BB\uC740 \uC804\uB9AC\uD488\uC744 \uC804\uC2DC\uD558\uAE30 \uC88B\uC740 \uC7A5\uC2DD\uB300\uC785\uB2C8\uB2E4.", new Vector2I(2, 1));

		AddRoomItem(RoomDesignationCategory, RoomType.PrivateRoom);
		AddRoomItem(RoomDesignationCategory, RoomType.Dormitory);
		AddRoomItem(RoomDesignationCategory, RoomType.DiningRoom);
		AddRoomItem(RoomDesignationCategory, RoomType.Kitchen);
		AddRoomItem(RoomDesignationCategory, RoomType.StorageRoom);
		AddRoomItem(RoomDesignationCategory, RoomType.Workshop);
		AddRoomItem(RoomDesignationCategory, RoomType.Infirmary);
		AddRoomItem(RoomDesignationCategory, RoomType.TrainingRoom);
		AddRoomItem(RoomDesignationCategory, RoomType.BathRoom);
		AddRoomItem(RoomDesignationCategory, RoomType.Lounge);
		AddRoomItem(RoomDesignationCategory, RoomType.None, RoomDeleteName, true);
	}

	private void AddItem(string category, TileBuildType buildType, string displayName, string description, Vector2I size)
	{
		if (!_itemsByCategory.TryGetValue(category, out List<BuildPanelItem>? items))
		{
			items = new List<BuildPanelItem>();
			_itemsByCategory[category] = items;
		}

		items.Add(new BuildPanelItem(category, buildType, displayName, description, size));
	}

	private void AddRoomItem(string category, RoomType roomType, string? displayName = null, bool removeMode = false)
	{
		if (!_roomItemsByCategory.TryGetValue(category, out List<RoomPanelItem>? items))
		{
			items = new List<RoomPanelItem>();
			_roomItemsByCategory[category] = items;
		}

		items.Add(new RoomPanelItem(category, roomType, displayName ?? BaseRoomManager.GetRoomDisplayName(roomType), removeMode));
	}

	private void BuildMiniInventoryPanel()
	{
		PanelContainer panel = CreatePanel(new Color(0.04f, 0.045f, 0.05f, 0.20f), new Color(0.72f, 0.7f, 0.62f, 0.08f), 6);
		panel.Name = "MiniInventoryPanel";
		panel.MouseFilter = MouseFilterEnum.Stop;
		panel.AnchorLeft = 0.0f;
		panel.AnchorTop = 0.0f;
		panel.AnchorRight = 0.0f;
		panel.AnchorBottom = 0.0f;
		panel.OffsetLeft = 10.0f;
		panel.OffsetTop = 10.0f;
		panel.OffsetRight = 190.0f;
		panel.OffsetBottom = 170.0f;
		AddChild(panel);

		ScrollContainer scrollContainer = new()
		{
			Name = "InventoryScroll",
			MouseFilter = MouseFilterEnum.Stop,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		panel.AddChild(scrollContainer);

		_inventoryGrid = new GridContainer
		{
			Name = "InventoryGrid",
			Columns = 2,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_inventoryGrid.AddThemeConstantOverride("h_separation", 12);
		_inventoryGrid.AddThemeConstantOverride("v_separation", 4);
		scrollContainer.AddChild(_inventoryGrid);
	}

	private void BuildInfoPanel()
	{
		PanelContainer panel = CreatePanel(new Color(0.04f, 0.045f, 0.05f, 0.78f), new Color(0.72f, 0.7f, 0.62f, 0.24f), 6);
		panel.Name = "BuildItemInfoPanel";
		panel.MouseFilter = MouseFilterEnum.Stop;
		panel.Visible = false;
		panel.AnchorLeft = 0.0f;
		panel.AnchorTop = 0.5f;
		panel.AnchorRight = 0.0f;
		panel.AnchorBottom = 0.5f;
		panel.OffsetLeft = 10.0f;
		panel.OffsetTop = -90.0f;
		panel.OffsetRight = 210.0f;
		panel.OffsetBottom = -175.0f;
		AddChild(panel);
		_infoPanel = panel;

		MarginContainer margin = new();
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		panel.AddChild(margin);

		_infoLabel = new Label
		{
			Name = "InfoLabel",
			Text = "",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_infoLabel.AddThemeFontSizeOverride("font_size", 10);
		margin.AddChild(_infoLabel);
	}

	private void BuildRightTabs()
	{
		PanelContainer panel = CreatePanel(new Color(0.04f, 0.045f, 0.05f, 0.72f), new Color(0.7f, 0.62f, 0.45f, 0.2f), 6);
		panel.Name = "MainTabPanel";
		panel.MouseFilter = MouseFilterEnum.Stop;
		panel.AnchorLeft = 1.0f;
		panel.AnchorRight = 1.0f;
		panel.AnchorTop = 0.52f;
		panel.AnchorBottom = 1.0f;
		panel.OffsetLeft = -206.0f;
		panel.OffsetRight = -12.0f;
		panel.OffsetTop = -70.0f;
		panel.OffsetBottom = -18.0f;
		AddChild(panel);

		VBoxContainer column = new()
		{
			Name = "MainTabs",
			MouseFilter = MouseFilterEnum.Ignore
		};
		column.AddThemeConstantOverride("separation", 8);
		panel.AddChild(column);

		string[] labels =
		{
			MainTabBuild,
			"\uBA85\uB839",
			MainTabMercenaryManagement,
			MainTabBaseManagement,
			"\uC758\uB8B0",
			MainTabStorage,
            "\uC124\uC815"
		};

		foreach (string label in labels)
		{
			Button button = CreateButton(label, new Vector2(168.0f, 48.0f));
			button.Pressed += () => HandleMainTabPressed(label);
			column.AddChild(button);
			_mainTabButtons.Add(button);
		}
	}

	private void BuildBottomPanel()
	{
		_bottomPanel = CreatePanel(new Color(0.10f, 0.10f, 0.10f, 0.82f), new Color(0.82f, 0.62f, 0.34f, 0.36f), 4);
		_bottomPanel.Name = "BuildBottomPanel";
		_bottomPanel.MouseFilter = MouseFilterEnum.Stop;
		_bottomPanel.SetAnchorsPreset(LayoutPreset.BottomLeft, false);
		_bottomPanel.GrowVertical = GrowDirection.Begin;
		_bottomPanel.ClipContents = true;
		_bottomPanel.CustomMinimumSize = Vector2.Zero;
		_bottomPanel.OffsetLeft = ManagementPanelLeftMargin;
		_bottomPanel.OffsetRight = ManagementPanelLeftMargin + ManagementPanelMinWidth;
		_bottomPanel.OffsetBottom = -ManagementPanelBottomMargin;
		_bottomPanel.OffsetTop = _bottomPanel.OffsetBottom - ManagementPanelPreferredHeight;
		_bottomPanel.Size = new Vector2(ManagementPanelMinWidth, ManagementPanelPreferredHeight);
		_bottomPanel.Visible = false;
		AddChild(_bottomPanel);
		UpdateManagementPanelLayout();

		VBoxContainer root = new()
		{
			Name = "BuildPanelRows",
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
			CustomMinimumSize = Vector2.Zero
		};
		root.AddThemeConstantOverride("separation", 6);
		_bottomPanel.AddChild(root);

		_selectionLabel = new Label
		{
			Name = "SelectionLabel",
			Text = NoSelection,
			HorizontalAlignment = HorizontalAlignment.Left,
			MouseFilter = MouseFilterEnum.Ignore
		};
		_selectionLabel.AddThemeFontSizeOverride("font_size", 16);
		root.AddChild(_selectionLabel);

		_materialRow = new HBoxContainer
		{
			Name = "BuildMaterialSelector",
			MouseFilter = MouseFilterEnum.Ignore,
			Visible = false
		};
		_materialRow.AddThemeConstantOverride("separation", 8);
		root.AddChild(_materialRow);

		ScrollContainer itemScroll = new()
		{
			Name = "BuildItemScroll",
			CustomMinimumSize = Vector2.Zero,
			MouseFilter = MouseFilterEnum.Stop,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
			VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Auto
		};
		root.AddChild(itemScroll);

		_itemRow = new HBoxContainer
		{
			Name = "BuildItems",
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.Fill,
			ClipContents = true,
			CustomMinimumSize = Vector2.Zero
		};
		_itemRow.AddThemeConstantOverride("separation", 10);
		itemScroll.AddChild(_itemRow);

		_categoryRow = new HBoxContainer
		{
			Name = "BuildCategories",
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			ClipContents = true,
			CustomMinimumSize = Vector2.Zero
		};
		_categoryRow.AddThemeConstantOverride("separation", 8);
		root.AddChild(_categoryRow);

		RebuildCategoryButtons(GetBuildCategories());
		PopulateBuildItems(StructureCategory);
	}

	private void UpdateManagementPanelLayout()
	{
		if (_bottomPanel == null)
		{
			return;
		}

		Vector2 viewportSize = GetViewportRect().Size;
		float maxPanelWidth = Mathf.Max(280.0f, viewportSize.X - ManagementPanelLeftMargin - ManagementPanelRightReservedWidth - ManagementPanelRightMargin);
		float panelWidth = Mathf.Clamp(maxPanelWidth, 280.0f, maxPanelWidth);

		float viewportHeightCap = Mathf.Max(ManagementPanelMinHeight, viewportSize.Y * ManagementPanelMaxViewportRatio);
		float availableHeightCap = Mathf.Max(ManagementPanelMinHeight, viewportSize.Y - ManagementPanelTopMargin - ManagementPanelBottomMargin);
		float maxPanelHeight = Mathf.Min(viewportHeightCap, availableHeightCap);
		float panelHeight = Mathf.Clamp(ManagementPanelPreferredHeight, ManagementPanelMinHeight, maxPanelHeight);
		float requestedBottomReserve = ShouldReserveSelectedMercenaryHudSpace()
			? ManagementPanelBottomReservedHeight
			: ManagementPanelBottomMargin;
		float maxBottomReserve = Mathf.Max(ManagementPanelBottomMargin, viewportSize.Y - panelHeight - ManagementPanelTopMargin);
		float bottomReserve = Mathf.Min(requestedBottomReserve, maxBottomReserve);

		_bottomPanel.SetAnchorsPreset(LayoutPreset.BottomLeft, false);
		_bottomPanel.GrowVertical = GrowDirection.Begin;
		_bottomPanel.ClipContents = true;
		_bottomPanel.CustomMinimumSize = Vector2.Zero;
		_bottomPanel.OffsetLeft = ManagementPanelLeftMargin;
		_bottomPanel.OffsetRight = ManagementPanelLeftMargin + panelWidth;
		_bottomPanel.OffsetBottom = -bottomReserve;
		_bottomPanel.OffsetTop = _bottomPanel.OffsetBottom - panelHeight;
		_bottomPanel.Size = new Vector2(panelWidth, panelHeight);
	}

	private bool ShouldReserveSelectedMercenaryHudSpace()
	{
		return _selectedMercenary != null
			&& GodotObject.IsInstanceValid(_selectedMercenary)
			&& !_selectedMercenary.IsQueuedForDeletion();
	}

	private static string[] GetBuildCategories()
	{
		return new[]
		{
			StructureCategory,
			LifeCategory,
			DiningCategory,
			StorageCategory,
			ProductionCategory,
			TrainingCategory,
			MedicalCategory,
			DecorCategory,
			SpecialCategory
		};
	}

	private static string[] GetBaseManagementCategories()
	{
		return new[]
		{
			RoomDesignationCategory,
			ZoneCategory,
			StoragePolicyCategory,
			GuardZoneCategory
		};
	}

	private static string[] GetMercenaryFilters()
	{
		return new[]
		{
			MercenaryFilterAll,
			MercenaryFilterCombat,
			MercenaryFilterHaul,
			MercenaryFilterFarm,
			MercenaryFilterMedical,
			MercenaryFilterCraft
		};
	}

	private static string[] GetStorageOverviewCategories()
	{
		return new[]
		{
			StorageOverviewCategory
		};
	}

	private void RebuildMaterialButtons()
	{
		if (_materialRow == null)
		{
			return;
		}

		foreach (Node child in _materialRow.GetChildren())
		{
			child.QueueFree();
		}

		_materialButtons.Clear();

		bool showMaterials = _activePanelMode == PanelMode.Build
			&& BuildStructureDefinitions.IsMaterialSensitiveBuildType(_selectedBuildType);
		_materialRow.Visible = showMaterials;

		if (!showMaterials)
		{
			return;
		}

		Label label = new()
		{
			Text = "\uC7AC\uB8CC:",
			CustomMinimumSize = new Vector2(54.0f, 28.0f),
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		label.AddThemeFontSizeOverride("font_size", 13);
		_materialRow.AddChild(label);

		foreach (BuildMaterialType materialType in BuildStructureDefinitions.GetMaterialOptions(_selectedBuildType))
		{
			Button button = CreateButton(BuildStructureDefinitions.GetMaterialDisplayName(materialType), new Vector2(72.0f, 30.0f));
			button.Disabled = !BuildStructureDefinitions.CanUseMaterial(_selectedBuildType, materialType);
			button.Pressed += () => SelectBuildMaterial(materialType);
			_materialRow.AddChild(button);
			_materialButtons[materialType] = button;
		}

		UpdateMaterialButtonStyles();
	}

	private void SelectBuildMaterial(BuildMaterialType materialType)
	{
		if (!BuildStructureDefinitions.CanUseMaterial(_selectedBuildType, materialType))
		{
			return;
		}

		_selectedBuildMaterialType = BuildStructureDefinitions.NormalizeMaterial(_selectedBuildType, materialType);
		BuildMaterialSelected?.Invoke(_selectedBuildMaterialType);
		RebuildMaterialButtons();
		UpdateSelectionLabel();
		UpdateMaterialButtonStyles();
		ShowBuildItemInfo(GetSelectedItem());
	}

	private void RebuildCategoryButtons(IEnumerable<string> categories)
	{
		if (_categoryRow == null)
		{
			return;
		}

		foreach (Node child in _categoryRow.GetChildren())
		{
			child.QueueFree();
		}

		_categoryButtons.Clear();

		foreach (string category in categories)
		{
			Button button = CreateButton(category, new Vector2(98.0f, 34.0f));
			button.Pressed += () => PopulateCategory(category);
			_categoryRow.AddChild(button);
			_categoryButtons[category] = button;
		}

		UpdateCategoryButtonStyles();
	}

	private void PopulateCategory(string category)
	{
		if (_activePanelMode == PanelMode.StorageOverview)
		{
			PopulateStorageOverview();
			return;
		}

		if (_activePanelMode == PanelMode.BaseManagement)
		{
			PopulateBaseManagementItems(category);
			return;
		}

		if (_activePanelMode == PanelMode.MercenaryManagement)
		{
			PopulateMercenaryItems(category);
			return;
		}

		PopulateBuildItems(category);
	}

	private void PopulateBuildItems(string category)
	{
		_activeCategory = category;
		UpdateCategoryButtonStyles();
		ClearItemRow();
		_itemButtons.Clear();
		_roomButtons.Clear();
		_mercenaryButtons.Clear();
		_storageButtons.Clear();

		if (_itemRow == null)
		{
			return;
		}

		if (!_itemsByCategory.TryGetValue(category, out List<BuildPanelItem>? items) || items.Count == 0)
		{
			RebuildMaterialButtons();
			AddComingSoonLabel();
			ShowBuildItemInfo(null);
			return;
		}

		foreach (BuildPanelItem item in items)
		{
			Button button = CreateButton(item.DisplayName, new Vector2(96.0f, 80.0f));
			button.MouseEntered += () => ShowBuildItemInfo(item);
			button.FocusEntered += () => ShowBuildItemInfo(item);
			button.Pressed += () => SelectBuildItem(item);
			_itemRow.AddChild(button);
			_itemButtons[item.BuildType] = button;
		}

		ShowBuildItemInfo(GetSelectedItem());
		RebuildMaterialButtons();
		UpdateItemButtonStyles();
	}

	private void PopulateBaseManagementItems(string category)
	{
		_activeCategory = category;
		UpdateCategoryButtonStyles();
		ClearItemRow();
		_itemButtons.Clear();
		_roomButtons.Clear();
		_mercenaryButtons.Clear();
		_storageButtons.Clear();
		RebuildMaterialButtons();

		if (_itemRow == null)
		{
			return;
		}

		if (!_roomItemsByCategory.TryGetValue(category, out List<RoomPanelItem>? items) || items.Count == 0)
		{
			SetSelectedRoomDesignation(RoomType.None, false);
			RoomDesignationSelected?.Invoke(RoomType.None, false);
			AddComingSoonLabel();
			ShowRoomItemInfo(null);
			return;
		}

		foreach (RoomPanelItem item in items)
		{
			Button button = CreateButton(item.DisplayName, new Vector2(108.0f, 80.0f));
			button.MouseEntered += () => ShowRoomItemInfo(item);
			button.FocusEntered += () => ShowRoomItemInfo(item);
			button.Pressed += () => SelectRoomItem(item);
			_itemRow.AddChild(button);
			_roomButtons[item.ButtonKey] = button;
		}

		ShowRoomItemInfo(GetSelectedRoomItem());
		UpdateRoomButtonStyles();
	}

	private void PopulateMercenaryItems(string filter)
	{
		_activeCategory = filter;
		UpdateCategoryButtonStyles();
		ClearItemRow();
		_itemButtons.Clear();
		_roomButtons.Clear();
		_mercenaryButtons.Clear();
		_storageButtons.Clear();
		RebuildMaterialButtons();

		if (_itemRow == null)
		{
			return;
		}

		List<MercenaryController> mercenaries = GetCurrentMercenaries();

		foreach (MercenaryController mercenary in mercenaries)
		{
			if (!MatchesMercenaryFilter(mercenary, filter))
			{
				continue;
			}

			Button button = CreateButton(GetMercenaryButtonText(mercenary), new Vector2(188.0f, 132.0f));
			button.AddThemeFontSizeOverride("font_size", 12);
			button.Pressed += () => SelectMercenaryItem(mercenary);
			_itemRow.AddChild(button);
			_mercenaryButtons[mercenary.GetInstanceId()] = button;
		}

		if (_mercenaryButtons.Count == 0)
		{
			Label emptyLabel = new()
			{
				Text = "\uC6A9\uBCD1 \uC5C6\uC74C",
				CustomMinimumSize = new Vector2(240.0f, 56.0f),
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Center,
				MouseFilter = MouseFilterEnum.Ignore
			};
			emptyLabel.AddThemeFontSizeOverride("font_size", 18);
			_itemRow.AddChild(emptyLabel);
		}

		AddSelectedMercenaryWorkPanel();
		UpdateMercenaryButtonStyles();
	}

	private void PopulateStorageOverview()
	{
		_activeCategory = StorageOverviewCategory;
		ClearItemRow();
		_itemButtons.Clear();
		_roomButtons.Clear();
		_mercenaryButtons.Clear();
		RebuildMaterialButtons();

		if (_itemRow == null)
		{
			return;
		}

		if (_buildManager == null)
		{
			AddComingSoonLabel();
			return;
		}

		IReadOnlyList<Vector2I> storageCells = _buildManager.GetStorageOriginCells();
		RebuildStorageSelectorButtons(storageCells);

		if (storageCells.Count == 0)
		{
			Label emptyLabel = new()
			{
				Text = "\uBCF4\uAD00\uD568 \uC5C6\uC74C",
				CustomMinimumSize = new Vector2(240.0f, 56.0f),
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Center,
				MouseFilter = MouseFilterEnum.Ignore
			};
			emptyLabel.AddThemeFontSizeOverride("font_size", 18);
			_itemRow.AddChild(emptyLabel);
			return;
		}

		if (_selectedStorageCell.HasValue && !_buildManager.TryResolveStorageOriginCell(_selectedStorageCell.Value, out _))
		{
			_selectedStorageCell = null;
		}

		if (_selectedStorageCell.HasValue)
		{
			AddStorageManagementSummaryPanel(_selectedStorageCell);
			AddStorageManagementContentsPanel(_selectedStorageCell);
			AddStorageManagementPolicyPanel(_selectedStorageCell);
		}
		else
		{
			AddStorageOverviewTotalsPanel(storageCells);
		}

		UpdateCategoryButtonStyles();
		UpdateStorageButtonStyles();
	}

	private void RebuildStorageSelectorButtons(IReadOnlyList<Vector2I> storageCells)
	{
		if (_categoryRow == null || _buildManager == null)
		{
			return;
		}

		foreach (Node child in _categoryRow.GetChildren())
		{
			child.QueueFree();
		}

		_categoryButtons.Clear();
		_storageButtons.Clear();

		Button allButton = CreateButton(StorageOverviewCategory, new Vector2(104.0f, 34.0f));
		allButton.Pressed += SelectStorageOverviewAll;
		PrepareSingleLineButton(allButton);
		_categoryRow.AddChild(allButton);
		_categoryButtons[StorageOverviewCategory] = allButton;

		if (ShowStockpileZoneControls)
		{
			Button addZoneButton = CreateButton("\uC800\uC7A5 \uAD6C\uC5ED \uC9C0\uC815", new Vector2(118.0f, 34.0f));
			addZoneButton.AddThemeFontSizeOverride("font_size", 10);
			PrepareSingleLineButton(addZoneButton);
			addZoneButton.Pressed += () => StockpileZoneDesignationSelected?.Invoke(false);
			_categoryRow.AddChild(addZoneButton);

			Button removeZoneButton = CreateButton("\uAD6C\uC5ED \uC0AD\uC81C", new Vector2(96.0f, 34.0f));
			removeZoneButton.AddThemeFontSizeOverride("font_size", 10);
			PrepareSingleLineButton(removeZoneButton);
			removeZoneButton.Pressed += () => StockpileZoneDesignationSelected?.Invoke(true);
			_categoryRow.AddChild(removeZoneButton);
		}

		ScrollContainer selectorScroll = new()
		{
			Name = "StorageSelectorScroll",
			CustomMinimumSize = Vector2.Zero,
			MouseFilter = MouseFilterEnum.Stop,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			ClipContents = true,
			VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Auto
		};
		_categoryRow.AddChild(selectorScroll);

		HBoxContainer selectorRow = new()
		{
			Name = "StorageSelectorRow",
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			ClipContents = true
		};
		selectorRow.AddThemeConstantOverride("separation", 6);
		selectorScroll.AddChild(selectorRow);

		Dictionary<string, int> nameTotals = BuildStorageNameTotals(storageCells);
		Dictionary<string, int> nameSeen = new();

		foreach (Vector2I storageCell in storageCells)
		{
			string buttonText = GetStorageSelectorButtonText(storageCell, nameTotals, nameSeen);
			Button button = CreateButton(buttonText, new Vector2(116.0f, 34.0f));
			button.AddThemeFontSizeOverride("font_size", 12);
			PrepareSingleLineButton(button);
			button.Pressed += () => SelectStorageItem(storageCell);
			selectorRow.AddChild(button);
			_storageButtons[storageCell] = button;
		}

		UpdateCategoryButtonStyles();
		UpdateStorageButtonStyles();
	}

	private void AddStorageOverviewTotalsPanel(IReadOnlyList<Vector2I> storageCells)
	{
		if (_itemRow == null || _buildManager == null)
		{
			return;
		}

		PanelContainer summaryPanel = CreatePanel(new Color(0.07f, 0.07f, 0.07f, 0.78f), new Color(0.45f, 0.58f, 0.72f, 0.55f), 6);
		summaryPanel.CustomMinimumSize = new Vector2(240.0f, 80.0f);
		summaryPanel.SizeFlagsHorizontal = SizeFlags.Fill;
		summaryPanel.ClipContents = true;
		summaryPanel.AddChild(CreateStorageDetailLabel(
			$"{StorageOverviewCategory}\n"
			+ $"\uBCF4\uAD00\uD568 {storageCells.Count}\n"
			+ $"\uC804\uCCB4 \uC790\uC6D0 \uD569\uC0B0",
			12));
		_itemRow.AddChild(summaryPanel);

		PanelContainer contentsPanel = CreatePanel(new Color(0.07f, 0.07f, 0.07f, 0.78f), new Color(0.45f, 0.58f, 0.72f, 0.55f), 6);
		contentsPanel.CustomMinimumSize = new Vector2(520.0f, 80.0f);
		contentsPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		contentsPanel.ClipContents = true;
		ScrollContainer contentScroll = new()
		{
			Name = "StorageOverviewTotalsScroll",
			CustomMinimumSize = new Vector2(360.0f, 0.0f),
			MouseFilter = MouseFilterEnum.Stop,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		contentsPanel.AddChild(contentScroll);
		contentScroll.AddChild(CreateStorageDetailLabel(
			$"\uC804\uCCB4 \uBCF4\uC720\n{BuildStorageTotalSummary()}",
			12));
		_itemRow.AddChild(contentsPanel);
	}

	private void AddStorageManagementSummaryPanel(Vector2I? selectedStorageCell)
	{
		if (_itemRow == null || _buildManager == null || !selectedStorageCell.HasValue)
		{
			return;
		}

		Vector2I storageCell = selectedStorageCell.Value;
		int usedWeight = _buildManager.GetStorageUsedWeight(storageCell);
		int capacity = _buildManager.GetStorageWeightCapacity(storageCell);
		string useText = _buildManager.GetStorageInteractionUserLabel(storageCell);

		PanelContainer panel = CreatePanel(new Color(0.07f, 0.07f, 0.07f, 0.78f), new Color(0.45f, 0.58f, 0.72f, 0.55f), 6);
		panel.CustomMinimumSize = new Vector2(240.0f, 80.0f);
		panel.SizeFlagsHorizontal = SizeFlags.Fill;
		panel.ClipContents = true;
		panel.AddChild(CreateStorageDetailLabel(
			$"{_buildManager.GetStorageDisplayName(storageCell)}\n"
			+ $"\uBB34\uAC8C {usedWeight} / {capacity}\n"
			+ $"\uC815\uCC45: {_buildManager.GetStoragePolicySummary(storageCell)}\n"
			+ $"\uC0AC\uC6A9 \uC911: {useText}",
			12));
		_itemRow.AddChild(panel);
	}

	private void AddStorageManagementContentsPanel(Vector2I? selectedStorageCell)
	{
		if (_itemRow == null || _buildManager == null || !selectedStorageCell.HasValue)
		{
			return;
		}

		Vector2I storageCell = selectedStorageCell.Value;
		_buildManager.TryGetStorageContents(storageCell, out Dictionary<BaseResourceType, int> contents);
		PanelContainer panel = CreatePanel(new Color(0.07f, 0.07f, 0.07f, 0.78f), new Color(0.45f, 0.58f, 0.72f, 0.55f), 6);
		panel.CustomMinimumSize = new Vector2(320.0f, 80.0f);
		panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		panel.ClipContents = true;
		ScrollContainer contentScroll = new()
		{
			Name = "StorageContentsScroll",
			CustomMinimumSize = new Vector2(260.0f, 0.0f),
			MouseFilter = MouseFilterEnum.Stop,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		panel.AddChild(contentScroll);
		contentScroll.AddChild(CreateStorageDetailLabel(
			$"\uB0B4\uC6A9\uBB3C\n{BuildStorageContentsText(contents)}",
			12));
		_itemRow.AddChild(panel);
	}

	private void AddStorageManagementPolicyPanel(Vector2I? selectedStorageCell)
	{
		if (_itemRow == null || _buildManager == null || !selectedStorageCell.HasValue)
		{
			return;
		}

		Vector2I storageCell = selectedStorageCell.Value;
		StoragePolicy policy = _buildManager.GetStoragePolicy(storageCell);
		PanelContainer panel = CreatePanel(new Color(0.07f, 0.07f, 0.07f, 0.78f), new Color(0.72f, 0.62f, 0.30f, 0.55f), 6);
		panel.CustomMinimumSize = new Vector2(420.0f, 80.0f);
		panel.SizeFlagsHorizontal = SizeFlags.Fill;
		panel.ClipContents = true;
		ScrollContainer policyScroll = new()
		{
			Name = "StoragePolicyScroll",
			CustomMinimumSize = Vector2.Zero,
			MouseFilter = MouseFilterEnum.Stop,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		panel.AddChild(policyScroll);

		HBoxContainer columns = new()
		{
			MouseFilter = MouseFilterEnum.Stop,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.Fill,
			CustomMinimumSize = Vector2.Zero
		};
		columns.ClipContents = true;
		policyScroll.AddChild(columns);

		VBoxContainer left = new() { CustomMinimumSize = new Vector2(136.0f, 72.0f), MouseFilter = MouseFilterEnum.Stop, ClipContents = true };
		columns.AddChild(left);
		left.AddChild(CreateSmallLabel("\uC800\uC7A5 \uC120\uD638\uB3C4", 12));
		Button priorityButton = CreateButton(StoragePolicyHelpers.GetPriorityDisplayName(policy.Priority), new Vector2(118.0f, 30.0f));
		priorityButton.AddThemeFontSizeOverride("font_size", 12);
		priorityButton.Pressed += () => CycleStoragePriority(storageCell);
		left.AddChild(priorityButton);
		left.AddChild(CreateSmallLabel("\n\uC800\uC7A5\uC18C \uC81C\uD55C:\n" + _buildManager.GetStorageLimitText(storageCell), 11));

		VBoxContainer presets = new() { CustomMinimumSize = new Vector2(148.0f, 72.0f), MouseFilter = MouseFilterEnum.Stop, ClipContents = true };
		columns.AddChild(presets);
		presets.AddChild(CreateSmallLabel("\uD504\uB9AC\uC14B", 12));
		foreach (StoragePolicyPreset preset in GetStoragePresetOrder())
		{
			Button button = CreateButton(StoragePolicyHelpers.GetPresetDisplayName(preset), new Vector2(132.0f, 24.0f));
			button.AddThemeFontSizeOverride("font_size", 11);
			button.Pressed += () => ApplyStoragePreset(storageCell, preset);
			ApplyButtonStyle(button, policy.Preset == preset ? ButtonTone.SelectedItem : ButtonTone.Item);
			presets.AddChild(button);
		}

		VBoxContainer filters = new() { CustomMinimumSize = new Vector2(178.0f, 72.0f), MouseFilter = MouseFilterEnum.Stop, ClipContents = true };
		columns.AddChild(filters);
		filters.AddChild(CreateSmallLabel("\uC0C1\uC138 \uD544\uD130", 12));
		foreach (StorageResourceCategory category in GetStorageCategoryOrder())
		{
			HBoxContainer row = new() { MouseFilter = MouseFilterEnum.Stop };
			filters.AddChild(row);

			Button categoryButton = CreateButton(GetCategoryButtonText(policy, category), new Vector2(70.0f, 24.0f));
			categoryButton.AddThemeFontSizeOverride("font_size", 10);
			categoryButton.Pressed += () => ToggleStorageCategory(storageCell, category);
			row.AddChild(categoryButton);

			foreach (BaseResourceType resourceType in StoragePolicyHelpers.GetResourcesInCategory(category))
			{
				bool hardAllowed = _buildManager.CanStorageCapabilityAllow(storageCell, resourceType);
				bool allowed = hardAllowed && policy.UserAllows(resourceType);
				Button resourceButton = CreateButton(BaseBuildManager.GetResourceMarker(resourceType), new Vector2(30.0f, 24.0f));
				resourceButton.AddThemeFontSizeOverride("font_size", 10);
				resourceButton.Disabled = !hardAllowed;
				resourceButton.Pressed += () => ToggleStorageResource(storageCell, resourceType);
				ApplyButtonStyle(resourceButton, allowed ? ButtonTone.SelectedItem : ButtonTone.Item);
				row.AddChild(resourceButton);
			}
		}

		_itemRow.AddChild(panel);
	}

	private void AddSelectedMercenaryWorkPanel()
	{
		if (_itemRow == null
			|| _selectedMercenary == null
			|| !GodotObject.IsInstanceValid(_selectedMercenary)
			|| _selectedMercenary.IsQueuedForDeletion())
		{
			return;
		}

		MercenaryController mercenary = _selectedMercenary;
		MercenaryProfile profile = mercenary.Profile;
		MercenaryWorkSettings settings = profile.GetWorkSettings();
		PanelContainer panel = CreatePanel(new Color(0.08f, 0.08f, 0.08f, 0.72f), new Color(0.86f, 0.64f, 0.35f, 0.55f), 6);
		panel.CustomMinimumSize = new Vector2(330.0f, 120.0f);

		VBoxContainer content = new()
		{
			MouseFilter = MouseFilterEnum.Stop
		};
		panel.AddChild(content);

		Label titleLabel = new()
		{
			Text = $"\uC791\uC5C5 \uC6B0\uC120\uC21C\uC704: {profile.DisplayName} [{profile.Rank}]",
			MouseFilter = MouseFilterEnum.Ignore
		};
		titleLabel.AddThemeFontSizeOverride("font_size", 14);
		content.AddChild(titleLabel);

		Label hintLabel = new()
		{
			Text = "\uD074\uB9AD\uD558\uBA74 \uB054 > \uB0AE\uC74C > \uBCF4\uD1B5 > \uB192\uC74C\uC73C\uB85C \uBCC0\uACBD",
			MouseFilter = MouseFilterEnum.Ignore
		};
		hintLabel.AddThemeFontSizeOverride("font_size", 11);
		content.AddChild(hintLabel);

		GridContainer buttonGrid = new()
		{
			Columns = 4,
			MouseFilter = MouseFilterEnum.Stop
		};
		content.AddChild(buttonGrid);

		foreach (MercenaryWorkType workType in MercenaryWorkSettings.OrderedTypes)
		{
			Button button = CreateButton(GetWorkPriorityButtonText(settings, workType), new Vector2(74.0f, 38.0f));
			button.AddThemeFontSizeOverride("font_size", 12);
			button.Pressed += () => CycleMercenaryWorkPriority(mercenary, workType);
			buttonGrid.AddChild(button);
		}

		_itemRow.AddChild(panel);
	}

	private void ClearItemRow()
	{
		if (_itemRow == null)
		{
			return;
		}

		foreach (Node child in _itemRow.GetChildren())
		{
			child.QueueFree();
		}
	}

	private void AddComingSoonLabel()
	{
		if (_itemRow == null)
		{
			return;
		}

		Label comingSoonLabel = new()
		{
			Text = ComingSoon,
			CustomMinimumSize = new Vector2(240.0f, 56.0f),
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		comingSoonLabel.AddThemeFontSizeOverride("font_size", 18);
		_itemRow.AddChild(comingSoonLabel);
	}

	private void HandleMainTabPressed(string label)
	{
		_activeMainTab = label;

		if (label == MainTabBuild)
		{
			RoomDesignationSelected?.Invoke(RoomType.None, false);
			_activePanelMode = PanelMode.Build;
			_activeCategory = StructureCategory;
			_selectedStorageCell = null;
			RebuildCategoryButtons(GetBuildCategories());
			PopulateBuildItems(StructureCategory);

			if (_bottomPanel != null)
			{
				_bottomPanel.Visible = true;
			}
		}
		else if (label == MainTabBaseManagement)
		{
			BuildModeSelected?.Invoke(TileBuildType.None);
			_activePanelMode = PanelMode.BaseManagement;
			_activeCategory = RoomDesignationCategory;
			_selectedBuildType = TileBuildType.None;
			_selectedStorageCell = null;
			RebuildCategoryButtons(GetBaseManagementCategories());
			PopulateBaseManagementItems(RoomDesignationCategory);

			if (_bottomPanel != null)
			{
				_bottomPanel.Visible = true;
			}
		}
		else if (label == MainTabMercenaryManagement)
		{
			BuildModeSelected?.Invoke(TileBuildType.None);
			RoomDesignationSelected?.Invoke(RoomType.None, false);
			_activePanelMode = PanelMode.MercenaryManagement;
			_activeCategory = MercenaryFilterAll;
			_selectedBuildType = TileBuildType.None;
			_selectedRoomType = RoomType.None;
			_selectedRoomRemoveMode = false;
			_selectedStorageCell = null;
			RebuildCategoryButtons(GetMercenaryFilters());
			PopulateMercenaryItems(MercenaryFilterAll);
			_mercenaryListRefreshTimer = 1.0f;

			if (_bottomPanel != null)
			{
				_bottomPanel.Visible = true;
			}
		}
		else if (label == MainTabStorage)
		{
			BuildModeSelected?.Invoke(TileBuildType.None);
			RoomDesignationSelected?.Invoke(RoomType.None, false);
			_activePanelMode = PanelMode.StorageOverview;
			_activeCategory = StorageOverviewCategory;
			_selectedBuildType = TileBuildType.None;
			_selectedRoomType = RoomType.None;
			_selectedRoomRemoveMode = false;
			_selectedMercenary = null;
			RebuildCategoryButtons(GetStorageOverviewCategories());
			PopulateStorageOverview();
			_storageListRefreshTimer = 1.0f;

			if (_bottomPanel != null)
			{
				_bottomPanel.Visible = true;
			}
		}
		else if (_bottomPanel != null)
		{
			_activePanelMode = PanelMode.None;
			_bottomPanel.Visible = false;
		}

		UpdateSelectionLabel();
		UpdateMainTabButtonStyles();
	}

	private void SelectBuildItem(BuildPanelItem item)
	{
		_selectedRoomType = RoomType.None;
		_selectedRoomRemoveMode = false;
		SetSelectedBuildType(item.BuildType);
		ShowBuildItemInfo(item);
		BuildModeSelected?.Invoke(item.BuildType);
	}

	private void SelectRoomItem(RoomPanelItem item)
	{
		SetSelectedRoomDesignation(item.RoomType, item.IsRemoveMode);
		ShowRoomItemInfo(item);
		RoomDesignationSelected?.Invoke(item.RoomType, item.IsRemoveMode);
	}

	private void SelectMercenaryItem(MercenaryController mercenary)
	{
		_selectedMercenary = mercenary;
		_selectedBuildType = TileBuildType.None;
		_selectedRoomType = RoomType.None;
		_selectedRoomRemoveMode = false;
		_selectedStorageCell = null;
		UpdateSelectionLabel();
		UpdateMercenaryButtonStyles();
		MercenarySelected?.Invoke(mercenary);
	}

	private void SelectStorageItem(Vector2I storageCell)
	{
		if (_buildManager == null || !_buildManager.TryResolveStorageOriginCell(storageCell, out Vector2I originCell))
		{
			return;
		}

		_selectedStorageCell = originCell;
		_selectedBuildType = TileBuildType.None;
		_selectedRoomType = RoomType.None;
		_selectedRoomRemoveMode = false;
		_selectedMercenary = null;
		ShowStorageInfo(originCell);
		if (_activePanelMode == PanelMode.StorageOverview)
		{
			PopulateStorageOverview();
		}
		StorageSelected?.Invoke(originCell);
	}

	private void SelectStorageOverviewAll()
	{
		_selectedStorageCell = null;
		_selectedBuildType = TileBuildType.None;
		_selectedRoomType = RoomType.None;
		_selectedRoomRemoveMode = false;
		_selectedMercenary = null;
		if (_activePanelMode == PanelMode.StorageOverview)
		{
			PopulateStorageOverview();
		}
		UpdateSelectionLabel();
	}

	private void CycleMercenaryWorkPriority(MercenaryController mercenary, MercenaryWorkType workType)
	{
		if (!GodotObject.IsInstanceValid(mercenary) || mercenary.IsQueuedForDeletion())
		{
			return;
		}

		mercenary.Profile.GetWorkSettings().CyclePriority(workType);
		MercenaryWorkSettingsChanged?.Invoke(mercenary);
		PopulateMercenaryItems(_activeCategory);
		UpdateSelectionLabel();
	}

	private void UpdateSelectionLabel()
	{
		if (_selectionLabel == null)
		{
			return;
		}

		if (_selectedRoomRemoveMode)
		{
			_selectionLabel.Text = $"{MainTabBaseManagement} > {RoomDesignationCategory} > {RoomDeleteName}";
			return;
		}

		if (_selectedRoomType != RoomType.None)
		{
			_selectionLabel.Text = $"{MainTabBaseManagement} > {RoomDesignationCategory} > {BaseRoomManager.GetRoomDisplayName(_selectedRoomType)}";
			return;
		}

		if (_selectedMercenary != null && GodotObject.IsInstanceValid(_selectedMercenary))
		{
			_selectionLabel.Text = $"{MainTabMercenaryManagement} > {_selectedMercenary.Profile.DisplayName} [{_selectedMercenary.Profile.Rank}]";
			return;
		}

		if (_selectedStorageCell.HasValue && _buildManager != null)
		{
			_selectionLabel.Text = $"{MainTabStorage} > {_buildManager.GetStorageDisplayName(_selectedStorageCell.Value)}";
			return;
		}

		if (_activePanelMode == PanelMode.StorageOverview)
		{
			_selectionLabel.Text = $"{MainTabStorage} > {StorageOverviewCategory}";
			return;
		}

		BuildPanelItem? selectedItem = GetSelectedItem();
		if (selectedItem == null)
		{
			_selectionLabel.Text = NoSelection;
			return;
		}

		string materialText = BuildStructureDefinitions.IsMaterialSensitiveBuildType(selectedItem.Value.BuildType)
			? $" / \uC7AC\uB8CC: {BuildStructureDefinitions.GetMaterialDisplayName(_selectedBuildMaterialType)}"
			: "";
		_selectionLabel.Text = $"{MainTabBuild} > {selectedItem.Value.Category} > {selectedItem.Value.DisplayName}{materialText}";
	}

	private void ShowBuildItemInfo(BuildPanelItem? item)
	{
		if (_infoLabel == null)
		{
			return;
		}

		if (item == null)
		{
			HideInfoPanel();
			return;
		}

		ShowInfoPanel();
		bool materialSensitive = BuildStructureDefinitions.IsMaterialSensitiveBuildType(item.Value.BuildType);
		BuildStructureDefinition definition = _buildManager != null
			? _buildManager.GetCurrentBuildDefinition(item.Value.BuildType)
			: BuildStructureDefinitions.Get(item.Value.BuildType, _selectedBuildMaterialType);
		string displayName = materialSensitive ? definition.DisplayName : item.Value.DisplayName;
		string costText = GetCostText(item.Value.BuildType);
		string methodText = _buildManager != null && _buildManager.UsesDirectConstructionSite(item.Value.BuildType)
			? "\uC6A9\uBCD1 \uC9C1\uC811 \uAC74\uC124"
			: "\uC989\uC2DC \uBC30\uCE58";
		string performanceText = materialSensitive
			? $"\n\uB0B4\uAD6C\uB3C4: {definition.Durability}"
				+ $"\n\uAC74\uC124 \uC791\uC5C5\uB7C9: x{definition.RequiredWorkMultiplier:0.0}"
				+ $"\n\uBC29 \uD488\uC9C8: +{definition.RoomQualityBonus:0.0}"
				+ $"\n\uC704\uC0DD: +{definition.HygieneModifier:0.0}"
				+ $"\n\uC774\uB3D9 \uC18D\uB3C4: x{definition.MoveSpeedMultiplier:0.00}"
			: "";
		string description = materialSensitive && !string.IsNullOrWhiteSpace(definition.Description)
			? definition.Description
			: item.Value.Description;
		_infoLabel.Text = $"{displayName}\n"
			+ $"\uD06C\uAE30: {item.Value.Size.X}x{item.Value.Size.Y}\n"
			+ $"\uD544\uC694 \uC7AC\uB8CC: {costText}\n"
			+ $"\uBC29\uC2DD: {methodText}\n"
			+ $"{performanceText}\n"
			+ $"\uBD84\uB958: {item.Value.Category}\n\n"
			+ $"\uC124\uBA85: {description}";
	}

	private void ShowRoomItemInfo(RoomPanelItem? item)
	{
		if (_infoLabel == null)
		{
			return;
		}

		if (item == null)
		{
			HideInfoPanel();
			return;
		}

		ShowInfoPanel();
		if (item.Value.IsRemoveMode)
		{
			_infoLabel.Text = $"{RoomDeleteName}\n"
				+ "\uD06C\uAE30: \uC9C0\uC815 \uC601\uC5ED\n"
				+ "\uC694\uAD6C: -\n"
				+ "\uBD84\uB958: \uBC29 \uC9C0\uC815\n\n"
				+ "\uC124\uBA85: \uB4DC\uB798\uADF8\uD55C \uC601\uC5ED\uACFC \uACB9\uCE58\uB294 \uBC29\uC744 \uC0AD\uC81C\uD569\uB2C8\uB2E4.";
			return;
		}

		_infoLabel.Text = $"{item.Value.DisplayName}\n"
			+ "\uD06C\uAE30: \uC9C0\uC815 \uC601\uC5ED\n"
			+ $"\uC694\uAD6C: {BaseRoomManager.GetRoomRequirementText(item.Value.RoomType)}\n"
			+ "\uBD84\uB958: \uBC29 \uC9C0\uC815\n\n"
			+ $"\uC124\uBA85: {BaseRoomManager.GetRoomDescription(item.Value.RoomType)}";
	}

	private void ShowInfoPanel()
	{
		if (_infoPanel != null)
		{
			_infoPanel.Visible = true;
		}
	}

	private void HideInfoPanel()
	{
		if (_infoLabel != null)
		{
			_infoLabel.Text = "";
		}

		if (_infoPanel != null)
		{
			_infoPanel.Visible = false;
		}
	}

	private List<MercenaryController> GetCurrentMercenaries()
	{
		List<MercenaryController> mercenaries = new();
		SceneTree? tree = GetTree();

		if (tree == null)
		{
			return mercenaries;
		}

		foreach (Node node in tree.GetNodesInGroup("mercenaries"))
		{
			if (node is MercenaryController mercenary
				&& GodotObject.IsInstanceValid(mercenary)
				&& !mercenary.IsQueuedForDeletion())
			{
				mercenaries.Add(mercenary);
			}
		}

		return mercenaries;
	}

	private static bool MatchesMercenaryFilter(MercenaryController mercenary, string filter)
	{
		MercenaryProfile profile = mercenary.Profile;

		return filter switch
		{
			MercenaryFilterCombat => HasAnyRole(profile, MercenaryRole.Fighter, MercenaryRole.Tank, MercenaryRole.Scout, MercenaryRole.Guard),
			MercenaryFilterHaul => HasAnyRole(profile, MercenaryRole.Hauler, MercenaryRole.Worker),
			MercenaryFilterFarm => HasAnyRole(profile, MercenaryRole.Farmer, MercenaryRole.Worker),
			MercenaryFilterMedical => HasAnyRole(profile, MercenaryRole.Medic),
			MercenaryFilterCraft => HasAnyRole(profile, MercenaryRole.Crafter, MercenaryRole.Worker),
			_ => true
		};
	}

	private static bool HasAnyRole(MercenaryProfile profile, params MercenaryRole[] roles)
	{
		foreach (MercenaryRole role in roles)
		{
			if (profile.PrimaryRole == role || profile.SecondaryRole == role)
			{
				return true;
			}
		}

		return false;
	}

	private static string GetMercenaryButtonText(MercenaryController mercenary)
	{
		MercenaryProfile profile = mercenary.Profile;
		MercenaryStats stats = profile.Stats;
		MercenaryCondition condition = profile.Condition;
		int hunger = Mathf.RoundToInt(condition.Hunger);
		int sleepiness = Mathf.RoundToInt(condition.Sleepiness);
		int mood = Mathf.RoundToInt(condition.Mood);
		int stress = Mathf.RoundToInt(condition.Stress);
		int hygiene = Mathf.RoundToInt(condition.Hygiene);

		return $"{profile.DisplayName} [{profile.Rank}] {profile.PrimaryRole}\n"
			+ $"HP {condition.Health}/{stats.MaxHealth} | \uBC30\uACE0\uD514 {hunger} | \uC878\uB9BC {sleepiness}\n"
			+ $"\uC6B4\uBC18 {stats.MaxCarryWeight} | \uAE30\uBD84 {mood} | \uC2A4\uD2B8\uB808\uC2A4 {stress}\n"
			+ $"\uAC00\uBC29 {mercenary.GetInventoryWeightSummary()}\n"
			+ $"\uC0C1\uD0DC: {mercenary.GetConditionStatusSummary()} | \uC704\uC0DD {hygiene}\n"
			+ $"\uD604\uC7AC: {mercenary.GetCurrentWorkSummary()}\n"
			+ $"\uB2F4\uB2F9: {profile.GetWorkSettings().GetTopSummary(2)}";
	}

	private static string GetWorkPriorityButtonText(MercenaryWorkSettings settings, MercenaryWorkType workType)
	{
		MercenaryWorkPriority priority = settings.GetPriority(workType);
		return $"{MercenaryWorkSettings.GetWorkTypeDisplayName(workType)}\n{MercenaryWorkSettings.GetPriorityDisplayName(priority)}";
	}

	private string BuildStorageTotalSummary()
	{
		if (_buildManager == null)
		{
			return "-";
		}

		List<string> lines = new();

		foreach (BaseResourceType resourceType in BaseBuildManager.GetAllResourceTypes())
		{
			int amount = _buildManager.GetTotalStoredAmount(resourceType);
			if (amount <= 0)
			{
				continue;
			}

			lines.Add($"{BaseBuildManager.GetResourceDisplayName(resourceType)} {amount}");
		}

		return lines.Count > 0
			? string.Join("\n", lines)
			: "\uC5C6\uC74C";
	}

	private void CycleStoragePriority(Vector2I storageCell)
	{
		if (_buildManager == null)
		{
			return;
		}

		_buildManager.CycleStoragePriority(storageCell);
		RefreshStorageManagementSelection(storageCell);
	}

	private void ApplyStoragePreset(Vector2I storageCell, StoragePolicyPreset preset)
	{
		if (_buildManager == null)
		{
			return;
		}

		_buildManager.ApplyStoragePolicyPreset(storageCell, preset);
		RefreshStorageManagementSelection(storageCell);
	}

	private void ToggleStorageCategory(Vector2I storageCell, StorageResourceCategory category)
	{
		if (_buildManager == null)
		{
			return;
		}

		_buildManager.ToggleStorageCategoryAllowed(storageCell, category);
		RefreshStorageManagementSelection(storageCell);
	}

	private void ToggleStorageResource(Vector2I storageCell, BaseResourceType resourceType)
	{
		if (_buildManager == null)
		{
			return;
		}

		_buildManager.ToggleStorageResourceAllowed(storageCell, resourceType);
		RefreshStorageManagementSelection(storageCell);
	}

	private void RefreshStorageManagementSelection(Vector2I storageCell)
	{
		_selectedStorageCell = storageCell;
		ShowStorageInfo(storageCell);
		PopulateStorageOverview();
	}

	private void RefreshStorageOverviewText()
	{
		if (_buildManager == null)
		{
			return;
		}

		IReadOnlyList<Vector2I> storageCells = _buildManager.GetStorageOriginCells();
		if (!IsStorageOverviewStructureCurrent(storageCells))
		{
			PopulateStorageOverview();
			return;
		}

		Dictionary<string, int> nameTotals = BuildStorageNameTotals(storageCells);
		Dictionary<string, int> nameSeen = new();

		foreach (Vector2I storageCell in storageCells)
		{
			if (_storageButtons.TryGetValue(storageCell, out Button? button)
				&& GodotObject.IsInstanceValid(button)
				&& !button.IsQueuedForDeletion())
			{
				button.Text = GetStorageSelectorButtonText(storageCell, nameTotals, nameSeen);
			}
		}

		if (_selectedStorageCell.HasValue
			&& _buildManager.TryResolveStorageOriginCell(_selectedStorageCell.Value, out Vector2I originCell))
		{
			ShowStorageInfo(originCell);
		}

		UpdateStorageButtonStyles();
		UpdateSelectionLabel();
	}

	private bool IsStorageOverviewStructureCurrent(IReadOnlyList<Vector2I> storageCells)
	{
		if (_storageButtons.Count != storageCells.Count)
		{
			return false;
		}

		foreach (Vector2I storageCell in storageCells)
		{
			if (!_storageButtons.TryGetValue(storageCell, out Button? button)
				|| !GodotObject.IsInstanceValid(button)
				|| button.IsQueuedForDeletion())
			{
				return false;
			}
		}

		return true;
	}

	private Dictionary<string, int> BuildStorageNameTotals(IReadOnlyList<Vector2I> storageCells)
	{
		Dictionary<string, int> totals = new();

		if (_buildManager == null)
		{
			return totals;
		}

		foreach (Vector2I storageCell in storageCells)
		{
			string displayName = _buildManager.GetStorageDisplayName(storageCell);
			totals.TryGetValue(displayName, out int count);
			totals[displayName] = count + 1;
		}

		return totals;
	}

	private string GetStorageSelectorButtonText(Vector2I storageCell, Dictionary<string, int> nameTotals, Dictionary<string, int> nameSeen)
	{
		if (_buildManager == null)
		{
			return "\uBCF4\uAD00\uD568";
		}

		string displayName = _buildManager.GetStorageDisplayName(storageCell);
		nameSeen.TryGetValue(displayName, out int seen);
		seen++;
		nameSeen[displayName] = seen;

		if (nameTotals.TryGetValue(displayName, out int total) && total > 1)
		{
			return $"{displayName} {seen}";
		}

		return displayName;
	}

	private static StoragePolicyPreset[] GetStoragePresetOrder()
	{
		return new[]
		{
			StoragePolicyPreset.All,
			StoragePolicyPreset.None,
			StoragePolicyPreset.Food,
			StoragePolicyPreset.Meals,
			StoragePolicyPreset.FoodAndMeals,
			StoragePolicyPreset.RawMaterials,
			StoragePolicyPreset.ProcessedMaterials,
			StoragePolicyPreset.ConstructionMaterials,
			StoragePolicyPreset.Medical
		};
	}

	private static StorageResourceCategory[] GetStorageCategoryOrder()
	{
		return new[]
		{
			StorageResourceCategory.Food,
			StorageResourceCategory.Meal,
			StorageResourceCategory.RawMaterial,
			StorageResourceCategory.ProcessedMaterial,
			StorageResourceCategory.Medical
		};
	}

	private static string GetCategoryButtonText(StoragePolicy policy, StorageResourceCategory category)
	{
		string marker = policy.IsCategoryFullyAllowed(category)
			? "[+]"
			: policy.IsCategoryPartiallyAllowed(category)
				? "[~]"
				: "[-]";
		return $"{marker} {StoragePolicyHelpers.GetCategoryDisplayName(category)}";
	}

	private string GetStorageButtonText(Vector2I storageCell)
	{
		if (_buildManager == null)
		{
			return "\uBCF4\uAD00\uD568";
		}

		_buildManager.TryGetStorageContents(storageCell, out Dictionary<BaseResourceType, int> contents);
		int usedWeight = _buildManager.GetStorageUsedWeight(storageCell);
		int capacity = _buildManager.GetStorageWeightCapacity(storageCell);
		string summary = BuildStorageCompactContentsText(contents);

		return $"{_buildManager.GetStorageDisplayName(storageCell)}  {usedWeight}/{capacity}\n"
			+ $"{_buildManager.GetStoragePolicySummary(storageCell)} / {summary}";
	}

	private static string BuildStorageContentsText(Dictionary<BaseResourceType, int> contents)
	{
		List<string> lines = new();

		foreach (BaseResourceType resourceType in BaseBuildManager.GetAllResourceTypes())
		{
			if (contents.TryGetValue(resourceType, out int amount) && amount > 0)
			{
				lines.Add($"{BaseBuildManager.GetResourceDisplayName(resourceType)} x{amount}");
			}
		}

		return lines.Count == 0 ? "\uBE44\uC5B4 \uC788\uC74C" : string.Join("\n", lines);
	}

	private static string BuildStorageCompactContentsText(Dictionary<BaseResourceType, int> contents)
	{
		List<string> parts = new();

		foreach (BaseResourceType resourceType in BaseBuildManager.GetAllResourceTypes())
		{
			if (contents.TryGetValue(resourceType, out int amount) && amount > 0)
			{
				parts.Add($"{BaseBuildManager.GetResourceMarker(resourceType)} {amount}");
			}

			if (parts.Count >= 4)
			{
				break;
			}
		}

		return parts.Count == 0 ? "\uBE44\uC5B4 \uC788\uC74C" : string.Join(", ", parts);
	}

	private string GetCostText(TileBuildType buildType)
	{
		if (_buildManager == null)
		{
			return "-";
		}

		IReadOnlyDictionary<BaseResourceType, int> cost = _buildManager.GetBuildCost(buildType);
		List<string> parts = new();

		foreach (KeyValuePair<BaseResourceType, int> entry in cost)
		{
			if (entry.Value <= 0)
			{
				continue;
			}

			parts.Add($"{GetResourceMarker(entry.Key)} {entry.Value}");
		}

		return parts.Count == 0 ? "-" : string.Join(", ", parts);
	}

	private void RefreshInventory()
	{
		if (_inventoryGrid == null)
		{
			return;
		}

		foreach (Node child in _inventoryGrid.GetChildren())
		{
			child.QueueFree();
		}

		foreach (BaseResourceType resourceType in BaseBuildManager.GetAllResourceTypes())
		{
			AddInventoryEntry(BaseBuildManager.GetResourceMarker(resourceType), _buildManager?.GetTotalStoredAmount(resourceType) ?? 0);
		}
	}

	private void AddInventoryEntry(string marker, int amount)
	{
		if (_inventoryGrid == null || amount <= 0)
		{
			return;
		}

		Label markerLabel = new()
		{
			Text = marker,
			CustomMinimumSize = new Vector2(48.0f, 22.0f),
			HorizontalAlignment = HorizontalAlignment.Left,
			MouseFilter = MouseFilterEnum.Ignore
		};
		markerLabel.AddThemeFontSizeOverride("font_size", 15);
		markerLabel.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.0f, 0.0f, 0.72f));
		markerLabel.AddThemeConstantOverride("outline_size", 2);
		_inventoryGrid.AddChild(markerLabel);

		Label amountLabel = new()
		{
			Text = amount.ToString(),
			CustomMinimumSize = new Vector2(76.0f, 22.0f),
			HorizontalAlignment = HorizontalAlignment.Right,
			MouseFilter = MouseFilterEnum.Ignore
		};
		amountLabel.AddThemeFontSizeOverride("font_size", 15);
		amountLabel.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.0f, 0.0f, 0.72f));
		amountLabel.AddThemeConstantOverride("outline_size", 2);
		_inventoryGrid.AddChild(amountLabel);
	}

	private BuildPanelItem? GetSelectedItem()
	{
		if (_selectedBuildType == TileBuildType.None)
		{
			return null;
		}

		if (_itemsByCategory.TryGetValue(_activeCategory, out List<BuildPanelItem>? activeItems))
		{
			foreach (BuildPanelItem item in activeItems)
			{
				if (item.BuildType == _selectedBuildType)
				{
					return item;
				}
			}
		}

		foreach (List<BuildPanelItem> items in _itemsByCategory.Values)
		{
			foreach (BuildPanelItem item in items)
			{
				if (item.BuildType == _selectedBuildType)
				{
					return item;
				}
			}
		}

		return null;
	}

	private RoomPanelItem? GetSelectedRoomItem()
	{
		if (_selectedRoomRemoveMode)
		{
			return new RoomPanelItem(RoomDesignationCategory, RoomType.None, RoomDeleteName, true);
		}

		if (_selectedRoomType == RoomType.None)
		{
			return null;
		}

		return new RoomPanelItem(RoomDesignationCategory, _selectedRoomType, BaseRoomManager.GetRoomDisplayName(_selectedRoomType), false);
	}

	private static string GetResourceMarker(BaseResourceType resourceType)
	{
		return BaseBuildManager.GetResourceMarker(resourceType);
	}

	private void UpdateMainTabButtonStyles()
	{
		foreach (Button button in _mainTabButtons)
		{
			bool selected = button.Text == _activeMainTab;
			ApplyButtonStyle(button, selected ? ButtonTone.SelectedMain : ButtonTone.Main);
		}
	}

	private void UpdateCategoryButtonStyles()
	{
		foreach (KeyValuePair<string, Button> entry in _categoryButtons)
		{
			bool selected = _activePanelMode == PanelMode.StorageOverview && entry.Key == StorageOverviewCategory
				? !_selectedStorageCell.HasValue
				: entry.Key == _activeCategory;
			ApplyButtonStyle(entry.Value, selected ? ButtonTone.SelectedCategory : ButtonTone.Category);
		}
	}

	private void UpdateItemButtonStyles()
	{
		foreach (KeyValuePair<TileBuildType, Button> entry in _itemButtons)
		{
			ApplyButtonStyle(entry.Value, entry.Key == _selectedBuildType ? ButtonTone.SelectedItem : ButtonTone.Item);
		}
	}

	private void UpdateMaterialButtonStyles()
	{
		foreach (KeyValuePair<BuildMaterialType, Button> entry in _materialButtons)
		{
			ApplyButtonStyle(entry.Value, entry.Key == _selectedBuildMaterialType ? ButtonTone.SelectedItem : ButtonTone.Item);
		}
	}

	private void UpdateRoomButtonStyles()
	{
		foreach (KeyValuePair<string, Button> entry in _roomButtons)
		{
			string selectedKey = _selectedRoomRemoveMode ? RoomPanelItem.DeleteKey : _selectedRoomType.ToString();
			ApplyButtonStyle(entry.Value, entry.Key == selectedKey ? ButtonTone.SelectedItem : ButtonTone.Item);
		}
	}

	private void UpdateMercenaryButtonStyles()
	{
		ulong selectedId = _selectedMercenary != null && GodotObject.IsInstanceValid(_selectedMercenary)
			? _selectedMercenary.GetInstanceId()
			: 0;

		foreach (KeyValuePair<ulong, Button> entry in _mercenaryButtons)
		{
			ApplyButtonStyle(entry.Value, entry.Key == selectedId ? ButtonTone.SelectedItem : ButtonTone.Item);
		}
	}

	private void UpdateStorageButtonStyles()
	{
		foreach (KeyValuePair<Vector2I, Button> entry in _storageButtons)
		{
			ApplyButtonStyle(entry.Value, _selectedStorageCell.HasValue && entry.Key == _selectedStorageCell.Value ? ButtonTone.SelectedItem : ButtonTone.Item);
		}
	}

	private static PanelContainer CreatePanel(Color fillColor, Color borderColor, int cornerRadius)
	{
		PanelContainer panel = new();
		StyleBoxFlat style = new()
		{
			BgColor = fillColor,
			BorderColor = borderColor,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = cornerRadius,
			CornerRadiusTopRight = cornerRadius,
			CornerRadiusBottomLeft = cornerRadius,
			CornerRadiusBottomRight = cornerRadius,
			ContentMarginLeft = 10.0f,
			ContentMarginTop = 10.0f,
			ContentMarginRight = 10.0f,
			ContentMarginBottom = 10.0f
		};
		panel.AddThemeStyleboxOverride("panel", style);
		return panel;
	}

	private static Label CreateSmallLabel(string text, int fontSize)
	{
		Label label = new()
		{
			Text = text,
			MouseFilter = MouseFilterEnum.Ignore,
			VerticalAlignment = VerticalAlignment.Top,
			HorizontalAlignment = HorizontalAlignment.Left,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		return label;
	}

	private static Label CreateStorageDetailLabel(string text, int fontSize)
	{
		Label label = new()
		{
			Text = text,
			CustomMinimumSize = new Vector2(220.0f, 0.0f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
			VerticalAlignment = VerticalAlignment.Top,
			HorizontalAlignment = HorizontalAlignment.Left,
			AutowrapMode = TextServer.AutowrapMode.Off,
			ClipText = true,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		return label;
	}

	private static void PrepareSingleLineButton(Button button)
	{
		button.AutowrapMode = TextServer.AutowrapMode.Off;
		button.ClipText = true;
		button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		button.SizeFlagsHorizontal = SizeFlags.Fill;
	}

	private static Button CreateButton(string text, Vector2 minimumSize)
	{
		Button button = new()
		{
			Text = text,
			CustomMinimumSize = minimumSize,
			FocusMode = FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop
		};
		button.AddThemeFontSizeOverride("font_size", 18);
		ApplyButtonStyle(button, ButtonTone.Item);
		return button;
	}

	private static void ApplyButtonStyle(Button button, ButtonTone tone)
	{
		Color fillColor = tone switch
		{
			ButtonTone.Main => new Color(0.18f, 0.18f, 0.18f, 0.92f),
			ButtonTone.SelectedMain => new Color(0.52f, 0.35f, 0.16f, 0.98f),
			ButtonTone.Category => new Color(0.50f, 0.26f, 0.02f, 0.94f),
			ButtonTone.SelectedCategory => new Color(0.78f, 0.44f, 0.06f, 0.98f),
			ButtonTone.SelectedItem => new Color(0.56f, 0.56f, 0.56f, 0.98f),
			_ => new Color(0.32f, 0.32f, 0.32f, 0.94f)
		};
		Color borderColor = tone is ButtonTone.SelectedMain or ButtonTone.SelectedCategory or ButtonTone.SelectedItem
			? new Color(1.0f, 0.82f, 0.42f, 0.85f)
			: new Color(0.08f, 0.08f, 0.08f, 0.55f);
		StyleBoxFlat normal = new()
		{
			BgColor = fillColor,
			BorderColor = borderColor,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4
		};
		StyleBoxFlat hover = (StyleBoxFlat)normal.Duplicate();
		hover.BgColor = fillColor.Lightened(0.12f);
		StyleBoxFlat pressed = (StyleBoxFlat)normal.Duplicate();
		pressed.BgColor = fillColor.Darkened(0.16f);
		button.AddThemeStyleboxOverride("normal", normal);
		button.AddThemeStyleboxOverride("hover", hover);
		button.AddThemeStyleboxOverride("pressed", pressed);
	}

	private readonly struct BuildPanelItem
	{
		public BuildPanelItem(string category, TileBuildType buildType, string displayName, string description, Vector2I size)
		{
			Category = category;
			BuildType = buildType;
			DisplayName = displayName;
			Description = description;
			Size = size;
		}

		public string Category { get; }
		public TileBuildType BuildType { get; }
		public string DisplayName { get; }
		public string Description { get; }
		public Vector2I Size { get; }
	}

	private readonly struct RoomPanelItem
	{
		public const string DeleteKey = "__delete_room";

		public RoomPanelItem(string category, RoomType roomType, string displayName, bool isRemoveMode)
		{
			Category = category;
			RoomType = roomType;
			DisplayName = displayName;
			IsRemoveMode = isRemoveMode;
		}

		public string Category { get; }
		public RoomType RoomType { get; }
		public string DisplayName { get; }
		public bool IsRemoveMode { get; }
		public string ButtonKey => IsRemoveMode ? DeleteKey : RoomType.ToString();
	}

	private enum PanelMode
	{
		None,
		Build,
		BaseManagement,
		MercenaryManagement,
		StorageOverview
	}

	private enum ButtonTone
	{
		Main,
		SelectedMain,
		Category,
		SelectedCategory,
		Item,
		SelectedItem
	}
}
