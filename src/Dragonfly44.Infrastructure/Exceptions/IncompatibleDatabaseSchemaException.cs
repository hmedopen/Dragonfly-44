namespace Dragonfly44.Infrastructure.Exceptions;

/// <summary>
/// Exception thrown when an existing database schema does not match the expected structure
/// and cannot be safely migrated without risking data loss or corruption.
/// </summary>
public class IncompatibleDatabaseSchemaException : Exception
{
    public string? BackupPath { get; }

    public IncompatibleDatabaseSchemaException(string message, string? backupPath = null)
        : base(message)
    {
        BackupPath = backupPath;
    }

    public IncompatibleDatabaseSchemaException(string message, Exception innerException, string? backupPath = null)
        : base(message, innerException)
    {
        BackupPath = backupPath;
    }
}
