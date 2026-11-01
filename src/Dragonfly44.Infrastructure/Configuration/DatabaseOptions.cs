namespace Dragonfly44.Infrastructure.Configuration;

public class DatabaseOptions
{
    public const string SectionName = "Database";
    public string ConnectionString { get; set; } = string.Empty;
    public string? DatabaseFileName { get; set; } = "dragonfly44.db";
    public bool AutoMigrate { get; set; } = true;
}
