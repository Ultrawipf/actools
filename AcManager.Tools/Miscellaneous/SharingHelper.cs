﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Internal;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Miscellaneous {
    public enum SharedEntryType {
        [LocalizedDescription(nameof(ToolsStrings.Shared_ControlsPreset))]
        ControlsPreset,

        [LocalizedDescription(nameof(ToolsStrings.Shared_ForceFeedbackPreset))]
        ForceFeedbackPreset,

        [LocalizedDescription(nameof(ToolsStrings.Shared_QuickDrivePreset))]
        QuickDrivePreset,

        [LocalizedDescription(nameof(ToolsStrings.Shared_RaceGridPreset))]
        RaceGridPreset,

        [LocalizedDescription(nameof(ToolsStrings.Shared_AudioSettingsPreset))]
        AudioSettingsPreset,

        [LocalizedDescription(nameof(ToolsStrings.Shared_VideoSettingsPreset))]
        VideoSettingsPreset,

        [LocalizedDescription(nameof(ToolsStrings.Shared_CustomShowroomPreset))]
        CustomShowroomPreset,

        [LocalizedDescription(nameof(ToolsStrings.Shared_CustomPreviewsPreset))]
        CustomPreviewsPreset,

        [LocalizedDescription(nameof(ToolsStrings.Shared_AssistsSetupPreset))]
        AssistsSetupPreset,

        [LocalizedDescription(nameof(ToolsStrings.Shared_TrackStatePreset))]
        TrackStatePreset,

        [LocalizedDescription(nameof(ToolsStrings.Shared_RhmPreset))]
        RhmPreset,

        [LocalizedDescription(nameof(ToolsStrings.Shared_Replay))]
        Replay,

        [LocalizedDescription(nameof(ToolsStrings.Shared_CarSetup))]
        CarSetup,

        [LocalizedDescription(nameof(ToolsStrings.Shared_PpFilter))]
        PpFilter,

        [LocalizedDescription(nameof(ToolsStrings.Shared_UserChampionship))]
        UserChampionship,

        [LocalizedDescription(nameof(ToolsStrings.Shared_Results))]
        Results,

        [LocalizedDescription(nameof(ToolsStrings.Shared_Weather))]
        Weather,

        [LocalizedDescription(nameof(ToolsStrings.Shared_CspSettings))]
        CspSettings,

        [LocalizedDescription(nameof(ToolsStrings.Shared_BakedShadowsPreset))]
        BakedShadowsPreset,

        [LocalizedDescription(nameof(ToolsStrings.Shared_CarLodsGenerationPreset))]
        CarLodsGenerationPreset,

        // Non-shareable, for internal use:
        [LocalizedDescription(nameof(ToolsStrings.Shared_AmbientShadowsPreset))]
        AmbientShadowsPreset,

        [LocalizedDescription(nameof(ToolsStrings.Shared_InGameAppsPreset))]
        InGameAppsPreset,

        [LocalizedDescription(nameof(ToolsStrings.Shared_PackServerPreset))]
        PackServerPreset,

        [LocalizedDescription(nameof(ToolsStrings.Shared_AcPreviewsPreset))]
        AcPreviewsPreset,

        [LocalizedDescription(nameof(ToolsStrings.Shared_AcShowroomPreset))]
        AcShowroomPreset,

        [LocalizedDescription(nameof(ToolsStrings.Shared_TyresGenerationExamplesPreset))]
        TyresGenerationExamplesPreset,

        [LocalizedDescription(nameof(ToolsStrings.Shared_TyresGenerationParamsPreset))]
        TyresGenerationParamsPreset,
    }

    public class SharedEntry : NotifyPropertyChanged {
        [CanBeNull]
        public string Id { get; set; }

        public SharedEntryType EntryType { get; set; }

        [CanBeNull]
        public string Name { get; set; }

        [CanBeNull]
        public string Target { get; set; }

        [JsonIgnore, CanBeNull]
        public string Author { get; set; }

        [JsonIgnore, CanBeNull]
        public byte[] Data { get; set; }

        [JsonIgnore, CanBeNull]
        public string Url => Id == null ? null : InternalUtils.ShareResult.GetUrl(Id);

        [CanBeNull]
        public string RemovalKey { get; set; }

        [CanBeNull]
        public string LocalSource { get; set; }

        [JsonIgnore]
        private string _json;

        [NotNull]
        public string GetFileName() {
            switch (EntryType) {
                case SharedEntryType.Weather:
                    return FileUtils.EnsureFileNameIsValid(Regex.Replace(Target ?? Name ?? @"shared_weather", @"\W+", "").ToLowerInvariant(), true);

                default:
                    // TODO: even localized?
                    return FileUtils.EnsureFileNameIsValid((Name ?? EntryType.GetDescription()) +
                            // (Target == null ? "" : " for " + Target) +
                            (Author == null ? "" : @" (" + Author + @")") + SharingHelper.GetExtenstion(EntryType), true);
            }
        }

        internal static SharedEntry Deserialize(string s) {
            var value = JsonConvert.DeserializeObject<SharedEntry>(s);
            value._json = s;
            return value;
        }

        internal string Serialize() {
            return _json ?? JsonConvert.SerializeObject(this, Formatting.None);
        }
    }

    public class SharedMetadata : Dictionary<string, string> { }

    public class SharingHelper : NotifyPropertyChanged {
        public static SharingHelper Instance { get; private set; }

        public static void Initialize() {
            Debug.Assert(Instance == null);
            Instance = new SharingHelper();
        }

        public static string GetExtenstion(SharedEntryType type) {
            switch (type) {
                case SharedEntryType.CarSetup:
                case SharedEntryType.ControlsPreset:
                case SharedEntryType.ForceFeedbackPreset:
                case SharedEntryType.PpFilter:
                case SharedEntryType.CspSettings:
                    return @".ini";

                case SharedEntryType.QuickDrivePreset:
                case SharedEntryType.AudioSettingsPreset:
                case SharedEntryType.VideoSettingsPreset:
                case SharedEntryType.AssistsSetupPreset:
                case SharedEntryType.TrackStatePreset:
                case SharedEntryType.RaceGridPreset:
                case SharedEntryType.CustomShowroomPreset:
                case SharedEntryType.CustomPreviewsPreset:
                    return @".cmpreset";

                case SharedEntryType.Replay:
                    return @".lnk";

                case SharedEntryType.RhmPreset:
                    return @".xml";

                case SharedEntryType.Weather:
                case SharedEntryType.UserChampionship:
                case SharedEntryType.Results:
                    return "";

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private const string IniMetadataPrefix = "# __cmattr:";

        public static string SetMetadata(SharedEntryType type, string data, SharedMetadata metadata) {
            switch (type) {
                case SharedEntryType.CarSetup:
                    var s = new StringBuilder();
                    foreach (var pair in metadata.Where(x => x.Value != null)) {
                        if (pair.Key.Contains(@":")) {
                            throw new Exception(@"Invalid key");
                        }
                        s.Append(IniMetadataPrefix + pair.Key + @":" + Storage.Encode(pair.Value) + '\n');
                    }
                    return s + data;

                default:
                    throw new NotSupportedException();
            }
        }

        public static SharedMetadata GetMetadata(SharedEntryType type, string data, out string cleaned) {
            switch (type) {
                case SharedEntryType.CarSetup:
                    var r = new SharedMetadata();
                    var s = data.Split('\n');
                    foreach (var k in s.Where(x => x.StartsWith(IniMetadataPrefix)).Select(l => l.Split(new[] { ':' }, 3)).Where(k => k.Length == 3)) {
                        r[k[1]] = Storage.Decode(k[2]);
                    }
                    cleaned = s.Where(x => !x.StartsWith(IniMetadataPrefix)).JoinToString('\n');
                    return r;

                default:
                    throw new NotSupportedException();
            }
        }

        [ItemCanBeNull]
        public static async Task<SharedEntry> GetSharedAsync(string id, CancellationToken cancellation = default) {
            InternalUtils.SharedEntryLoaded loaded;
            try {
                loaded = await InternalUtils.GetSharedEntryAsync(id, CmApiProvider.UserAgent, cancellation);
                if (loaded == null || cancellation.IsCancellationRequested) {
                    return null;
                }
            } catch (Exception e) {
                NonfatalError.Notify(ToolsStrings.SharingHelper_CannotGetShared, ToolsStrings.Common_CannotDownloadFile_Commentary, e);
                return null;
            }

            SharedEntryType entryType;
            try {
                entryType = (SharedEntryType)Enum.Parse(typeof(SharedEntryType), loaded.EntryType);
            } catch (Exception) {
                NonfatalError.Notify(string.Format(ToolsStrings.SharingHelper_NotSupported, loaded.EntryType),
                        ToolsStrings.SharingHelper_NotSupported_Commentary);
                Logging.Warning("Unsupported entry type: " + loaded.EntryType);
                return null;
            }

            return new SharedEntry {
                Id = id,
                EntryType = entryType,
                Name = loaded.Name,
                Target = loaded.Target,
                Author = loaded.Author,
                Data = loaded.Data
            };
        }

        private BetterObservableCollection<SharedEntry> _history;

        public BetterObservableCollection<SharedEntry> History => _history ?? (_history = LoadHistory());

        private const string Key = "SharedHistory";

        private BetterObservableCollection<SharedEntry> LoadHistory() {
            try {
                return new BetterObservableCollection<SharedEntry>(ValuesStorage.GetStringList(Key).Select(SharedEntry.Deserialize));
            } catch (Exception e) {
                Logging.Warning("Can't load history: " + e);
                return new BetterObservableCollection<SharedEntry>();
            }
        }

        private void SaveHistory() {
            ValuesStorage.Storage.SetStringList(Key, History.Select(x => x.Serialize()));
        }

        public void AddToHistory(SharedEntryType type, string name, string target, InternalUtils.ShareResult result) {
            History.Add(new SharedEntry {
                EntryType = type,
                Id = result.Id,
                RemovalKey = result.RemovalKey,
                Name = name,
                Target = target
            });
            SaveHistory();
        }

        [ItemCanBeNull]
        public static async Task<string> ShareAsync(SharedEntryType type, string name, string target, byte[] data, string customId = null,
                CancellationToken cancellation = default) {
            var authorName = SettingsHolder.Sharing.ShareAnonymously ? null : SettingsHolder.Sharing.SharingName;
            var result = await InternalUtils.ShareEntryAsync(type.ToString(), name, target, authorName, data, CmApiProvider.UserAgent, customId, cancellation);
            if (result == null || cancellation.IsCancellationRequested) {
                return null;
            }

            Instance.AddToHistory(type, name, target, result);
            return result.Url;
        }
    }
}