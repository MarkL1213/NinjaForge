using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Xml.Linq;

namespace NinjaForge
{
    public class NinjaTraderInstallSettings
    {
        string _installDirectory = string.Empty;
        string _error= string.Empty;

        public string Error { get { return _error; } }
        public bool HasError { get { return !string.IsNullOrEmpty(_error); } }
        public NinjaTraderInstallSettings()
        {
            try
            {
                string regKey = "HKEY_LOCAL_MACHINE\\SOFTWARE\\NinjaTrader, LLC\\NinjaTrader";
                string? regValue = Registry.GetValue(regKey, "InstallDir", string.Empty) as string;
                if (string.IsNullOrEmpty(regValue))
                {
                    _error = "NinjaTrader 8 install not detected.";
                }
                else _installDirectory = regValue;
            }
            catch (Exception ex)
            {
                _error = $"NinjaTrader 8 install not detected. {ex.Message}";
            }
        }

        public string InstallDirectory { get { return _installDirectory; } }
        public string DBDirectory { get { return Path.Combine(DocumentsDirectory, "db"); } }
        public string Executable { get { return Path.Combine(_installDirectory,"bin","NinjaTrader.exe"); } }
        public string DocumentsDirectory { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"NinjaTrader 8"); }
}
    }

    public class StartupWorkspace
    {
        public string WorkspaceName { get; set; } = string.Empty;

        public string FrameworkSafeName()
        {
            if (string.IsNullOrEmpty(WorkspaceName)) return WorkspaceName;

            string name = WorkspaceName;

            if (char.IsDigit(name[0])) name = "N" + name;
            var allowedChars = name.Where(c =>
                        char.IsLetterOrDigit(c)
                        );

            return new string(allowedChars.ToArray());
        }
    }

    public class WorkspaceFile : IDisposable
    {
        private NinjaTraderInstallSettings _ntInstall = new NinjaTraderInstallSettings();
        private Process? _pProcess = null;
        private string _backupPath = string.Empty;
        private bool _disposedValue;
        public string ConfigFileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        
        public WorkspaceFile(Process? p=null)
        {
            _pProcess = p;
            ConfigFileName = "_Workspaces.xml";
            FilePath = Path.Combine(_ntInstall.DocumentsDirectory, "workspaces");
        }

        public bool LaunchNinjaTrader(bool safeMode)
        {
            App app = (Application.Current as App)!;

            if (_ntInstall.HasError || !File.Exists(_ntInstall.Executable))
            {
                MessageBox.Show(_ntInstall.HasError
                    ? $"Launch error: Valid install not detected. {_ntInstall.Error}"
                    : $"Launch error: Valid install not detected.",
                    "Launch Error", MessageBoxButton.OK);

                return false;
            }

            if (_pProcess != null || app.IsNinjaTraderRunning)
            {
                MessageBox.Show($"Launch error: NinjaTrader is already running.",
                            "Launch Error", MessageBoxButton.OK);
                return false;
            }

            try
            {
                _pProcess = new Process();
                _pProcess.StartInfo.FileName = _ntInstall.Executable;
                _pProcess.StartInfo.UseShellExecute = false;
                if (safeMode) _pProcess.StartInfo.Arguments = "-safe";
                
                app.IsNinjaTraderRunning = true;
                _pProcess.Exited += app.NinjaTradeAppExited;

                return _pProcess.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Launch error: NinjaTrader failed startup. {ex.Message}",
                            "Launch Error", MessageBoxButton.OK);

                app.IsNinjaTraderRunning = false;
                CleanupProcess();

                return false;
            }
        }

        public void CleanupProcess()
        {
            if (_pProcess != null)
            {
                App app = (Application.Current as App)!;

                _pProcess.Exited -= app.NinjaTradeAppExited;
                _pProcess.Dispose();
                _pProcess = null;
            }
        }

        public string SetStartupWorkspace(StartupWorkspace workspace)
        {
            string fullPath = Path.Combine(FilePath, ConfigFileName);
            
            string tmpPath = Path.Combine(FilePath, ConfigFileName + ".tmp");
            XDocument xDoc = new XDocument();
            try
            {
                if (!File.Exists(fullPath)) { return "File does not exist"; }

                xDoc = XDocument.Load(fullPath);

                XElement? root = xDoc.Element("NinjaTrader");
                if (root == null) { return "Root node is missing."; }

                XElement? activeElement = root.Element("ActiveWorkspace");
                if (activeElement == null) { return "Active workspace node is missing."; }

                activeElement.Value = workspace.WorkspaceName;

                
                xDoc.Save(tmpPath);
                
                File.Move(tmpPath, fullPath, true);
            }
            catch (Exception e)
            {
                return e.Message;
            }

            return string.Empty;
        }

        internal string CreateBackup()
        {
            string fullPath = Path.Combine(FilePath, ConfigFileName);
            _backupPath = Path.Combine(FilePath, ConfigFileName + ".bak");

            try
            {
                if (!File.Exists(fullPath)) { return "File does not exist"; }
                File.Copy(fullPath, _backupPath, true);
            }
            catch (Exception e)
            {

                return e.Message;
            }

            return string.Empty;
        }

        public bool RestoreFromBackup()
        {
            if(string.IsNullOrEmpty(_backupPath)) return false;
            if (File.Exists(_backupPath))
            {
                string fullPath = Path.Combine(FilePath, ConfigFileName);

                File.Copy(_backupPath, fullPath, true);
                File.Delete(_backupPath); // Optional: Clean up after restore
                return true;
            }

            return false;
        }

        public List<StartupWorkspace> DetectWorkspaces()
        {
            List<StartupWorkspace> workspaces = new List<StartupWorkspace>();
            if (!Directory.Exists(FilePath))
            {
                return workspaces;
            }
            try
            {
                Directory.EnumerateFiles(FilePath, "*.xml").ToList().ForEach(file =>
                {
                    string fileName = Path.GetFileName(file);
                    if (fileName != ConfigFileName)
                    {
                        string fileNameNoExtension = Path.GetFileNameWithoutExtension(file);
                        workspaces.Add(new StartupWorkspace() { WorkspaceName = fileNameNoExtension });
                    }
                });
            }
            catch
            {
                return workspaces;
            }
            return workspaces;
        }

        public string LookupCurrentWorkspace()
        {
            string fullPath = Path.Combine(FilePath, ConfigFileName);
            XDocument xDoc = new XDocument();
            try
            {
                if (!File.Exists(fullPath)) return string.Empty;
                xDoc = XDocument.Load(fullPath);

                XElement? root = xDoc.Element("NinjaTrader");
                if (root == null) { return string.Empty; }

                XElement? activeElement = root.Element("ActiveWorkspace");
                if (activeElement == null) { return string.Empty; }

                return activeElement.Value;
            }
            catch
            {
                return string.Empty;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    CleanupProcess();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public class NinjaTraderCleaner
    {
        public string DocumentsDirectory { set; get; }

        public string Error { get; private set; } = string.Empty;

        public enum DataInterval { Tick, Minute, Day, Replay, Cache, All };

        public NinjaTraderCleaner(string documentsDirectory) { DocumentsDirectory = documentsDirectory; }

        public bool VerifyDocumentsDirectory()
        {
            Error = string.Empty;
            if (string.IsNullOrEmpty(DocumentsDirectory))
            {
                Error = "Install directory is not set.";
                return false;
            }
            if (!Directory.Exists(DocumentsDirectory))
            {
                Error = $"Install directory '{DocumentsDirectory}' does not exist.";
                return false;
            }
            return true;
        }

        private bool DeleteAllRecursive(string directoryToClean)
        {
            try
            {
                DirectoryInfo di = new DirectoryInfo(directoryToClean);

                foreach (FileInfo file in di.EnumerateFiles())
                {
                    file.Delete();
                }
                foreach (DirectoryInfo dir in di.EnumerateDirectories())
                {
                    dir.Delete(true);
                }
            }
            catch (Exception e)
            {
                Error = e.Message;
                return false;
            }
            return true;
        }

        public bool CleanAll()
        {
            if (!CleanupCache()) return false;
            if (!CleanupDB()) return false;
            if (!CleanupLogs()) return false;
            if (!CleanupTraces()) return false;
            if (!CleanupStrategyAnalyzerLogs()) return false;
            return true;
        }

        public bool CleanupStrategyAnalyzerLogs()
        {
            Error = string.Empty;
            if (!VerifyDocumentsDirectory()) return false;

            string logDirectory = Path.Combine(DocumentsDirectory, "strategyanalyzerlogs");
            if (!Directory.Exists(logDirectory)) return true;
            return DeleteAllRecursive(logDirectory);
        }

        public bool CleanupLogs()
        {
            Error = string.Empty;
            if (!VerifyDocumentsDirectory()) return false;

            string logDirectory = Path.Combine(DocumentsDirectory, "log");
            if (!Directory.Exists(logDirectory)) return true;
            return DeleteAllRecursive(logDirectory);
        }

        public bool CleanupTraces()
        {
            Error = string.Empty;
            if (!VerifyDocumentsDirectory()) return false;

            string traceDirectory = Path.Combine(DocumentsDirectory, "trace");
            if (!Directory.Exists(traceDirectory)) return true;
            return DeleteAllRecursive(traceDirectory);
        }

        public bool CleanupCache()
        {
            Error = string.Empty;
            if (!VerifyDocumentsDirectory()) return false;

            string cacheDirectory = Path.Combine(DocumentsDirectory, "cache");
            if (!Directory.Exists(cacheDirectory)) return true;
            return DeleteAllRecursive(cacheDirectory);
        }

        public bool CleanupDB(DataInterval interval = DataInterval.All)
        {
            Error = string.Empty;
            if (!VerifyDocumentsDirectory()) return false;

            string dbDirectory = string.Empty;
            switch (interval)
            {
                case DataInterval.All:
                    if (!CleanupDB(DataInterval.Tick)) return false;
                    if (!CleanupDB(DataInterval.Minute)) return false;
                    if (!CleanupDB(DataInterval.Day)) return false;
                    if (!CleanupDB(DataInterval.Replay)) return false;
                    if (!CleanupDB(DataInterval.Cache)) return false;
                    return true;
                case DataInterval.Tick:
                    dbDirectory = Path.Combine(DocumentsDirectory, "db","tick");
                    break;
                case DataInterval.Minute:
                    dbDirectory = Path.Combine(DocumentsDirectory, "db","minute");
                    break;
                case DataInterval.Day:
                    dbDirectory = Path.Combine(DocumentsDirectory, "db","day");
                    break;
                case DataInterval.Replay:
                    dbDirectory = Path.Combine(DocumentsDirectory, "db","replay");
                    break;
                case DataInterval.Cache:
                    dbDirectory = Path.Combine(DocumentsDirectory, "db","cache");
                    break;
            }

            if (string.IsNullOrEmpty(dbDirectory))
            {
                Error = $"Database interval unknown.";
                return false;
            }

            if (!Directory.Exists(dbDirectory))
            {
                Error = $"Database directory '{dbDirectory}' does not exist.";
                return false;
            }

            return DeleteAllRecursive(dbDirectory);
        }
    }

    ///
    /// IGNORE: This class is a work in progress for potential new feature support.
    ///
    public class NinjaTraderDatabaseManager
    {
        public string ManagerDBDirectory
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NinjaTrader 8 Backup", "db"); }
        }

        private NinjaTraderInstallSettings _ntInstall;
        private NinjaTraderDatabaseManager()
        {
            _ntInstall = new NinjaTraderInstallSettings();
        }
        public enum DataInterval { Tick, Minute, Day, Replay }

        public string Error { get; private set; } = string.Empty;

        public string GetIntervalFolder(string DBRoot,DataInterval interval)
        {
            string dir = string.Empty;
            switch (interval)
            {
                case DataInterval.Tick: dir = Path.Combine(_ntInstall.DBDirectory, "tick"); break;
                case DataInterval.Day: dir = Path.Combine(_ntInstall.DBDirectory, "day"); break;
                case DataInterval.Minute: dir = Path.Combine(_ntInstall.DBDirectory, "minute"); break;
                case DataInterval.Replay: dir = Path.Combine(_ntInstall.DBDirectory, "replay"); break;
            }
            return dir;
        }

        List<string> FindAllSymbols(DataInterval interval)
        {
            Error = string.Empty;
            List<string> symbols = new List<string>();
            string dir = GetIntervalFolder(_ntInstall.DBDirectory, interval);
            if (!Directory.Exists(dir))
            {
                Error = $"Database directory '{dir}' does not exist.";
                return symbols;
            }

            DirectoryInfo dbInfo = new DirectoryInfo(dir);
            foreach (DirectoryInfo di in dbInfo.EnumerateDirectories(dir)) {
                symbols.Add(di.Name);
            }

            return symbols;
        }

        //////////
        //need utility functions to move specific data files into and out of the NT DB
        //////////
        //BackupAllDataFiles(symbol="")
        public bool BackupAllDataFiles(string symbolToBackup = "")
        {
            if (!BackupAllDataFiles(DataInterval.Tick, symbolToBackup)) return false;
            if (!BackupAllDataFiles(DataInterval.Minute, symbolToBackup)) return false;
            if (!BackupAllDataFiles(DataInterval.Day, symbolToBackup)) return false;
            return BackupAllDataFiles(DataInterval.Replay, symbolToBackup);
        }

        public bool BackupAllDataFiles(DataInterval interval, string symbolToBackup = "")
        {
            Error = string.Empty;
            List<string> symbols;
            if (string.IsNullOrEmpty(symbolToBackup))
            {
                symbols = FindAllSymbols(interval);
                if (!string.IsNullOrEmpty(Error)) return false;
            }
            else
            {
                symbols = new List<string>();
                symbols.Add(symbolToBackup);
            }

            string intervalFolder = GetIntervalFolder(_ntInstall.DBDirectory, interval);
            string backupIntervalFolder = GetIntervalFolder(ManagerDBDirectory, interval);

            foreach (string symbol in symbols)
            {
                string symbolFolderName = Path.Combine(intervalFolder, symbol);
                DirectoryInfo si = new DirectoryInfo(symbolFolderName);
                if (!si.Exists)
                {
                    Error = "";
                    return false;
                }

                string backupSymbolFolderName = Path.Combine(backupIntervalFolder, symbol);
                DirectoryInfo bsi = new DirectoryInfo(backupSymbolFolderName);
                if (!bsi.Exists)
                {
                    try
                    {
                        bsi.Create();
                    }
                    catch (Exception ex)
                    {
                        Error = ex.Message;
                        return false;
                    }
                }

                foreach (FileInfo fi in si.EnumerateFiles())
                {
                    string backupFileName = Path.Combine(backupSymbolFolderName, fi.Name);
                    FileInfo bfi = new FileInfo(backupFileName);
                    bool backupCreated = false;
                    string tmpBackupFile = bfi.FullName + ".tmp";
                    if (bfi.Exists)
                    {
                        try
                        {
                            if (File.Exists(tmpBackupFile))
                            {
                                File.Delete(tmpBackupFile);
                            }
                            File.Move(bfi.FullName, tmpBackupFile);
                        }
                        catch (Exception ex)
                        {
                            Error = ex.Message;
                            return false;
                        }
                        backupCreated = true;
                    }

                    try
                    {
                        bfi = fi.CopyTo(backupFileName);
                    }
                    catch (Exception ex)
                    {
                        Error = ex.Message;
                        if (backupCreated)
                        {
                            File.Move(tmpBackupFile, backupFileName);
                        }
                    }

                    if (backupCreated)
                    {
                        try
                        {
                            File.Delete(tmpBackupFile);
                        }
                        catch (Exception ex)
                        {
                            Error = ex.Message;
                            return false;
                        }
                    }
                }
            }

            return true;
        }


        //RestoreDataFile(DataInterval interval, string symbol, string fileName)
        //BackupAndRemoveDataFile(DataInterval interval, string symbol, string fileName)
    }

}
