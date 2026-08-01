# korrnellHelper(康乃爾小幫手)Spec

## Problem Statement

女兒就讀的康乃薾雙語中小學(Korrnell Academy)透過學校 APP 發送大量 PDF 格式通知單(如開學須知、接送安排、防疫措施、服裝儀容、才藝選課等)。這些通知單內容繁雜、每份文件常常橫跨十幾個不同主題,資訊分散在多份文件、多個章節與表格之中。家長難以在需要的當下快速找到特定資訊(例如「幾點接小孩」「開學要帶什麼文具」),導致重要通知被忽略、遺忘,進而錯過截止日期或造成準備疏漏。

## Solution

建立一個名為「korrnellHelper(康乃爾小幫手)」的 RAG + AI 系統。使用者(MVP 階段僅限開發者本人)透過 LINE 官方帳號以自然語言提問(例如「開學要穿什麼」),系統會在已收錄的學校通知單知識庫中做向量檢索,取出相關內容片段,交由 AI 生成精準、有依據的回答。

文件的建立流程採「半自動、人工把關」設計:開發者收到 PDF 通知單後,使用 Claude Code 透過一個本地 skill 讀取 PDF 並轉換為結構化 Markdown(含自動擷取的 metadata),經人工檢查內容無誤後,再透過另一個獨立的上傳指令將其送入後端系統完成 chunk 切分、embedding 與索引。

## User Stories

1. As a parent, I want to ask questions about school notices in natural language via LINE, so that I can quickly get answers without manually searching through PDF files.
2. As a parent, I want the bot to answer based on the most recent school year's notices, so that I don't get outdated information carried over from previous years.
3. As a parent, I want the bot to ignore messages from senders not on an approved whitelist, so that my AI API quota isn't consumed by strangers and my child's information stays private.
4. As the administrator, I want to convert a PDF notice into a well-structured Markdown file using a local Claude Code skill by simply providing a file path, so that I don't have to manually parse tables and dates by hand.
5. As the administrator, I want the conversion skill to automatically extract metadata (school year, published date) from the PDF content, so that I don't have to type it manually every time.
6. As the administrator, I want to be alerted when metadata can't be confidently extracted from a document, so that I can manually verify and fill it in before uploading.
7. As the administrator, I want the converted Markdown to preserve the original document's section headings, so that the backend can later chunk it accurately by topic.
8. As the administrator, I want each converted PDF to produce exactly one Markdown file (not pre-split into multiple files), so that chunk-splitting logic stays centralized in the backend instead of being duplicated across tools.
9. As the administrator, I want converted files stored in a flat `resource/` directory using a date-prefixed filename (e.g. `2026-07-31_小一暑期銜接課程須知.md`), so that I can visually scan files in chronological order without navigating nested folders.
10. As the administrator, I want to review the generated Markdown file before uploading it, so that I can catch and correct any conversion errors (especially in tables) before they pollute the knowledge base.
11. As the administrator, I want a separate upload command that pushes a reviewed Markdown file to the backend via an authenticated API, so that conversion and ingestion remain two independently controllable steps.
12. As the administrator, I want the upload API to require a simple API key, so that unauthorized parties cannot inject or corrupt the knowledge base.
13. As the administrator, I want the backend to split an uploaded Markdown document into chunks by section heading, so that retrieval returns focused, topic-specific context instead of an entire multi-topic document.
14. As the administrator, I want each chunk to be embedded and stored along with its source metadata, so that future queries can prioritize by recency when multiple documents cover overlapping topics.
15. As a parent, I want my natural-language question to be embedded and matched via vector similarity search against stored chunks, so that the AI's answer is grounded in actual notice content rather than hallucinated.
16. As a parent, I want the AI-generated answer to be produced from my question plus the retrieved chunk(s) as context, so that answers are accurate and traceable to real notices.
17. As the administrator, I want the entire backend to run on a free-tier cloud container platform, so that operating cost stays at $0 for personal-scale usage.
18. As the administrator, I want the vector database to run on a free-tier managed Postgres service, so that I don't need to operate my own database infrastructure.
19. As the administrator, I want the backend built with lightweight Clean Architecture and CQRS (Command for ingestion, Query for question-answering), so that responsibilities stay separated and the codebase remains testable and maintainable without unnecessary DDD tactical ceremony.
20. As the administrator, I want the MVP's user-facing scope limited to myself, so that I avoid the complexity of multi-tenancy, data isolation, and access management prematurely.
21. As the administrator, I want the architecture to avoid hardcoding an assumption that only one user will ever exist, so that extending access to other parents later requires minimal rework.
22. As a parent, I want the bot to only respond to senders on an approved LINE User ID whitelist, so that it functions as a private assistant rather than a public one.

## Implementation Decisions

### PDF → Markdown 轉換(本地 Claude Code skill)

- 輸入:一個 PDF 檔案路徑
- 行為:讀取 PDF 內容,轉換為結構化 Markdown,保留原文件的章節標題階層(對應原文件中如 `< 學校運作說明 >` 這類段落標題),做為後端 chunk 切分依據
- Metadata:嘗試從文件內容自動擷取「學年度」「公告日期」等資訊,寫入 Markdown 檔案開頭的 YAML frontmatter;無法確信擷取到時留空,並提示使用者手動補值
- 輸出:一個 PDF 對應一個 Markdown 檔案(不在此步驟做 chunk 切分),存放於專案目錄下的 `resource/` 資料夾,採扁平結構,檔名格式為 `{published_date}_{文件標題}.md`
- 此 skill 不觸發上傳,轉換與上傳是兩個獨立步驟,讓使用者能在上傳前人工檢查內容(尤其是表格與日期的轉換正確性)

### Markdown → 後端上傳(獨立 CLI / skill)

- 讀取 `resource/` 中已經過人工確認的 Markdown 檔案
- 呼叫後端「新增文件」API,於 Header 帶入 API Key 做驗證
- 傳遞內容:Markdown 原文 + frontmatter 中的 metadata

### 後端(.NET Core)

- 架構:輕量版 Clean Architecture(Domain / Application / Infrastructure / Presentation 分層)+ CQRS,Command 與 Query 職責分離;不採用 Aggregate Root、Value Object、Domain Event 等重型 DDD 戰術模式(目前 domain 複雜度不需要)
- **Add Document(Command)**:接收 Markdown 內容 + metadata → 依 Markdown 標題邊界切分為多個 chunk → 呼叫 Gemini API 產生每個 chunk 的 embedding → 將 chunk 文字、向量、metadata(學年度、公告日期、來源文件)寫入 Supabase(pgvector)
- **Answer Question(Query)**:接收自然語言問題(來自 LINE webhook)→ 呼叫 Gemini API 將問題轉為向量 → 對 Supabase pgvector 做相似度搜尋(視情況優先取最新學年度/日期的 metadata)→ 組合檢索到的 chunk 內容為 context → 呼叫 Gemini 生成模型,以「問題 + context」產生自然語言回答 → 回傳給呼叫端(LINE webhook handler)供回覆使用
- **LINE Webhook endpoint**:接收 LINE 傳入訊息,比對發送者 LINE User ID 是否在環境變數設定的白名單內;不在白名單則忽略或回覆無權限訊息;在白名單則將文字內容導入 Answer Question Query,並將生成結果回覆至 LINE
- **Add Document endpoint 驗證**:簡單 API Key,經由 Request Header 驗證,Key 存於 Cloud Run 服務的環境變數

### 資料儲存

- Supabase(Postgres + pgvector extension),使用免費額度
- 每個 chunk 連同其 embedding 向量與 metadata(學年度、公告日期、來源文件)一起儲存,供檢索時判斷時效性 / 新舊版本

### AI 供應商

- Google Gemini API,同時負責 embedding(chunk 與查詢問題)與生成回答(單一供應商,簡化金鑰與額度管理)

### 主機

- 後端打包為 Docker container,部署於 Google Cloud Run(scale-to-zero,免費額度)

## Testing Decisions

- 好的測試應驗證外部行為(輸入 → 輸出/副作用),而非驗證實作細節;避免測試綁死在特定內部函式呼叫順序上
- 專案目前是全新建立,repo 內尚無既有測試可參考,因此以下 seam 為建議的優先測試邊界,盡量集中在最少的邊界上(理想是集中在 CQRS 的 Command/Query handler 這一層):
  1. **Add Document Command Handler**(最高優先 seam):給定 Markdown 原文 + metadata,驗證是否依標題正確切出預期的 chunk 邊界,並驗證是否以正確的資料呼叫下游的 embedding / 儲存介面(下游的 Gemini API、Supabase 於此邊界用假物件替換,不做真實網路呼叫)
  2. **Answer Question Query Handler**:給定自然語言問題與假的向量搜尋結果,驗證 context 組裝邏輯是否正確,以及送給生成模型的 prompt 輸入是否符合預期 —— 這是涵蓋核心 RAG 行為的關鍵 seam
  3. **LINE Webhook 邊界**:驗證白名單機制本身(允許/拒絕的 sender ID),獨立於下游 Query 邏輯之外測試
- 不在本規格自動化測試範圍內:
  - PDF 轉 Markdown 的 Claude Code skill 本身(其輸出品質由人工審閱把關,屬於流程設計的一部分,非程式邏輯正確性問題)
  - 與 LINE / Gemini / Supabase 的端對端整合(以免費層沙盒帳號做人工 smoke test 驗證即可,不建議在此規格中要求自動化)

## Out of Scope

- 多使用者/多租戶支援(開放給其他家長、其他班級或學校使用)—— 架構上避免寫死單一使用者假設,但本規格不包含邀請流程、權限管理介面或跨使用者資料隔離的實作
- OCR / 掃描圖片型 PDF 處理 —— 目前通知單約 90% 為文字型,圖片型 PDF 的處理不在本規格範圍
- 全自動、無人工審閱的 PDF → Markdown 轉換(例如完全交由 Gemini 自動讀取 PDF 產生內容)—— 已於決策過程中明確捨棄,改採人工把關流程
- 網頁版上傳/管理介面(React)—— 本 MVP 上傳僅透過 CLI/skill 完成
- 自動從學校 APP 抓取 PDF(無公開 API 可用,不在本規格中嘗試)
- Discord / Telegram 作為訊息通道 —— 已決定改用 LINE
- 完整版 DDD 戰術模式(Aggregate Root、Value Object、Domain Event)
- 舊文件的自動刪除/封存機制 —— 版本新舊問題透過查詢時依 metadata 判斷優先順序處理,不涉及資料清理自動化

## Further Notes

- 規劃過程中檢視過的實際範例文件(`小一暑期銜接課程暨新生訓練須知.pdf`)確認了通知單普遍具有「多主題、多頁、含表格」的特性,驗證了「依標題切 chunk」這個設計方向的合理性
- 開發者本身已有一個對應 .NET 8 / Clean Architecture / DDD 風格的 `backend-ddd` Claude Code skill,實作階段應沿用此既有慣例來 scaffold 後端程式碼
- 後端專案骨架已建立於 `KorrnellHelper/KorrnellHelper`,採標準四層結構(`KorrnellHelper.Api` / `KorrnellHelper.Application` / `KorrnellHelper.Domain` / `KorrnellHelper.Infrastructure`),目前皆為預設模板內容(`WeatherForecastController`、各專案的 `Class1.cs` 佔位檔),尚未開始實作,後續開發應在此既有骨架上進行,不需重新建立專案
- 目前 `KorrnellHelper` 專案目錄本身並非獨立的 git repository,而是巢狀在一個不相關的個人 repo(`github.com/yv025014/my_tracker`)之下;實作開始前應考慮先建立專屬的 repository
- 各項免費額度(Gemini API 配額、Cloud Run 請求/運算額度、Supabase 500MB 儲存空間)目前假設足以支撐個人規模使用,但尚未經過實際壓測;若未來使用型態改變(例如擴充多使用者),應重新評估
