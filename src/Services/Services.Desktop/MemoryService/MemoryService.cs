﻿// Copyright (c) Fantasy Copilot. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FantasyCopilot.DI.Container;
using FantasyCopilot.Models.App.Gpt;
using FantasyCopilot.Models.Constants;
using FantasyCopilot.Services.Interfaces;
using FantasyCopilot.Toolkits.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Memory.Sqlite;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SemanticFunctions;
using Microsoft.SemanticKernel.SkillDefinition;
using Microsoft.SemanticKernel.Text;

namespace FantasyCopilot.Services;

/// <summary>
/// Memory service.
/// </summary>
public sealed partial class MemoryService : IMemoryService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryService"/> class.
    /// </summary>
    public MemoryService(
        IKernelService kernelService,
        ISettingsToolkit settingsToolkit,
        ILogger<MemoryService> logger)
    {
        _logger = logger;
        _kernel = Locator.Current.GetVariable<IKernel>();
        Locator.Current.VariableChanged += OnVariableChanged;
        _kernelService = kernelService;
        _settingsToolkit = settingsToolkit;
    }

    /// <inheritdoc/>
    public async Task<bool> ConnectSQLiteKnowledgeBaseAsync(string databasePath)
    {
        ThrowIfNotSupportMemory();
        if (!File.Exists(databasePath))
        {
            _logger.LogError($"The database file corresponding to the knowledge base has been deleted or moved");
            return false;
        }

        try
        {
            DisconnectSQLiteKnowledgeBase();
            _logger.LogDebug($"Connecting to database: {databasePath}");
            var store = await SqliteMemoryStore.ConnectAsync(databasePath);
            _memoryStore = store;
            _kernel.UseMemory(store);
            _logger.LogDebug($"Fetching available collections...");
            var collections = await _kernel.Memory.GetCollectionsAsync();
            _logger.LogDebug($"Found {collections.Count} collections");
            _tempMemoryCollections = collections.ToList();
            return true;
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "An error occurred while trying to connect to the database");
            DisconnectSQLiteKnowledgeBase();
            return false;
        }
    }

    /// <inheritdoc/>
    public void DisconnectSQLiteKnowledgeBase()
    {
        try
        {
            if (_memoryStore != null)
            {
                _logger.LogDebug($"Closing database");
                _memoryStore?.Dispose();
                _memoryStore = null;
                _logger.LogDebug($"Database closed");
            }
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "An error occurred while trying to close the database");
        }
    }

    /// <inheritdoc/>
    public (int ImportedFiles, int TotalFiles) GetImportToMemoryProgress()
        => (_tempMemoryFileImportedCount, _tempMemoryFileTotalCount);

    /// <inheritdoc/>
    public async Task<bool> ImportFileToMemoryAsync(string filePath)
    {
        ThrowIfNotSupportMemory();
        _tempMemoryFileTotalCount = 1;
        _tempMemoryFileFailedCount = 0;
        _tempMemoryFileImportedCount = 0;
        _logger.LogDebug($"Importing file: {filePath}");
        await SaveContextInformationAsync(filePath);
        _logger.LogDebug($"Import file finished");
        return _tempMemoryFileFailedCount == 0;
    }

    /// <inheritdoc/>
    public async Task<(int TotalCount, int FailCount)> ImportFolderToMemoryAsync(string folderPath, string searchPattern)
    {
        ThrowIfNotSupportMemory();
        _tempMemoryFileTotalCount = 0;
        _tempMemoryFileFailedCount = 0;
        _tempMemoryFileImportedCount = 0;
        _logger.LogDebug($"Importing folder: {folderPath}");
        var files = await GetFilesAsync(folderPath, searchPattern);
        _tempMemoryFileTotalCount = files.Count;
        var tasks = new List<Task>();
        foreach (var file in files)
        {
            _logger.LogDebug($"Importing {file.FullName}");
            tasks.Add(SaveContextInformationAsync(file.FullName, folderPath));
        }

        await Task.WhenAll(tasks);
        _logger.LogDebug($"Import folder finished");
        return (_tempMemoryFileTotalCount, _tempMemoryFileFailedCount);
    }

    /// <inheritdoc/>
    public async Task<MessageResponse> SearchMemoryAsync(string query, SessionOptions options, CancellationToken cancellationToken)
    {
        ThrowIfNotSupportMemory();
        var text = string.Empty;
        var id = string.Empty;
        var collectionId = _tempMemoryCollections.FirstOrDefault();
        await foreach (var item in _kernel.Memory.SearchAsync(collectionId ?? AppConstants.KnowledgeBaseCollectionId, query, cancellationToken: cancellationToken))
        {
            text = item.Metadata.Text;
            id = item.Metadata.AdditionalMetadata ?? item.Metadata.Id;
        }

        if (string.IsNullOrEmpty(text))
        {
            return new MessageResponse(false, "No answer");
        }

        options.MaxResponseTokens = _settingsToolkit.ReadLocalSetting(SettingNames.ContextResponseTokenLength, 512);
        var func = GetQAFunction(options);
        var variables = new ContextVariables(query);
        variables.Set("Content", text);
        var context = await _kernel.RunAsync(variables, func);
        text = context.Result.Trim();
        var isError = context.ErrorOccurred || string.IsNullOrEmpty(text);
        var data = isError ? "Something error" : text;
        return new MessageResponse(isError, data, id);
    }

    private static string BuildFileId(string filePath, string rootDirectory)
        => string.IsNullOrEmpty(rootDirectory)
            ? filePath.Replace('\\', '/')
            : Path.GetRelativePath(rootDirectory, filePath).Replace('\\', '/');

    private static async Task<List<FileInfo>> GetFilesAsync(string rootDirectory, string extensionList)
    {
        if (string.IsNullOrEmpty(extensionList))
        {
            throw new System.InvalidCastException("Extension list is empty.");
        }

        var sp = extensionList.Split(',');
        var directory = new DirectoryInfo(rootDirectory);
        var files = new List<FileInfo>();
        await Task.Run(() =>
        {
            files = sp
                .SelectMany(p => directory.GetFiles(p.Trim(), SearchOption.AllDirectories))
                .ToList();
        });
        return files;
    }

    private async Task SaveContextInformationAsync(string filePath, string rootDirectory = default)
    {
        var fileId = BuildFileId(filePath, rootDirectory);

        try
        {
            var maxContentLength = _settingsToolkit.ReadLocalSetting(SettingNames.MaxSplitContentLength, 1024);
            var maxTokenLength = _settingsToolkit.ReadLocalSetting(SettingNames.MaxParagraphTokenLength, 512);
            var content = await File.ReadAllTextAsync(filePath);
            var isMarkdown = Path.GetExtension(filePath).Equals(".md", System.StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(content))
            {
                _tempMemoryFileFailedCount++;
                return;
            }

            var needSplit = content.Length > maxContentLength;
            if (needSplit)
            {
                List<string> lines;
                List<string> paragraphs;

                if (isMarkdown)
                {
                    lines = TextChunker.SplitMarkDownLines(content, maxTokenLength);
                    paragraphs = TextChunker.SplitMarkdownParagraphs(lines, maxTokenLength);
                }
                else
                {
                    lines = TextChunker.SplitPlainTextLines(content, maxTokenLength);
                    paragraphs = TextChunker.SplitPlainTextParagraphs(lines, maxTokenLength);
                }

                for (var i = 0; i < paragraphs.Count; i++)
                {
                    await _kernel.Memory.SaveInformationAsync(
                        AppConstants.KnowledgeBaseCollectionId,
                        text: $"{paragraphs[i]}",
                        id: $"{fileId}_{i}",
                        additionalMetadata: Path.GetFileName(filePath));
                }
            }
            else
            {
                await _kernel.Memory.SaveInformationAsync(
                    AppConstants.KnowledgeBaseCollectionId,
                    text: $"{content}",
                    id: fileId,
                    additionalMetadata: Path.GetFileName(filePath));
            }
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error saving context");
            _tempMemoryFileFailedCount++;
        }

        _tempMemoryFileImportedCount++;
    }

    private void ThrowIfNotSupportMemory()
    {
        if (!_kernelService.IsMemorySupport)
        {
            throw new System.InvalidOperationException("Memory is not supported.");
        }
    }

    private ISKFunction GetQAFunction(SessionOptions options)
    {
        var config = new PromptTemplateConfig
        {
            Description = WorkflowConstants.QADescription,
            Completion =
            {
                MaxTokens = options.MaxResponseTokens,
                FrequencyPenalty = options.FrequencyPenalty,
                PresencePenalty = options.PresencePenalty,
                Temperature = options.Temperature,
                TopP = options.TopP,
            },
        };

        var promptTemplate = new PromptTemplate(WorkflowConstants.QAPrompt, config, _kernel);
        var functionConfig = new SemanticFunctionConfig(config, promptTemplate);
        var function = _kernel.RegisterSemanticFunction(WorkflowConstants.QASkillId, functionConfig);
        return function;
    }

    private void OnVariableChanged(object sender, Type e)
    {
        if (e == typeof(IKernel))
        {
            _kernel = Locator.Current.GetVariable<IKernel>();
            if (_memoryStore != null)
            {
                _kernel.UseMemory(_memoryStore);
            }
        }
    }
}
