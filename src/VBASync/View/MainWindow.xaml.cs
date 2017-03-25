﻿using Ookii.Dialogs.Wpf;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using VBASync.Localization;
using VBASync.Model;

namespace VBASync.WPF {
    internal partial class MainWindow {
        private readonly MainViewModel _vm;

        private bool _doUpdateIncludeAll = true;
        private VbaFolder _evf;

        public MainWindow(MainViewModel vm) {
            InitializeComponent();
            DataContext = _vm = vm;
            DataContextChanged += (s, e) => QuietRefreshIfInputsOk();
            ((INotifyPropertyChanged)DataContext).PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "Action" || e.PropertyName == "FilePath" || e.PropertyName == "FolderPath")
                {
                    QuietRefreshIfInputsOk();
                }
            };
            QuietRefreshIfInputsOk();
        }

        private ISession Session => (ISession)DataContext;

        private void ApplyButton_Click(object sender, RoutedEventArgs e) {
            var vm = ChangesGrid.DataContext as ChangesViewModel;
            if (vm == null) {
                return;
            }
            if (Session.Action == ActionType.Extract) {
                foreach (var p in vm.Where(p => p.Commit).ToArray()) {
                    var fileName = p.ModuleName + ModuleProcessing.ExtensionFromType(p.ModuleType);
                    switch (p.ChangeType) {
                    case ChangeType.DeleteFile:
                        File.Delete(Path.Combine(Session.FolderPath, fileName));
                        if (p.ModuleType == ModuleType.Form) {
                            File.Delete(Path.Combine(Session.FolderPath, p.ModuleName + ".frx"));
                        }
                        break;
                    case ChangeType.ChangeFormControls:
                        File.Copy(Path.Combine(_evf.FolderPath, p.ModuleName + ".frx"), Path.Combine(Session.FolderPath, p.ModuleName + ".frx"), true);
                        break;
                    default:
                        File.Copy(Path.Combine(_evf.FolderPath, fileName), Path.Combine(Session.FolderPath, fileName), true);
                        if (p.ChangeType == ChangeType.AddFile && p.ModuleType == ModuleType.Form) {
                            File.Copy(Path.Combine(_evf.FolderPath, p.ModuleName + ".frx"), Path.Combine(Session.FolderPath, p.ModuleName + ".frx"), true);
                        }
                        break;
                    }
                    vm.Remove(p);
                }
            } else {
                foreach (var p in vm.Where(p => p.Commit).ToArray()) {
                    var fileName = p.ModuleName + ModuleProcessing.ExtensionFromType(p.ModuleType);
                    switch (p.ChangeType) {
                    case ChangeType.DeleteFile:
                        File.Delete(Path.Combine(_evf.FolderPath, fileName));
                        if (p.ModuleType == ModuleType.Form) {
                            File.Delete(Path.Combine(_evf.FolderPath, p.ModuleName + ".frx"));
                        }
                        break;
                    case ChangeType.ChangeFormControls:
                        File.Copy(Path.Combine(Session.FolderPath, p.ModuleName + ".frx"), Path.Combine(_evf.FolderPath, p.ModuleName + ".frx"), true);
                        break;
                    default:
                        File.Copy(Path.Combine(Session.FolderPath, fileName), Path.Combine(_evf.FolderPath, fileName), true);
                        if (p.ChangeType == ChangeType.AddFile && p.ModuleType == ModuleType.Form) {
                            File.Copy(Path.Combine(Session.FolderPath, p.ModuleName + ".frx"), Path.Combine(_evf.FolderPath, p.ModuleName + ".frx"), true);
                        }
                        break;
                    }
                    vm.Remove(p);
                }
                _evf.Write(Session.FilePath);
            }
            UpdateIncludeAllBox();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) {
            Application.Current.Shutdown();
        }

        private void ChangesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
            var sel = (Patch)ChangesGrid.SelectedItem;
            var fileName = sel.ModuleName + (sel.ChangeType != ChangeType.ChangeFormControls ? ModuleProcessing.ExtensionFromType(sel.ModuleType) : ".frx");
            string oldPath;
            string newPath;
            if (Session.Action == ActionType.Extract) {
                oldPath = Path.Combine(Session.FolderPath, fileName);
                newPath = Path.Combine(_evf.FolderPath, fileName);
            } else {
                oldPath = Path.Combine(_evf.FolderPath, fileName);
                newPath = Path.Combine(Session.FolderPath, fileName);
            }
            if (sel.ChangeType == ChangeType.ChangeFormControls) {
                Lib.FrxFilesAreDifferent(oldPath, newPath, out var explain);
                MessageBox.Show(explain, "FRX file difference", MessageBoxButton.OK, MessageBoxImage.Information);
            } else {
                var diffExePath = Environment.ExpandEnvironmentVariables(Session.DiffTool);
                if (!File.Exists(oldPath) || !File.Exists(newPath) || !File.Exists(diffExePath)) {
                    return;
                }
                var p = new Process {
                    StartInfo = new ProcessStartInfo(diffExePath, Session.DiffToolParameters.Replace("{OldFile}", oldPath).Replace("{NewFile}", newPath)) {
                        UseShellExecute = false
                    }
                };
                p.Start();
            }
        }

        private void ExitMenu_Click(object sender, RoutedEventArgs e) {
            CancelButton_Click(null, null);
        }

        private void FileBrowseBox_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                OkButton.Focus();
            }
        }

        private void FileBrowseButton_Click(object sender, RoutedEventArgs e) {
            var dlg = new VistaOpenFileDialog {
                Filter = $"{VBASyncResources.MWOpenAllFiles}|*.*|"
                    + $"{VBASyncResources.MWOpenAllSupported}|*.doc;*.dot;*.xls;*.xlt;*.docm;*.dotm;*.docb;*.xlsm;*.xla;*.xlam;*.xlsb;"
                    + "*.pptm;*.potm;*.ppam;*.ppsm;*.sldm;*.docx;*.dotx;*.xlsx;*.xltx;*.pptx;*.potx;*.ppsx;*.sldx;*.otm;*.bin|"
                    + $"{VBASyncResources.MWOpenWord97}|*.doc;*.dot|"
                    + $"{VBASyncResources.MWOpenExcel97}|*.xls;*.xlt;*.xla|"
                    + $"{VBASyncResources.MWOpenWord07}|*.docx;*.docm;*.dotx;*.dotm;*.docb|"
                    + $"{VBASyncResources.MWOpenExcel07}|*.xlsx;*.xlsm;*.xltx;*.xltm;*.xlsb;*.xlam|"
                    + $"{VBASyncResources.MWOpenPpt07}|*.pptx;*.pptm;*.potx;*.potm;*.ppam;*.ppsx;*.ppsm;*.sldx;*.sldm|"
                    + $"{VBASyncResources.MWOpenOutlook}|*.otm|"
                    + $"{VBASyncResources.MWOpenSAlone}|*.bin",
                FilterIndex = 2
            };
            if (dlg.ShowDialog() == true) {
                Session.FilePath = dlg.FileName;
            }
        }

        private void FolderBrowseBox_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                OkButton.Focus();
            }
        }

        private void FolderBrowseButton_Click(object sender, RoutedEventArgs e) {
            var dlg = new VistaFolderBrowserDialog();
            if (dlg.ShowDialog() == true) {
                Session.FolderPath = dlg.SelectedPath;
            }
        }

        private void IncludeAllBox_Click(object sender, RoutedEventArgs e) {
            var vm = ChangesGrid.DataContext as ChangesViewModel;
            if (vm == null || IncludeAllBox.IsChecked == null) {
                return;
            }
            try {
                _doUpdateIncludeAll = false;
                foreach (var ch in vm) {
                    ch.Commit = IncludeAllBox.IsChecked.Value;
                }
            } finally {
                _doUpdateIncludeAll = true;
            }
            ChangesGrid.Items.Refresh();
        }

        private void LoadIni(AppIniFile ini) {
            Session.Action = ini.GetActionType("General", "ActionType") ?? ActionType.Extract;
            Session.FolderPath = ini.GetString("General", "FolderPath");
            Session.FilePath = ini.GetString("General", "FilePath");
            Session.Language = ini.GetString("General", "Language");
            Session.DiffTool = ini.GetString("DiffTool", "Path");
            Session.DiffToolParameters = ini.GetString("DiffTool", "Parameters") ?? "\"{OldFile}\" \"{NewFile}\"";
        }

        private void LoadLastMenu_Click(object sender, RoutedEventArgs e) {
            LoadIni(new AppIniFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VBA Sync Tool", "LastSession.ini")));
        }

        private void LoadSessionMenu_Click(object sender, RoutedEventArgs e) {
            var dlg = new VistaOpenFileDialog {
                Filter = $"{VBASyncResources.MWOpenAllFiles}|*.*|"
                    + $"{VBASyncResources.MWOpenSession}|*.ini",
                FilterIndex = 2
            };
            if (dlg.ShowDialog() == true) {
                LoadIni(new AppIniFile(dlg.FileName, Encoding.UTF8));
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e) {
            ApplyButton_Click(null, null);
            Application.Current.Shutdown();
        }

        private void QuietRefreshIfInputsOk() {
            if (!File.Exists(Session.FilePath) || !Directory.Exists(Session.FolderPath)) {
                return;
            }
            try {
                RefreshButton_Click(null, null);
            } catch {
                ChangesGrid.DataContext = null;
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e) {
            if (string.IsNullOrEmpty(Session.FolderPath) || string.IsNullOrEmpty(Session.FilePath)) {
                return;
            }
            var folderModules = Lib.GetFolderModules(Session.FolderPath);
            _evf?.Dispose();
            _evf = new VbaFolder();
            _evf.Read(Session.FilePath, folderModules.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Item1));
            var changes = Lib.GetModulePatches(Session, _evf.FolderPath, folderModules, _evf.ModuleTexts.ToList()).ToList();
            var projChange = Lib.GetProjectPatch(Session, _evf.FolderPath);
            if (projChange != null) {
                changes.Add(projChange);
            }
            var cvm = new ChangesViewModel(changes);
            ChangesGrid.DataContext = cvm;
            foreach (var ch in cvm) {
                ch.CommitChanged += (s2, e2) => UpdateIncludeAllBox();
            }
            UpdateIncludeAllBox();
        }

        private void SaveSession(Stream st) {
            var actionTypeText = Session.Action == ActionType.Extract ? "Extract" : "Publish";
            var nl = Environment.NewLine;
            var buf = Encoding.UTF8.GetBytes($"ActionType={actionTypeText}{nl}"
                                             + $"FolderPath=\"{Session.FolderPath}\"{nl}"
                                             + $"FilePath=\"{Session.FilePath}\"{nl}"
                                             + $"Language=\"{Session.Language}\"{nl}{nl}"
                                             + $"[DiffTool]{nl}"
                                             + $"Path=\"{Session.DiffTool}\"{nl}"
                                             + $"Parameters=\"{Session.DiffToolParameters}\"{nl}");
            st.Write(buf, 0, buf.Length);
        }

        private void SaveSessionMenu_Click(object sender, RoutedEventArgs e) {
            var dlg = new VistaSaveFileDialog {
                Filter = $"{VBASyncResources.MWOpenAllFiles}|*.*|"
                    + $"{VBASyncResources.MWOpenSession}|*.ini",
                FilterIndex = 2
            };
            if (dlg.ShowDialog() == true) {
                var path = dlg.FileName;
                if (!Path.HasExtension(path)) {
                    path += ".ini";
                }
                using (var fs = new FileStream(path, FileMode.Create)) {
                    SaveSession(fs);
                }
            }
        }

        internal void SettingsMenu_Click(object sender, RoutedEventArgs e) {
            new SettingsWindow(Session, s => DataContext = s).ShowDialog();
        }

        private void UpdateIncludeAllBox() {
            if (!_doUpdateIncludeAll) {
                return;
            }
            var vm = (ChangesViewModel)ChangesGrid.DataContext;
            if (vm.All(p => p.Commit)) {
                IncludeAllBox.IsChecked = true;
            } else if (vm.All(p => !p.Commit)) {
                IncludeAllBox.IsChecked = false;
            } else {
                IncludeAllBox.IsChecked = null;
            }
        }

        private void Window_Closed(object sender, EventArgs e) {
            _evf?.Dispose();

            var lastSessionPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VBA Sync Tool", "LastSession.ini");
            Directory.CreateDirectory(Path.GetDirectoryName(lastSessionPath));
            using (var st = new FileStream(lastSessionPath, FileMode.Create))
            {
                SaveSession(st);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            QuietRefreshIfInputsOk();

            if (!string.IsNullOrEmpty(_vm.FolderPath) && !string.IsNullOrEmpty(_vm.FilePath)) {
                RefreshButton_Click(null, null);
                if (_vm.AutoRun) {
                    OkButton_Click(null, null);
                }
            }
        }
    }
}
