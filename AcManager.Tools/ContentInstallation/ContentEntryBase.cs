﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation {
    public abstract class ContentEntryBase : NotifyPropertyChanged {
        [NotNull]
        public string Id { get; }

        public string DisplayId => string.IsNullOrEmpty(Id) ? "N/A" : Id;

        /// <summary>
        /// Empty if object’s in root.
        /// </summary>
        [NotNull]
        public string EntryPath { get; }

        [NotNull]
        public string DisplayPath => string.IsNullOrEmpty(EntryPath) ? "N/A" : Path.DirectorySeparatorChar + EntryPath;

        [NotNull]
        public string Name { get; }

        [CanBeNull]
        public string Version { get; }

        [CanBeNull]
        public byte[] IconData { get; protected set; }

        private bool _singleEntry;

        public bool SingleEntry {
            get => _singleEntry;
            set {
                if (Equals(value, _singleEntry)) return;
                _singleEntry = value;
                OnPropertyChanged();
            }
        }

        public abstract string NewFormat { get; }

        public abstract string ExistingFormat { get; }

        protected ContentEntryBase([NotNull] string path, [NotNull] string id, string name = null, string version = null,
                byte[] iconData = null) {
            EntryPath = path ?? throw new ArgumentNullException(nameof(path));
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? id;
            Version = version;
            IconData = iconData;
        }

        private bool _installEntry;

        public bool InstallEntry {
            get => _installEntry;
            set {
                if (Equals(value, _installEntry)) return;
                _installEntry = value;
                OnPropertyChanged();
            }
        }

        private void InitializeOptions() {
            if (_updateOptions == null) {
                var oldValue = _selectedOption;
                _updateOptions = GetUpdateOptions().ToArray();
                _selectedOption = GetDefaultUpdateOption(_updateOptions);
                OnSelectedOptionChanged(oldValue, _selectedOption);
            }
        }

        protected void ResetUpdateOptions() {
            var oldValue = _selectedOption;
            _updateOptions = GetUpdateOptions().ToArray();
            _selectedOption = GetDefaultUpdateOption(_updateOptions);
            OnSelectedOptionChanged(oldValue, _selectedOption);
            OnPropertyChanged(nameof(UpdateOptions));
            OnPropertyChanged(nameof(SelectedOption));
        }

        protected virtual UpdateOption GetDefaultUpdateOption(UpdateOption[] list) {
            return list.FirstOrDefault();
        }

        private UpdateOption _selectedOption;

        [CanBeNull]
        public UpdateOption SelectedOption {
            get {
                InitializeOptions();
                return _selectedOption;
            }
            set {
                if (Equals(value, _selectedOption)) return;
                var oldValue = _selectedOption;
                _selectedOption = value;
                OnSelectedOptionChanged(oldValue, value);
                OnPropertyChanged();
            }
        }

        public string GetNew(string displayName) {
            return string.Format(NewFormat, displayName);
        }

        public string GetExisting(string displayName) {
            return string.Format(ExistingFormat, displayName);
        }

        private UpdateOption[] _updateOptions;
        public IReadOnlyList<UpdateOption> UpdateOptions {
            get {
                InitializeOptions();
                return _updateOptions;
            }
        }

        protected virtual void OnSelectedOptionChanged(UpdateOption oldValue, UpdateOption newValue) {}

        protected virtual IEnumerable<UpdateOption> GetUpdateOptions() {
            return new[] {
                new UpdateOption(ToolsStrings.Installator_UpdateEverything),
                new UpdateOption(ToolsStrings.Installator_RemoveExistingFirst) { RemoveExisting = true }
            };
        }

        protected virtual CopyCallback GetCopyCallback([NotNull] string destination) {
            var filter = SelectedOption?.Filter;
            return fileInfo => {
                var filename = fileInfo.Key;
                if (EntryPath != string.Empty && !FileUtils.IsAffected(EntryPath, filename)) return null;

                var subFilename = FileUtils.GetRelativePath(filename, EntryPath);
                return filter == null || filter(subFilename) ? Path.Combine(destination, subFilename) : null;
            };
        }

        [ItemCanBeNull]
        public async Task<InstallationDetails> GetInstallationDetails(CancellationToken cancellation) {
            var destination = await GetDestination(cancellation);
            return destination != null ?
                    new InstallationDetails(GetCopyCallback(destination),
                            SelectedOption?.CleanUp?.Invoke(destination)?.ToArray()) :
                    null;
        }

        [ItemCanBeNull]
        protected abstract Task<string> GetDestination(CancellationToken cancellation);

        private BetterImage.BitmapEntry? _icon;
        public BetterImage.BitmapEntry? Icon => IconData == null ? null :
                _icon ?? (_icon = BetterImage.LoadBitmapSourceFromBytes(IconData, 32));

        #region From Wrapper
        private bool _active = true;

        public bool Active {
            get => _active;
            set {
                if (Equals(value, _active)) return;
                _active = value;
                OnPropertyChanged();
            }
        }

        private bool _noConflictMode;

        public bool NoConflictMode {
            get => _noConflictMode;
            set {
                if (value == _noConflictMode) return;
                _noConflictMode = value;
                OnPropertyChanged();
            }
        }

        public async Task CheckExistingAsync() {
            var tuple = await GetExistingNameAndVersionAsync();
            IsNew = tuple == null;
            ExistingName = tuple?.Item1;
            ExistingVersion = tuple?.Item2;
            IsNewer = Version.IsVersionNewerThan(ExistingVersion);
            IsOlder = Version.IsVersionOlderThan(ExistingVersion);
        }

        [ItemCanBeNull]
        protected abstract Task<Tuple<string, string>> GetExistingNameAndVersionAsync();

        public bool IsNew { get; set; }

        [CanBeNull]
        private string _existingVersion;

        [CanBeNull]
        public string ExistingVersion {
            get => _existingVersion;
            set {
                if (value == _existingVersion) return;
                _existingVersion = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        [CanBeNull]
        private string _existingName;

        [CanBeNull]
        public string ExistingName {
            get => _existingName;
            set {
                if (value == _existingName) return;
                _existingName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        private bool _isNewer;

        public bool IsNewer {
            get => _isNewer;
            set {
                if (value == _isNewer) return;
                _isNewer = value;
                OnPropertyChanged();
            }
        }

        private bool _isOlder;

        public bool IsOlder {
            get => _isOlder;
            set {
                if (value == _isOlder) return;
                _isOlder = value;
                OnPropertyChanged();
            }
        }

        public string DisplayName => IsNew ? GetNew(Name) : GetExisting(ExistingName ?? Name);
        #endregion
    }

    public class CmThemeEntry : ContentEntryBase {
        public CmThemeEntry([NotNull] string path, [NotNull] string id, string version)
                : base(path, id, AcStringValues.NameFromId(id.ApartFromLast(".xaml", StringComparison.OrdinalIgnoreCase)), version) { }

        public override string NewFormat => "New CM theme {0}";
        public override string ExistingFormat => "Update for a CM theme {0}";

        public static string GetVersion(string data, out bool isTheme) {
            var doc = XDocument.Parse(data);
            var n = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml/presentation");

            isTheme = doc.Root?.Name == n + "ResourceDictionary";
            if (!isTheme) return null;

            var nx = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
            var ns = XNamespace.Get("clr-namespace:System;assembly=mscorlib");
            return doc.Descendants(ns + "String")
                      .FirstOrDefault(x => x.Attribute(nx + "Key")?.Value == "Version")?.Value;
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            yield return new UpdateOption("Install") {
                RemoveExisting = false
            };
        }

        protected override CopyCallback GetCopyCallback(string destination) {
            var xaml = EntryPath;
            if (string.IsNullOrWhiteSpace(xaml)) return info => null;

            var resources = EntryPath.ApartFromLast(".xaml", StringComparison.OrdinalIgnoreCase);
            return fileInfo => {
                var filename = fileInfo.Key;
                return FileUtils.ArePathsEqual(filename, xaml) ? Path.Combine(destination, Path.GetFileName(xaml))
                        : FileUtils.IsAffected(resources, filename) ? Path.Combine(destination, FileUtils.GetRelativePath(filename, resources)) : null;
            };
        }

        protected override async Task<Tuple<string, string>> GetExistingNameAndVersionAsync() {
            var existing = Path.Combine(FilesStorage.Instance.GetDirectory("Themes"), Id);
            return File.Exists(existing) ? Tuple.Create(Name, GetVersion(await FileUtils.ReadAllTextAsync(existing), out var _)) : null;
        }

        protected override Task<string> GetDestination(CancellationToken cancellation) {
            return Task.FromResult(FilesStorage.Instance.GetDirectory("Themes"));
        }
    }

    public abstract class ContentEntryBase<T> : ContentEntryBase where T : AcCommonObject {
        public ContentEntryBase([NotNull] string path, [NotNull] string id, string name = null, string version = null, byte[] iconData = null)
                : base(path, id, name, version, iconData) { }

        public abstract FileAcManager<T> GetManager();

        private T _acObjectNew;

        [ItemCanBeNull]
        public async Task<T> GetExistingAcObjectAsync() {
            return _acObjectNew ?? (_acObjectNew = await GetManager().GetByIdAsync(Id));
        }

        protected T GetExistingAcObject() {
            return _acObjectNew ?? (_acObjectNew = GetManager().GetById(Id));
        }

        protected override async Task<Tuple<string, string>> GetExistingNameAndVersionAsync() {
            var obj = await GetExistingAcObjectAsync();
            return obj == null ? null : Tuple.Create(obj.DisplayName, (obj as IAcObjectVersionInformation)?.Version);
        }

        protected override async Task<string> GetDestination(CancellationToken cancellation) {
            var manager = GetManager();
            if (manager == null) return null;

            var destination = await manager.PrepareForAdditionalContentAsync(Id,
                    SelectedOption != null && SelectedOption.RemoveExisting);
            return cancellation.IsCancellationRequested ? null : destination;
        }
    }

    public class CarContentEntry : ContentEntryBase<CarObject> {
        public CarContentEntry([NotNull] string path, [NotNull] string id, string name = null, string version = null, byte[] iconData = null)
                : base(path, id, name, version, iconData) { }

        public override string NewFormat => ToolsStrings.ContentInstallation_CarNew;
        public override string ExistingFormat => ToolsStrings.ContentInstallation_CarExisting;

        public override FileAcManager<CarObject> GetManager() {
            return CarsManager.Instance;
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            bool UiFilter(string x) {
                return x != @"ui\ui_car.json" && x != @"ui\brand.png" && x != @"logo.png" && (!x.StartsWith(@"skins\") || !x.EndsWith(@"\ui_skin.json"));
            }

            bool PreviewsFilter(string x) {
                return !x.StartsWith(@"skins\") || !x.EndsWith(@"\preview.jpg");
            }

            return base.GetUpdateOptions().Union(new[] {
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformation) { Filter = UiFilter },
                new UpdateOption(ToolsStrings.ContentInstallation_KeepSkinsPreviews) { Filter = PreviewsFilter },
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformationAndSkinsPreviews) { Filter = x => UiFilter(x) && PreviewsFilter(x) }
            });
        }
    }

    public sealed class TrackContentLayoutEntry : NotifyPropertyChanged, IWithId {
        /// <summary>
        /// KN5-files referenced in assigned models.ini if exists.
        /// </summary>
        [CanBeNull]
        public readonly List<string> Kn5Files;

        public string DisplayKn5Files => Kn5Files?.JoinToReadableString();

        // Similar to Kn5Files, but here is a list of files required, but not provided in the source.
        [CanBeNull]
        public readonly List<string> RequiredKn5Files;

        private string[] _missingKn5Files = new string[0];

        public string[] MissingKn5Files {
            get => _missingKn5Files;
            set {
                value = value ?? new string[0];
                if (Equals(value, _missingKn5Files)) return;
                _missingKn5Files = value;
                DisplayMissingKn5Files = value.JoinToReadableString();
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayMissingKn5Files));
            }
        }

        public string DisplayMissingKn5Files { get; private set; }

        /// <summary>
        /// If it’s not an actual layout, but instead just a base-track in a multi-layout situation, Id is empty!
        /// </summary>
        [NotNull]
        public string Id { get; }

        private bool _active = true;

        public bool Active {
            get => _active;
            set {
                if (Equals(value, _active)) return;
                _active = value;
                OnPropertyChanged();
            }
        }

        [CanBeNull]
        public string Name { get; }

        [CanBeNull]
        public string Version { get; }

        [CanBeNull]
        public byte[] IconData { get; }

        public TrackContentLayoutEntry([NotNull] string id, [CanBeNull] List<string> kn5Files, [CanBeNull] List<string> requiredKn5Files,
                string name = null, string version = null, byte[] iconData = null) {
            Kn5Files = kn5Files;
            RequiredKn5Files = requiredKn5Files;
            Id = id;
            Name = name;
            Version = version;
            IconData = iconData;
        }

        public string DisplayId => string.IsNullOrEmpty(Id) ? "N/A" : Id;

        public string DisplayName => ExistingLayout == null ? $"{Name} (new layout)" :
                Name == ExistingLayout.LayoutName ? $"{Name} (update for layout)" : $"{Name} (update for {ExistingLayout.LayoutName})";

        private TrackObjectBase _existingLayout;

        public TrackObjectBase ExistingLayout {
            get => _existingLayout;
            set {
                if (Equals(value, _existingLayout)) return;
                _existingLayout = value;
                OnPropertyChanged();
            }
        }

        private BetterImage.BitmapEntry? _icon;
        public BetterImage.BitmapEntry? Icon => IconData == null ? null :
                _icon ?? (_icon = BetterImage.LoadBitmapSourceFromBytes(IconData, 32));
    }

    public class TrackContentEntry : ContentEntryBase<TrackObject> {
        // In case there are no extra layouts, but models.ini, here will be stored list of referenced KN5 files
        [CanBeNull]
        public readonly List<string> Kn5Files;

        // Similar to Kn5Files, but here is a list of files required, but not provided in the source.
        [CanBeNull]
        public readonly List<string> RequiredKn5Files;

        private string[] _missingKn5Files = new string[0];

        public string[] MissingKn5Files {
            get => _missingKn5Files;
            set {
                value = value ?? new string[0];
                if (Equals(value, _missingKn5Files)) return;
                _missingKn5Files = value;
                DisplayMissingKn5Files = value.JoinToReadableString();
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayMissingKn5Files));
            }
        }

        public string DisplayMissingKn5Files { get; private set; }

        // Layouts!
        [CanBeNull]
        public IReadOnlyList<TrackContentLayoutEntry> Layouts { get; }

        private static string GetName([CanBeNull] IReadOnlyList<TrackContentLayoutEntry> layouts) {
            if (layouts == null) return null;
            return TrackObject.FindNameForMultiLayoutMode(layouts.Select(x => x.Name).ToList()) ??
                    layouts.FirstOrDefault()?.Name;
        }

        private TrackContentEntry([NotNull] string path, [NotNull] string id, [CanBeNull] List<string> kn5Files,
                [CanBeNull] List<string> requiredKn5Files, string name = null, string version = null,
                byte[] iconData = null) : base(path, id, name, version, iconData) {
            RequiredKn5Files = requiredKn5Files;
            Kn5Files = kn5Files;
        }

        private TrackContentEntry([NotNull] string path, [NotNull] string id, [NotNull] IReadOnlyList<TrackContentLayoutEntry> layouts)
                : base(path, id, GetName(layouts), layouts.FirstOrDefault()?.Version) {
            Layouts = layouts.ToList();
            foreach (var layout in Layouts) {
                layout.PropertyChanged += OnLayoutPropertyChanged;
            }
        }

        public static async Task<TrackContentEntry> Create([NotNull] string path, [NotNull] string id, [CanBeNull] List<string> kn5Files,
                [CanBeNull] List<string> requiredKn5Files, string name = null, string version = null, byte[] iconData = null) {
            var result = new TrackContentEntry(path, id, kn5Files, requiredKn5Files, name, version, iconData);
            await result.Initialize().ConfigureAwait(false);
            return result;
        }

        public static async Task<TrackContentEntry> Create([NotNull] string path, [NotNull] string id, [NotNull] IReadOnlyList<TrackContentLayoutEntry> layouts) {
            var result = new TrackContentEntry(path, id, layouts);
            await result.Initialize().ConfigureAwait(false);
            return result;
        }

        public static IEnumerable<string> GetLayoutModelsNames(IniFile file) {
            return file.GetSections("MODEL").Select(x => x.GetNonEmpty("FILE")).NonNull();
        }

        public static IEnumerable<string> GetModelsNames(IniFile file) {
            return file.GetSections("MODEL").Concat(file.GetSections("DYNAMIC_OBJECT")).Select(x => x.GetNonEmpty("FILE")).NonNull();
        }

        private TrackObjectBase _noLayoutsExistingLayout;

        public TrackObjectBase NoLayoutsExistingLayout {
            get => _noLayoutsExistingLayout;
            set {
                if (Equals(value, _noLayoutsExistingLayout)) return;
                _noLayoutsExistingLayout = value;
                OnPropertyChanged();
            }
        }

        private async Task Initialize() {
            TrackObject existing;
            try {
                existing = await GetExistingAcObjectAsync();
            } catch (Exception) {
                // Specially for LINQPad scripts
                return;
            }

            if (existing == null) return;

            // Second part of that track trickery. Now, we have to offer user to combine existing and new track
            // in a lot of various ways

            var existingName = existing.DisplayNameWithoutCount;
            var existingModels = ((IEnumerable<TrackObjectBase>)existing.MultiLayouts ?? new TrackObjectBase[] { existing })
                    .SelectMany(x => GetModelsNames(new IniFile(x.ModelsFilename))).ToList();

            // Let’s find out if any KN5 files are missing, just in case
            string[] GetMissingKn5Files(IEnumerable<string> required) {
                return required?.Where(x => !existingModels.Any(y => string.Equals(x, y, StringComparison.OrdinalIgnoreCase))).ToArray()
                        ?? new string[0];
            }

            MissingKn5Files = GetMissingKn5Files(RequiredKn5Files);
            if (Layouts != null) {
                foreach (var layout in Layouts) {
                    layout.MissingKn5Files = GetMissingKn5Files(layout.RequiredKn5Files);
                }
            }

            var newModels = Layouts?.SelectMany(x => x.Kn5Files).ToList() ?? Kn5Files;

            // If there is a conflict, it will be changed later
            SharedModelsOverlap = newModels?.Any(existingModels.Contains) == true;

            if (Layouts == null) {
                if (!existing.MultiLayoutMode) {
                    // Simplest case — basic track+basic track, nothing complicated.
                    _existingFormat = $"Update for a track {existingName}";
                    NoLayoutsExistingLayout = existing;
                    NoConflictMode = false;
                } else {
                    // Multi-layout track installed, but user also has a basic track to install? OK…
                    var existingBasic = existing.LayoutId == null;
                    if (existingBasic) {
                        // Just an update, I guess?
                        _existingFormat = $"Update for a track {existingName}";
                        NoLayoutsExistingLayout = existing;
                        NoConflictMode = false;
                    } else {
                        // There is no basic track! So, it’s like an additional layout
                        _existingFormat = Name == existingName ?
                                $"New layout for a track {existingName}" :
                                $"New layout {Name} for a track {existingName}";
                        NoConflictMode = true;
                    }
                }
            } else {
                // Sometimes, basic track might end up in layouts if there are other layouts.
                var newBasicLayout = Layouts.FirstOrDefault(x => x.Id == "");
                var newBasic = newBasicLayout != null;

                if (!existing.MultiLayoutMode) {
                    if (!newBasic) {
                        // Simple case: basic track installed, additional layouts are being added
                        _existingFormat = PluralizingConverter.PluralizeExt(Layouts.Count,
                                $"New {{layout}} for a track {existingName}");

                        NoConflictMode = true;
                    } else {
                        // Basic track installed, user is adding additional layouts, but one of them is basic as well! What to do?…
                        _existingFormat = PluralizingConverter.PluralizeExt(Layouts.Count,
                                $"Update for a track {existingName}, plus additional {{layout}}");
                        newBasicLayout.ExistingLayout = existing;
                        HasNewExtraLayouts = true;
                        NoConflictMode = false;
                    }
                } else {
                    // Oops… Layouts+layouts.
                    // Is already installed track has that thing when one of layouts is basic track?
                    var existingBasic = existing.LayoutId == null;
                    var newLayouts = Layouts.Count(x => existing.GetLayoutByLayoutId(x.Id) == null);

                    if (!(existingBasic && newBasic) && newLayouts == Layouts.Count) {
                        // Blessed case! No conflicts
                        _existingFormat = PluralizingConverter.PluralizeExt(Layouts.Count,
                                $"New {{layout}} for a track {existingName}");
                        NoConflictMode = true;
                    } else {
                        // What can I say…
                        _existingFormat = PluralizingConverter.PluralizeExt(Layouts.Count, newLayouts > 0 ?
                                $"Update for a track {existingName}, plus additional {{layout}}" :
                                $"Update for a track {existingName}");
                        HasNewExtraLayouts = newLayouts > 0;
                        NoConflictMode = false;

                        foreach (var layout in Layouts) {
                            layout.ExistingLayout = existing.GetLayoutByLayoutId(layout.Id);
                        }

                        if (newBasic && existingBasic){
                            newBasicLayout.ExistingLayout = existing;
                        }
                    }
                }
            }

            // Good luck with testing, lol
            // x_x
        }

        private List<string> _overlappedModels;
        public string DisplayOverlappedModels => _overlappedModels?.JoinToReadableString();

        private void UpdateSharedModelsOverlap() {
            List<string> overlappedModels;

            var existing = GetExistingAcObject();
            if (existing == null || Layouts?.All(x => !x.Active) == true) {
                overlappedModels = null;
            } else {
                var activeLayouts = Layouts?.Where(x => x.Active).ToList();

                // Already installed models apart from models which are ready to be installed by specific layouts
                var existingModels = ((IEnumerable<TrackObjectBase>)existing.MultiLayouts ?? new TrackObjectBase[] { existing })
                        .SelectMany(x => GetModelsNames(new IniFile(x.ModelsFilename)).ApartFrom(
                                activeLayouts == null
                                        ? (x.LayoutId == null ? Kn5Files : null)
                                        : activeLayouts.GetByIdOrDefault(x.LayoutId ?? "", StringComparison.InvariantCulture)?.Kn5Files))
                        .Distinct().ToList();

                if (existingModels.Count == 0) {
                    overlappedModels = null;
                } else if (activeLayouts != null) {
                    overlappedModels = activeLayouts.SelectMany(x => x.Kn5Files).Where(existingModels.Contains).Distinct().ToList();
                } else if (NoLayoutsExistingLayout != null) {
                    // If current track as a layout is an update for a existing one, then there are no previous shared models
                    overlappedModels = null;
                } else {
                    overlappedModels = Kn5Files?.Where(existingModels.Contains).ToList();
                }
            }

            _overlappedModels = overlappedModels ?? new List<string>();
            OnPropertyChanged(nameof(DisplayOverlappedModels));
            SharedModelsOverlap = _overlappedModels.Count > 0;
        }

        private void OnLayoutPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(TrackContentLayoutEntry.Active)) {
                UpdateSharedModelsOverlap();
            }
        }

        protected override void OnSelectedOptionChanged(UpdateOption oldValue, UpdateOption newValue) {
            base.OnSelectedOptionChanged(oldValue, newValue);
            UpdateSharedModelsOverlap();
        }

        private bool _hasNewExtraLayouts;

        public bool HasNewExtraLayouts {
            get => _hasNewExtraLayouts;
            set {
                if (Equals(value, _hasNewExtraLayouts)) return;
                _hasNewExtraLayouts = value;
                OnPropertyChanged();
            }
        }

        private bool _sharedModelsOverlap;

        public bool SharedModelsOverlap {
            get => _sharedModelsOverlap;
            set {
                if (value == _sharedModelsOverlap) return;
                _sharedModelsOverlap = value;
                OnPropertyChanged();
            }
        }

        private bool _keepExistingSharedModels = true;

        public bool KeepExistingSharedModels {
            get => _keepExistingSharedModels;
            set {
                if (Equals(value, _keepExistingSharedModels)) return;
                _keepExistingSharedModels = value;
                OnPropertyChanged();
            }
        }

        public override string NewFormat => ToolsStrings.ContentInstallation_TrackNew;

        private string _existingFormat = ToolsStrings.ContentInstallation_TrackExisting;
        public override string ExistingFormat => _existingFormat;

        protected override CopyCallback GetCopyCallback(string destination) {
            var filter = NoConflictMode ? null : SelectedOption?.Filter;

            Logging.Write("INSTALLING TRACK…");

            UpdateSharedModelsOverlap();
            if (SharedModelsOverlap && KeepExistingSharedModels) {
                Logging.Write($"We need to keep shared models: {_overlappedModels.JoinToString(", ")}");

                var shared = _overlappedModels;
                filter = filter.And(path => !shared.Any(x => FileUtils.ArePathsEqual(x, path)));
            }

            var disabled = Layouts?.Where(x => !x.Active).Select(x => x.Id).ToList();
            if (disabled?.Count > 0) {
                Logging.Write($"Disabled layouts: {disabled.JoinToString(", ")}");

                if (disabled.Count == Layouts.Count) {
                    Logging.Write("Everything is disabled!");
                    return p => null;
                }

                var inisToCopy = Layouts.Where(x => x.Active).Select(x => x.Id == "" ? "models.ini" : $@"models_{x.Id}.ini")
                                        .Select(FileUtils.NormalizePath).Distinct().ToList();
                var modelsToCopy = Layouts.Where(x => x.Active).SelectMany(x => x.Kn5Files)
                                          .Select(FileUtils.NormalizePath).Distinct().ToList();
                Logging.Write($"INIs to copy: {inisToCopy.JoinToString(", ")}");
                Logging.Write($"Models to copy: {modelsToCopy.JoinToString(", ")}");

                var mainDisabled = disabled.Contains("");
                if (mainDisabled) {
                    disabled.Remove("");
                }

                filter = filter.And(path => string.IsNullOrEmpty(Path.GetDirectoryName(path))
                        // If file is in track’s root directory
                        ? modelsToCopy.Any(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase)) ||
                                inisToCopy.Any(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase))
                        // If file is in subfolder in track’s root directory
                        : FileUtils.IsAffected(@"ui", path)
                                ? (string.Equals(Path.GetDirectoryName(path), @"ui", StringComparison.OrdinalIgnoreCase) ? !mainDisabled :
                                        !disabled.Any(x => FileUtils.IsAffected($@"ui\{x}", path)))
                                : !disabled.Any(x => FileUtils.IsAffected(x, path)));
            }

            return fileInfo => {
                var filename = fileInfo.Key;
                if (EntryPath != string.Empty && !FileUtils.IsAffected(EntryPath, filename)) return null;

                var subFilename = FileUtils.GetRelativePath(filename, EntryPath);
                return filter == null || filter(subFilename) ? Path.Combine(destination, subFilename) : null;
            };
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            UpdateSharedModelsOverlap();

            if (NoConflictMode) {
                return new[] { new UpdateOption("Just Install") };
            }

            bool UiFilter(string x) {
                if (!FileUtils.IsAffected("ui", x)) return true;

                var name = Path.GetFileName(x).ToLowerInvariant();
                return name != "ui_track.json" && name != "preview.png" && name != "outline.png";
            }

            return base.GetUpdateOptions().Concat(new[] {
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformation) { Filter = UiFilter }
            }.NonNull());
        }

        public override FileAcManager<TrackObject> GetManager() {
            return TracksManager.Instance;
        }
    }

    public class ShowroomContentEntry : ContentEntryBase<ShowroomObject> {
        public ShowroomContentEntry([NotNull] string path, [NotNull] string id, string name = null, string version = null, byte[] iconData = null)
                : base(path, id, name, version, iconData) { }

        public override string NewFormat => ToolsStrings.ContentInstallation_ShowroomNew;
        public override string ExistingFormat => ToolsStrings.ContentInstallation_ShowroomExisting;

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            bool UiFilter(string x) {
                return !FileUtils.ArePathsEqual(x, @"ui\ui_showroom.json");
            }

            bool PreviewFilter(string x) {
                return !FileUtils.ArePathsEqual(x, @"preview.jpg");
            }

            return base.GetUpdateOptions().Union(new[] {
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformation){ Filter = UiFilter },
                new UpdateOption("Keep Preview") { Filter = PreviewFilter },
                new UpdateOption("Keep UI Information & Preview") { Filter = x => UiFilter(x) && PreviewFilter(x) }
            });
        }

        public override FileAcManager<ShowroomObject> GetManager() {
            return ShowroomsManager.Instance;
        }
    }

    public class CarSkinContentEntry : ContentEntryBase<CarSkinObject> {
        [NotNull]
        private readonly CarObject _car;

        public CarSkinContentEntry([NotNull] string path, [NotNull] string id, [NotNull] string carId, string name = null, byte[] iconData = null)
                : base(path, id, name, null, iconData) {
            _car = CarsManager.Instance.GetById(carId) ?? throw new Exception($"Car “{carId}” for a skin not found");
            NewFormat = string.Format(ToolsStrings.ContentInstallation_CarSkinNew, "{0}", _car.DisplayName);
            ExistingFormat = string.Format(ToolsStrings.ContentInstallation_CarSkinExisting, "{0}", _car.DisplayName);
        }

        public override string NewFormat { get; }
        public override string ExistingFormat { get; }

        public override FileAcManager<CarSkinObject> GetManager() {
            return _car.SkinsManager;
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            bool UiFilter(string x) {
                return !FileUtils.ArePathsEqual(x, @"ui_skin.json");
            }

            bool PreviewFilter(string x) {
                return !FileUtils.ArePathsEqual(x, @"preview.jpg");
            }

            return base.GetUpdateOptions().Union(new[] {
                new UpdateOption(ToolsStrings.ContentInstallation_KeepUiInformation){ Filter = UiFilter },
                new UpdateOption("Keep Preview") { Filter = PreviewFilter },
                new UpdateOption("Keep UI Information & Preview") { Filter = x => UiFilter(x) && PreviewFilter(x) }
            });
        }
    }

    public class FontContentEntry : ContentEntryBase<FontObject> {
        public FontContentEntry([NotNull] string path, [NotNull] string id, string name = null, byte[] iconData = null)
                : base(path, id, name, iconData: iconData) { }

        public override string NewFormat => ToolsStrings.ContentInstallation_FontNew;
        public override string ExistingFormat => ToolsStrings.ContentInstallation_FontExisting;

        public override FileAcManager<FontObject> GetManager() {
            return FontsManager.Instance;
        }

        protected override CopyCallback GetCopyCallback(string destination) {
            var bitmapExtension = Path.GetExtension(EntryPath);
            var mainEntry = EntryPath.ApartFromLast(bitmapExtension) + FontObject.FontExtension;

            return info => {
                if (FileUtils.ArePathsEqual(info.Key, mainEntry)) {
                    return destination;
                }

                if (FileUtils.ArePathsEqual(info.Key, EntryPath)) {
                    return destination.ApartFromLast(FontObject.FontExtension) + bitmapExtension;
                }

                return null;
            };
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            return new[] { new UpdateOption(ToolsStrings.Installator_UpdateEverything) };
        }
    }

    public class TrueTypeFontContentEntry : ContentEntryBase<TrueTypeFontObject> {
        public TrueTypeFontContentEntry([NotNull] string path, [NotNull] string id, string name = null, byte[] iconData = null)
                : base(path, id, name, iconData: iconData) { }

        public override string NewFormat => "New TrueType font {0}";
        public override string ExistingFormat => "Update for a TrueType font {0}";

        public override FileAcManager<TrueTypeFontObject> GetManager() {
            return TrueTypeFontsManager.Instance;
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            return new[] { new UpdateOption(ToolsStrings.Installator_UpdateEverything) };
        }
    }

    public class PythonAppContentEntry : ContentEntryBase<PythonAppObject> {
        [CanBeNull]
        private readonly List<string> _icons;

        public PythonAppContentEntry([NotNull] string path, [NotNull] string id, string name = null, string version = null,
                byte[] iconData = null, IEnumerable<string> icons = null) : base(path, id, name, version, iconData) {
            _icons = icons?.ToList();
        }

        public override string NewFormat => "New app {0}";
        public override string ExistingFormat => "Update for a app {0}";

        public override FileAcManager<PythonAppObject> GetManager() {
            return PythonAppsManager.Instance;
        }

        protected override CopyCallback GetCopyCallback(string destination) {
            var callback = base.GetCopyCallback(destination);
            var icons = _icons;
            if (icons == null) return callback;

            return info => {
                var b = callback(info);
                return b != null || !icons.Contains(info.Key) ? b :
                        Path.Combine(FileUtils.GetGuiIconsFilename(AcRootDirectory.Instance.RequireValue),
                                Path.GetFileName(info.Key) ?? "icon.tmp");
            };
        }
    }

    public class WeatherContentEntry : ContentEntryBase<WeatherObject> {
        public WeatherContentEntry([NotNull] string path, [NotNull] string id, string name = null, byte[] iconData = null)
                : base(path, id, name, iconData: iconData) { }

        public override string NewFormat => ToolsStrings.ContentInstallation_WeatherNew;
        public override string ExistingFormat => ToolsStrings.ContentInstallation_WeatherExisting;

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            bool PreviewFilter(string x) {
                return x != @"preview.jpg";
            }

            IEnumerable<string> RemoveClouds(string location) {
                yield return Path.Combine(location, "clouds");
            }

            return new[] {
                new UpdateOption(ToolsStrings.Installator_UpdateEverything),
                new UpdateOption(ToolsStrings.Installator_RemoveExistingFirst) { RemoveExisting = true },
                new UpdateOption("Update Everything, Remove Existing Clouds If Any"){ CleanUp = RemoveClouds },
                new UpdateOption("Keep Preview"){ Filter = PreviewFilter },
                new UpdateOption("Update Everything, Remove Existing Clouds If Any & Keep Preview"){ Filter = PreviewFilter, CleanUp = RemoveClouds },
            };
        }

        protected override UpdateOption GetDefaultUpdateOption(UpdateOption[] list) {
            return list.ElementAtOrDefault(2) ?? base.GetDefaultUpdateOption(list);
        }

        public override FileAcManager<WeatherObject> GetManager() {
            return WeatherManager.Instance;
        }
    }

    public class PpFilterContentEntry : ContentEntryBase<PpFilterObject> {
        public PpFilterContentEntry([NotNull] string path, [NotNull] string id, string name = null, byte[] iconData = null)
                : base(path, id, name, iconData: iconData) { }

        public override string NewFormat => "New PP-filter {0}";
        public override string ExistingFormat => "Update for a PP-filter {0}";

        public override FileAcManager<PpFilterObject> GetManager() {
            return PpFiltersManager.Instance;
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            return new[] { new UpdateOption(ToolsStrings.Installator_UpdateEverything) };
        }
    }

    public class DriverModelContentEntry : ContentEntryBase<DriverModelObject> {
        public DriverModelContentEntry([NotNull] string path, [NotNull] string id, string name = null, byte[] iconData = null)
                : base(path, id, name, iconData: iconData) { }

        public override string NewFormat => "New driver model {0}";
        public override string ExistingFormat => "Update for a driver model {0}";

        public override FileAcManager<DriverModelObject> GetManager() {
            return DriverModelsManager.Instance;
        }

        protected override IEnumerable<UpdateOption> GetUpdateOptions() {
            return new[] { new UpdateOption(ToolsStrings.Installator_UpdateEverything) };
        }
    }
}
