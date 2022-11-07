using System.Collections.Generic;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Tools.Helpers.AcSettings {
    public class AudioSettings : IniPresetableSettings {
        public static string DefaultDeviceName => "System Default";

        internal AudioSettings() : base("audio") { }

        private int _skidsEntryPoint;

        public int SkidsEntryPoint {
            get => _skidsEntryPoint;
            set => Apply(value.Clamp(0, 200), ref _skidsEntryPoint);
        }

        private string _endPointName;

        public string EndPointName {
            get => _endPointName;
            set => Apply(value, ref _endPointName);
        }

        #region Levels
        private double _levelMaster;

        public double LevelMaster {
            get => _levelMaster;
            set => Apply(value.Clamp(0, 100d), ref _levelMaster);
        }

        private double _levelTyres;

        public double LevelTyres {
            get => _levelTyres;
            set => Apply(value.Clamp(0, 100d), ref _levelTyres);
        }

        private double _levelEngine;

        private double _levelBrakes;

        public double LevelBrakes {
            get => _levelBrakes;
            set => Apply(value.Clamp(0, 100d), ref _levelBrakes);
        }

        private double _levelDirtBottom;

        public double LevelDirtBottom {
            get => _levelDirtBottom;
            set => Apply(value.Clamp(0, 100d), ref _levelDirtBottom);
        }

        public double LevelEngine {
            get => _levelEngine;
            set => Apply(value.Clamp(0, 100d), ref _levelEngine);
        }

        private double _levelSurfaces;

        public double LevelSurfaces {
            get => _levelSurfaces;
            set => Apply(value.Clamp(0, 100d), ref _levelSurfaces);
        }

        private double _levelWind;

        public double LevelWind {
            get => _levelWind;
            set => Apply(value.Clamp(0, 100d), ref _levelWind);
        }

        private double _levelOpponents;

        public double LevelOpponents {
            get => _levelOpponents;
            set => Apply(value.Clamp(0, 100d), ref _levelOpponents);
        }

        private double _levelUi;

        public double LevelUi {
            get => _levelUi;
            set => Apply(value.Clamp(0, 100d), ref _levelUi);
        }
        #endregion

        private Dictionary<string, CustomItem> _customItems = new Dictionary<string, CustomItem>();

        public class CustomItem : Displayable {
            public string Id { get; set; }
            public double DefaultValue { get; set; }

            private double _value;

            public double Value {
                get => _value;
                set => Apply(value, ref _value);
            }
        }

        public CustomItem GetItem(string id, string displayName = null, double defaultValue = -1) {
            var item = _customItems.GetValueOrSet(id, () => {
                var ret = new CustomItem { Id = id, Value = Ini?["LEVELS_EXT"].GetDouble(id, defaultValue) * 100d ?? 100d };
                ret.PropertyChanged += (sender, args) => Save();
                return ret;
            });
            if (displayName != null) item.DisplayName = displayName;
            if (defaultValue != -1) item.DefaultValue = defaultValue;
            return item;
        }

        protected override void LoadFromIni() {
            EndPointName = Ini["SETTINGS"].GetNonEmpty("DRIVER_NAME").Or(DefaultDeviceName);

            var levels = Ini["LEVELS"];
            LevelMaster = levels.GetDouble("MASTER", 1.0) * 100d;
            LevelTyres = levels.GetDouble("TYRES", 0.8) * 100d;
            LevelBrakes = levels.GetDouble("BRAKES", 0.8) * 100d;
            LevelEngine = levels.GetDouble("ENGINE", 1.0) * 100d;
            LevelSurfaces = levels.GetDouble("SURFACES", 1.0) * 100d;
            LevelWind = levels.GetDouble("WIND", 0.9) * 100d;
            LevelOpponents = levels.GetDouble("OPPONENTS", 0.9) * 100d;
            LevelDirtBottom = levels.GetDouble("DIRT_BOTTOM", 1.0) * 100d;
            LevelUi = levels.GetDouble("UISOUNDS", 0.7) * 100d;

            // Latency = Ini["SETTINGS"].GetEntry("LATENCY", Latencies, 1);
            SkidsEntryPoint = Ini["SKIDS"].GetInt("ENTRY_POINT", 100);

            var levelsExt = Ini["LEVELS_EXT"];
            foreach (var p in levelsExt.Keys) {
                var item = GetItem(p);
                item.Value = levelsExt.GetDouble(p, item.DefaultValue) * 100d;
            }
        }

        protected override void SetToIni(IniFile ini) {
            ini["SETTINGS"].SetOrRemove("DRIVER_NAME", EndPointName == DefaultDeviceName ? null : EndPointName);

            var levels = ini["LEVELS"];
            levels.Set("MASTER", LevelMaster / 100d);
            levels.Set("TYRES", LevelTyres / 100d);
            levels.Set("BRAKES", LevelBrakes / 100d);
            levels.Set("ENGINE", LevelEngine / 100d);
            levels.Set("SURFACES", LevelSurfaces / 100d);
            levels.Set("WIND", LevelWind / 100d);
            levels.Set("OPPONENTS", LevelOpponents / 100d);
            levels.Set("DIRT_BOTTOM", LevelDirtBottom / 100d);
            levels.Set("UISOUNDS", LevelUi / 100d);

            // Ini["SETTINGS"].Set("LATENCY", Latency);
            ini["SKIDS"].Set("ENTRY_POINT", SkidsEntryPoint);

            var levelsExt = Ini["LEVELS_EXT"];
            foreach (var p in _customItems) {
                levelsExt.Set(p.Key, p.Value.Value / 100d);
            }
        }

        protected override void InvokeChanged() {
            AcSettingsHolder.AudioPresetChanged();
        }
    }
}