using Dragonfly44.Application.Interfaces;
using Dragonfly44.Application.Services;
using Dragonfly44.Infrastructure.DependencyInjection;
using Dragonfly44.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dragonfly44.Tests;

public class LiveEndToEndVerificationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _tempStorageDir;

    public LiveEndToEndVerificationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"dragonfly44_live_e2e_{Guid.NewGuid():N}.db");
        _tempStorageDir = Path.Combine(Path.GetTempPath(), $"dragonfly44_live_storage_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempStorageDir);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
        }
        catch { }
        try
        {
            if (Directory.Exists(_tempStorageDir)) Directory.Delete(_tempStorageDir, true);
        }
        catch { }
    }

    [Fact]
    public async Task CompletePhase3Workflow_ShouldExecuteCleanlyThroughDI()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:DatabaseFileName"] = Path.GetFileName(_testDbPath),
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_testDbPath}",
                ["Storage:AppDataDirectory"] = _tempStorageDir
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug());
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDragonflyInfrastructure(configuration, $"Data Source={_testDbPath}");

        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        // Verify DI registrations
        var dbInitializer = sp.GetRequiredService<IDatabaseInitializer>();
        var projectService = sp.GetRequiredService<IProjectService>();
        var chapterService = sp.GetRequiredService<IChapterService>();
        var draftService = sp.GetRequiredService<IDraftService>();
        var ingestionService = sp.GetRequiredService<IMangaIngestionService>();
        var storageService = sp.GetRequiredService<IStorageService>();
        var metadataReader = sp.GetRequiredService<IImageMetadataReader>();

        dbInitializer.Should().NotBeNull();
        projectService.Should().NotBeNull();
        chapterService.Should().NotBeNull();
        draftService.Should().NotBeNull();
        ingestionService.Should().NotBeNull();
        storageService.Should().NotBeNull();
        metadataReader.Should().NotBeNull();

        await dbInitializer.InitializeAsync();

        // 1. Create Project
        var projResult = await projectService.CreateProjectAsync("Demon Slayer (Kimetsu no Yaiba)", "ja", "ar", "Dark fantasy manga series");
        projResult.IsSuccess.Should().BeTrue();
        var project = projResult.Value!;
        project.Name.Should().Be("Demon Slayer (Kimetsu no Yaiba)");

        // 2. Create Chapters
        var ch1Result = await chapterService.CreateChapterAsync(project.Id, "Cruelty");
        var ch2Result = await chapterService.CreateChapterAsync(project.Id, "Unknown Stranger");
        ch1Result.IsSuccess.Should().BeTrue();
        ch2Result.IsSuccess.Should().BeTrue();
        var ch1 = ch1Result.Value!;
        var ch2 = ch2Result.Value!;
        ch1.ChapterNumber.Should().Be(1);
        ch2.ChapterNumber.Should().Be(2);

        // 3. Prepare Manga Ingestion Test Folder with unnatural names
        var testFolder = Path.Combine(Path.GetTempPath(), "df44_live_manga_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testFolder);

        void WritePng(string filename, int w, int h)
        {
            using var stream = File.Create(Path.Combine(testFolder, filename));
            stream.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            stream.Write(new byte[] { 0x00, 0x00, 0x00, 0x0D });
            stream.Write(new byte[] { 0x49, 0x48, 0x44, 0x52 });
            stream.WriteByte((byte)((w >> 24) & 0xFF));
            stream.WriteByte((byte)((w >> 16) & 0xFF));
            stream.WriteByte((byte)((w >> 8) & 0xFF));
            stream.WriteByte((byte)(w & 0xFF));
            stream.WriteByte((byte)((h >> 24) & 0xFF));
            stream.WriteByte((byte)((h >> 16) & 0xFF));
            stream.WriteByte((byte)((h >> 8) & 0xFF));
            stream.WriteByte((byte)(h & 0xFF));
            stream.Write(new byte[] { 0x08, 0x06, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
        }

        WritePng("page11.png", 1200, 1800);
        WritePng("page2.png", 1200, 1800);
        WritePng("page10.png", 1200, 1800);
        WritePng("page1.png", 1200, 1800);
        WritePng("page3.png", 1200, 1800);
        File.WriteAllText(Path.Combine(testFolder, "unsupported_readme.txt"), "Ignore me!");
        File.WriteAllText(Path.Combine(testFolder, "translation_notes.pdf"), "Ignore me!");

        // 4. Ingest folder
        var ingestResult = await ingestionService.IngestChapterFolderAsync(project.Id, ch1.Id, testFolder);
        ingestResult.IsSuccess.Should().BeTrue();
        var pages = ingestResult.Value!;
        pages.Should().HaveCount(5);

        // 5. Verify Natural Sorting & Storage
        var expectedOrder = new[] { "page1.png", "page2.png", "page3.png", "page10.png", "page11.png" };
        for (int i = 0; i < pages.Count; i++)
        {
            pages[i].OriginalFileName.Should().Be(expectedOrder[i]);
            pages[i].PageNumber.Should().Be(i + 1);

            var absPath = storageService.ResolveAbsolutePath(pages[i].ImageRelativePath);
            File.Exists(absPath).Should().BeTrue();
        }

        // 6. Test Independent Drafts
        var draftResult = await draftService.CreateDraftAsync("Character Design Sketches", "Initial cover drafts");
        draftResult.IsSuccess.Should().BeTrue();
        var draft = draftResult.Value!;

        var draftIngestResult = await ingestionService.IngestDraftFolderAsync(draft.Id, testFolder);
        draftIngestResult.IsSuccess.Should().BeTrue();
        draftIngestResult.Value.Should().HaveCount(5);

        // Cleanup temp folder
        Directory.Delete(testFolder, true);
    }

    [Fact]
    public async Task CompletePhase4Workflow_MangaCanvasAndRenderingEngine()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:DatabaseFileName"] = Path.GetFileName(_testDbPath),
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_testDbPath}",
                ["Storage:AppDataDirectory"] = _tempStorageDir
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug());
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDragonflyInfrastructure(configuration, $"Data Source={_testDbPath}");

        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var dbInitializer = sp.GetRequiredService<IDatabaseInitializer>();
            var projectService = sp.GetRequiredService<IProjectService>();
            var chapterService = sp.GetRequiredService<IChapterService>();
            var ingestionService = sp.GetRequiredService<IMangaIngestionService>();
            var pageRepository = sp.GetRequiredService<IPageRepository>();
            var storageService = sp.GetRequiredService<IStorageService>();

            await dbInitializer.InitializeAsync();

            // 1. Create Project & Chapter
            var proj = (await projectService.CreateProjectAsync("Berserk", "ja", "en")).Value!;
            var ch = (await chapterService.CreateChapterAsync(proj.Id, "Chapter 1: The Black Swordsman")).Value!;

            // 2. Create sample images with various formats (PNG, JPEG, WebP)
            var imgDir = Path.Combine(Path.GetTempPath(), "df44_phase4_imgs_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(imgDir);

            // PNG
            var pngFile = Path.Combine(imgDir, "page_001.png");
            using (var stream = File.Create(pngFile))
            {
                stream.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13 });
                stream.Write(new byte[] { 0x49, 0x48, 0x44, 0x52, 0, 0, 0x05, 0x78, 0, 0, 0x08, 0x34, 8, 6, 0, 0, 0 }); // 1400x2100
            }

            // JPEG
            var jpgFile = Path.Combine(imgDir, "page_002.jpg");
            using (var stream = File.Create(jpgFile))
            {
                stream.Write(new byte[] { 0xFF, 0xD8, 0xFF, 0xC0, 0, 17, 8, 0x08, 0x34, 0x05, 0x78, 3, 1, 17, 0, 2, 17, 1, 3, 17, 1, 0xFF, 0xD9 }); // 1400x2100
            }

            // WebP
            var webpFile = Path.Combine(imgDir, "page_003.webp");
            using (var stream = File.Create(webpFile))
            {
                stream.Write(new byte[] { 0x52, 0x49, 0x46, 0x46, 0x24, 0, 0, 0, 0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38, 0x58, 10, 0, 0, 0, 0, 0, 0, 0 });
                stream.Write(new byte[] { 0x77, 0x05, 0, 0x33, 0x08, 0 }); // 1400x2100
            }

            // 3. Ingest Pages
            var ingestResult = await ingestionService.IngestChapterFolderAsync(proj.Id, ch.Id, imgDir);
            ingestResult.IsSuccess.Should().BeTrue();
            ingestResult.Value.Should().HaveCount(3);

            Directory.Delete(imgDir, true);

            // 4. Test Canvas Navigation Session
            var rawPages = await pageRepository.GetByChapterIdAsync(ch.Id);
            rawPages.Should().HaveCount(3);

            var pageLayouts = rawPages.Select(p => new Dragonfly44.Application.Rendering.MangaPageLayoutInfo(
                p.Id,
                p.PageNumber,
                p.OriginalFileName,
                storageService.ResolveAbsolutePath(p.ImageRelativePath),
                p.FileFormat,
                p.Width,
                p.Height)).ToList();

            var session = new Dragonfly44.Application.Rendering.CanvasNavigationSession();
            session.LoadPages(pageLayouts, ch.Title);

            // Verify Page Mode
            session.TotalPages.Should().Be(3);
            session.CurrentPageIndex.Should().Be(0);
            session.PageIndicatorText.Should().Be("Page 1 of 3");

            session.NextPage();
            session.CurrentPageIndex.Should().Be(1);
            session.PageIndicatorText.Should().Be("Page 2 of 3");

            // Fit Page
            session.FitPage(1200f, 900f);
            session.Transform.Zoom.Should().BeGreaterThan(0.1f);
            session.Transform.Zoom.Should().BeLessThanOrEqualTo(1.0f);

            // Switch to Continuous Mode
            session.ToggleViewMode();
            session.IsContinuousMode.Should().BeTrue();
            session.CurrentPageIndex.Should().Be(1); // Position preserved!

            // Verify Continuous Layout Offsets
            session.Pages[0].OffsetY.Should().Be(0f);
            session.Pages[1].OffsetY.Should().Be(2100f + session.LayoutCalculator.PageGap);

            // Viewport intersection query
            var visible = session.LayoutCalculator.GetVisiblePageIndices(session.Pages, 0f, 1500f, 0f);
            visible.Should().Contain(0);

            // Overlay Compatibility
            var bubble = new Dragonfly44.Application.Rendering.PageOverlayItem
            {
                PageNumber = 1,
                Bounds = new Dragonfly44.Application.Rendering.NormalizedRect(0.2f, 0.3f, 0.4f, 0.2f),
                ReadingOrder = 1
            };
            var (px, py, pw, ph) = bubble.Bounds.ToPixels(session.Pages[0].Width, session.Pages[0].Height);
            px.Should().Be(280f);
            py.Should().Be(630f);
            pw.Should().Be(560f);
            ph.Should().Be(420f);
        }

        // 5. Test Persistence Across Database Context Restart
        using (var restartScope = provider.CreateScope())
        {
            var projectService = restartScope.ServiceProvider.GetRequiredService<IProjectService>();
            var chapterService = restartScope.ServiceProvider.GetRequiredService<IChapterService>();
            var pageRepository = restartScope.ServiceProvider.GetRequiredService<IPageRepository>();

            var projects = await projectService.GetAllProjectsAsync();
            projects.Should().ContainSingle(p => p.Name == "Berserk");

            var reloadedProj = projects.First(p => p.Name == "Berserk");
            var chapters = await chapterService.GetChaptersByProjectAsync(reloadedProj.Id);
            chapters.Should().ContainSingle();

            var pages = await pageRepository.GetByChapterIdAsync(chapters[0].Id);
            pages.Should().HaveCount(3);
            pages.Select(p => p.FileFormat).Should().Contain(new[] { "PNG", "JPEG", "WEBP" });
        }
    }
}
