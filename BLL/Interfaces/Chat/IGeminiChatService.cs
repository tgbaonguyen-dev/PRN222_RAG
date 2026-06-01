using BLL.DTOs.Chat;

namespace BLL.Interfaces.Chat;

public interface IGeminiChatService
{
    Task<string> GenerateAsync(string systemPrompt, List<GeminiChatMessage> history, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> StreamGenerateAsync(string systemPrompt, List<GeminiChatMessage> history, CancellationToken cancellationToken = default);
}
