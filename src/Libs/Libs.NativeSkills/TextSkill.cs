﻿// Copyright (c) Fantasy Copilot. All rights reserved.

using System.Threading.Tasks;
using FantasyCopilot.DI.Container;
using FantasyCopilot.Models.App;
using FantasyCopilot.Models.App.Workspace.Steps;
using FantasyCopilot.Models.Constants;
using FantasyCopilot.Services.Interfaces;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.TemplateEngine;

namespace FantasyCopilot.Libs.NativeSkills;

/// <summary>
/// Text skill.
/// </summary>
public sealed class TextSkill
{
    private readonly ITranslateService _translateService;
    private readonly WorkflowContext _workflowContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextSkill"/> class.
    /// </summary>
    public TextSkill()
    {
        _workflowContext = Locator.Current.GetVariable<WorkflowContext>();
        _translateService = Locator.Current.GetService<ITranslateService>();
    }

    /// <summary>
    /// Translate the text into the specified language.
    /// </summary>
    /// <param name="context">Context.</param>
    /// <returns>Translated text.</returns>
    [SKFunction(WorkflowConstants.Text.TranslateDescription)]
    [SKFunctionName(WorkflowConstants.Text.TranslateName)]
    [SKFunctionContextParameter(Description = "The text to be translated.", Name = "INPUT")]
    public async Task<string> TranslateAsync(SKContext context)
    {
        var parameters = _workflowContext.GetStepParameters<TranslateStep>();
        if (parameters == null)
        {
            context.Fail("Do not have translate parameters");
            return default;
        }

        var text = context.Result;
        if (string.IsNullOrEmpty(text))
        {
            context.Fail("The text content to be translated cannot be empty");
        }

        var result = await _translateService.TranslateTextAsync(text, parameters.Source, parameters.Target, context.CancellationToken);
        return result;
    }

    /// <summary>
    /// Overwrite input text, output as new text.
    /// </summary>
    /// <param name="context">Context.</param>
    /// <returns>New text.</returns>
    [SKFunctionName(WorkflowConstants.Text.OverwriteName)]
    [SKFunction(WorkflowConstants.Text.OverwriteDescription)]
    [SKFunctionContextParameter(Description = "The text to be overwritten.", Name = "INPUT")]
    public async Task<string> TextOverwriteAsync(SKContext context)
    {
        var parameters = _workflowContext.GetStepParameters<TextOverwriteStep>();
        if (parameters == null)
        {
            context.Fail("Do not have overwrite parameters");
            return default;
        }

        var templateEngine = new PromptTemplateEngine();
        var finalResult = await templateEngine.RenderAsync(parameters.Text, context);
        return finalResult;
    }

    /// <summary>
    /// Copy the value of one variable in the context to another variable.
    /// </summary>
    /// <param name="context">Current context.</param>
    /// <returns><see cref="Task"/>.</returns>
    [SKFunctionName(WorkflowConstants.Text.VariableRedirectName)]
    [SKFunction(WorkflowConstants.Text.VariableRedirectDescription)]
    public Task<SKContext> VariableRedirectAsync(SKContext context)
    {
        var parameters = _workflowContext.GetStepParameters<VariableRedirectStep>();
        if (parameters == null
            || string.IsNullOrEmpty(parameters.SourceName)
            || string.IsNullOrEmpty(parameters.TargetName))
        {
            context.Fail("Do not have variable redirect parameters");
            return default;
        }

        var hasSource = context.Variables.TryGetValue(parameters.SourceName, out string value);
        if (!hasSource)
        {
            context.Fail("Do not have source variable.");
            return default;
        }

        context.Variables.Set(parameters.TargetName, value);
        return Task.FromResult(context);
    }

    /// <summary>
    /// Create a new variable.
    /// </summary>
    /// <param name="context">Current context.</param>
    /// <returns><see cref="Task"/>.</returns>
    [SKFunctionName(WorkflowConstants.Text.VariableCreateName)]
    [SKFunction(WorkflowConstants.Text.VariableCreateDescription)]
    public async Task<SKContext> VariableCreateAsync(SKContext context)
    {
        var parameters = _workflowContext.GetStepParameters<VariableCreateStep>();
        if (parameters == null
            || string.IsNullOrEmpty(parameters.Name)
            || string.IsNullOrEmpty(parameters.Value))
        {
            context.Fail("Invalid variable");
            return default;
        }

        var engine = new PromptTemplateEngine();
        var value = await engine.RenderAsync(parameters.Value, context);

        context.Variables.Set(parameters.Name, value);
        return context;
    }
}
