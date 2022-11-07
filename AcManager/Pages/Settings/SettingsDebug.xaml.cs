﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AcManager.Controls.Presentation;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Effects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;
using Microsoft.Win32;

namespace AcManager.Pages.Settings {
    [Localizable(false)]
    public partial class SettingsDebug : INotifyPropertyChanged {
        private sealed class RingStyle : Displayable {
            public string Key { get; }

            public RingStyle(string key) {
                Key = key;
                DisplayName = AcStringValues.NameFromId(key.ApartFromLast("ProgressRingStyle"));
            }
        }

        public SettingsDebug() {
            InitializeComponent();
            DataContext = new ViewModel();

            // ReSharper disable once RedundantAssignment
            ModernProgressRing popupRing = null;

#if DEBUG
            var dialog = new ModernDialog {
                Width = 100,
                Height = 100,
                ShowTopBlob = false,
                ShowTitle = false,
                Topmost = true,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                LocationAndSizeKey = "ringDev",
                Content = popupRing = new ModernProgressRing {
                    Width = 80d,
                    Height = 80d,
                    IsActive = true
                }
            };
            dialog.Show();
#endif

            ProgressRingsComboBox.SelectionChanged += (sender, args) => {
                var s = ((ComboBox)sender).SelectedItem as RingStyle;
                ModernProgressRing.Style = ExtraProgressRings.StylesLazy.GetValueOrDefault(s?.Key ?? "")?.Value;
                if (popupRing != null) {
                    popupRing.Style = ExtraProgressRings.StylesLazy.GetValueOrDefault(s?.Key ?? "")?.Value;
                }
            };

            ProgressRingsComboBox.ItemsSource = ExtraProgressRings.StylesLazy.Keys.Select(x => new RingStyle(x)).ToArray();
            ProgressRingsComboBox.SelectedItem = ProgressRingsComboBox.ItemsSource.OfType<RingStyle>().FirstOrDefault();
            ProgressRingColor = (Color)FindResource("AccentColor");

            ExtraProgressRings.StylesUpdated += (sender, args) => {
                var selected = (ProgressRingsComboBox.SelectedItem as RingStyle)?.Key;
                ProgressRingsComboBox.ItemsSource = ExtraProgressRings.StylesLazy.Keys.Select(x => new RingStyle(x)).ToArray();
                ProgressRingsComboBox.SelectedItem = ProgressRingsComboBox.ItemsSource.OfType<RingStyle>().FirstOrDefault(x => x.Key == selected);
            };
        }

        private Color _progressRingColor;

        public Color ProgressRingColor {
            get => _progressRingColor;
            set {
                if (Equals(value, _progressRingColor)) return;
                _progressRingColor = value;
                OnPropertyChanged();
                ModernProgressRing.Resources["AccentColor"] = value;
                ModernProgressRing.Resources["Accent"] = new SolidColorBrush(value);
            }
        }

        public class ViewModel : NotifyPropertyChanged {
            private ICommand _decryptHelperCommand;

            public ICommand DecryptHelperCommand {
                get {
                    return _decryptHelperCommand ?? (_decryptHelperCommand = new DelegateCommand(() => {
                        var m = Prompt.Show("DH:", "DH", placeholder: "<key>=<value>");
                        if (m == null) return;

                        var s = m.Split(new[] { '=' }, 2);
                        if (s.Length == 2) {
                            ModernDialog.ShowMessage("d: " + ValuesStorage.Storage.Decrypt(s[0], s[1]));
                        }
                    }));
                }
            }

            private DelegateCommand _decompressValuesCommand;

            public DelegateCommand DecompressValuesCommand => _decompressValuesCommand ?? (_decompressValuesCommand = new DelegateCommand(() => {
                var dialog = new OpenFileDialog {
                    Filter = "Storages (*.data)|*.data|All files (*.*)|*.*",
                    Title = "Pick storage to resave"
                };

                if (dialog.ShowDialog() == true) {
                    var copy = FileUtils.EnsureUnique(dialog.FileName);
                    File.Copy(dialog.FileName, copy);
                    new Storage(copy).ForceSave();
                    WindowsHelper.ViewFile(copy);
                }
            }));

            private AsyncCommand _asyncBaseCommand;

            public AsyncCommand AsyncBaseCommand => _asyncBaseCommand ?? (_asyncBaseCommand = new AsyncCommand(() => Task.Delay(2000)));

            private AsyncCommand<CancellationToken?> _asyncCancelCommand;

            public AsyncCommand<CancellationToken?> AsyncCancelCommand
                => _asyncCancelCommand ?? (_asyncCancelCommand = new AsyncCommand<CancellationToken?>(c => Task.Delay(2000, c ?? default(CancellationToken))));

            private AsyncCommand<IProgress<double>> _asyncProgressCommand;

            public AsyncCommand<IProgress<double>> AsyncProgressCommand
                => _asyncProgressCommand ?? (_asyncProgressCommand = new AsyncCommand<IProgress<double>>(async c => {
                    for (var i = 0; i < 100; i++) {
                        c?.Report(i / 100d);
                        await Task.Delay(16);
                    }
                }));

            private AsyncCommand<IProgress<string>> _asyncProgressMsgCommand;

            public AsyncCommand<IProgress<string>> AsyncProgressMsgCommand
                => _asyncProgressMsgCommand ?? (_asyncProgressMsgCommand = new AsyncCommand<IProgress<string>>(async c => {
                    for (var i = 0; i < 100; i++) {
                        c?.Report($"Progress: {i}%");
                        await Task.Delay(16);
                    }
                }));

            private AsyncCommand<Tuple<IProgress<double>, CancellationToken>> _asyncProgressCancelCommand;

            public AsyncCommand<Tuple<IProgress<double>, CancellationToken>> AsyncProgressCancelCommand
                => _asyncProgressCancelCommand ?? (_asyncProgressCancelCommand = new AsyncCommand<Tuple<IProgress<double>, CancellationToken>>(async t => {
                    for (var i = 0; i < 100 && !t.Item2.IsCancellationRequested; i++) {
                        t.Item1?.Report(i / 100d);
                        await Task.Delay(16, t.Item2);
                    }
                }));

            private AsyncCommand<Tuple<IProgress<AsyncProgressEntry>, CancellationToken>> _asyncProgressMsgCancelCommand;

            public AsyncCommand<Tuple<IProgress<AsyncProgressEntry>, CancellationToken>> AsyncProgressMsgCancelCommand
                =>
                        _asyncProgressMsgCancelCommand
                                ?? (_asyncProgressMsgCancelCommand = new AsyncCommand<Tuple<IProgress<AsyncProgressEntry>, CancellationToken>>(async t => {
                                    for (var i = 0; i < 100 && !t.Item2.IsCancellationRequested; i++) {
                                        t.Item1?.Report($"Progress: {i}%", i, 100);
                                        await Task.Delay(16, t.Item2);
                                    }
                                }));

            private ICommand _magickNetMemoryLeakingCommand;

            public ICommand MagickNetMemoryLeakingCommand => _magickNetMemoryLeakingCommand ?? (_magickNetMemoryLeakingCommand = new AsyncCommand(async () => {
                var image = AcPaths.GetDocumentsScreensDirectory();
                var filename = new DirectoryInfo(image).GetFiles("*.bmp")
                        .OrderByDescending(f => f.LastWriteTime)
                        .FirstOrDefault();
                if (filename == null) {
                    ModernDialog.ShowMessage("Can’t start. At least one bmp-file in screens directory required.");
                    return;
                }

                using (var w = new WaitingDialog($"Copying {filename.Name}…")) {
                    Logging.Write("TESTING MEMORY LEAKS!");

                    var from = filename.FullName;
                    var to = filename.FullName + ".jpg";
                    Logging.Write("FROM: " + from);
                    Logging.Write("TO: " + to);

                    var i = 0;
                    while (!w.CancellationToken.IsCancellationRequested) {
                        await Task.Run(() => { ImageUtils.ApplyPreviewImageMagick(from, to, 1022, 575, new AcPreviewImageInformation()); });
                        w.Report(PluralizingConverter.PluralizeExt(++i, "Copied {0} time"));
                        await Task.Yield();
                    }
                }
            }));

            private DelegateCommand _convertOutlineCommand;

            public DelegateCommand ConvertOutlineCommand => _convertOutlineCommand ?? (_convertOutlineCommand = new DelegateCommand(() => {
                var track = TracksManager.Instance.Default;
                if (track == null) return;

                var filename = track.OutlineImage.ApartFromLast(".png", StringComparison.OrdinalIgnoreCase) + "_new.png";
                var size = new Size(CommonAcConsts.TrackOutlineWidth, CommonAcConsts.TrackOutlineHeight);

                var result = new ContentPresenter {
                    Width = CommonAcConsts.TrackOutlineWidth,
                    Height = CommonAcConsts.TrackOutlineHeight,
                    Content = new Image {
                        Width = CommonAcConsts.TrackOutlineWidth,
                        Height = CommonAcConsts.TrackOutlineHeight,
                        Source = BetterImage.LoadBitmapSource(track.OutlineImage).ImageSource,
                        Effect = new InvertKeepColorEffect(),
                    },
                };

                result.Measure(size);
                result.Arrange(new Rect(size));
                result.ApplyTemplate();
                result.UpdateLayout();

                var bmp = new RenderTargetBitmap(CommonAcConsts.TrackOutlineWidth, CommonAcConsts.TrackOutlineHeight, 96, 96, PixelFormats.Pbgra32);
                bmp.Render(result);
                bmp.SaveTo(filename);
                WindowsHelper.ViewFile(filename);
            }));

            private static BitmapImage LoadBitmapImage(string filename) {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri(filename);
                bi.EndInit();
                bi.Freeze();
                return bi;
            }

            private void MakeCollage(IEnumerable<string> imageFilenames, string outputFilename) {
                var collage = new Grid();
                foreach (var filename in imageFilenames) {
                    collage.Children.Add(new Image {
                        Source = LoadBitmapImage(filename),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    });
                }

                collage.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                collage.Arrange(new Rect(0d, 0d, collage.DesiredSize.Width, collage.DesiredSize.Height));

                var bitmap = new RenderTargetBitmap((int)collage.DesiredSize.Width, (int)collage.DesiredSize.Height,
                        96, 96, PixelFormats.Default);
                bitmap.Render(collage);
                bitmap.SaveTo(outputFilename);
            }

            private DelegateCommand _testCommand;

            public DelegateCommand TestCommand => _testCommand ?? (_testCommand = new DelegateCommand(() => {
                ModernDialog.ShowMessage(SuggestionLists.CarSkinDriverNamesList.JoinToString("\n"));
                ModernDialog.ShowMessage(SuggestionLists.CarSkinTeamsList.JoinToString("\n"));

                //MakeCollage(Directory.GetFiles(@"C:\Users\Carrot\Desktop\Temp\0", "ic_*.png"), @"C:\Users\Carrot\Desktop\Temp\0\comb.png");
            }));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}