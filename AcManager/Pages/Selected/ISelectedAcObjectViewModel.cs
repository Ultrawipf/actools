﻿using System.Windows.Input;
using AcManager.Tools.AcObjectsNew;
using JetBrains.Annotations;

namespace AcManager.Pages.Selected {
    public interface ISelectedAcObjectViewModel {
        [NotNull]
        AcCommonObject SelectedAcObject { get; }

        void Load();

        void Unload();

        ICommand FindInformationCommand { get; }

        ICommand ChangeIdCommand { get; }

        ICommand CloneCommand { get; }
    }
}