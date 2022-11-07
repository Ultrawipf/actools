﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Media;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific;
using AcTools.Render.Kn5SpecificSpecial;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.CustomShowroom {
    public partial class TrackMapRendererTools {
        private ViewModel Model => (ViewModel)DataContext;

        public TrackMapRendererTools(TrackObjectBase track, TrackMapPreparationRenderer renderer) {
            DataContext = new ViewModel(track, renderer);
            InitializeComponent();
            Buttons = new Button[0];
        }

        public class SurfaceDescription {
            public string Key { get; }

            public bool IsValidTrack { get; }

            public bool IsPitlane { get; }

            public double DirtAdditive { get; }

            public double Friction { get; }

            [CanBeNull]
            public string AudioEffect { get; }

            private SurfaceDescription(IniFileSection section) {
                Key = section.GetNonEmpty("KEY") ?? "";
                IsValidTrack = section.GetBool("IS_VALID_TRACK", false);
                IsPitlane = section.GetBool("IS_PITLANE", false);
                DirtAdditive = section.GetDouble("DIRT_ADDITIVE", 0.0);
                Friction = section.GetDouble("FRICTION", 0.8);
                AudioEffect = section.GetNonEmpty("WAV");
            }

            private string _description;

            public string Description => _description ?? (_description = new[] {
                $"Grip: {Friction * 100d:F1}%",
                IsPitlane ? "pitlane" : null,
                IsValidTrack ? null : "offroad",
                DirtAdditive > 0 ? "dirt" : null,
                AudioEffect == null ? null : "has sound"
            }.NonNull().JoinToString(@"; "));

            public bool ShouldBeVisibleOnMap() {
                return IsValidTrack && Friction > 0.9 && !string.Equals(AudioEffect, @"kerb.wav", StringComparison.OrdinalIgnoreCase);
            }

            public static IEnumerable<SurfaceDescription> Load(string filename) {
                return new IniFile(filename).GetSections("SURFACE")
                        .Where(x => x.GetNonEmpty("KEY") != null)
                        .Select(x => new SurfaceDescription(x));
            }

            public static IEnumerable<SurfaceDescription> LoadDefault() {
                var root = AcRootDirectory.Instance.Value;
                if (root == null) return new SurfaceDescription[0];
                return Load(Path.Combine(root, @"system", @"data", @"surfaces.ini"));
            }

            public static IEnumerable<SurfaceDescription> LoadAll(string filename) {
                return Load(filename).Concat(LoadDefault()).Distinct(new DistinctComparer()).OrderBy(x => x.Key);
            }

            private class DistinctComparer : IEqualityComparer<SurfaceDescription> {
                public bool Equals(SurfaceDescription x, SurfaceDescription y) {
                    return x?.Key.Equals(y?.Key, StringComparison.Ordinal) == true;
                }

                public int GetHashCode(SurfaceDescription obj) {
                    return obj.Key.GetHashCode();
                }
            }
        }

        public class ViewModel : NotifyPropertyChanged, INotifyDataErrorInfo, ITrackMapRendererFilter {
            public TrackObjectBase Track { get; }

            public TrackMapPreparationRenderer Renderer { get; }

            public bool SurfaceMode => !Renderer.AiLaneMode;

            private Regex _regex;

            [CanBeNull]
            private readonly ISaveHelper _save;

            private class SaveableData {
                public string Filter;
                public bool IgnoreCase, AiLaneActualWidth;
                public double Scale = 1.0, Margin = 10d;
            }

            [CanBeNull]
            private readonly ISaveHelper _saveShared;

            private class SaveableSharedData {
                public bool UseFxaa = true, ShowPitlane, ShowSpecialMarks, ShowAiPitLaneMarks;
                public double AiLaneWidth = 10d, AiPitLaneWidth = 6d, SpecialMarksWidth = 20d, SpecialMarksThickness = 6d;
                public Color AiPitLaneColor = Color.FromArgb(255, 100, 100, 100);
            }

            public ViewModel(TrackObjectBase track, TrackMapPreparationRenderer renderer) {
                Track = track;
                Renderer = renderer;
                Renderer.SetFilter(this);

                Surfaces = SurfaceDescription.LoadAll(Path.Combine(track.DataDirectory, "surfaces.ini")).ToList();

                _save = new SaveHelper<SaveableData>(".TrackMapRendererTools:" + track.Id, () => new SaveableData {
                    Filter = Filter,
                    IgnoreCase = FilterIgnoreCase,
                    Scale = Scale,
                    Margin = Margin,
                    AiLaneActualWidth = AiLaneActualWidth,
                }, o => {
                    if (o.Filter == null) {
                        UpdateFilter(Surfaces.Where(x => x.ShouldBeVisibleOnMap()));
                    } else {
                        Filter = o.Filter;
                    }

                    FilterIgnoreCase = o.IgnoreCase;
                    Scale = o.Scale;
                    Margin = o.Margin;
                    AiLaneActualWidth = o.AiLaneActualWidth;
                }, storage: CacheStorage.Storage);
                _saveShared = new SaveHelper<SaveableSharedData>(".TrackMapRendererTools", () => new SaveableSharedData {
                    UseFxaa = UseFxaa,
                    AiLaneWidth = AiLaneWidth,
                    ShowPitlane = ShowPitlane,
                    ShowSpecialMarks = ShowSpecialMarks,
                    AiPitLaneWidth = AiPitLaneWidth,
                    AiPitLaneColor = AiPitLaneColor,
                    ShowAiPitLaneMarks = ShowAiPitLaneMarks,
                    SpecialMarksWidth = SpecialMarksWidth,
                    SpecialMarksThickness = SpecialMarksThickness,
                }, o => {
                    UseFxaa = o.UseFxaa;
                    AiLaneWidth = o.AiLaneWidth;
                    ShowPitlane = o.ShowPitlane;
                    ShowSpecialMarks = o.ShowSpecialMarks;
                    AiPitLaneWidth = o.AiPitLaneWidth;
                    AiPitLaneColor = o.AiPitLaneColor;
                    ShowAiPitLaneMarks = o.ShowAiPitLaneMarks;
                    SpecialMarksWidth = o.SpecialMarksWidth;
                    SpecialMarksThickness = o.SpecialMarksThickness;
                });
                _save.Initialize();
                _saveShared.Initialize();
            }

            private bool _nonUserChange;
            private string _userFilter;

            public void UpdateFilter(IEnumerable<SurfaceDescription> ofType) {
                _nonUserChange = true;
                var surfaces = ofType.Select(x => x.Key).Where(x => x.Length > 0).ToList();
                if (surfaces.Count > 0) {
                    var digitSurfaces = surfaces.Where(x => char.IsDigit(x[0])).Select(Regex.Escape).JoinToString('|');
                    var digitlessSurfaces = surfaces.Where(x => !char.IsDigit(x[0])).Select(Regex.Escape).JoinToString('|');
                    if (digitSurfaces.Contains('|')) digitSurfaces = $"({digitSurfaces})";
                    if (digitlessSurfaces.Contains('|')) digitlessSurfaces = $"({digitlessSurfaces})";
                    Filter = new[] {
                        string.IsNullOrEmpty(digitSurfaces) ? null : $@"\d*{digitSurfaces}",
                        string.IsNullOrEmpty(digitlessSurfaces) ? null : $@"\d+{digitlessSurfaces}"
                    }.NonNull().JoinToString('|');
                } else {
                    Filter = _userFilter ?? @"\d+(ROAD|ASPHALT)";
                }
                _nonUserChange = false;
            }

            public List<SurfaceDescription> Surfaces { get; }

            private string _filterError;

            [CanBeNull]
            public string FilterError {
                set {
                    if (Equals(value, _filterError)) return;
                    _filterError = value;
                    OnErrorsChanged(nameof(Filter));
                }
            }

            private void RecreateFilter() {
                try {
                    _regex = new Regex(Filter, FilterIgnoreCase ? RegexOptions.Compiled | RegexOptions.IgnoreCase : RegexOptions.Compiled);
                    FilterError = null;
                } catch (Exception e) {
                    var s = e.Message.Split(new[] { @" - " }, 2, StringSplitOptions.None);
                    FilterError = s.Length == 2 ? s[1] : e.Message;
                }

                Renderer.Update();
            }

            private string _filter;

            public string Filter {
                get => _filter;
                set {
                    if (Equals(value, _filter)) return;

                    if (!_nonUserChange) {
                        _userFilter = value;
                    }

                    _filter = value;
                    OnPropertyChanged();
                    RecreateFilter();

                    _save?.SaveLater();
                }
            }

            private bool _filterIgnoreCase;

            public bool FilterIgnoreCase {
                get => _filterIgnoreCase;
                set {
                    if (Equals(value, _filterIgnoreCase)) return;
                    _filterIgnoreCase = value;
                    OnPropertyChanged();
                    RecreateFilter();

                    _save?.SaveLater();
                }
            }

            public IEnumerable GetErrors(string propertyName) {
                switch (propertyName) {
                    case nameof(Filter):
                        return string.IsNullOrWhiteSpace(_filterError) ? null : new[] { _filterError };
                    default:
                        return null;
                }
            }

            public bool HasErrors => !string.IsNullOrWhiteSpace(_filterError);
            public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

            public void OnErrorsChanged([CallerMemberName] string propertyName = null) {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            }

            bool ITrackMapRendererFilter.Filter(string name) {
                return _regex?.IsMatch(name ?? "") != false;
            }

            private bool _useFxaa;

            public bool UseFxaa {
                get => _useFxaa;
                set {
                    if (Equals(value, _useFxaa)) return;
                    _useFxaa = value;
                    OnPropertyChanged();
                    Renderer.UseFxaa = value;
                    Renderer.IsDirty = true;
                    _saveShared?.SaveLater();
                }
            }

            private double _margin;

            public double Margin {
                get => _margin;
                set {
                    value = value.Clamp(0, 200);
                    if (Equals(value, _margin)) return;
                    _margin = value;
                    OnPropertyChanged();
                    Renderer.Margin = (float)value;
                    Renderer.IsDirty = true;
                    _save?.SaveLater();
                }
            }

            private double _scale;

            public double Scale {
                get => _scale;
                set {
                    value = value.Clamp(0.00001, 100);
                    if (Equals(value, _scale)) return;
                    _scale = value;
                    OnPropertyChanged();
                    Renderer.Scale = (float)value;
                    Renderer.IsDirty = true;
                    _save?.SaveLater();
                }
            }

            private double _aiLaneWidth;

            public double AiLaneWidth {
                get => _aiLaneWidth;
                set {
                    value = value.Clamp(0, 200);
                    if (Equals(value, _aiLaneWidth)) return;
                    _aiLaneWidth = value;
                    OnPropertyChanged();
                    Renderer.AiLaneWidth = (float)value;
                    _saveShared?.SaveLater();
                }
            }

            private bool _aiLaneActualWidth;

            public bool AiLaneActualWidth {
                get => _aiLaneActualWidth;
                set {
                    if (Equals(value, _aiLaneActualWidth)) return;
                    _aiLaneActualWidth = value;
                    OnPropertyChanged();
                    Renderer.AiLaneActualWidth = value;
                    _save?.SaveLater();
                }
            }

            private bool _showPitlane;

            public bool ShowPitlane {
                get => _showPitlane;
                set => Apply(value, ref _showPitlane, () => {
                    Renderer.ShowPitlane = value;
                    _saveShared?.SaveLater();
                });
            }

            private double _aiPitLaneWidth;

            public double AiPitLaneWidth {
                get => _aiPitLaneWidth;
                set => Apply(value, ref _aiPitLaneWidth, () => {
                    Renderer.AiPitLaneWidth = (float)value;
                    _saveShared?.SaveLater();
                });
            }

            private Color _aiPitLaneColor;

            public Color AiPitLaneColor {
                get => _aiPitLaneColor;
                set => Apply(value, ref _aiPitLaneColor, () => {
                    Renderer.AiPitLaneColor = value.ToColor().ToVector3();
                    _saveShared?.SaveLater();
                });
            }

            private bool _showSpecialMarks;

            public bool ShowSpecialMarks {
                get => _showSpecialMarks;
                set => Apply(value, ref _showSpecialMarks, () => {
                    Renderer.ShowSpecialMarks = value;
                    _saveShared?.SaveLater();
                });
            }

            private bool _showAiPitLaneMarks;

            public bool ShowAiPitLaneMarks {
                get => _showAiPitLaneMarks;
                set => Apply(value, ref _showAiPitLaneMarks, () => {
                    Renderer.ShowAiPitLaneMarks = value;
                    _saveShared?.SaveLater();
                });
            }

            private double _specialMarksWidth;

            public double SpecialMarksWidth {
                get => _specialMarksWidth;
                set => Apply(value, ref _specialMarksWidth, () => {
                    Renderer.SpecialMarksWidth = (float)value;
                    _saveShared?.SaveLater();
                });
            }

            private double _specialMarksThickness;

            public double SpecialMarksThickness {
                get => _specialMarksThickness;
                set => Apply(value, ref _specialMarksThickness, () => {
                    Renderer.SpecialMarksThickness = (float)value;
                    _saveShared?.SaveLater();
                });
            }

            private DelegateCommand _cameraToStartCommand;

            public DelegateCommand CameraToStartCommand
                => _cameraToStartCommand ?? (_cameraToStartCommand = new DelegateCommand(() => Renderer.MoveCameraToStart()));

            private DelegateCommand _resetCameraCommand;

            public DelegateCommand ResetCameraCommand
                => _resetCameraCommand ?? (_resetCameraCommand = new DelegateCommand(() => ((IKn5ObjectRenderer)Renderer).ResetCamera()));

            private DelegateCommand _saveCommand;

            public DelegateCommand SaveCommand => _saveCommand ?? (_saveCommand = new DelegateCommand(() => {
                var mapPng = Track.MapImage;
                if (File.Exists(mapPng)) {
                    FileUtils.Recycle(mapPng);
                }

                Renderer.Shot(mapPng);

                var mapIni = Path.Combine(Track.DataDirectory, "map.ini");
                if (File.Exists(mapIni)) {
                    FileUtils.Recycle(mapIni);
                }

                Renderer.SaveInformation(mapIni);
            }));
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            Model.UpdateFilter(SurfacesListBox.SelectedItems.OfType<SurfaceDescription>());
        }
    }
}