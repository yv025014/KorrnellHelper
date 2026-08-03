-- korrnellHelper document chunk store.
-- Applied idempotently on startup by SchemaInitializer — safe to run repeatedly.

create extension if not exists vector;

create table if not exists document_chunks (
    id uuid primary key default gen_random_uuid(),
    source_document text not null,
    heading text,
    content text not null,
    embedding vector(768) not null,
    school_year integer,
    published_date date,
    created_at timestamptz not null default now()
);

create index if not exists document_chunks_embedding_idx
    on document_chunks using hnsw (embedding vector_cosine_ops);

-- Whitelist entries added at runtime via the LINE "#AddUser=" command, in addition
-- to the static admin(s) configured via the Line:AllowedUserIds environment variable.
create table if not exists allowed_line_users (
    line_user_id text primary key,
    added_by text not null,
    added_at timestamptz not null default now()
);
