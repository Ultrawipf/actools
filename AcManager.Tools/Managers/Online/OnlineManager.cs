using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Lists;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public partial class OnlineManager : NotifyPropertyChanged {
        private static OnlineManager _instance;

        public static OnlineManager Instance => _instance ?? (_instance = new OnlineManager());

        public ChangeableObservableCollection<ServerEntry> List { get; } = new ChangeableObservableCollection<ServerEntry>();

        private readonly HoldedList<ServerEntry> _holdedList = new HoldedList<ServerEntry>(4);
        private readonly List<ServerEntry> _removeWhenReleased = new List<ServerEntry>(2);

        private OnlineManager() {
            _holdedList.Released += OnHoldedListReleased;

            CarsManager.Instance.WrappersList.CollectionReady += OnCarsListCollectionReady;
            CarsManager.Instance.WrappersList.ItemPropertyChanged += OnCarPropertyChanged;
            CarSkinsManager.AnySkinsCollectionReady += OnAnySkinsCollectionReady;
            TracksManager.Instance.WrappersList.CollectionReady += OnTracksListCollectionReady;
            TracksManager.Instance.WrappersList.ItemPropertyChanged += OnTrackPropertyChanged;
            WeatherManager.Instance.WrappersList.CollectionReady += OnWeatherListCollectionReady;
            SettingsHolder.Content.PropertyChanged += OnContentPropertyChanged;

            LoadAvailableIds().Forget();
        }

        private void OnContentPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs) {
            if (propertyChangedEventArgs.PropertyName == nameof(SettingsHolder.ContentSettings.MissingContentIndexCheck)) {
                if (_availableIdsLoaded) {
                    UpdateMissing();
                } else {
                    LoadAvailableIds().Forget();
                }
            }
        }

        private void UpdateMissing() {
            foreach (var entry in List) {
                entry.UpdateMissing();
            }
        }

        private static bool _availableIdsLoaded;

        private static async Task LoadAvailableIds() {
            if (_availableIdsLoaded) return;
            _availableIdsLoaded = true;

            try {
                var ids = await CmApiProvider.GetContentAsync<Dictionary<string, string[]>>();
                if (ids == null) {
                    Logging.Warning("Can’t load lists of available-to-download IDs");
                    return;
                }

                AvailableCarIds = ids.GetValueOrDefault("cars");
                AvailableTrackIds = ids.GetValueOrDefault("tracks");
                _instance?.UpdateMissing();
            } catch (Exception e) {
                Logging.Warning(e);
            }
        }

        private static string[] AvailableCarIds { get; set; }
        private static string[] AvailableTrackIds { get; set; }

        public static bool IsCarAvailable(string carId) {
            return AvailableCarIds?.Contains(carId) == true;
        }

        public static bool IsTrackAvailable(string trackId) {
            return AvailableTrackIds?.Contains(trackId.Replace(@"/", @"-")) == true;

            /*var ids = AvailableTrackIds;
            if (ids == null) return false;

            if (Array.IndexOf(ids, trackId) != -1) return true;

            var i = trackId.IndexOf('/');
            if (i == -1) return false;

            var c = trackId.Substring(0, i);
            if (Array.IndexOf(ids, c) != -1) return true;

            for (var j = 0; j < 100; j++) {
                var k = trackId.IndexOf('-', i + 1);
                if (k == -1) return false;

                i = k;
                c = trackId.Substring(0, i).Replace('/', '-');
                if (Array.IndexOf(ids, c) != -1) return true;
            }

            return false;*/
        }

        private void OnAnySkinsCollectionReady(object sender, SkinsCollectionReadyEventArgs e) {
            if (e.JustReady) return;

            for (var i = List.Count - 1; i >= 0; i--) {
                List[i].CheckCarSkins(e.CarId);
            }
        }

        private void OnCarPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(CarObject.Version)) return;

            var car = (CarObject)sender;
            for (var i = List.Count - 1; i >= 0; i--) {
                List[i].OnCarVersionChanged(car);
            }
        }

        private void OnTrackPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(TrackObjectBase.Version)) return;

            var track = (TrackObjectBase)sender;
            for (var i = List.Count - 1; i >= 0; i--) {
                List[i].OnTrackVersionChanged(track);
            }
        }

        private void OnCarsListCollectionReady(object sender, CollectionReadyEventArgs e) {
            if (e.JustReady) return;

            var dirty = false;
            for (var i = List.Count - 1; i >= 0; i--) {
                dirty |= List[i].CheckCars();
            }

            if (dirty) {
                PingEverything(null).Forget();
            }
        }

        private void OnTracksListCollectionReady(object sender, CollectionReadyEventArgs e) {
            if (e.JustReady) return;

            var dirty = false;
            for (var i = List.Count - 1; i >= 0; i--) {
                dirty |= List[i].CheckTrack();
            }

            if (dirty) {
                PingEverything(null).Forget();
            }
        }

        private void OnWeatherListCollectionReady(object sender, CollectionReadyEventArgs e) {
            if (e.JustReady) return;

            var dirty = false;
            for (var i = List.Count - 1; i >= 0; i--) {
                dirty |= List[i].CheckWeather();
            }

            if (dirty) {
                PingEverything(null).Forget();
            }
        }

        private void OnHoldedListReleased(object sender, ReleasedEventArgs<ServerEntry> e) {
            var index = _removeWhenReleased.IndexOf(e.Value);
            if (index != -1) {
                _removeWhenReleased.RemoveAt(index);
                List.Remove(e.Value);
            }
        }

        [CanBeNull]
        public ServerEntry GetById(string id) {
            return List.GetByIdOrDefault(id);
        }

        [CanBeNull]
        public Holder<ServerEntry> HoldById(string id) {
            return _holdedList.Get(GetById(id));
        }

        public bool IsHolded(ServerEntry entry) {
            return _holdedList.Contains(entry);
        }

        public void RemoveWhenReleased(ServerEntry entry) {
            _removeWhenReleased.Add(entry);
        }

        public void AvoidRemoval(ServerEntry entry) {
            _removeWhenReleased.Remove(entry);
        }

        /// <summary>
        /// Throws an exception.
        /// </summary>
        /// <returns>Null if the request was cancelled.</returns>
        [ItemCanBeNull]
        public async Task<IReadOnlyList<ServerInformationComplete>> ScanForServers(string address, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            if (address == null) throw new ArgumentNullException(nameof(address));

            // assume address is something like [HOSTNAME]:[HTTP PORT]
            string ip;
            int port;
            if (!KunosApiProvider.ParseAddress(address, out ip, out port)) {
                throw new Exception(ToolsStrings.Online_CannotParseAddress);
            }

            if (port > 0) {
                progress?.Report(AsyncProgressEntry.FromStringIndetermitate(ToolsStrings.Online_GettingInformationDirectly));

                ServerInformationComplete information;

                try {
                    information = await KunosApiProvider.GetInformationDirectAsync(ip, port);
                } catch (WebException) {
                    if (cancellation.IsCancellationRequested) return null;

                    // assume address is [HOSTNAME]:[TCP PORT]
                    progress?.Report(AsyncProgressEntry.FromStringIndetermitate(ToolsStrings.Online_TryingToFindOutHttpPort));
                    var pair = await KunosApiProvider.TryToPingServerAsync(ip, port, SettingsHolder.Online.PingTimeout);
                    if (cancellation.IsCancellationRequested) return null;

                    if (pair != null) {
                        progress?.Report(AsyncProgressEntry.FromStringIndetermitate(ToolsStrings.Online_GettingInformationDirectly_SecondAttempt));

                        try {
                            information = await KunosApiProvider.GetInformationDirectAsync(ip, pair.Item1);
                        } catch (WebException) {
                            information = null;
                        }
                    } else {
                        information = null;
                    }
                }

                if (cancellation.IsCancellationRequested) return null;
                return information == null ? new ServerInformationComplete[0] : new [] { information };
            } else {
                var result = new List<ServerInformationComplete>();

                // assume address is [HOSTNAME]
                progress?.Report(AsyncProgressEntry.FromStringIndetermitate(ToolsStrings.Common_Scanning));

                var scanned = 0;
                var total = SettingsHolder.Online.PortsEnumeration.ToPortsDiapason().Count();

                await SettingsHolder.Online.PortsEnumeration.ToPortsDiapason().Select(async p => {
                    var pair = await KunosApiProvider.TryToPingServerAsync(ip, p, SettingsHolder.Online.ScanPingTimeout);
                    if (pair != null && pair.Item1 > 1024 && pair.Item1 < 65536) {
                        if (cancellation.IsCancellationRequested) return;

                        try {
                            var information = await KunosApiProvider.GetInformationDirectAsync(ip, pair.Item1);
                            if (cancellation.IsCancellationRequested) return;
                            result.Add(information);
                        } catch (WebException) { }
                    }

                    scanned++;
                    progress?.Report(new AsyncProgressEntry(string.Format(ToolsStrings.Online_ScanningProgress, scanned, total,
                            PluralizingConverter.PluralizeExt(result.Count, ToolsStrings.Online_ScanningProgress_Found)), scanned, total));
                }).WhenAll(200, cancellation);

                return result;
            }
        }
    }
}