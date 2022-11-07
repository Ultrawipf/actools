﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.AcManagersNew {
    /// <summary>
    /// AcManager for files (but without watching).
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class FileAcManager<T> : BaseAcManager<T>, IFileAcManager where T : AcCommonObject {
        protected FileAcManager() {
            Superintendent.Instance.Closing += Superintendent_Closing;
            Superintendent.Instance.SavingAll += SuperintendentSavingAll;
        }

        private void SuperintendentSavingAll(object sender, EventArgs e) {
            foreach (var item in InnerWrappersList.Select(x => x.Value).OfType<T>().Where(x => x.Changed)) {
                item.SaveAsync();
            }
        }

        private void Superintendent_Closing(object sender, Superintendent.ClosingEventArgs e) {
            foreach (var item in InnerWrappersList.Select(x => x.Value).OfType<T>().Where(x => x.Changed)) {
                Logging.Debug(item);
                e.Add(item.DisplayName);
            }
        }

        [CanBeNull]
        public abstract IAcDirectories Directories { get; }

        protected bool Filter(string filename) {
            if (Directories == null) return false;
            return Filter(Directories.GetId(filename), filename);
        }

        protected override IEnumerable<AcPlaceholderNew> ScanOverride() {
            var directories = Directories;
            if (directories == null) return new AcPlaceholderNew[0];
            return directories.GetContentDirectories().Select(dir => {
                var id = directories.GetId(dir);
                return Filter(id, dir) ? CreateAcPlaceholder(id, directories.CheckIfEnabled(dir)) : null;
            }).NonNull();
        }

        /// <summary>
        /// Returns comment — why ID is invalid.
        /// </summary>
        [Pure]
        protected virtual string CheckIfIdValid(string id) {
            return null;
        }

        protected virtual void AssertId(string id) {
            var message = CheckIfIdValid(id);
            if (message != null) {
                throw new InformativeException("Invalid ID: " + id, message);
            }
        }

        protected virtual async Task MoveOverrideAsync(string oldId, string newId, string oldLocation, string newLocation,
                IEnumerable<Tuple<string, string>> attachedOldNew, bool newEnabled) {
            AssertId(newId);

            await Task.Run(() => {
                FileUtils.Move(oldLocation, newLocation);
                foreach (var tuple in attachedOldNew.Where(x => FileUtils.Exists(x.Item1))) {
                    FileUtils.Move(tuple.Item1, tuple.Item2);
                }
            });

            var obj = CreateAndLoadAcObject(newId, newEnabled);
            obj.PreviousId = oldId;
            ReplaceInList(oldId, new AcItemWrapper(this, obj));
        }

        protected virtual async Task CloneOverrideAsync(string oldId, string newId, string oldLocation, string newLocation,
                IEnumerable<Tuple<string, string>> attachedOldNew, bool newEnabled) {
            AssertId(newId);

            await Task.Run(() => {
                FileUtils.Copy(oldLocation, newLocation);
                foreach (var tuple in attachedOldNew.Where(x => FileUtils.Exists(x.Item1))) {
                    FileUtils.Copy(tuple.Item1, tuple.Item2);
                }
            });

            AddInList(new AcItemWrapper(this, CreateAndLoadAcObject(newId, newEnabled)));
        }

        protected virtual async Task DeleteOverrideAsync(string id, string location, IEnumerable<string> attached) {
            await Task.Run(() => FileUtils.RecycleVisible(attached.Prepend(location).ToArray()));
            if (!FileUtils.Exists(location)) {
                RemoveFromList(id);
            }
        }

        /// <summary>
        /// Delete several entries at once (recycling is a very slow operation, so it’s better to make it only once
        /// for all entries requred to be removed).
        /// </summary>
        /// <param name="list">Tuple is (ID, location, attached)</param>
        protected virtual async Task DeleteOverrideAsync(IEnumerable<Tuple<string, string, IEnumerable<string>>> list) {
            var actualList = list.ToList();
            await Task.Run(() => FileUtils.RecycleVisible(actualList.SelectMany(x => x.Item3.Prepend(x.Item2)).ToArray()));
            foreach (var e in actualList) {
                if (!FileUtils.Exists(e.Item2)) {
                    RemoveFromList(e.Item1);
                }
            }
        }

        protected virtual async Task CleanSpaceOverrideAsync(string id, string location) {
            AssertId(id);
            await Task.Run(() => FileUtils.RecycleVisible(GetAttachedFiles(location).Prepend(location).ToArray()));
            if (!FileUtils.Exists(location)) {
                RemoveFromList(id);
            }
        }

        [ItemCanBeNull, NotNull]
        public virtual IEnumerable<string> GetAttachedFiles(string location) {
            return new string[0];
        }

        public async Task RenameAsync([NotNull] string oldId, [NotNull] string newId, bool newEnabled) {
            var directories = Directories;
            if (directories?.Actual != true) return;
            if (oldId == null) {
                throw new ArgumentNullException(nameof(oldId));
            }

            if (GetWrapperById(newId)?.Value.Enabled == newEnabled) {
                throw new ToggleException("Object with the same ID already exists.");
            }

            // find object which is being renamed
            var wrapper = GetWrapperById(oldId);
            var obj = wrapper?.Value as T;
            if (obj == null) {
                throw new ArgumentException(ToolsStrings.AcObject_IdIsWrong, nameof(oldId));
            }

            // files to move
            var currentLocation = obj.Location;
            var newLocation = directories.GetLocation(newId, newEnabled);
            if (FileUtils.Exists(newLocation)) throw new ToggleException(ToolsStrings.AcObject_PlaceIsTaken);

            var currentAttached = GetAttachedFiles(currentLocation).NonNull().ToList();
            var newAttached = GetAttachedFiles(newLocation).NonNull().ToList();
            if (newAttached.Any(FileUtils.Exists)) {
                throw new ToggleException(ToolsStrings.AcObject_PlaceIsTaken);
            }

            // let’s move!
            try {
                await MoveOverrideAsync(oldId, newId, currentLocation, newLocation, currentAttached.Zip(newAttached, Tuple.Create), newEnabled);
                AcObjectNew.MoveRatings<T>(oldId, newId, false);
            } catch (InformativeException) {
                throw;
            } catch (Exception e) {
                throw new ToggleException(e.Message);
            }
        }

        public async Task CloneAsync(string oldId, string newId, bool newEnabled) {
            var directories = Directories;
            if (directories?.Actual != true) return;
            if (oldId == null) throw new ArgumentNullException(nameof(oldId));

            // find object which is being renamed
            var wrapper = GetWrapperById(oldId);
            var obj = wrapper?.Value as T;
            if (obj == null) throw new ArgumentException(ToolsStrings.AcObject_IdIsWrong, nameof(oldId));

            // files to move
            var currentLocation = obj.Location;
            string newLocation;
            try {
                newLocation = directories.GetLocation(newId, newEnabled);
            } catch (Exception) {
                throw new InformativeException(ToolsStrings.Common_CannotDo, ToolsStrings.AcObject_DisablingNotSupported_Commentary);
            }

            if (FileUtils.Exists(newLocation)) throw new ToggleException(ToolsStrings.AcObject_PlaceIsTaken);

            var currentAttached = GetAttachedFiles(currentLocation).NonNull().ToList();
            var newAttached = GetAttachedFiles(newLocation).NonNull().ToList();
            if (newAttached.Any(FileUtils.Exists)) throw new ToggleException(ToolsStrings.AcObject_PlaceIsTaken);

            // let’s move!
            try {
                await CloneOverrideAsync(oldId, newId, currentLocation, newLocation, currentAttached.Zip(newAttached, Tuple.Create), newEnabled);
                AcObjectNew.MoveRatings<T>(oldId, newId, true);
            } catch (InformativeException) {
                throw;
            } catch (Exception e) {
                throw new ToggleException(e.Message);
            }
        }

        public Task ToggleAsync(string id, bool? enabled = null) {
            var directories = Directories;
            if (directories?.Actual != true) return Task.Delay(0);
            if (id == null) throw new ArgumentNullException(nameof(id));

            var wrapper = GetWrapperById(id);
            if (wrapper == null) {
                Logging.Warning($"Not found: {id}");
                throw new ArgumentException(ToolsStrings.AcObject_IdIsWrong, nameof(id));
            }

            return enabled == wrapper.Value.Enabled ? Task.Delay(0) :
                RenameAsync(id, id, enabled ?? !wrapper.Value.Enabled);
        }

        public async Task ToggleAsync(IEnumerable<string> ids, bool? enabled = null) {
            var directories = Directories;
            if (directories?.Actual != true) return;
            if (ids == null) throw new ArgumentNullException(nameof(ids));

            var objs = ids.Select(GetWrapperById).ToList();
            if (objs.Contains(null)) throw new ArgumentException(ToolsStrings.AcObject_IdIsWrong, nameof(ids));

            try {
                foreach (var wrapper in objs) {
                    if (enabled == wrapper.Value.Enabled) continue;
                    await RenameAsync(wrapper.Id, wrapper.Id, enabled ?? !wrapper.Value.Enabled);
                }
            } catch (Exception ex) {
                NonfatalError.Notify(ToolsStrings.AcObject_CannotDelete, ToolsStrings.AcObject_CannotToggle_Commentary, ex);
            }
        }

        public virtual Task DeleteAsync([NotNull] string id) {
            var directories = Directories;
            if (directories?.Actual != true) return Task.Delay(0);
            if (id == null) throw new ArgumentNullException(nameof(id));

            var obj = GetById(id);
            if (obj == null) throw new ArgumentException(ToolsStrings.AcObject_IdIsWrong, nameof(id));

            return DeleteOverrideAsync(id, obj.Location, GetAttachedFiles(obj.Location).NonNull());
        }

        public async Task DeleteAsync([NotNull] IEnumerable<string> ids) {
            var directories = Directories;
            if (directories?.Actual != true) return;
            if (ids == null) throw new ArgumentNullException(nameof(ids));

            var objs = (await ids.Select(GetByIdAsync).WhenAll(10)).ToList();
            if (objs.Contains(null)) throw new ArgumentException(ToolsStrings.AcObject_IdIsWrong, nameof(ids));

            try {
                if (MessageDialog.Show(
                        string.Format("Are you sure you want to move {0} to the Recycle Bin?", objs.Select(x => x.DisplayName).JoinToReadableString()),
                        "Are You Sure?", MessageBoxButton.YesNo, new MessageDialog.ShowMessageCallbacks(
                                () => SettingsHolder.Content.DeleteConfirmation ? (MessageBoxResult?)null : MessageBoxResult.Yes,
                                r => {
                                    if (r == MessageBoxResult.Yes) {
                                        SettingsHolder.Content.DeleteConfirmation = false;
                                    }
                                })) == MessageBoxResult.Yes) {
                    await DeleteOverrideAsync(objs.Select(x => Tuple.Create(x.Id, x.Location, GetAttachedFiles(x.Location).NonNull())));
                }
            } catch (Exception ex) {
                NonfatalError.Notify(ToolsStrings.AcObject_CannotDelete, ToolsStrings.AcObject_CannotToggle_Commentary, ex);
            }
        }

        /*async Task IFileAcManager.DeleteAsync(IEnumerable<string> id) {
            await DeleteAsync(id);
        }*/

        public async Task<string> PrepareForAdditionalContentAsync([NotNull] string id, bool removeExisting) {
            if (id == null) throw new ArgumentNullException(nameof(id));

            var existing = GetById(id);
            var location = existing?.Location ?? Directories?.GetLocation(id, true);
            if (location == null) {
                throw new NotSupportedException("Directories are not set, AC root might be missing");
            }

            if (removeExisting && FileUtils.Exists(location)) {
                await CleanSpaceOverrideAsync(id, location);
                if (FileUtils.Exists(location)) {
                    throw new OperationCanceledException(ToolsStrings.AcObject_CannotRemove);
                }
            }

            return location;
        }
    }
}