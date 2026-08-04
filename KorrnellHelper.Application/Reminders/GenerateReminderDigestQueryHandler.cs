using KorrnellHelper.Application.Ai;
using KorrnellHelper.Application.Documents;
using KorrnellHelper.Application.Line;
using KorrnellHelper.Domain.Documents;

namespace KorrnellHelper.Application.Reminders;

/// <summary>
/// Scans every currently-valid document chunk (not a top-k similarity search — this task
/// requires completeness, not "most relevant to one question") and asks the AI to identify
/// activities whose date range overlaps the next <see cref="WindowDays"/> days. Reused
/// unchanged by both the admin's on-demand "#TestReminder" command and the scheduled push to
/// the full whitelist, so both are guaranteed to see identical content for the same day.
/// </summary>
public sealed class GenerateReminderDigestQueryHandler(
    IDocumentChunkStore store,
    IAnswerGenerator answerGenerator,
    TimeProvider timeProvider)
{
    private const int WindowDays = 7;

    // The AI is asked to reply with exactly this literal string when nothing qualifies,
    // distinguishing "genuinely nothing upcoming" from any other short reply.
    private const string NoActivitiesSentinel = "NO_UPCOMING_ACTIVITIES";

    private const string NoActivitiesMessage =
        "您好！我是「康乃爾小幫手」🔔\n目前未來 7 天內沒有需要留意的活動或截止日期。";

    // Baked in here (not left to the model) so both consumers get byte-identical framing
    // regardless of how the AI phrases the activity list itself.
    private const string DigestHeader =
        "您好！我是「康乃爾小幫手」🔔\n以下是系統自動整理未來一週的重要活動與辦理事項：\n";

    private const string DigestFooter =
        "\n\n⚠️ 以上內容為系統自動整理，如有疑問或內容有誤，請以 Korrnell APP 上的原始公告為準。";

    public async Task<GenerateReminderDigestResult> HandleAsync(CancellationToken cancellationToken = default)
    {
        var allChunks = await store.GetAllAsync(cancellationToken);
        var currentChunks = ChunkRanker.SelectMostRecentPerTopic(allChunks);

        if (currentChunks.Count == 0)
        {
            return new GenerateReminderDigestResult(false, NoActivitiesMessage);
        }

        var referenceDate = GetTaipeiReferenceDate();
        var prompt = BuildPrompt(referenceDate, currentChunks);
        var rawResponse = await answerGenerator.GenerateAsync(prompt, cancellationToken);

        if (rawResponse.Trim() == NoActivitiesSentinel)
        {
            return new GenerateReminderDigestResult(false, NoActivitiesMessage);
        }

        var digest = LineAnswerFormatter.Format(DigestHeader + rawResponse.Trim() + DigestFooter);
        return new GenerateReminderDigestResult(true, digest);
    }

    // "Today" must be Taipei-local, not the server's UTC date — #TestReminder can be triggered
    // at any hour, and near-midnight UTC that's a different calendar day in Taipei.
    private DateOnly GetTaipeiReferenceDate()
    {
        var taipeiZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");
        var taipeiNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), taipeiZone);
        return DateOnly.FromDateTime(taipeiNow.DateTime);
    }

    private static string BuildPrompt(DateOnly referenceDate, IReadOnlyList<DocumentChunk> chunks)
    {
        var windowEnd = referenceDate.AddDays(WindowDays - 1);
        var context = DocumentChunkPromptFormatter.FormatAsContext(chunks);

        return $"""
            你是「康乃爾小幫手」,協助家長掌握學校通知單裡需要留意的重要活動。請只根據下方提供的參考資料判斷,不要編造參考資料中沒有的資訊。

            今天的日期是 {referenceDate:yyyy-MM-dd}。請找出「活動的時間範圍(開始日期～結束日期)」與「{referenceDate:yyyy-MM-dd} 到 {windowEnd:yyyy-MM-dd}」這個窗口有重疊的活動——只要時間範圍有重疊就算,不是只看某個日期點是否剛好落在窗口內(例如活動已經開始、但還沒結束,也算重疊)。

            找到的每一筆活動,請依照以下格式列出,依時間先後排序,可以有多筆:

            1. [活動名稱]([狀態描述,例如:進行中～8/7止 / 8/13～8/17])
            • 辦理事項：...
            • 開始時間：...
            • 截止時間：...
            • 辦理方式：...

            如果某個日期不確定是否為需要家長採取行動的開始日或截止日(例如只是文中順帶提到、不是真正的待辦事項),請不要列入,寧可排除不確定的,也不要誤判。

            如果掃描完所有參考資料後,沒有任何活動符合上述條件,請只回覆這一段文字,不要加任何其他內容："{NoActivitiesSentinel}"

            參考資料:
            {context}
            """;
    }
}
