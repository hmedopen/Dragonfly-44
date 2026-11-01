namespace Dragonfly44.Infrastructure.Configuration;

public class StorageOptions
{
    public const string SectionName = "Storage";
    public string AppDataDirectory { get; set; } = string.Empty;
    public string LogsDirectory { get; set; } = string.Empty;
    public string ProjectsRootDirectory { get; set; } = string.Empty;
}
