using System;

namespace TaskApp.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class UserExportMetadata
{
    public DateTime ExportedAt { get; set; }
    public string AppVersion { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public Guid OriginalUserId { get; set; }
}
