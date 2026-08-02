using KorrnellHelper.Domain.Chunking;
using Xunit;

namespace KorrnellHelper.Tests.Chunking;

public class MarkdownChunkerTests
{
    [Fact]
    public void Split_OneChunkPerH2Heading_TitleAndIntroBecomeTheFirstChunk()
    {
        const string markdown = """
            # 小一暑期銜接課程暨新生訓練須知

            7/31(五)15:00公告一年級班級。

            ## 重要日期與須辦理事項整理表

            | 序號 | 日期 |
            |---|---|
            | 1 | 7/31 |

            ## 學校運作說明

            ### 學校作息規劃及請假

            #### 上學安排

            學生入校時間：早上7:30至8:00
            """;

        var sections = MarkdownChunker.Split(markdown);

        Assert.Equal(3, sections.Count);

        Assert.Equal("小一暑期銜接課程暨新生訓練須知", sections[0].Heading);
        Assert.Contains("7/31(五)15:00公告一年級班級", sections[0].Content);
        Assert.DoesNotContain("重要日期與須辦理事項整理表", sections[0].Content);

        Assert.Equal("重要日期與須辦理事項整理表", sections[1].Heading);
        Assert.Contains("| 1 | 7/31 |", sections[1].Content);
        Assert.DoesNotContain("學校運作說明", sections[1].Content);

        Assert.Equal("學校運作說明", sections[2].Heading);
        Assert.Contains("學校作息規劃及請假", sections[2].Content);
        Assert.Contains("上學安排", sections[2].Content);
        Assert.Contains("早上7:30至8:00", sections[2].Content);
    }

    [Fact]
    public void Split_NoH2Headings_WholeDocumentIsOneChunk()
    {
        const string markdown = """
            # 標題

            內文第一段。
            內文第二段。
            """;

        var sections = MarkdownChunker.Split(markdown);

        var section = Assert.Single(sections);
        Assert.Equal("標題", section.Heading);
        Assert.Contains("內文第一段", section.Content);
        Assert.Contains("內文第二段", section.Content);
    }

    [Fact]
    public void Split_NoH1Title_FirstChunkHasNullHeading()
    {
        const string markdown = """
            這是一段沒有標題的開頭文字。

            ## 第一節

            內容
            """;

        var sections = MarkdownChunker.Split(markdown);

        Assert.Equal(2, sections.Count);
        Assert.Null(sections[0].Heading);
        Assert.Contains("這是一段沒有標題的開頭文字", sections[0].Content);
        Assert.Equal("第一節", sections[1].Heading);
    }

    [Fact]
    public void Split_StartsDirectlyWithH2_DoesNotEmitEmptyLeadingChunk()
    {
        const string markdown = """
            ## 第一節

            內容一

            ## 第二節

            內容二
            """;

        var sections = MarkdownChunker.Split(markdown);

        Assert.Equal(2, sections.Count);
        Assert.Equal("第一節", sections[0].Heading);
        Assert.Equal("第二節", sections[1].Heading);
    }

    [Fact]
    public void Split_HeadingImmediatelyFollowedByAnotherHeading_SkipsTheEmptySection()
    {
        // A title with no intro paragraph before the first "## " is a normal
        // document shape — the resulting section must never carry empty
        // Content, since that gets embedded and Gemini's API rejects empty
        // input text.
        const string markdown = """
            # 標題

            ## 第一節

            內容
            """;

        var sections = MarkdownChunker.Split(markdown);

        var section = Assert.Single(sections);
        Assert.Equal("第一節", section.Heading);
        Assert.Equal("內容", section.Content);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n\n  ")]
    public void Split_BlankInput_ReturnsEmpty(string markdown)
    {
        var sections = MarkdownChunker.Split(markdown);

        Assert.Empty(sections);
    }

    [Fact]
    public void Split_DoesNotTreatH3OrDeeperAsChunkBoundaries()
    {
        const string markdown = """
            ## 服裝儀容

            ### 暑期課程期間

            請穿著便服。

            ### 正式開學後

            應穿著全套制式服裝。
            """;

        var sections = MarkdownChunker.Split(markdown);

        var section = Assert.Single(sections);
        Assert.Equal("服裝儀容", section.Heading);
        Assert.Contains("暑期課程期間", section.Content);
        Assert.Contains("正式開學後", section.Content);
    }
}
