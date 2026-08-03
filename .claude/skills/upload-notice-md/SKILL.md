---
name: upload-notice-md
description: Upload one or more reviewed Markdown notices from resource/ to the korrnellHelper backend's Add Document API, so they become searchable/answerable. Use when the user asks to upload, ingest, or index a Markdown file from resource/ after they've reviewed it. Never runs on a raw PDF — that's pdf-to-notice-md's job.
---

# Upload Notice Markdown

Sends one or more already-converted, already-reviewed Markdown files from
`resource/` to the backend's `POST /api/documents` endpoint, so their content
gets chunked, embedded, and stored for the Answer Question API to retrieve
later. This is a deliberately separate, manual step from conversion — see
`pdf-to-notice-md` for why.

## Inputs

The user provides one or more file paths under `resource/`. If they haven't,
ask which file(s) — don't guess or upload every file in the directory
unprompted, since some files there may not have been reviewed yet. If a
given path doesn't exist, say so and stop for that file rather than
guessing what they meant.

## Prerequisites

Read `.env.local` at the project root for two values:

- `KORRNELL_API_KEY` — required. If the file or the key is missing, stop and
  tell the user to add it (this is the same key set on the backend via
  `dotnet user-secrets set "Ingest:ApiKey" ...`).
- `KORRNELL_API_BASE_URL` — the backend's base URL. If missing, default to
  `http://localhost:5299` (the local dev server) and mention that default to
  the user, since it'll need to change once the backend is deployed.

## Steps, for each file

1. **Read the file.** If it has no `---`-delimited frontmatter block at the
   top at all, stop for that file and tell the user — it doesn't look like
   something `pdf-to-notice-md` produced, don't guess at metadata to fill
   the gap.

2. **Parse the frontmatter as plain `key: value` text lines, not with a
   general-purpose YAML parser.** This matters: a real YAML parser infers
   types, and an unquoted date like `published_date: 2026-07-31` (exactly
   how `pdf-to-notice-md` writes it) parses to a native date object that
   then fails to JSON-serialize in step 4. Reading each line as literal text
   and trimming quotes/whitespace avoids that entirely, and this frontmatter
   is simple enough (flat, one key per line) that nothing is lost by doing
   so.
   - `source_file` → becomes the request's `sourceDocument`. If it's empty,
     fall back to the Markdown file's own filename instead.
   - `school_year` → becomes `schoolYear`, sent as a **JSON number**
     (`115`, not `"115"`). Empty/missing → JSON `null`, not `0` or an empty
     string — the backend treats this as "unknown", not "year zero".
   - `published_date` → becomes `publishedDate`, sent as a **JSON string**
     in `YYYY-MM-DD` form, exactly as it appears in the frontmatter — don't
     reformat or reparse it. Empty/missing → JSON `null`.
   - `title` is informational only — it isn't sent to the API.

3. **Take everything after the closing `---`** as `markdownContent`, with
   leading blank lines trimmed. Frontmatter itself must NOT be included —
   the backend chunks by `##` heading and has no frontmatter parser.

4. **Build the request body** as JSON:
   ```json
   {
     "sourceDocument": "...",
     "markdownContent": "...",
     "schoolYear": 115,
     "publishedDate": "2026-07-31"
   }
   ```
   Build this with a script (e.g. Python's `json.dump` on a plain `dict` of
   the string/int/None values from step 2 — not anything YAML-typed), not by
   hand-interpolating a shell string — the content is Traditional Chinese
   prose with quotes, newlines, and tables in it, and manual escaping will
   corrupt it.

5. **POST it**: `{KORRNELL_API_BASE_URL}/api/documents`, header
   `X-Api-Key: {KORRNELL_API_KEY}`, `Content-Type: application/json`. If the
   request fails to connect at all (not an HTTP error response, but a
   connection refused/timeout), don't treat that like a 401 or retry blindly
   — tell the user the backend at `KORRNELL_API_BASE_URL` seems unreachable
   and to confirm it's actually running.

6. **Report the result** for each file:
   - `201` → success. Report the `chunksCreated` count from the response —
     this comes from the backend's own database transaction actually
     committing, so a non-zero count here already *is* the confirmation
     that the chunks landed in Supabase (no separate verification query is
     needed to trust it).
   - `401` → the API key is wrong or missing; tell the user to check
     `.env.local` against the backend's configured key, don't retry blindly.
   - Anything else → show the response body (it usually explains what went
     wrong) rather than just the status code.

## Explicitly out of scope

- Never re-derive or guess metadata that should already be in the
  frontmatter — if `pdf-to-notice-md` left a field empty for the user to
  fill in by hand and it's still empty, upload it as `null` and say so;
  don't try to infer it here.
- Never modify the source `.md` file in `resource/`.
