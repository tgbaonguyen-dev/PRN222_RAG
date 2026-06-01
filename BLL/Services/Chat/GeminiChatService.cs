using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BLL.DTOs.Chat;
using BLL.Interfaces.Chat;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BLL.Services.Chat;

/// <summary>
/// Gọi Gemini REST API (generateContent / streamGenerateContent)
/// với API key rotation và retry logic giống GeminiEmbeddingService.
/// </summary>
public class GeminiChatService : IGeminiChatService
{
    private static readonly HttpClient HttpClient = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ILogger<GeminiChatService> _logger;
    private readonly string[] _apiKeys;
    private readonly string _model;
    private int _keyIndex;

    public GeminiChatService(IConfiguration configuration, ILogger<GeminiChatService> logger)
    {
        _logger = logger;

        var apiKeysFromConfig = configuration.GetSection("Gemini:ApiKeys").Get<string[]>() ?? Array.Empty<string>();
        var apiKeysFromEnv = (Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        _apiKeys = apiKeysFromConfig
            .Concat(apiKeysFromEnv)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (_apiKeys.Length == 0)
        {
            throw new InvalidOperationException("Missing Gemini API keys. Set Gemini:ApiKeys or GEMINI_API_KEY.");
        }

        _model = configuration["Gemini:ChatModel"] ?? "gemini-2.5-flash";
        _logger.LogDebug("Gemini chat service initialized. Model={Model}, ApiKeyCount={Count}", _model, _apiKeys.Length);
    }

    /// <summary>
    /// Gửi request đồng bộ tới Gemini và nhận response đầy đủ.
    /// </summary>
    public async Task<string> GenerateAsync(string systemPrompt, List<GeminiChatMessage> history, CancellationToken cancellationToken = default)
    {
        var requestBody = BuildRequestBody(systemPrompt, history);
        Exception? lastError = null;
        var attempts = Math.Max(_apiKeys.Length, 5);

        for (var i = 0; i < attempts; i++)
        {
            var apiKey = GetNextApiKey();
            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={apiKey}";
                var jsonContent = JsonSerializer.Serialize(requestBody, JsonOptions);

                _logger.LogInformation("Gemini chat request. Attempt={Attempt}/{Attempts}, Model={Model}",
                    i + 1, attempts, _model);

                using var response = await HttpClient.PostAsync(
                    url,
                    new StringContent(jsonContent, Encoding.UTF8, "application/json"),
                    cancellationToken);

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Gemini chat failed. Attempt={Attempt}, StatusCode={StatusCode}, Body={Body}",
                        i + 1, (int)response.StatusCode, Truncate(responseBody, 1000));

                    lastError = new InvalidOperationException($"Gemini API returned {(int)response.StatusCode}: {Truncate(responseBody, 500)}");

                    if ((int)response.StatusCode is 429)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    }
                    else if ((int)response.StatusCode is 503)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    }
                    continue;
                }

                return ExtractTextFromResponse(responseBody);
            }
            catch (HttpRequestException ex)
            {
                lastError = ex;
                _logger.LogWarning(ex, "Gemini chat HTTP error. Attempt={Attempt}", i + 1);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }

        throw new InvalidOperationException("Gemini chat failed after all retry attempts.", lastError);
    }

    /// <summary>
    /// Gửi request streaming tới Gemini, yield từng text chunk qua SSE.
    /// </summary>
    public async IAsyncEnumerable<string> StreamGenerateAsync(
        string systemPrompt,
        List<GeminiChatMessage> history,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var requestBody = BuildRequestBody(systemPrompt, history);

        // Obtain the streaming response (retry logic lives here, no yield)
        var (response, stream) = await GetStreamResponseAsync(requestBody, cancellationToken);

        // Read and yield SSE events outside of try-catch
        using (response)
        {
            await using (stream)
            {
                using var reader = new StreamReader(stream);

                while (!reader.EndOfStream)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var line = await reader.ReadLineAsync(cancellationToken);

                    if (string.IsNullOrEmpty(line)) continue;
                    if (!line.StartsWith("data: ")) continue;

                    var dataJson = line["data: ".Length..];
                    var text = ExtractTextFromStreamChunk(dataJson);

                    if (!string.IsNullOrEmpty(text))
                    {
                        yield return text;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Helper: Obtains a successful streaming HTTP response with retry logic.
    /// Separated from the iterator method to avoid yield-in-try-catch.
    /// </summary>
    private async Task<(HttpResponseMessage Response, Stream Body)> GetStreamResponseAsync(
        object requestBody, CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        var attempts = Math.Max(_apiKeys.Length, 5);

        for (var i = 0; i < attempts; i++)
        {
            var apiKey = GetNextApiKey();
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:streamGenerateContent?alt=sse&key={apiKey}";
            var jsonContent = JsonSerializer.Serialize(requestBody, JsonOptions);

            _logger.LogInformation("Gemini stream request. Attempt={Attempt}/{Attempts}, Model={Model}",
                i + 1, attempts, _model);

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
                };

                var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Gemini stream failed. Attempt={Attempt}, StatusCode={StatusCode}", i + 1, (int)response.StatusCode);
                    lastError = new InvalidOperationException($"Gemini stream API returned {(int)response.StatusCode}");

                    if ((int)response.StatusCode is 429)
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    else if ((int)response.StatusCode is 503)
                        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

                    response.Dispose();
                    continue;
                }

                var body = await response.Content.ReadAsStreamAsync(cancellationToken);
                return (response, body);
            }
            catch (HttpRequestException ex)
            {
                lastError = ex;
                _logger.LogWarning(ex, "Gemini stream HTTP error. Attempt={Attempt}", i + 1);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }

        throw new InvalidOperationException("Gemini stream failed after all retry attempts.", lastError);
    }

    private object BuildRequestBody(string systemPrompt, List<GeminiChatMessage> history)
    {
        var contents = new List<object>();

        foreach (var msg in history)
        {
            // Gemini API uses "user" and "model" roles (not "assistant")
            var role = msg.Role == "assistant" ? "model" : msg.Role;
            if (role == "system") continue; // system instruction handled separately

            contents.Add(new
            {
                role,
                parts = new[] { new { text = msg.Content } }
            });
        }

        return new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents,
            generationConfig = new
            {
                temperature = 0.3,
                maxOutputTokens = 8192
            }
        };
    }

    private static string ExtractTextFromResponse(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var candidates = doc.RootElement.GetProperty("candidates");
            var content = candidates[0].GetProperty("content");
            var parts = content.GetProperty("parts");
            var sb = new StringBuilder();
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textEl))
                {
                    sb.Append(textEl.GetString());
                }
            }
            return sb.ToString();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string ExtractTextFromStreamChunk(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var candidates = doc.RootElement.GetProperty("candidates");
            var content = candidates[0].GetProperty("content");
            var parts = content.GetProperty("parts");
            var sb = new StringBuilder();
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textEl))
                {
                    sb.Append(textEl.GetString());
                }
            }
            return sb.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private string GetNextApiKey()
    {
        var index = Interlocked.Increment(ref _keyIndex) % _apiKeys.Length;
        return _apiKeys[index];
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;
        return value[..maxLength] + "...";
    }
}
