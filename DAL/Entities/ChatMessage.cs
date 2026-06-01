using System;

namespace DAL.Entities;

public class ChatMessage
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    /// <summary>
    /// Message role: "system", "user", or "assistant"
    /// </summary>
    public string Role { get; set; } = null!;

    public string Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual ChatSession Session { get; set; } = null!;
}

/// <summary>
/// Static constants for ChatMessage.Role values, avoiding magic strings.
/// </summary>
public static class ChatRole
{
    public const string System = "system";
    public const string User = "user";
    public const string Assistant = "assistant";
}
