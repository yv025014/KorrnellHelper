using KorrnellHelper.Domain.Documents;
using Xunit;

namespace KorrnellHelper.Tests.Documents;

public class ChunkRankerTests
{
    private static DocumentChunk MakeChunk(
        string? heading, int? schoolYear = null, DateOnly? publishedDate = null, string content = "content")
    {
        return new DocumentChunk
        {
            Id = Guid.NewGuid(),
            SourceDocument = "doc.md",
            Heading = heading,
            Content = content,
            Embedding = [0f],
            SchoolYear = schoolYear,
            PublishedDate = publishedDate,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    [Fact]
    public void SelectMostRecentPerTopic_SameHeadingDifferentSchoolYear_KeepsOnlyTheNewest()
    {
        var old = MakeChunk("開學須知", schoolYear: 114, content: "114 學年度內容");
        var newer = MakeChunk("開學須知", schoolYear: 115, content: "115 學年度內容");

        var result = ChunkRanker.SelectMostRecentPerTopic([old, newer]);

        var kept = Assert.Single(result);
        Assert.Equal("115 學年度內容", kept.Content);
    }

    [Fact]
    public void SelectMostRecentPerTopic_DifferentHeadings_KeepsBoth()
    {
        var a = MakeChunk("服裝儀容", schoolYear: 115);
        var b = MakeChunk("疫苗接種提醒", schoolYear: 115);

        var result = ChunkRanker.SelectMostRecentPerTopic([a, b]);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void SelectMostRecentPerTopic_PreservesOriginalSimilarityOrder()
    {
        var best = MakeChunk("服裝儀容", schoolYear: 115);
        var second = MakeChunk("疫苗接種提醒", schoolYear: 115);
        var thirdOlderDuplicateOfBest = MakeChunk("服裝儀容", schoolYear: 114);

        var result = ChunkRanker.SelectMostRecentPerTopic([best, second, thirdOlderDuplicateOfBest]);

        Assert.Equal(2, result.Count);
        Assert.Equal("服裝儀容", result[0].Heading);
        Assert.Equal("疫苗接種提醒", result[1].Heading);
    }

    [Fact]
    public void SelectMostRecentPerTopic_NullHeadings_NeverMergedTogether()
    {
        var a = MakeChunk(heading: null, schoolYear: 114, content: "a");
        var b = MakeChunk(heading: null, schoolYear: 115, content: "b");

        var result = ChunkRanker.SelectMostRecentPerTopic([a, b]);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void SelectMostRecentPerTopic_SameSchoolYearDifferentPublishedDate_PrefersLaterDate()
    {
        var earlier = MakeChunk("放學安排", schoolYear: 115, publishedDate: new DateOnly(2026, 7, 1), content: "舊公告");
        var later = MakeChunk("放學安排", schoolYear: 115, publishedDate: new DateOnly(2026, 7, 31), content: "新公告");

        var result = ChunkRanker.SelectMostRecentPerTopic([earlier, later]);

        var kept = Assert.Single(result);
        Assert.Equal("新公告", kept.Content);
    }

    [Fact]
    public void SelectMostRecentPerTopic_UnknownMetadataRanksBelowKnownMetadata()
    {
        var known = MakeChunk("服裝儀容", schoolYear: 114, content: "已知學年度");
        var unknown = MakeChunk("服裝儀容", schoolYear: null, content: "未知學年度");

        var result = ChunkRanker.SelectMostRecentPerTopic([known, unknown]);

        var kept = Assert.Single(result);
        Assert.Equal("已知學年度", kept.Content);
    }

    [Fact]
    public void SelectMostRecentPerTopic_EmptyInput_ReturnsEmpty()
    {
        var result = ChunkRanker.SelectMostRecentPerTopic([]);

        Assert.Empty(result);
    }
}
