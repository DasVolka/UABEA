using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Threading;
using System.ComponentModel;

namespace UABEAvalonia
{
    public partial class GameObjectViewWindow : Window
    {
        private InfoWindow win;
        private AssetWorkspace workspace;

        private bool ignoreDropdownEvent;
        private AssetContainer? selectedGo;
        private TreeViewItem? selectedTreeItem;
        private string searchText = "";
        private System.Timers.Timer? _searchDebounceTimer;
        private Dictionary<long, string> _gameObjectNameCache = new();

        public GameObjectViewWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated events
            gameObjectTreeView.SelectionChanged += GameObjectTreeView_SelectionChanged;
            gameObjectTreeView.DoubleTapped += GameObjectTreeView_DoubleTapped;
            cbxFiles.SelectionChanged += CbxFiles_SelectionChanged;
            // Initialize the checkbox
            chkSortAlphabetically.IsCheckedChanged += ChkSortAlphabetically_CheckedChanged;
            searchBox.TextChanged += SearchBox_TextChanged;
        }

        private void ChkSortAlphabetically_CheckedChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            PopulateHierarchyTreeView();
        }

        private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_searchDebounceTimer == null)
            {
                _searchDebounceTimer = new System.Timers.Timer(300); // 300ms delay
                _searchDebounceTimer.Elapsed += (s, e) =>
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        searchText = searchBox.Text?.ToLowerInvariant() ?? "";
                        PopulateHierarchyTreeView();
                    });
                };
                _searchDebounceTimer.AutoReset = false;
            }

            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }


        public GameObjectViewWindow(InfoWindow win, AssetWorkspace workspace) : this()
        {
            this.win = win;
            this.workspace = workspace;

            ignoreDropdownEvent = true;

            componentTreeView.Init(win, workspace);
            PopulateFilesComboBox();
            PopulateHierarchyTreeView();
        }

        public GameObjectViewWindow(InfoWindow win, AssetWorkspace workspace, AssetContainer selectedGo) : this()
        {
            this.win = win;
            this.workspace = workspace;
            this.selectedGo = selectedGo;

            ignoreDropdownEvent = true;

            componentTreeView.Init(win, workspace);
            PopulateFilesComboBox();
            PopulateHierarchyTreeView();

            if (selectedTreeItem != null)
            {
                TreeViewItem curItem = selectedTreeItem;
                while (curItem.Parent is TreeViewItem)
                {
                    curItem = (TreeViewItem)curItem.Parent;
                    curItem.IsExpanded = true;
                }
                gameObjectTreeView.SelectedItem = selectedTreeItem;
            }
        }

        private void GameObjectTreeView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0)
                return;

            object? selectedItemObj = e.AddedItems[0];
            if (selectedItemObj == null)
                return;

            TreeViewItem selectedItem = (TreeViewItem)selectedItemObj;
            if (selectedItem.Tag == null)
                return;

            AssetContainer gameObjectCont = (AssetContainer)selectedItem.Tag;
            AssetTypeValueField gameObjectBf = workspace.GetBaseField(gameObjectCont);
            AssetTypeValueField components = gameObjectBf["m_Component"]["Array"];

            componentTreeView.Reset();

            foreach (AssetTypeValueField data in components)
            {
                AssetTypeValueField component = data[data.Children.Count - 1];
                AssetContainer componentCont = workspace.GetAssetContainer(gameObjectCont.FileInstance, component, false);
                componentTreeView.LoadComponent(componentCont);
            }
        }

        private void GameObjectTreeView_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            if (gameObjectTreeView.SelectedItem != null)
            {
                TreeViewItem item = (TreeViewItem)gameObjectTreeView.SelectedItem;
                item.IsExpanded = !item.IsExpanded;
            }
        }

        private void CbxFiles_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // this event happens after the constructor
            // is called, so this is the only way to do it
            if (ignoreDropdownEvent)
            {
                ignoreDropdownEvent = false;
                return;
            }

            PopulateHierarchyTreeView();
        }

        private void PopulateFilesComboBox()
        {
            foreach (AssetsFileInstance fileInstance in workspace.LoadedFiles)
            {
                ComboBoxItem comboItem = new ComboBoxItem()
                {
                    Content = fileInstance.name,
                    Tag = fileInstance
                };
                cbxFiles.Items?.Add(comboItem);
            }
            cbxFiles.SelectedIndex = 0;
        }

        private void PopulateHierarchyTreeView()
        {
            ComboBoxItem? selectedComboItem = (ComboBoxItem?)cbxFiles.SelectedItem;
            if (selectedComboItem == null)
                return;

            AssetsFileInstance? fileInstance = (AssetsFileInstance?)selectedComboItem.Tag;
            if (fileInstance == null)
                return;

            // clear treeview
            gameObjectTreeView.Items.Clear();

            List<AssetContainer> rootTransforms = new List<AssetContainer>();

            foreach (var asset in workspace.LoadedAssets)
            {
                AssetContainer assetCont = asset.Value;

                AssetClassID assetType = (AssetClassID)assetCont.ClassId;
                bool isTransformType = assetType == AssetClassID.Transform || assetType == AssetClassID.RectTransform;

                if (assetCont.FileInstance == fileInstance && isTransformType)
                {
                    AssetTypeValueField transformBf = workspace.GetBaseField(assetCont);
                    AssetTypeValueField transformFatherBf = transformBf["m_Father"];
                    long pathId = transformFatherBf["m_PathID"].AsLong;
                    // is root GameObject
                    if (pathId == 0)
                    {
                        rootTransforms.Add(assetCont);
                    }
                }
            }

            // Sort root transforms if the checkbox is checked
            if (chkSortAlphabetically.IsChecked == true)
            {
                rootTransforms.Sort((a, b) => {
                    var aName = GetGameObjectName(a);
                    var bName = GetGameObjectName(b);
                    return string.Compare(aName, bName, System.StringComparison.OrdinalIgnoreCase);
                });
            }

            foreach (var rootTransform in rootTransforms)
            {
                AssetTypeValueField transformBf = workspace.GetBaseField(rootTransform);
                LoadGameObjectTreeItem(rootTransform, transformBf, null);
            }
        }


        private void LoadGameObjectTreeItem(AssetContainer transformCont, AssetTypeValueField transformBf, TreeViewItem? parentTreeItem)
        {
            AssetTypeValueField gameObjectRef = transformBf["m_GameObject"];
            AssetContainer gameObjectCont = workspace.GetAssetContainer(transformCont.FileInstance, gameObjectRef, false);

            if (gameObjectCont == null)
                return;

            AssetTypeValueField gameObjectBf = workspace.GetBaseField(gameObjectCont);
            string name = gameObjectBf["m_Name"].AsString;

            // Create current tree item
            TreeViewItem currentItem = new TreeViewItem();
            currentItem.Header = name;
            currentItem.Tag = gameObjectCont;

            // Check if this is a root node
            bool isRoot = parentTreeItem == null;

            // Only evaluate search for root nodes
            if (isRoot && !string.IsNullOrWhiteSpace(searchText))
            {
                bool matchesSearch = name.ToLowerInvariant().Contains(searchText);
                if (!matchesSearch)
                    return;
            }

            // Get all child transforms
            List<AssetContainer> childTransforms = new List<AssetContainer>();

            AssetTypeValueField children = transformBf["m_Children"]["Array"];
            foreach (AssetTypeValueField child in children)
            {
                AssetContainer childTransformCont = workspace.GetAssetContainer(transformCont.FileInstance, child, false);

                childTransforms.Add(childTransformCont);
            }

            // Sort child transforms if the checkbox is checked
            if (chkSortAlphabetically.IsChecked == true)
            {
                childTransforms.Sort((a, b) => {
                    var aName = GetGameObjectName(a);
                    var bName = GetGameObjectName(b);
                    return string.Compare(aName, bName, System.StringComparison.OrdinalIgnoreCase);
                });
            }


            foreach (var childTransformCont in childTransforms)
            {
                AssetTypeValueField childTransformBf = workspace.GetBaseField(childTransformCont);
                LoadGameObjectTreeItem(childTransformCont, childTransformBf, currentItem);
            }

            // If this is the selected GameObject, update the reference
            if (selectedGo != null &&
                gameObjectCont.FileInstance == selectedGo.FileInstance &&
                gameObjectCont.PathId == selectedGo.PathId)
            {
                selectedTreeItem = currentItem;
            }

            // Add to tree
            if (isRoot)
            {
                gameObjectTreeView.Items?.Add(currentItem);
            }
            else
            {
                parentTreeItem?.Items?.Add(currentItem);
            }
        }

        private string GetGameObjectName(AssetContainer transformCont)
        {
            AssetTypeValueField transformBf = workspace.GetBaseField(transformCont);
            AssetTypeValueField gameObjectRef = transformBf["m_GameObject"];
            long pathId = gameObjectRef["m_PathID"].AsLong;

            if (_gameObjectNameCache.TryGetValue(pathId, out string? cachedName))
            {
                return cachedName;
            }

            AssetContainer gameObjectCont = workspace.GetAssetContainer(transformCont.FileInstance, gameObjectRef, false);
            if (gameObjectCont == null)
                return string.Empty;

            AssetTypeValueField gameObjectBf = workspace.GetBaseField(gameObjectCont);
            string name = gameObjectBf["m_Name"].AsString;
            _gameObjectNameCache[pathId] = name;
            return name;
        }
    }
}
