---
name: pdf-to-notice-md
description: Convert a Korrnell Academy school notice PDF into a structured Markdown file with metadata frontmatter, saved into resource/. Use when the user gives a PDF file path and asks to convert, transcribe, or ingest a school notice for korrnellHelper. Does not upload anything — conversion only.
---

# PDF → Notice Markdown

Converts one school notice PDF into one Markdown file that preserves the source
document's structure, ready for later chunking by the backend. This skill only
writes a local file — it never calls the Add Document API or any network
endpoint.

## Inputs

The user provides a path to a PDF file. If they haven't, ask for it.

## Steps

1. **Read the PDF** at the given path (all pages).

2. **Convert to Markdown**, preserving the source document's structure:
   - The document's main title becomes an `#` heading.
   - Each distinct section (e.g. things marked like `< 學校運作說明 >`,
     numbered headings, or otherwise visually distinct blocks) becomes its own
     `##` heading. Keep the section boundaries the source actually uses — don't
     invent new groupings and don't merge unrelated sections together.
   - Tables become Markdown tables, with columns/rows matching the source
     table's layout (not the raw left-to-right text extraction order, which
     can be jumbled — read the table visually).
   - Lists, bolded warnings, and specific dates/times/phone numbers must be
     transcribed exactly as written. Do not paraphrase or summarize numeric
     or date content.
   - Do not add commentary, summaries, or content that isn't in the source
     PDF. This is a transcription, not a rewrite.
   - Decorative images/screenshots (e.g. app UI screenshots) can be noted
     with a short `*[screenshot: ...]*` placeholder if they're referenced by
     the surrounding text (like "見下方示意圖"), but don't attempt to
     transcribe their visual content in detail.

3. **Extract metadata** for the YAML frontmatter:
   - `title`: the document's main title.
   - `school_year`: look for an explicit "OOO學年度" mention in the text
     (e.g. "115學年度"). If the document doesn't state one anywhere, leave
     this field empty — do not guess from event dates alone. If more than
     one distinct school year is mentioned (e.g. a notice spanning a
     transition between two years), use the one the document's own title or
     main subject refers to, not just whichever appears first.
   - `published_date`: the date the notice itself was issued/announced —
     look for an explicit issuance marker near the top of the document (e.g.
     "OO/OO(O)公告", "發布日期", "OO/OO 15:00公告..."). Do NOT substitute a
     content-anchor date like "開學日" (first day of school) or any other
     date the notice merely instructs action on — those describe events the
     notice is *about*, not when it was *issued*, and can easily be weeks
     apart from the true announcement date. If multiple issuance-style
     markers appear, use the earliest one (that's when the notice first went
     out). If no explicit issuance marker exists anywhere in the text, leave
     this field empty rather than guessing from an unrelated date.
   - `source_file`: the original PDF's filename (not the full path).
   - Only fill a field when you're actually confident — an empty field the
     user fixes by hand is much better than a wrong guess that silently
     misleads retrieval later.

4. **Determine the output filename**:
   - Format: `{published_date}_{title-slug}.md`, e.g.
     `2026-07-31_小一暑期銜接課程暨新生訓練須知.md`.
   - `published_date` uses `YYYY-MM-DD`. If `published_date` couldn't be
     determined in step 3, prefix the filename with `UNKNOWN-DATE_` instead
     of a date, so it's still sorted visibly apart from dated files rather
     than silently guessed.
   - `title-slug` is the document's title with `/` and other
     filesystem-unsafe characters removed; keep it human-readable (Chinese
     characters are fine).

5. **Write the file** to `resource/<filename>` at the project root, where
   `<filename>` is the full name (including the `.md` extension) produced in
   step 4 — don't append a second `.md`. Create the `resource/` directory if
   it doesn't exist yet. Do not overwrite an
   existing file with the same name without asking — school notices can
   share very similar titles across school years, so if a name collision
   happens, confirm with the user whether it's actually a duplicate or needs
   a distinguishing suffix.

6. **Report back to the user**:
   - The output file path.
   - Any frontmatter fields left empty, and why (what you looked for but
     couldn't confidently find), so they know what to fill in by hand before
     using the upload step.

## Explicitly out of scope

- Never call the Add Document API or any other network endpoint — this skill
  only produces a local file. Uploading is a separate, deliberate step the
  user triggers themselves after reviewing the output.
- Never chunk the Markdown content — chunk-splitting by heading happens in
  the backend at upload time, not here.
