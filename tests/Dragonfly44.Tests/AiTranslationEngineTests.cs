using System.Net;
using System.Text.Json;
using Dragonfly44.Application.AI.Interfaces;
using Dragonfly44.Application.AI.Models;
using Dragonfly44.Application.Interfaces;
using Dragonfly44.Domain.Entities;
using Dragonfly44.Domain.Enums;
using Dragonfly44.Infrastructure.AI.Providers;
using Dragonfly44.Infrastructure.AI.Services;
using Dragonfly44.Infrastructure.Configuration;
using Dragonfly44.Infrastructure.Persistence;
using Dragonfly44.Infrastructure.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dragonfly44.Tests;

public class AiTranslationEngineTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _tempStorageDir;
    private readonly Dragonfly44DbContext _dbContext;
    private readonly SqliteDatabaseInitializer _initializer;
    private readonly DpapiSecureCredentialStorage _credentialStorage;
    private readonly TranslationPromptBuilder _promptBuilder;
    private readonly ModelCostCalculator _costCalculator;
    private readonly AiProviderRegistry _providerRegistry;

    private readonly Project _project;
    private readonly Chapter _chapter;
    private readonly Page _page;
    private readonly Bubble _bubble;

    public AiTranslationEngineTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"dragonfly44_ai_test_{Guid.NewGuid():N}.db");
        _tempStorageDir = Path.Combine(Path.GetTempPath(), $"dragonfly44_ai_storage_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempStorageDir);

        var options = new DbContextOptionsBuilder<Dragonfly44DbContext>()
            .UseSqlite($"Data Source={_testDbPath}")
            .Options;

        _dbContext = new Dragonfly44DbContext(options);

        var dbOptions = Options.Create(new DatabaseOptions
        {
            ConnectionString = $"Data Source={_testDbPath}",
            DatabaseFileName = Path.GetFileName(_testDbPath)
        });

        var storageOptions = Options.Create(new StorageOptions
        {
            AppDataDirectory = _tempStorageDir,
            ProjectsRootDirectory = Path.Combine(_tempStorageDir, "projects")
        });

        _initializer = new SqliteDatabaseInitializer(
            _dbContext,
            dbOptions,
            storageOptions,
            NullLogger<SqliteDatabaseInitializer>.Instance);

        _initializer.InitializeAsync().GetAwaiter().GetResult();

        // Security & AI Services
        _credentialStorage = new DpapiSecureCredentialStorage(
            NullLogger<DpapiSecureCredentialStorage>.Instance,
            Path.Combine(_tempStorageDir, "credentials.enc"));

        _promptBuilder = new TranslationPromptBuilder();
        _costCalculator = new ModelCostCalculator();
        _providerRegistry = new AiProviderRegistry();

        // Seed domain hierarchy
        _project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Dragonfly Manga",
            SourceLanguage = "ja",
            TargetLanguage = "ar"
        };
        _dbContext.Projects.Add(_project);

        _chapter = new Chapter
        {
            Id = Guid.NewGuid(),
            ProjectId = _project.Id,
            ChapterNumber = 1,
            Title = "Chapter 1: The Encounter",
            Status = ChapterStatus.Draft
        };
        _dbContext.Chapters.Add(_chapter);

        _page = new Page
        {
            Id = Guid.NewGuid(),
            ChapterId = _chapter.Id,
            PageNumber = 1,
            ImageRelativePath = "p01.jpg",
            OriginalFileName = "p01.jpg"
        };
        _dbContext.Pages.Add(_page);

        _bubble = new Bubble
        {
            Id = Guid.NewGuid(),
            PageId = _page.Id,
            RawText = "待て！行かせはしない！",
            TranslatedText = "انتظر! لن أدعك تذهب!",
            ShapeType = BubbleShapeType.Shout,
            ReadingOrderIndex = 1,
            IsAiTranslated = false
        };
        _dbContext.Bubbles.Add(_bubble);
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        try
        {
            if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
            if (Directory.Exists(_tempStorageDir)) Directory.Delete(_tempStorageDir, true);
        }
        catch
        {
            // Ignore temp file cleanup exceptions
        }
    }

    // ========================================================================
    // 1. PROVIDER ABSTRACTION & REGISTRY
    // ========================================================================

    [Fact]
    public void AiProviderRegistry_ShouldRegisterAndRetrieveProviders()
    {
        var dummyClient = new HttpClient(new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var openAi = new OpenAiTranslationProvider(dummyClient, NullLogger<OpenAiTranslationProvider>.Instance);
        var anthropic = new AnthropicTranslationProvider(dummyClient, NullLogger<AnthropicTranslationProvider>.Instance);

        _providerRegistry.RegisterProvider(openAi);
        _providerRegistry.RegisterProvider(anthropic);

        _providerRegistry.HasProvider("OpenAI").Should().BeTrue();
        _providerRegistry.HasProvider("Anthropic").Should().BeTrue();
        _providerRegistry.HasProvider("NonExistent").Should().BeFalse();

        var retrievedOpenAi = _providerRegistry.GetProvider("OpenAI");
        retrievedOpenAi.Should().NotBeNull();
        retrievedOpenAi!.SupportedModels.Should().Contain(m => m.Id == "gpt-4o");
        retrievedOpenAi.SupportedModels.Should().Contain(m => m.Id == "gpt-4o-mini");

        var retrievedAnthropic = _providerRegistry.GetProvider("anthropic"); // Case-insensitive
        retrievedAnthropic.Should().NotBeNull();
        retrievedAnthropic!.SupportedModels.Should().Contain(m => m.Id == "claude-3-5-sonnet-20241022");
    }

    // ========================================================================
    // 2. JAPANESE-TO-ARABIC PROMPT BUILDER
    // ========================================================================

    [Fact]
    public void TranslationPromptBuilder_ShouldConstructMangaSpecificPrompt_WithContextAndVersion()
    {
        var context = new MangaDialogueContext
        {
            CharacterName = "Ren",
            SceneContextNotes = "Shout bubble in high tension confrontation",
            SurroundingDialogueContext = "誰だ？！"
        };

        var systemPrompt = _promptBuilder.BuildSystemPrompt("Japanese", "Arabic");
        var userPrompt = _promptBuilder.BuildUserPrompt("待て！行かせはしない！", context);

        _promptBuilder.PromptVersion.Should().StartWith("1.");
        systemPrompt.Should().Contain("manga translator");
        systemPrompt.Should().Contain("Arabic");
        systemPrompt.Should().Contain("JSON");
        userPrompt.Should().Contain("待て！行かせはしない！");
        userPrompt.Should().Contain("Speaker/Character: Ren");
        userPrompt.Should().Contain("Surrounding Dialogue: 誰だ？！");
    }

    // ========================================================================
    // 3. TOKEN USAGE & COST CALCULATOR
    // ========================================================================

    [Theory]
    [InlineData("OpenAI", "gpt-4o", 1000, 500, 0.0075)] // 1000*$2.50/M + 500*$10.00/M = 0.0025 + 0.005 = 0.0075
    [InlineData("OpenAI", "gpt-4o-mini", 1000, 500, 0.00045)] // 1000*$0.15/M + 500*$0.60/M = 0.00015 + 0.00030 = 0.00045
    [InlineData("Anthropic", "claude-3-5-sonnet-20241022", 1000, 500, 0.0105)] // 1000*$3.00/M + 500*$15.00/M = 0.003 + 0.0075 = 0.0105
    public void ModelCostCalculator_ShouldCalculateAccurateCosts(string provider, string modelId, int inTokens, int outTokens, decimal expectedCost)
    {
        var cost = _costCalculator.CalculateCost(provider, modelId, inTokens, outTokens);

        cost.Should().NotBeNull();
        cost!.Value.Should().BeApproximately(expectedCost, 0.000001m);
    }

    // ========================================================================
    // 4. SECURE DPAPI CREDENTIAL STORAGE
    // ========================================================================

    [Fact]
    public async Task DpapiSecureCredentialStorage_ShouldEncryptAndStoreCredentials_WithoutPlaintextExposure()
    {
        const string provider = "OpenAI";
        const string secretKey = "sk-test-dragonfly-secret-key-123456789";

        // Store credential
        await _credentialStorage.SetCredentialAsync(provider, secretKey);

        // Verify existence
        var exists = await _credentialStorage.HasCredentialAsync(provider);
        exists.Should().BeTrue();

        // Retrieve and verify plaintext roundtrip
        var retrievedKey = await _credentialStorage.GetCredentialAsync(provider);
        retrievedKey.Should().Be(secretKey);

        // Verify physical file is encrypted and does NOT contain plaintext secret
        var encFilePath = Path.Combine(_tempStorageDir, "credentials.enc");
        File.Exists(encFilePath).Should().BeTrue();

        var encryptedBytes = await File.ReadAllBytesAsync(encFilePath);
        var rawFileString = System.Text.Encoding.UTF8.GetString(encryptedBytes);
        rawFileString.Should().NotContain(secretKey, "Plaintext API key must NEVER be written unencrypted to disk");

        // Delete credential
        await _credentialStorage.DeleteCredentialAsync(provider);
        var afterDelete = await _credentialStorage.HasCredentialAsync(provider);
        afterDelete.Should().BeFalse();

        var nullKey = await _credentialStorage.GetCredentialAsync(provider);
        nullKey.Should().BeNull();
    }

    // ========================================================================
    // 5. TRANSLATION ORCHESTRATION & DATA INTEGRITY GUARANTEE
    // ========================================================================

    [Fact]
    public async Task TranslationService_TranslateDialogueAsync_MustNeverSilentlyOverwriteManualTranslation()
    {
        // Setup fake provider that returns a valid translation
        var fakeProvider = new FakeTranslationProvider(
            "OpenAI",
            new List<AiModelInfo> { new("gpt-4o", "GPT-4o", "Flagship", 128000, 2.50m, 10.00m) },
            req => TranslationResponse.Success(
                translatedText: "قف مكانك! لن تمر أبداً!",
                rawResponseJson: "{}",
                inputTokens: 50,
                outputTokens: 20,
                notes: "Strong imperative phrasing"));

        _providerRegistry.RegisterProvider(fakeProvider);
        await _credentialStorage.SetCredentialAsync("OpenAI", "sk-valid-key");

        var fakeSettings = new FakeSettingsService();
        fakeSettings.Set("Ai:DefaultProvider", "OpenAI");

        var service = new TranslationService(
            _dbContext,
            _providerRegistry,
            _credentialStorage,
            _promptBuilder,
            _costCalculator,
            fakeSettings,
            NullLogger<TranslationService>.Instance);

        var originalManualText = _bubble.TranslatedText; // "انتظر! لن أدعك تذهب!"

        // Act: Translate dialogue
        var result = await service.TranslateDialogueAsync(_bubble.Id);

        // Assert: Translation operation succeeds
        result.IsSuccess.Should().BeTrue();
        result.TranslatedText.Should().Be("قف مكانك! لن تمر أبداً!");
        result.HistoryEntryId.Should().NotBeNull();

        // CRITICAL DATA INTEGRITY CHECK:
        // The bubble's actual TranslatedText in the database MUST NOT BE CHANGED YET!
        var bubbleInDb = await _dbContext.Bubbles.FindAsync(_bubble.Id);
        bubbleInDb!.TranslatedText.Should().Be(originalManualText,
            "CRITICAL: AI translation must NEVER silently overwrite manual translations before explicit user acceptance.");
        bubbleInDb.IsAiTranslated.Should().BeFalse();

        // History entry must be recorded as Completed with IsAccepted = false (pending user review)
        var historyEntry = await _dbContext.TranslationHistories.FindAsync(result.HistoryEntryId!.Value);
        historyEntry.Should().NotBeNull();
        historyEntry!.Status.Should().Be(TranslationStatus.Completed);
        historyEntry.IsAccepted.Should().BeFalse();
        historyEntry.TranslatedText.Should().Be("قف مكانك! لن تمر أبداً!");
    }

    [Fact]
    public async Task TranslationService_AcceptTranslationAsync_ShouldApplyAndPreserveManualTranslation()
    {
        // Create pending history entry
        var entry = new TranslationHistoryEntry
        {
            Id = Guid.NewGuid(),
            BubbleId = _bubble.Id,
            SourceText = _bubble.RawText,
            TranslatedText = "قف! لن أسمح لك بالمرور!",
            Provider = "OpenAI",
            Model = "gpt-4o",
            Status = TranslationStatus.Completed,
            IsAccepted = false
        };
        _dbContext.TranslationHistories.Add(entry);
        await _dbContext.SaveChangesAsync();

        var initialManualText = _bubble.TranslatedText;

        var fakeSettings = new FakeSettingsService();
        var service = new TranslationService(
            _dbContext,
            _providerRegistry,
            _credentialStorage,
            _promptBuilder,
            _costCalculator,
            fakeSettings,
            NullLogger<TranslationService>.Instance);

        // Act: Accept translation
        var accepted = await service.AcceptTranslationAsync(entry.Id);
        accepted.Should().BeTrue();

        // Assert: Database bubble updated and manual text preserved in LastManualTranslation
        var bubble = await _dbContext.Bubbles.FindAsync(_bubble.Id);
        bubble!.TranslatedText.Should().Be("قف! لن أسمح لك بالمرور!");
        bubble.LastManualTranslation.Should().Be(initialManualText, "Previous manual translation must be preserved for reverting");
        bubble.IsAiTranslated.Should().BeTrue();

        var updatedEntry = await _dbContext.TranslationHistories.FindAsync(entry.Id);
        updatedEntry!.IsAccepted.Should().BeTrue();
        updatedEntry.Status.Should().Be(TranslationStatus.Accepted);
    }

    [Fact]
    public async Task TranslationService_RejectTranslationAsync_ShouldRejectWithoutTouchingBubble()
    {
        var entry = new TranslationHistoryEntry
        {
            Id = Guid.NewGuid(),
            BubbleId = _bubble.Id,
            SourceText = _bubble.RawText,
            TranslatedText = "ترجمة مرفوضة",
            Provider = "OpenAI",
            Model = "gpt-4o",
            Status = TranslationStatus.Completed,
            IsAccepted = false
        };
        _dbContext.TranslationHistories.Add(entry);
        await _dbContext.SaveChangesAsync();

        var originalText = _bubble.TranslatedText;

        var fakeSettings = new FakeSettingsService();
        var service = new TranslationService(
            _dbContext,
            _providerRegistry,
            _credentialStorage,
            _promptBuilder,
            _costCalculator,
            fakeSettings,
            NullLogger<TranslationService>.Instance);

        // Act: Reject
        var rejected = await service.RejectTranslationAsync(entry.Id);
        rejected.Should().BeTrue();

        // Assert: Bubble translation untouched
        var bubble = await _dbContext.Bubbles.FindAsync(_bubble.Id);
        bubble!.TranslatedText.Should().Be(originalText);

        var updatedEntry = await _dbContext.TranslationHistories.FindAsync(entry.Id);
        updatedEntry!.Status.Should().Be(TranslationStatus.Rejected);
        updatedEntry.IsAccepted.Should().BeFalse();
    }

    [Fact]
    public async Task TranslationService_RevertToManualTranslationAsync_ShouldRestoreOriginalManualText()
    {
        _bubble.TranslatedText = "ترجمة ذكاء اصطناعي مقبولة";
        _bubble.LastManualTranslation = "الترجمة اليدوية الأصلية للكاتب";
        _bubble.IsAiTranslated = true;
        await _dbContext.SaveChangesAsync();

        var fakeSettings = new FakeSettingsService();
        var service = new TranslationService(
            _dbContext,
            _providerRegistry,
            _credentialStorage,
            _promptBuilder,
            _costCalculator,
            fakeSettings,
            NullLogger<TranslationService>.Instance);

        // Act: Revert
        var reverted = await service.RevertToManualTranslationAsync(_bubble.Id);
        reverted.Should().BeTrue();

        // Assert
        var bubble = await _dbContext.Bubbles.FindAsync(_bubble.Id);
        bubble!.TranslatedText.Should().Be("الترجمة اليدوية الأصلية للكاتب");
        bubble.LastManualTranslation.Should().BeNull();
        bubble.IsAiTranslated.Should().BeFalse();
    }

    // ========================================================================
    // 6. HTTP RELIABILITY, RATE LIMITING (429) & ERROR HANDLING
    // ========================================================================

    [Fact]
    public async Task OpenAiTranslationProvider_ShouldHandleRateLimit429_Gracefully()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.TooManyRequests,
            Content = new StringContent("{\"error\": {\"message\": \"Rate limit exceeded. Please retry.\"}}")
        });

        var client = new HttpClient(handler);
        var provider = new OpenAiTranslationProvider(client, NullLogger<OpenAiTranslationProvider>.Instance);

        var request = new TranslationRequest
        {
            SourceText = "テスト",
            SourceLanguage = "Japanese",
            TargetLanguage = "Arabic",
            ApiKey = "sk-test",
            ModelId = "gpt-4o",
            SystemPrompt = "system",
            PromptVersion = "v1.0"
        };

        var response = await provider.TranslateAsync(request);

        response.IsSuccess.Should().BeFalse();
        response.ErrorMessage.Should().ContainEquivalentOf("rate limit");
    }

    [Fact]
    public async Task OpenAiTranslationProvider_ShouldParseValidStructuredJsonResponse()
    {
        var validJsonResponse = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = "```json\n{\n  \"translated_text\": \"مرحباً بالعالم!\",\n  \"notes\": \"Standard greeting\",\n  \"detected_register\": \"casual\"\n}\n```"
                    }
                }
            },
            usage = new
            {
                prompt_tokens = 25,
                completion_tokens = 15
            }
        });

        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(validJsonResponse)
        });

        var client = new HttpClient(handler);
        var provider = new OpenAiTranslationProvider(client, NullLogger<OpenAiTranslationProvider>.Instance);

        var request = new TranslationRequest
        {
            SourceText = "こんにちは世界",
            SourceLanguage = "Japanese",
            TargetLanguage = "Arabic",
            ApiKey = "sk-test",
            ModelId = "gpt-4o-mini",
            SystemPrompt = "system",
            PromptVersion = "v1.0"
        };

        var response = await provider.TranslateAsync(request);

        response.IsSuccess.Should().BeTrue();
        response.TranslatedText.Should().Be("مرحباً بالعالم!");
        response.Notes.Should().Be("Standard greeting");
        response.InputTokens.Should().Be(25);
        response.OutputTokens.Should().Be(15);
    }

    [Fact]
    public async Task AnthropicTranslationProvider_ShouldParseValidStructuredJsonResponse()
    {
        var validJsonResponse = JsonSerializer.Serialize(new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = "{\"translated_text\": \"صباح الخير!\", \"notes\": \"Polite greeting\"}"
                }
            },
            usage = new
            {
                input_tokens = 30,
                output_tokens = 12
            }
        });

        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(validJsonResponse)
        });

        var client = new HttpClient(handler);
        var provider = new AnthropicTranslationProvider(client, NullLogger<AnthropicTranslationProvider>.Instance);

        var request = new TranslationRequest
        {
            SourceText = "おはようございます",
            SourceLanguage = "Japanese",
            TargetLanguage = "Arabic",
            ApiKey = "sk-ant-test",
            ModelId = "claude-3-5-sonnet-20241022",
            SystemPrompt = "system",
            PromptVersion = "v1.0"
        };

        var response = await provider.TranslateAsync(request);

        response.IsSuccess.Should().BeTrue();
        response.TranslatedText.Should().Be("صباح الخير!");
        response.Notes.Should().Be("Polite greeting");
        response.InputTokens.Should().Be(30);
        response.OutputTokens.Should().Be(12);
    }

    // ========================================================================
    // HELPER TEST DOUBLES
    // ========================================================================

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }

    private class FakeSettingsService : ISettingsService
    {
        private readonly Dictionary<string, object> _settings = new();

        public T Get<T>(string key, T defaultValue)
        {
            if (_settings.TryGetValue(key, out var val))
            {
                return (T)Convert.ChangeType(val, typeof(T));
            }
            return defaultValue;
        }

        public void Set<T>(string key, T value) => _settings[key] = value!;
        public bool HasKey(string key) => _settings.ContainsKey(key);
        public void Remove(string key) => _settings.Remove(key);
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class FakeTranslationProvider : IAiTranslationProvider
    {
        public string ProviderName { get; }
        public IReadOnlyList<AiModelInfo> SupportedModels { get; }
        public AiProviderCapabilities Capabilities { get; }
        private readonly Func<TranslationRequest, TranslationResponse> _translateFunc;

        public FakeTranslationProvider(
            string name,
            IReadOnlyList<AiModelInfo> models,
            Func<TranslationRequest, TranslationResponse> translateFunc)
        {
            ProviderName = name;
            SupportedModels = models;
            Capabilities = new AiProviderCapabilities(supportsCustomEndpoint: true);
            _translateFunc = translateFunc;
        }

        public Task<TranslationResponse> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(_translateFunc(request));

        public Task<ConnectionTestResult> TestConnectionAsync(AiProviderConfiguration config, string apiKey, CancellationToken cancellationToken = default)
            => Task.FromResult(ConnectionTestResult.Success(120, "Connected"));
    }
}
