﻿// Copyright (c) Fantasy Copilot. All rights reserved.

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using FantasyCopilot.DI.Container;
using FantasyCopilot.Models.App;
using FantasyCopilot.Models.App.Gpt;
using FantasyCopilot.Models.Constants;
using FantasyCopilot.Services.Interfaces;
using FantasyCopilot.Toolkits.Interfaces;
using FantasyCopilot.ViewModels.Interfaces;
using Windows.ApplicationModel.DataTransfer;

namespace FantasyCopilot.ViewModels;

/// <summary>
/// Online prompts module view model.
/// </summary>
public sealed partial class OnlinePromptsModuleViewModel : ViewModelBase, IOnlinePromptsModuleViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OnlinePromptsModuleViewModel"/> class.
    /// </summary>
    public OnlinePromptsModuleViewModel(
        IPromptExplorerService promptExplorerService,
        IResourceToolkit resourceToolkit,
        ICacheToolkit cacheToolkit,
        IAppViewModel appViewModel)
    {
        _promptExplorerService = promptExplorerService;
        _cacheToolkit = cacheToolkit;
        _resourceToolkit = resourceToolkit;
        _appViewModel = appViewModel;
        Prompts = new ObservableCollection<OnlinePrompt>();
        Sources = new ObservableCollection<OnlinePromptSource>();

        AttachIsRunningToAsyncCommand(p => IsLoading = p, ChangeSourceCommand, RefreshCommand);
        var sources = _promptExplorerService.GetSupportSources();
        foreach (var item in sources)
        {
            Sources.Add(item);
        }
    }

    private static SessionMetadata GetMetadataFromOnlinePrompt(OnlinePrompt prompt)
    {
        return new SessionMetadata
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = prompt.Title,
            Description = prompt.Description,
            SystemPrompt = prompt.Prompt,
        };
    }

    [RelayCommand]
    private void CreateSession(OnlinePrompt prompt)
    {
        var metadata = GetMetadataFromOnlinePrompt(prompt);
        var vm = Locator.Current.GetService<ISessionViewModel>();
        vm.Initialize(default, metadata);
        _appViewModel.Navigate(PageType.ChatSession, vm);
    }

    [RelayCommand]
    private void CopyPrompt(OnlinePrompt prompt)
    {
        var dp = new DataPackage();
        dp.SetText(prompt.Prompt);
        Clipboard.SetContent(dp);
        _appViewModel.ShowTip(_resourceToolkit.GetLocalizedString(StringNames.PromptCopied), InfoType.Success);
    }

    [RelayCommand]
    private async Task FavoriteAsync(OnlinePrompt prompt)
    {
        var metadata = GetMetadataFromOnlinePrompt(prompt);
        await _cacheToolkit.AddOrUpdatePromptAsync(metadata);
        _appViewModel.ShowTip(_resourceToolkit.GetLocalizedString(StringNames.PromptPinned), InfoType.Success);
    }

    [RelayCommand]
    private async Task ChangeSourceAsync(OnlinePromptSource source)
    {
        if (IsLoading)
        {
            return;
        }

        TryClear(Prompts);

        SelectedSource = source;
        var prompts = await _promptExplorerService.GetSourceDetailAsync(source);
        IsError = prompts == default;
        if (!IsError)
        {
            CacheTime = prompts.CacheTime.ToString("yyyy-MM-dd HH:mm");
            foreach (var item in prompts.List)
            {
                Prompts.Add(item);
            }
        }
        else
        {
            CacheTime = "--/--";
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsLoading)
        {
            return;
        }

        var isSuccess = await _promptExplorerService.ReloadSourceAsync(SelectedSource);
        IsError = !isSuccess;
        if (isSuccess)
        {
            await ChangeSourceAsync(SelectedSource);
        }
        else
        {
            _appViewModel.ShowTip(_resourceToolkit.GetLocalizedString(StringNames.RefreshFailed), InfoType.Error);
        }
    }
}
