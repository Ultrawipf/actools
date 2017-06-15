﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Online;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Microsoft.VisualBasic.Logging;

namespace AcManager.Tools.Objects {
    public partial class ServerPresetObject {
        private static readonly string[] TrackDataToKeep = {
            @"surfaces.ini", @"drs_zones.ini"
        };

        public class PackedEntry : IDisposable {
            public readonly string Key;

            private string _filename;
            private bool _temporaryFilename;
            private readonly byte[] _content;

            private PackedEntry(string key, string filename, byte[] content) {
                Key = key;
                _filename = filename;
                _content = content;
            }

            [CanBeNull]
            public string GetFilename(string temporaryDirectory) {
                if (_filename == null) {
                    if (_content == null) return null;

                    _filename = FileUtils.GetTempFileName(temporaryDirectory, Path.GetExtension(Key));
                    _temporaryFilename = true;
                    File.WriteAllBytes(_filename, _content);
                }

                return _filename;
            }

            [CanBeNull]
            public byte[] GetContent() {
                return _content ?? (_filename != null && File.Exists(_filename) ? File.ReadAllBytes(_filename) : null);
            }

            public static PackedEntry FromFile(string key, string filename) {
                return new PackedEntry(key, filename, null);
            }

            public static PackedEntry FromContent(string key, string content) {
                return new PackedEntry(key, null, Encoding.UTF8.GetBytes(content));
            }

            public static PackedEntry FromContent(string key, byte[] content) {
                return new PackedEntry(key, null, content);
            }

            public void Dispose() {
                if (_temporaryFilename) {
                    File.Delete(_filename);
                    _filename = null;
                }
            }
        }

        public IEnumerable<PackedEntry> PackServerData(bool saveExecutable, bool linuxMode) {
            // Executable
            if (saveExecutable) {
                var serverDirectory = ServerPresetsManager.ServerDirectory;
                yield return PackedEntry.FromFile(
                        linuxMode ? "acServer" : "acServer.exe",
                        Path.Combine(serverDirectory, linuxMode ? "acServer" : "acServer.exe"));
            }

            // Welcome message
            if (!string.IsNullOrEmpty(WelcomeMessage)) {
                yield return PackedEntry.FromContent("cfg/welcome.txt", WelcomeMessage);
            }

            // Main config file
            var serverCfg = IniObject?.Clone() ?? new IniFile();
            SaveData(serverCfg);

            if (!string.IsNullOrEmpty(WelcomeMessage)) {
                serverCfg["SERVER"].Set("WELCOME_MESSAGE", "cfg/welcome.txt");
            }

            yield return PackedEntry.FromContent("cfg/server_cfg.ini", serverCfg.Stringify());

            // Entry list
            var entryList = EntryListIniObject?.Clone() ?? new IniFile();
            entryList.SetSections("CAR", DriverEntries, (entry, section) => entry.SaveTo(section));
            yield return PackedEntry.FromContent("cfg/entry_list.ini", serverCfg.Stringify());

            // Cars
            var root = AcRootDirectory.Instance.RequireValue;
            for (var i = 0; i < CarIds.Length; i++) {
                var carId = CarIds[i];
                var packedData = Path.Combine(FileUtils.GetCarDirectory(root, carId), "data.acd");
                if (File.Exists(packedData)) {
                    yield return PackedEntry.FromFile(Path.Combine(@"content", @"cars", carId, @"data.acd"), packedData);
                }
            }

            // Track
            var localPath = TrackLayoutId != null ? Path.Combine(TrackId, TrackLayoutId) : TrackId;
            foreach (var file in TrackDataToKeep) {
                var actualData = Path.Combine(FileUtils.GetTracksDirectory(root), localPath, @"data", file);
                if (File.Exists(actualData)) {
                    yield return PackedEntry.FromFile(Path.Combine(@"content", @"tracks", localPath, @"data", file), actualData);
                }
            }
        }

        private static void PrepareCar([NotNull] string carId) {
            var root = AcRootDirectory.Instance.RequireValue;
            var actualData = new FileInfo(Path.Combine(FileUtils.GetCarDirectory(root, carId), "data.acd"));
            var serverData = new FileInfo(Path.Combine(root, @"server", @"content", @"cars", carId, @"data.acd"));

            if (actualData.Exists && (!serverData.Exists || actualData.LastWriteTime > serverData.LastWriteTime)) {
                Directory.CreateDirectory(serverData.DirectoryName ?? "");
                FileUtils.HardlinkOrCopy(actualData.FullName, serverData.FullName, true);
            }
        }

        private static void PrepareTrack([NotNull] string trackId, [CanBeNull] string configurationId) {
            var root = AcRootDirectory.Instance.RequireValue;
            var localPath = configurationId != null ? Path.Combine(trackId, configurationId) : trackId;

            foreach (var file in TrackDataToKeep) {
                var actualData = new FileInfo(Path.Combine(FileUtils.GetTracksDirectory(root), localPath, @"data", file));
                var serverData = new FileInfo(Path.Combine(root, @"server", @"content", @"tracks", localPath, @"data", file));

                if (actualData.Exists && (!serverData.Exists || actualData.LastWriteTime > serverData.LastWriteTime)) {
                    Directory.CreateDirectory(serverData.DirectoryName ?? "");
                    FileUtils.HardlinkOrCopy(actualData.FullName, serverData.FullName, true);
                }
            }
        }

        /// <summary>
        /// Update data in server’s folder according to actual data.
        /// </summary>
        public async Task PrepareServer(IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default(CancellationToken)) {
            for (var i = 0; i < CarIds.Length; i++) {
                var carId = CarIds[i];
                progress?.Report(new AsyncProgressEntry(carId, i, CarIds.Length + 1));
                PrepareCar(carId);

                await Task.Delay(10, cancellation);
                if (cancellation.IsCancellationRequested) return;
            }

            progress?.Report(new AsyncProgressEntry(TrackId, CarIds.Length, CarIds.Length + 1));
            PrepareTrack(TrackId, TrackLayoutId);
        }

        public static string GetServerExecutableFilename() {
            return Path.Combine(AcRootDirectory.Instance.RequireValue, @"server", @"acServer.exe");
        }

        public void StopServer() {
            if (IsRunning) {
                _running?.Kill();
                SetRunning(null);
            }
        }

        /// <summary>
        /// Start server (all stdout stuff will end up in RunningLog).
        /// </summary>
        /// <exception cref="InformativeException">For some predictable errors.</exception>
        /// <exception cref="Exception">Process starting might cause loads of problems.</exception>
        public async Task RunServer(IProgress<AsyncProgressEntry> progress = null, CancellationToken cancellation = default(CancellationToken)) {
            StopServer();

            if (!Enabled) {
                throw new InformativeException("Can’t run server", "Preset is disabled.");
            }

            if (HasErrors) {
                throw new InformativeException("Can’t run server", "Preset has errors.");
            }

            if (TrackId == null) {
                throw new InformativeException("Can’t run server", "Track is not specified.");
            }

            var serverExecutable = GetServerExecutableFilename();
            if (!File.Exists(serverExecutable)) {
                throw new InformativeException("Can’t run server", "Server’s executable not found.");
            }

            if (SettingsHolder.Online.ServerPresetsUpdateDataAutomatically) {
                await PrepareServer(progress, cancellation);
            }

            var welcomeMessageLocal = IniObject?["SERVER"].GetNonEmpty("WELCOME_MESSAGE");
            var welcomeMessageFilename = WelcomeMessagePath;
            if (welcomeMessageLocal != null && welcomeMessageFilename != null && File.Exists(welcomeMessageFilename)) {
                using (FromServersDirectory()) {
                    var local = new FileInfo(welcomeMessageLocal);
                    if (!local.Exists || new FileInfo(welcomeMessageFilename).LastWriteTime > local.LastWriteTime) {
                        try {
                            File.Copy(welcomeMessageFilename, welcomeMessageLocal, true);
                        } catch (Exception e) {
                            Logging.Warning(e);
                        }
                    }
                }
            }

            var log = new BetterObservableCollection<string>();
            RunningLog = log;
            try {
                using (var process = new Process {
                    StartInfo = {
                        FileName = serverExecutable,
                        Arguments = $"-c presets/{Id}/server_cfg.ini -e presets/{Id}/entry_list.ini",
                        UseShellExecute = false,
                        WorkingDirectory = Path.GetDirectoryName(serverExecutable) ?? "",
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                    }
                }) {
                    process.Start();
                    SetRunning(process);
                    ChildProcessTracker.AddProcess(process);

                    progress?.Report(AsyncProgressEntry.Finished);

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.OutputDataReceived += (sender, args) => ActionExtension.InvokeInMainThread(() => log.Add(args.Data));
                    process.ErrorDataReceived += (sender, args) => ActionExtension.InvokeInMainThread(() => log.Add($@"[color=#ff0000]{args.Data}[/color]"));

                    await process.WaitForExitAsync(cancellation);
                    if (!process.HasExitedSafe()) {
                        process.Kill();
                    }

                    log.Add($@"[CM] Stopped: {process.ExitCode}");
                }
            } finally {
                SetRunning(null);
            }
        }

        private Process _running;

        private void SetRunning(Process running) {
            _running = running;
            OnPropertyChanged(nameof(IsRunning));
            _stopServerCommand?.RaiseCanExecuteChanged();
            _runServerCommand?.RaiseCanExecuteChanged();
            _restartServerCommand?.RaiseCanExecuteChanged();
        }

        public override void Reload() {
            if (IsRunning) {
                try {
                    _running.Kill();
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }

            base.Reload();
        }

        public bool IsRunning => _running != null;

        private BetterObservableCollection<string> _runningLog;

        public BetterObservableCollection<string> RunningLog {
            get { return _runningLog; }
            set {
                if (Equals(value, _runningLog)) return;
                _runningLog = value;
                OnPropertyChanged();
            }
        }

        private DelegateCommand _stopServerCommand;

        public DelegateCommand StopServerCommand => _stopServerCommand ?? (_stopServerCommand = new DelegateCommand(StopServer, () => IsRunning));

        private AsyncCommand _runServerCommand;

        public AsyncCommand RunServerCommand => _runServerCommand ??
                (_runServerCommand = new AsyncCommand(() => RunServer(), () => Enabled && !HasErrors && !IsRunning));

        private AsyncCommand _restartServerCommand;

        public AsyncCommand RestartServerCommand => _restartServerCommand ??
                (_restartServerCommand = new AsyncCommand(() => RunServer(), () => Enabled && !HasErrors && IsRunning));
    }
}
