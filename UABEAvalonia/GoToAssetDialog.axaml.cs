using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Collections.Generic;
using System.IO;

namespace UABEAvalonia
{
    public partial class GoToAssetDialog : Window
    {
        //controls
        private ComboBox ddFileId;
        private TextBox boxPathId;
        private Button btnOk;
        private Button btnCancel;

        private AssetWorkspace workspace;

        public GoToAssetDialog()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            //generated controls
            ddFileId = this.FindControl<ComboBox>("ddFileId");
            boxPathId = this.FindControl<TextBox>("boxPathId");
            btnOk = this.FindControl<Button>("btnOk");
            btnCancel = this.FindControl<Button>("btnCancel");
            //generated events
            btnOk.Click += BtnOk_Click;
            btnCancel.Click += BtnCancel_Click;
        }

        public GoToAssetDialog(AssetWorkspace workspace) : this()
        {
            this.workspace = workspace;

            int index = 0;
            List<string> loadedFiles = new List<string>();
            foreach (AssetsFileInstance inst in workspace.LoadedFiles)
            {
                loadedFiles.Add($"{index++} - {Path.GetFileName(inst.path)}");
            }
            ddFileId.Items = loadedFiles;
            boxPathId.Text = "1"; //todo get last id (including new assets)
        }

        private async void BtnOk_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            int fileId = ddFileId.SelectedIndex; //hopefully in order
            string pathIdText = boxPathId.Text;

            if (fileId < 0)
            {
                await MessageBoxUtil.ShowDialog(this, "Bad input", "File was invalid.");
                return;
            }

            if (!long.TryParse(pathIdText, out long pathId))
            {
                await MessageBoxUtil.ShowDialog(this, "Bad input", "Path ID was invalid.");
                return;
            }

            AssetPPtr pptr = new AssetPPtr(fileId, pathId);

            Close(pptr);
        }

        private void BtnCancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close(false);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}