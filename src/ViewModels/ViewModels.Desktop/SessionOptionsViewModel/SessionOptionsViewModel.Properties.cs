﻿// Copyright (c) Fantasy Copilot. All rights reserved.

using CommunityToolkit.Mvvm.ComponentModel;
using FantasyCopilot.Toolkits.Interfaces;

namespace FantasyCopilot.ViewModels;

/// <summary>
/// Session options view model.
/// </summary>
public sealed partial class SessionOptionsViewModel
{
    private readonly ISettingsToolkit _settingsToolkit;

    [ObservableProperty]
    private double _temperature;

    [ObservableProperty]
    private int _maxResponseTokens;

    [ObservableProperty]
    private double _topP;

    [ObservableProperty]
    private double _frequencyPenalty;

    [ObservableProperty]
    private double _presencePenalty;
}
