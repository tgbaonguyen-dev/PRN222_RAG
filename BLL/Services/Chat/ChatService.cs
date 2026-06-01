using System.Runtime.CompilerServices;
using System.Text;
using BLL.DTOs.Chat;
using BLL.Interfaces.Chat;
using BLL.Interfaces.Documents;
using DAL.Entities;
using DAL.Interfaces.Chat;
using Microsoft.Extensions.Logging;

namespace BLL.Services.Chat;

public class ChatService : IChatService
{
    private const int MaxHistoryMessages = 10;
    private const int TopKChunks = 5;

    private readonly IChatRepository _chatRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IGeminiChatService _geminiChatService;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        IChatRepository chatRepository,
        IEmbeddingService embeddingService,
        IGeminiChatService geminiChatService,
        ILogger<ChatService> logger)
    {
        _chatRepository = chatRepository;
        _embeddingService = embeddingService;
        _geminiChatService = geminiChatService;
        _logger = logger;
    }

    /// <summary>
    /// Luồng chính: nhận message → embed → tìm vector → build prompt → gọi LLM → lưu DB → trả kết quả
    /// </summary>
    public async Task<ChatResponse> SendMessageAsync(Guid userId, ChatRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Tạo hoặc lấy Session
            var session = await GetOrCreateSessionAsync(userId, request, cancellationToken);

            // 2. Lưu tin nhắn User vào DB
            await SaveUserMessageAsync(session.Id, request.Message, cancellationToken);

            // 3. Lấy lịch sử hội thoại gần nhất
            var historyMessages = await _chatRepository.GetRecentMessagesAsync(session.Id, MaxHistoryMessages, cancellationToken);

            // 4. Embed câu hỏi thành vector
            _logger.LogInformation("Embedding user question for session {SessionId}", session.Id);
            var queryEmbedding = await _embeddingService.EmbedAsync(request.Message, cancellationToken);

            // 5. Tìm Top K chunks liên quan
            _logger.LogInformation("Searching similar chunks. DocumentId={DocumentId}, TopK={TopK}", request.DocumentId, TopKChunks);
            var relevantChunks = await _chatRepository.SearchSimilarChunksAsync(queryEmbedding, TopKChunks, request.DocumentId, cancellationToken);

            // 6. Build System Prompt
            var systemPrompt = BuildSystemPrompt(relevantChunks);

            // 7. Build conversation history cho Gemini
            var geminiHistory = BuildGeminiHistory(historyMessages);

            // 8. Gọi Gemini để sinh câu trả lời
            _logger.LogInformation("Calling Gemini for chat response. SessionId={SessionId}, ChunksFound={ChunksFound}", session.Id, relevantChunks.Count);
            var reply = await _geminiChatService.GenerateAsync(systemPrompt, geminiHistory, cancellationToken);

            // 9. Lưu response Assistant vào DB
            await SaveAssistantMessageAsync(session.Id, reply, cancellationToken);

            // 10. Extract sources và trả về
            var sources = ExtractSources(relevantChunks);

            return new ChatResponse
            {
                SessionId = session.Id,
                SessionTitle = session.Title,
                Reply = reply,
                Sources = sources
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendMessageAsync failed. UserId={UserId}, SessionId={SessionId}", userId, request.SessionId);
            throw;
        }
    }

    /// <summary>
    /// Luồng Streaming: tương tự SendMessageAsync nhưng yield từng chunk text ra cho UI hiển thị real-time.
    /// Lưu ý: response đầy đủ sẽ được lưu vào DB sau khi stream xong.
    /// </summary>
    public async IAsyncEnumerable<string> StreamMessageAsync(
        Guid userId,
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 1-5: Chuẩn bị giống SendMessageAsync
        var session = await GetOrCreateSessionAsync(userId, request, cancellationToken);
        await SaveUserMessageAsync(session.Id, request.Message, cancellationToken);
        var historyMessages = await _chatRepository.GetRecentMessagesAsync(session.Id, MaxHistoryMessages, cancellationToken);
        var queryEmbedding = await _embeddingService.EmbedAsync(request.Message, cancellationToken);
        var relevantChunks = await _chatRepository.SearchSimilarChunksAsync(queryEmbedding, TopKChunks, request.DocumentId, cancellationToken);
        var systemPrompt = BuildSystemPrompt(relevantChunks);
        var geminiHistory = BuildGeminiHistory(historyMessages);

        _logger.LogInformation("Streaming Gemini response. SessionId={SessionId}, ChunksFound={ChunksFound}", session.Id, relevantChunks.Count);

        // 6. Stream response
        var fullResponse = new StringBuilder();
        await foreach (var chunk in _geminiChatService.StreamGenerateAsync(systemPrompt, geminiHistory, cancellationToken))
        {
            fullResponse.Append(chunk);
            yield return chunk;
        }

        // 7. Lưu response đầy đủ vào DB sau khi stream xong
        var completeReply = fullResponse.ToString();
        if (!string.IsNullOrWhiteSpace(completeReply))
        {
            await SaveAssistantMessageAsync(session.Id, completeReply, cancellationToken);
        }
    }

    public async Task<List<ChatSessionSummaryDto>> GetSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var sessions = await _chatRepository.GetUserSessionsAsync(userId, cancellationToken);
        return sessions.Select(s => new ChatSessionSummaryDto
        {
            Id = s.Id,
            Title = s.Title,
            CreatedAt = s.CreatedAt
        }).ToList();
    }

    public async Task<List<ChatMessageDto>> GetSessionMessagesAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        // Verify session belongs to user
        var session = await _chatRepository.GetSessionAsync(sessionId, cancellationToken);
        if (session == null || session.UserId != userId)
        {
            return [];
        }

        var messages = await _chatRepository.GetRecentMessagesAsync(sessionId, 100, cancellationToken);
        return messages.Select(m => new ChatMessageDto
        {
            Id = m.Id,
            Role = m.Role,
            Content = m.Content,
            CreatedAt = m.CreatedAt
        }).ToList();
    }

    #region Private Helpers

    private async Task<ChatSession> GetOrCreateSessionAsync(Guid userId, ChatRequest request, CancellationToken cancellationToken)
    {
        if (request.SessionId.HasValue)
        {
            var existing = await _chatRepository.GetSessionAsync(request.SessionId.Value, cancellationToken);
            if (existing != null && existing.UserId == userId)
            {
                return existing;
            }
        }

        // Tạo session mới, lấy 50 ký tự đầu của message làm Title
        var title = request.Message.Length > 50
            ? request.Message[..50] + "..."
            : request.Message;

        var session = new ChatSession
        {
            UserId = userId,
            Title = title,
            CreatedAt = DateTime.UtcNow
        };

        return await _chatRepository.CreateSessionAsync(session, cancellationToken);
    }

    private async Task SaveUserMessageAsync(Guid sessionId, string content, CancellationToken cancellationToken)
    {
        var message = new ChatMessage
        {
            SessionId = sessionId,
            Role = ChatRole.User,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };
        await _chatRepository.AddMessageAsync(message, cancellationToken);
    }

    private async Task SaveAssistantMessageAsync(Guid sessionId, string content, CancellationToken cancellationToken)
    {
        var message = new ChatMessage
        {
            SessionId = sessionId,
            Role = ChatRole.Assistant,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };
        await _chatRepository.AddMessageAsync(message, cancellationToken);
    }

    /// <summary>
    /// Build System Prompt chặt chẽ cho RAG: grounded generation + trích dẫn nguồn
    /// </summary>
    private static string BuildSystemPrompt(List<DocumentChunk> chunks)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Bạn là trợ lý thông tin nội bộ của hệ thống tài liệu học thuật.");
        sb.AppendLine();
        sb.AppendLine("### LUẬT BẮT BUỘC:");
        sb.AppendLine("1. CHỈ sử dụng thông tin trong phần [CONTEXT] bên dưới để trả lời. TUYỆT ĐỐI KHÔNG được bịa, suy đoán, hoặc sử dụng kiến thức bên ngoài.");
        sb.AppendLine("2. Nếu không tìm thấy thông tin liên quan trong [CONTEXT], trả lời CHÍNH XÁC: \"Xin lỗi, tôi không tìm thấy thông tin liên quan trong tài liệu được cung cấp.\"");
        sb.AppendLine("3. Cuối mỗi câu/đoạn cung cấp thông tin, BẮT BUỘC trích dẫn theo format: (Nguồn: [Tên tài liệu] - [Tên chương], Trang [số trang])");
        sb.AppendLine("4. Trả lời bằng Tiếng Việt, rõ ràng, có cấu trúc, sử dụng markdown khi cần thiết.");
        sb.AppendLine("5. Nếu câu hỏi là lời chào hỏi thông thường (xin chào, hello, hi, ...), hãy chào lại lịch sự và giới thiệu ngắn gọn rằng bạn là trợ lý tài liệu.");
        sb.AppendLine();

        if (chunks.Count == 0)
        {
            sb.AppendLine("[CONTEXT]");
            sb.AppendLine("Không có tài liệu nào được cung cấp.");
            sb.AppendLine("[/CONTEXT]");
        }
        else
        {
            sb.AppendLine("[CONTEXT]");
            for (var i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                var docTitle = chunk.Document?.Title ?? "N/A";
                var chapterTitle = chunk.Chapter?.Title ?? "N/A";
                var page = chunk.PageNumber?.ToString() ?? "N/A";

                sb.AppendLine($"--- Chunk {i + 1} ---");
                sb.AppendLine($"Tài liệu: {docTitle}");
                sb.AppendLine($"Chương: {chapterTitle}");
                sb.AppendLine($"Trang: {page}");
                sb.AppendLine("Nội dung:");
                sb.AppendLine(chunk.Content);
                sb.AppendLine();
            }
            sb.AppendLine("[/CONTEXT]");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Chuyển đổi danh sách ChatMessage từ DB thành format cho Gemini API
    /// </summary>
    private static List<GeminiChatMessage> BuildGeminiHistory(List<ChatMessage> historyMessages)
    {
        return historyMessages
            .Where(m => m.Role is ChatRole.User or ChatRole.Assistant)
            .Select(m => new GeminiChatMessage
            {
                Role = m.Role,
                Content = m.Content
            })
            .ToList();
    }

    /// <summary>
    /// Trích xuất metadata nguồn từ các chunks tìm được (deduplicated)
    /// </summary>
    private static List<ChatSourceDto> ExtractSources(List<DocumentChunk> chunks)
    {
        return chunks
            .Where(c => c.Document != null)
            .Select(c => new ChatSourceDto
            {
                DocumentId = c.DocumentId,
                DocumentTitle = c.Document!.Title,
                ChapterTitle = c.Chapter?.Title,
                PageNumber = c.PageNumber
            })
            .GroupBy(s => new { s.DocumentId, s.ChapterTitle, s.PageNumber })
            .Select(g => g.First())
            .ToList();
    }

    #endregion
}
