namespace TallerChat.Api;

public static class ChatEndpoints
{
    private const string NoResults =
        "No he encontrado información sobre eso en la base de conocimiento del taller. " +
        "Prueba a reformular la pregunta con otros términos (síntoma, sistema o pieza).";

    public static void MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");
        api.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        api.MapPost("/chat", ChatAsync).RequireRateLimiting("chat");
    }

    private static async Task<IResult> ChatAsync(
        ChatRequest req, KnowledgeService kb, GeminiClient llm, ILogger<GeminiClient> log, CancellationToken ct)
    {
        var question = req.Message?.Trim();
        if (string.IsNullOrEmpty(question) || question.Length > 500)
            return Results.BadRequest(new { error = "La pregunta debe tener entre 1 y 500 caracteres." });

        var history = (req.History ?? [])
            .TakeLast(6)
            .Select(t => new ChatTurn(t.Role == "assistant" ? "assistant" : "user", Cut(t.Text, 1500)))
            .ToList();

        // Preguntas de seguimiento muy cortas ("¿y cada cuánto?"): se amplía la búsqueda con la pregunta previa.
        var query = question;
        if (question.Length < 40 && history.LastOrDefault(t => t.Role == "user") is { } prev)
            query = $"{prev.Text} {question}";

        var chunks = await kb.SearchAsync(query, ct);
        if (chunks.Count == 0) return Results.Ok(new ChatResponse(NoResults, [], false));

        var sources = chunks.Select((c, i) => c.ToSource(i + 1)).ToList();
        var context = string.Join("\n\n", sources.Select(s =>
            $"[{s.Ref}] {s.Title} ({s.Category} / {s.SubCategory})\n" +
            string.Join("\n", s.Sections.Select(x => $"- {x.Label}: {x.Text}"))));

        try
        {
            var answer = await llm.AskAsync(question, history, context, ct);
            return Results.Ok(new ChatResponse(answer, sources, true));
        }
        catch (Exception ex) when (ex is GeminiException or HttpRequestException or TaskCanceledException)
        {
            log.LogWarning(ex, "Fallo al consultar Gemini");
            return Results.Ok(new ChatResponse(
                "No he podido redactar la respuesta ahora mismo (el modelo no responde o alcanzó su límite de uso). " +
                "Estas son las fuentes encontradas en la base de conocimiento.", sources, true));
        }
    }

    private static string Cut(string? s, int max) => s is null ? "" : s.Length <= max ? s : s[..max];
}
