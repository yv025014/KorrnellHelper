using System.Text.Json;
using KorrnellHelper.Api.Documents;
using KorrnellHelper.Api.HealthChecks;
using KorrnellHelper.Api.Line;
using KorrnellHelper.Api.Security;
using KorrnellHelper.Application.Ai;
using KorrnellHelper.Application.Documents;
using KorrnellHelper.Application.Line;
using KorrnellHelper.Infrastructure.Ai;
using KorrnellHelper.Infrastructure.Line;
using KorrnellHelper.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services
    .AddOptions<GeminiOptions>()
    .Bind(builder.Configuration.GetSection(GeminiOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<GeminiClient>(client =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/");
});
builder.Services.AddScoped<IEmbeddingGenerator>(sp => sp.GetRequiredService<GeminiClient>());
builder.Services.AddScoped<IAnswerGenerator>(sp => sp.GetRequiredService<GeminiClient>());

builder.Services.AddSingleton<NpgsqlDataSource>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("Supabase")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:Supabase configuration.");
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(
        PostgresConnectionStringFactory.Normalize(connectionString));
    dataSourceBuilder.UseVector();
    return dataSourceBuilder.Build();
});

builder.Services
    .AddHealthChecks()
    .AddCheck<DocumentStoreHealthCheck>("supabase-document-store")
    .AddCheck<GeminiEmbeddingHealthCheck>("gemini-embedding");

builder.Services
    .AddOptions<IngestOptions>()
    .Bind(builder.Configuration.GetSection(IngestOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IDocumentChunkStore, DocumentChunkStore>();
builder.Services.AddScoped<AddDocumentCommandHandler>();
builder.Services.AddScoped<IDocumentChunkSearcher, DocumentChunkSearcher>();
builder.Services.AddScoped<AnswerQuestionQueryHandler>();

builder.Services
    .AddOptions<LineOptions>()
    .Bind(builder.Configuration.GetSection(LineOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<LineReplyClient>(client =>
{
    client.BaseAddress = new Uri("https://api.line.me/v2/bot/");
});
builder.Services.AddScoped<ILineReplyClient>(sp => sp.GetRequiredService<LineReplyClient>());
builder.Services.AddScoped<IAllowedUserStore, AllowedUserStore>();
builder.Services.AddScoped<AddAllowedUserCommandHandler>();
builder.Services.AddScoped<ILineWebhookHandler, LineWebhookHandler>();
builder.Services.AddScoped<LineWebhookDispatcher>();

var app = builder.Build();

// Apply schema.sql idempotently before accepting traffic — see SchemaInitializer for why
// there's no separate migration tool for a single small table.
await using (var scope = app.Services.CreateAsyncScope())
{
    var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
    await SchemaInitializer.InitializeAsync(dataSource);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// No UseHttpsRedirection(): Cloud Run terminates TLS at the platform edge and forwards
// plain HTTP to the container, so the app never sees an "http" request to redirect from
// in production — adding this middleware would just risk a bogus redirect loop. Cloud
// Run's own *.run.app domain already refuses plain HTTP at the edge.
app.UseAuthorization();

app.MapControllers();

app.MapPost("/api/documents", async (
        AddDocumentRequest request,
        AddDocumentCommandHandler handler,
        CancellationToken cancellationToken) =>
    {
        var command = new AddDocumentCommand(
            request.SourceDocument,
            request.MarkdownContent,
            request.SchoolYear,
            request.PublishedDate);
        var chunksCreated = await handler.HandleAsync(command, cancellationToken);
        return Results.Created($"/api/documents/{Uri.EscapeDataString(request.SourceDocument)}", new { chunksCreated });
    })
    .AddEndpointFilter<ApiKeyAuthFilter>();

// Dev/verification surface for this ticket only — the LINE webhook (a later ticket) will call
// AnswerQuestionQueryHandler in-process instead of round-tripping through this HTTP endpoint.
app.MapPost("/api/questions", async (
        AnswerQuestionRequest request,
        AnswerQuestionQueryHandler handler,
        CancellationToken cancellationToken) =>
    {
        // Gemini's embedContent API rejects empty input text (confirmed live while building
        // Add Document) — reject here instead of letting that surface as an opaque 500.
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return Results.BadRequest(new { error = "Question must not be empty." });
        }

        var result = await handler.HandleAsync(new AnswerQuestionQuery(request.Question), cancellationToken);
        return Results.Ok(new
        {
            answer = result.Answer,
            usedChunks = result.UsedChunks.Select(c => new { c.SourceDocument, c.Heading, c.SchoolYear, c.PublishedDate }),
        });
    })
    .AddEndpointFilter<ApiKeyAuthFilter>();

app.MapPost("/webhook/line", async (
    HttpContext context,
    LineWebhookDispatcher dispatcher,
    CancellationToken cancellationToken) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var rawBody = await reader.ReadToEndAsync(cancellationToken);
    var signature = context.Request.Headers["X-Line-Signature"].ToString();

    var isValidSignature = await dispatcher.DispatchAsync(rawBody, signature);
    return isValidSignature ? Results.Ok() : Results.Unauthorized();
});

var isDevelopment = app.Environment.IsDevelopment();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                // Exception details can leak internal wiring (connection/driver errors) to
                // whoever can reach this endpoint — only surface them in Development, where
                // it's just the developer looking at their own machine.
                error = isDevelopment ? entry.Value.Exception?.Message : null,
            }),
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    },
});

app.Run();
