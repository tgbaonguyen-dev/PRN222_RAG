namespace BLL.DTOs.Chat;

public sealed class ChatRequest
{
    public Guid? SessionId { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? DocumentId { get; set; }
}

public sealed class ChatResponse
{
    public Guid SessionId { get; set; }
    public string SessionTitle { get; set; } = string.Empty;
    public string Reply { get; set; } = string.Empty;
    public List<ChatSourceDto> Sources { get; set; } = [];
}

public sealed class ChatSourceDto
{
    public Guid DocumentId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string? ChapterTitle { get; set; }
    public int? PageNumber { get; set; }
}

public sealed class ChatSessionSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class ChatMessageDto
{
    public Guid Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Represents a single message in the Gemini Chat API conversation history.
/// </summary>
public sealed class GeminiChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
