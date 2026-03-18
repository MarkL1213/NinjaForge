using System.Windows;
using System.Windows.Controls;

namespace NinjaForge
{
    public partial class MainWindow : Window
    {
        WorkspaceFile? _workspaceFile = null;
        NinjaTraderCleaner _cleaner;
        App? _application;

        public MainWindow()
        {
            _cleaner = new NinjaTraderCleaner(new NinjaTraderInstallSettings().DocumentsDirectory);

            _application = Application.Current as App;
            if (_application == null)
            {
                MessageBox.Show("Application is null");
                Application.Current.Shutdown(1);
                return;
            }

            _workspaceFile = _application.WorkspaceFile;
            if (_workspaceFile == null)
            {
                MessageBox.Show("Workspace file is null");
                Application.Current.Shutdown(2);
                return;
            }

            InitializeComponent();
            SafeModeCheckBox.IsChecked = _application.SafeMode;
            SafeModeCheckBox.Checked += SafeModeCheckBox_Checked;
            SafeModeCheckBox.Unchecked += SafeModeCheckBox_Unchecked;

            List<StartupWorkspace> validWorkspaces = _workspaceFile.DetectWorkspaces();
            string currentWorkspace = _workspaceFile.LookupCurrentWorkspace();
            int n = 0;
            foreach (StartupWorkspace workspace in validWorkspaces)
            {
                RadioButton rb = new RadioButton();
                rb.Name = workspace.FrameworkSafeName() + "_RadioButton";
                rb.Content = workspace.WorkspaceName;
                rb.Tag = workspace;
                rb.GroupName = "Workspaces";
                if (currentWorkspace ==  workspace.WorkspaceName) rb.IsChecked = true;
                rb.Checked += RadioButton_Checked;
                Thickness t = new Thickness();
                t.Left = 27;
                rb.Margin = t;
                RowDefinition rd = new RowDefinition();
                rd.Height = GridLength.Auto;
                LauncherControlGrid.RowDefinitions.Add(rd);

                LauncherControlGrid.Children.Add(rb);
                Grid.SetRow(rb, n++);
            }

            OnNinjaTraderExited();//set action button states
        }

        private void SafeModeCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _application!.SafeMode = false;
        }

        private void SafeModeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _application!.SafeMode = true;
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if(_workspaceFile == null) return;

            if (_workspaceFile.LaunchNinjaTrader(_application!.SafeMode))
            {
                Application.Current.Shutdown();
            }
        }

        private void LaunchAndCleanButton_Click(object sender, RoutedEventArgs e)
        {
            if (_workspaceFile == null) return;

            performCleanup();

            if (_workspaceFile.LaunchNinjaTrader(_application!.SafeMode))
            {
                Application.Current.Shutdown();
            }
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton? radioButton = sender as RadioButton;
            StartupWorkspace? workspace = radioButton?.Tag as StartupWorkspace;
            if (workspace == null)
            {
                MessageBox.Show("Error: Workspace radio button has no StartupWorkspace tag associated.");
                return;
            }

            string? result = _workspaceFile?.SetStartupWorkspace(workspace);
            if (!string.IsNullOrEmpty(result))
            {
                MessageBox.Show($"Error: Setting startup workspace to \"{workspace.WorkspaceName}\" failed: {result}");
            }
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void cleanAllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            bool isAllChecked = cleanAllCheckBox.IsChecked == null ? false : ((bool)cleanAllCheckBox.IsChecked);

            cleanLogCheckBox.IsEnabled = !isAllChecked;
            cleanTraceCheckBox.IsEnabled = !isAllChecked;
            cleanCacheCheckBox.IsEnabled = !isAllChecked;
            cleanDBCheckBox.IsEnabled = !isAllChecked;
            cleanAnalyzerLogsCheckBox.IsEnabled = !isAllChecked;
        }

        private void cleanButton_Click(object sender, RoutedEventArgs e)
        {
            performCleanup();
        }

        private void performCleanup()
        { 
            bool isAllChecked = cleanAllCheckBox.IsChecked == null ? false : ((bool)cleanAllCheckBox.IsChecked);

            if (isAllChecked)
            {
                string youHaveSelected = "WARNING ALL SELECTED DATA WILL BE DELETED\r\n-------------------\r\nYou have selected:";
                youHaveSelected += "\r\nClean ALL.";
                MessageBoxResult res = MessageBox.Show(youHaveSelected, "Confirm Cleanup Steps", MessageBoxButton.OKCancel);
                if (res.Equals(MessageBoxResult.Cancel))
                    return;


                if (!_cleaner.CleanAll())
                {
                    MessageBox.Show(_cleaner.Error, "Clean Error");
                    return;
                }
            }
            else
            {
                bool isCacheChecked = cleanCacheCheckBox.IsChecked == null ? false : ((bool)cleanCacheCheckBox.IsChecked);
                bool isLogChecked = cleanLogCheckBox.IsChecked == null ? false : ((bool)cleanLogCheckBox.IsChecked);
                bool isTraceChecked = cleanTraceCheckBox.IsChecked == null ? false : ((bool)cleanTraceCheckBox.IsChecked);
                bool isDBChecked = cleanDBCheckBox.IsChecked == null ? false : ((bool)cleanDBCheckBox.IsChecked);
                bool isAnalyzerLogsChecked = cleanAnalyzerLogsCheckBox.IsChecked == null ? false : ((bool)cleanAnalyzerLogsCheckBox.IsChecked);

                string youHaveSelected = "WARNING ALL SELECTED DATA WILL BE DELETED\r\n-------------------\r\nYou have selected:";
                if (isCacheChecked) youHaveSelected += "\r\n    Reflection cache cleanup.";
                if (isLogChecked) youHaveSelected += "\r\n    Log cleanup.";
                if (isTraceChecked) youHaveSelected += "\r\n    Trace cleanup.";
                if (isDBChecked) youHaveSelected += "\r\n    Full price DB cleanup.";
                if (isAnalyzerLogsChecked) youHaveSelected += "\r\n    Strategy analyzer log cleanup.";

                MessageBoxResult res = MessageBox.Show(youHaveSelected, "Confirm Cleanup Steps", MessageBoxButton.OKCancel);
                if (res.Equals(MessageBoxResult.Cancel))
                    return;

                if (isCacheChecked && !_cleaner.CleanupCache())
                {
                    MessageBox.Show(_cleaner.Error, "Cache Clean Error");
                    return;
                }
                if (isLogChecked && !_cleaner.CleanupLogs())
                {
                    MessageBox.Show(_cleaner.Error, "Log Clean Error");
                    return;
                }
                if (isTraceChecked && !_cleaner.CleanupTraces())
                {
                    MessageBox.Show(_cleaner.Error, "Trace Clean Error");
                    return;
                }
                if (isDBChecked && !_cleaner.CleanupDB())
                {
                    MessageBox.Show(_cleaner.Error, "DB Clean Error");
                    return;
                }
                if (isAnalyzerLogsChecked && !_cleaner.CleanupStrategyAnalyzerLogs())
                {
                    MessageBox.Show(_cleaner.Error, "Analyzer Log Clean Error");
                    return;
                }
            }
        }

        public void OnNinjaTraderExited()
        {
            App app=(Application.Current as App)!;
            LaunchAndCleanButton.IsEnabled = !app.IsNinjaTraderRunning;
            LaunchButton.IsEnabled = !app.IsNinjaTraderRunning;
            cleanButton.IsEnabled = !app.IsNinjaTraderRunning;
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow helpWindow = new AboutWindow();
            helpWindow.ShowDialog();

        }
    }
}