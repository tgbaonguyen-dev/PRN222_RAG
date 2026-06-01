using DAL.Data;
using DAL.Entities;
using DAL.Interfaces.Chat;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace DAL.Repositories.Chat;

public class ChatRepository : IChatRepository
{
    private readonly DBContext _context;

    public ChatRepository(DBContext context)
    {
        _context = context;
    }

    public async Task<ChatSession> CreateSessionAsync(ChatSession session, CancellationToken cancellationToken = default)
    {
        _context.ChatSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<ChatSession?> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return await _context.ChatSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
    }

    public async Task<List<ChatSession>> GetUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.ChatSessions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<ChatMessage> AddMessageAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);
        return message;
    }

    public async Task<List<ChatMessage>> GetRecentMessagesAsync(Guid sessionId, int count, CancellationToken cancellationToken = default)
    {
        // Lấy N messages gần nhất theo CreatedAt DESC, rồi đảo ngược để có thứ tự chronological
        var messages = await _context.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

        messages.Reverse();
        return messages;
    }

    /// <summary>
    /// Tìm kiếm Top K document chunks có vector embedding gần nhất với câu hỏi (Cosine Distance).
    /// Include thông tin Document và Chapter để phục vụ trích dẫn nguồn.
    /// </summary>
    public async Task<List<DocumentChunk>> SearchSimilarChunksAsync(
        Vector queryEmbedding,
        int topK,
        Guid? documentId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.DocumentChunks
            .Where(c => c.Embedding != null);

        if (documentId.HasValue)
        {
            query = query.Where(c => c.DocumentId == documentId.Value);
        }

        return await query
            .OrderBy(c => c.Embedding!.CosineDistance(queryEmbedding))
            .Take(topK)
            .Include(c => c.Document)
            .Include(c => c.Chapter)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateSessionTitleAsync(Guid sessionId, string title, CancellationToken cancellationToken = default)
    {
        await _context.ChatSessions
            .Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Title, title), cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
