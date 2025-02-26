﻿// Copyright (c) Fantasy Copilot. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using FantasyCopilot.DI.Container;
using FantasyCopilot.Models.App.Gpt;
using FantasyCopilot.Models.Constants;
using FantasyCopilot.Services.Interfaces;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;

namespace FantasyCopilot.Libs.NativeSkills;

/// <summary>
/// Text complete skill.
/// </summary>
public class TextCompleteSkill
{
    private readonly List<ChatHistory.Message> _completeHistory;
    private ITextCompletion _textCompletion;
    private CompleteRequestSettings _completeRequestSettings;
    private string _systemPrompt;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatSkill"/> class.
    /// </summary>
    public TextCompleteSkill()
    {
        var kernel = Locator.Current.GetVariable<IKernel>();
        _completeHistory = new List<ChatHistory.Message>();
        Locator.Current.VariableChanged += OnVariableChanged;
        _textCompletion = kernel.GetService<ITextCompletion>();
    }

    /// <summary>
    /// Initialize current session.
    /// </summary>
    /// <param name="context">Current context.</param>
    /// <returns><see cref="Task"/>.</returns>
    [SKFunction(WorkflowConstants.TextCompletion.InitializeDescription)]
    [SKFunctionName(WorkflowConstants.TextCompletion.InitializeName)]
    public Task InitializeAsync(SKContext context)
    {
        context.Variables.TryGetValue(AppConstants.SessionOptionsKey, out string optionsStr);
        var options = JsonSerializer.Deserialize<SessionOptions>(optionsStr);
        _completeRequestSettings = new CompleteRequestSettings()
        {
            TopP = options.TopP,
            FrequencyPenalty = options.FrequencyPenalty,
            PresencePenalty = options.PresencePenalty,
            MaxTokens = options.MaxResponseTokens,
            Temperature = options.Temperature,
        };

        return Task.CompletedTask;
    }

    /// <summary>
    /// Complete the given text.
    /// </summary>
    /// <param name="context">Current context.</param>
    /// <returns>Message response.</returns>
    [SKFunction(WorkflowConstants.TextCompletion.CompleteDescription)]
    [SKFunctionName(WorkflowConstants.TextCompletion.CompleteName)]
    public async Task<string> CompleteAsync(SKContext context)
    {
        string reply;
        try
        {
            _completeHistory.Add(new ChatHistory.Message(ChatHistory.AuthorRoles.User, context.Result));
            var previousText = GenerateContextString();
            reply = await _textCompletion.CompleteAsync(previousText, _completeRequestSettings, context.CancellationToken);
            reply = reply.Replace(previousText, string.Empty).Trim();

            // If the response is empty, remove the last sent message.
            if (string.IsNullOrEmpty(reply))
            {
                _completeHistory.RemoveAt(_completeHistory.Count - 1);
            }
            else
            {
                _completeHistory.Add(new ChatHistory.Message(ChatHistory.AuthorRoles.Assistant, reply));
            }
        }
        catch (AIException e)
        {
            reply = $"{AppConstants.ExceptionTag}{e.Message}{AppConstants.ExceptionTag}";
        }

        return reply;
    }

    /// <summary>
    /// Create new chat with prompt.
    /// </summary>
    /// <param name="prompt">System prompt.</param>
    public void Create(string prompt)
    {
        _systemPrompt = prompt;
        _completeHistory.Clear();
        _completeHistory.Add(new ChatHistory.Message(ChatHistory.AuthorRoles.System, prompt));
    }

    /// <summary>
    /// Set up chat history.
    /// </summary>
    /// <param name="messages">Chat history.</param>
    public void SetHistory(IEnumerable<Message> messages)
    {
        _completeHistory?.Clear();
        if (messages == null || _completeHistory == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(_systemPrompt))
        {
            _completeHistory.Add(new ChatHistory.Message(ChatHistory.AuthorRoles.System, _systemPrompt));
        }

        foreach (var message in messages)
        {
            if (message.IsUser)
            {
                _completeHistory.Add(new ChatHistory.Message(ChatHistory.AuthorRoles.User, message.Content));
            }
            else
            {
                _completeHistory.Add(new ChatHistory.Message(ChatHistory.AuthorRoles.Assistant, message.Content));
            }
        }
    }

    private void OnVariableChanged(object sender, Type e)
    {
        if (e == typeof(IKernel))
        {
            var kernel = Locator.Current.GetVariable<IKernel>();
            var kernelService = Locator.Current.GetService<IKernelService>();
            if (!kernelService.HasChatModel)
            {
                return;
            }

            var textCompletion = kernel.GetService<ITextCompletion>();

            _textCompletion = textCompletion;
        }
    }

    private string GenerateContextString()
    {
        if (_completeHistory == null || _completeHistory.Count == 0)
        {
            return string.Empty;
        }

        var textList = new List<string>();
        foreach (var item in _completeHistory)
        {
            var role = item.AuthorRole switch
            {
                ChatHistory.AuthorRoles.User => AppConstants.UserTag,
                ChatHistory.AuthorRoles.Assistant => AppConstants.AssistantTag,
                ChatHistory.AuthorRoles.System => AppConstants.SystemTag,
                _ => throw new NotSupportedException(),
            };

            if (string.IsNullOrEmpty(item.Content))
            {
                continue;
            }

            textList.Add($"{role}: {item.Content}");
        }

        var text = string.Join("\n\n", textList);
        text += $"\n\n{AppConstants.AssistantTag}: ";
        return text;
    }
}
