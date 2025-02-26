﻿// Copyright (c) Fantasy Copilot. All rights reserved.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FantasyCopilot.Models.App.Plugins;

namespace FantasyCopilot.ViewModels;

/// <summary>
/// Plugin item view model.
/// </summary>
public sealed partial class PluginItemViewModel
{
    [ObservableProperty]
    private string _id;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _description;

    [ObservableProperty]
    private string _author;

    [ObservableProperty]
    private string _authorSite;

    [ObservableProperty]
    private string _logo;

    [ObservableProperty]
    private string _repository;

    [ObservableProperty]
    private int _commandCount;

    /// <inheritdoc/>
    public ObservableCollection<PluginCommand> Commands { get; set; }
}
