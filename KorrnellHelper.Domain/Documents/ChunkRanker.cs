namespace KorrnellHelper.Domain.Documents;

/// <summary>
/// Collapses duplicate topics in a similarity-ranked candidate list down to
/// their most recent version — e.g. two years' "服裝儀容" sections both
/// matching a query should surface only the newer one, not both.
///
/// Topics are matched by exact <see cref="DocumentChunk.Heading"/> text. This
/// is safe only because headings aren't arbitrary user input — they always
/// come from the pdf-to-notice-md skill's transcription, where each "## "
/// heading is meant to name one coherent topic. Two genuinely unrelated
/// notices that happened to share generic heading text would incorrectly
/// merge; that's an accepted simplification given the closed, project-
/// controlled vocabulary of headings, not a general-purpose topic matcher.
/// </summary>
public static class ChunkRanker
{
    public static IReadOnlyList<DocumentChunk> SelectMostRecentPerTopic(
        IReadOnlyList<DocumentChunk> chunksOrderedBySimilarity)
    {
        var bestByTopic = new Dictionary<object, DocumentChunk>();
        var topicOrder = new List<object>();

        foreach (var chunk in chunksOrderedBySimilarity)
        {
            // A missing heading means "no known topic to merge on" — treat each
            // such chunk as its own topic rather than collapsing them together.
            object topicKey = chunk.Heading ?? (object)chunk.Id;

            if (!bestByTopic.TryGetValue(topicKey, out var existing))
            {
                bestByTopic[topicKey] = chunk;
                topicOrder.Add(topicKey);
            }
            else if (IsMoreRecent(chunk, existing))
            {
                bestByTopic[topicKey] = chunk;
            }
        }

        return topicOrder.Select(key => bestByTopic[key]).ToList();
    }

    private static bool IsMoreRecent(DocumentChunk candidate, DocumentChunk current)
    {
        var candidateYear = candidate.SchoolYear ?? int.MinValue;
        var currentYear = current.SchoolYear ?? int.MinValue;
        if (candidateYear != currentYear)
        {
            return candidateYear > currentYear;
        }

        var candidateDate = candidate.PublishedDate ?? DateOnly.MinValue;
        var currentDate = current.PublishedDate ?? DateOnly.MinValue;
        return candidateDate > currentDate;
    }
}
