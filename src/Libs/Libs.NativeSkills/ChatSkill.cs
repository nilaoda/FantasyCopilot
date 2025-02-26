﻿// Copyright (c) Fantasy Copilot. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using FantasyCopilot.DI.Container;
using FantasyCopilot.Models.App.Gpt;
using FantasyCopilot.Models.Constants;
using FantasyCopilot.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;

namespace FantasyCopilot.Libs.NativeSkills;

/// <summary>
/// Chat skill.
/// </summary>
public sealed class ChatSkill
{
    private readonly ILogger<ChatSkill> _logger;
    private IChatCompletion _chatCompletion;
    private ChatHistory _chatHistory;
    private ChatRequestSettings _chatRequestSettings;
    private string _systemPrompt;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatSkill"/> class.
    /// </summary>
    public ChatSkill()
    {
        var kernel = Locator.Current.GetVariable<IKernel>();
        _logger = Locator.Current.GetLogger<ChatSkill>();
        Locator.Current.VariableChanged += OnVariableChanged;
        _chatCompletion = kernel.GetService<IChatCompletion>();
    }

    /// <summary>
    /// Initialize current chat.
    /// </summary>
    /// <param name="context">Current context.</param>
    /// <returns><see cref="Task"/>.</returns>
    [SKFunction(WorkflowConstants.Chat.InitializeDescription)]
    [SKFunctionName(WorkflowConstants.Chat.InitializeName)]
    public Task InitializeAsync(SKContext context)
    {
        context.Variables.TryGetValue(AppConstants.SessionOptionsKey, out string optionsStr);
        var options = JsonSerializer.Deserialize<SessionOptions>(optionsStr);
        _chatRequestSettings = new ChatRequestSettings()
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
    /// Send message to LLM.
    /// </summary>
    /// <param name="context">Current context.</param>
    /// <returns>Message response.</returns>
    [SKFunction(WorkflowConstants.Chat.SendDescription)]
    [SKFunctionName(WorkflowConstants.Chat.SendName)]
    public async Task<string> SendAsync(SKContext context)
    {
        string reply;
        try
        {
            _chatHistory.AddMessage(ChatHistory.AuthorRoles.User, context.Result);
            reply = await _chatCompletion.GenerateMessageAsync(_chatHistory, _chatRequestSettings, context.CancellationToken);

            // If the response is empty, remove the last sent message.
            if (string.IsNullOrEmpty(reply))
            {
                _chatHistory.Messages.RemoveAt(_chatHistory.Messages.Count - 1);
            }
            else
            {
                _chatHistory.AddMessage(ChatHistory.AuthorRoles.Assistant, reply);
            }
        }
        catch (AIException e)
        {
            _logger.LogError(e, "Chat skill error.");
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
        _chatHistory = _chatCompletion?.CreateNewChat(prompt);
    }

    /// <summary>
    /// Set up chat history.
    /// </summary>
    /// <param name="messages">Chat history.</param>
    public void SetHistory(IEnumerable<Message> messages)
    {
        _chatHistory?.Messages.Clear();
        if (messages == null || _chatHistory == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(_systemPrompt))
        {
            _chatHistory.AddMessage(ChatHistory.AuthorRoles.System, _systemPrompt);
        }

        foreach (var message in messages)
        {
            if (message.IsUser)
            {
                _chatHistory.AddMessage(ChatHistory.AuthorRoles.User, message.Content);
            }
            else
            {
                _chatHistory.AddMessage(ChatHistory.AuthorRoles.Assistant, message.Content);
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

            var chatCompletion = kernel.GetService<IChatCompletion>();
            var chatHistory = chatCompletion.CreateNewChat(string.Empty);
            if (_chatHistory != null)
            {
                var messages = _chatHistory.Messages;
                foreach (var item in messages)
                {
                    chatHistory.AddMessage(item.AuthorRole, item.Content ?? string.Empty);
                }
            }

            _chatCompletion = chatCompletion;
            _chatHistory = chatHistory;
        }
    }
}
