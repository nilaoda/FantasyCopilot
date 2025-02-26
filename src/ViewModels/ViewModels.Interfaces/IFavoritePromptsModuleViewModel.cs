﻿// Copyright (c) Fantasy Copilot. All rights reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FantasyCopilot.Models.App.Gpt;

namespace FantasyCopilot.ViewModels.Interfaces;

/// <summary>
/// Custom prompt page view model interface.
/// </summary>
public interface IFavoritePromptsModuleViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// Is the prompt list empty.
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// Is list loading.
    /// </summary>
    bool IsLoading { get; }

    /// <summary>
    /// Prompts list.
    /// </summary>
    ObservableCollection<SessionMetadata> Prompts { get; }

    /// <summary>
    /// Open a saved prompt command.
    /// </summary>
    IRelayCommand<SessionMetadata> CreateSessionCommand { get; }

    /// <summary>
    /// Initialize the list.
    /// </summary>
    IAsyncRelayCommand InitializeCommand { get; }
}
