# Haya as code context for coding agents (MCP / CLI)

Status: research, nothing implemented. Question: is it worth a proof of concept (PoC) that exposes Haya's model, and
optionally Ferret's semantic search (https://github.com/dburriss/ferret), to a coding harness such as Claude Code, for
more semantic, token-efficient search over a codebase and a whole system?

**Verdict: yes, as a narrowly scoped PoC.** The goal is fewer tokens and turns, better localization, and answers to
questions that span repos. Do not expect a large lift in end-to-end task success. No published study evaluates a
developer-authored architecture model (C4-style, with responsibilities) as agent context, so the PoC is the evidence.

Evidence markers: **[P]** read in the paper, **[A]** abstract or snippet only, **[S]** secondary source or vendor blog,
**[U]** unverified. Most figures are the authors' own, so check the paper before quoting a number.

## Summary

1. **The baseline to beat is agentic grep, not vector RAG.** Strong agents that explore with grep and read far
   outperform BM25 or embedding retrieval, and differ little from each other (SWE-Explore).
2. **Structural tools improve localization, but barely move end-to-end resolve rate.**
   - LocAgent's graph tools gained 4–7 points of localization accuracy over OpenHands.
   - RepoGraph gained about 2 resolve points on SWE-bench Lite, and cost went up.
   - A graph MCP (Codebase-Memory) used about 10x fewer tokens for 9 points less answer quality.
3. **Entity search is the biggest single factor, graph traversal adds a little.** In LocAgent's ablation, removing the
   name/keyword search over code entities cost about 19 points of file accuracy. Removing traversal cost about 5 points.
4. **Natural-language summaries work best for coarse routing in large multi-repo systems.** A 46-repo study routed
   repo → directory → file by summaries and beat Copilot and Cursor. This is where Haya's responsibilities and
   collaborators are unique.
5. **Static dumps and overviews don't help.** AGENTS.md files did not raise success and added more than 20% cost; repo
   overviews were the least useful part. Long contexts degrade. So serve small, query-shaped answers through tools.
6. **Semantic code search is mixed.** Most of the positive evidence comes from vendors. It helps when the code uses
   different vocabulary from the question, and hurts when the agent already knows the identifier. Treat it as an extra
   tool next to grep, and leave it for a second phase.
7. **Haya already has most of the analysis.** The type graph from ADR018 covers every source type, not just annotated
   ones. So Haya can serve a code layer (as LocAgent does) and an architecture layer (as the multi-repo study does)
   from one model.

## Evidence

### Localization and structural retrieval

**LocAgent** (arXiv 2503.09089, 2025) [P]
- **Graph.** Nodes are directory, file, class and function. Edges are `contain`, `import`, `invoke` and `inherit`.
  There is a sparse hierarchical entity index (IDs, names, BM25).
- **Three tools.**
  - `SearchEntity`: keyword search returning entities.
  - `TraverseGraph`: breadth-first walk with direction, number of hops (steps out), and entity and relation filters.
    It returns a tree-formatted subgraph.
  - `RetrieveEntity`: code, path and line for given entity IDs.
- **Results on SWE-bench Lite.**

  | Method | File Acc@5 | Function Acc@10 |
  | --- | --- | --- |
  | BM25 | 61.7 | 36.9 |
  | CodeRankEmbed | 84.7 | 58.8 |
  | Agentless | 79.6 | 58.8 |
  | SWE-agent | 90.2 | 64.6 |
  | OpenHands | 90.2 | 70.1 |
  | LocAgent (Claude 3.5) | 94.2 | 77.4 |

  On their newer, less contaminated Loc-Bench, LocAgent scored 59.3 function Acc@10 against 59.1 for OpenHands.
- **Ablation (fine-tuned Qwen-7B).**
  - Without `SearchEntity`, file Acc@5 fell from 88.3 to 69.0.
  - Without `TraverseGraph`, function accuracy fell from 71.5 to 66.1.
  - A single hop (66.8) or only the `contain` edge (66.4) lost most of the traversal gain.
- **Cost.** $0.66 per issue, against $0.79 for OpenHands.
- **Why the authors think it works.** Issues surface through call and import relations, not directory layout. Tree
  output is easier for an LLM to read than adjacency lists.

**RepoGraph** (arXiv 2410.14684, ICLR 2025) [P]
- Line-level graph with `invoke` and `contain` edges, retrieved as an ego-graph of 1–2 hops around a search term.
- SWE-bench Lite resolve rate, baseline to +RepoGraph:
  - Agentless (GPT-4o): 27.3 → 29.7.
  - AutoCodeRover: 19.0 → 21.3.
  - SWE-agent: 18.3 → 20.3.
- Cost rose in every pair (Agentless $0.34 → $0.39). Flattening the subgraph into text hurt (26.0).

**CodexGraph** (arXiv 2408.03910, NAACL 2025) [A]
- A graph database queried in Cypher, with an LLM translating questions into Cypher.
- It used more tokens than RAG, and the translation step was a failure point.

**OrcaLoca** (arXiv 2502.00350, ICML 2025) [A]
- Schedules actions with a priority queue, scores relevance, and prunes context by distance in the graph.
- 65.3% function match on Lite, and +6.3 resolve points when added to an open framework.

**CoSIL** (arXiv 2503.22424, ASE 2025) [A]
- Builds module and function call graphs during the search, with no index.
- 43–45% top-1 function localization.

**Agentless** (arXiv 2407.01489, FSE 2025) [S]
- A fixed pipeline from repo tree and skeletons, to file, then function, then edit.
- 27.3% on Lite at $0.34. Simple structural prompts are cheap and competitive.

**AutoCodeRover** (arXiv 2404.05427, ISSTA 2024) [S]
- Search APIs over the abstract syntax tree (AST): search class, search method, search code snippet.

**SWE-agent** (arXiv 2405.15793, NeurIPS 2024) [S]
- A search tool that summarises matches (lists the matching files) beat both no search (15.7%) and an iterative
  one-hit-at-a-time search (12.0%). How tool output is shaped matters.

**SWE-Explore** (arXiv 2606.07297, 2026) [A]
- 848 issues across 203 repos and 10 languages.
- TF-IDF, BM25 and RAG were near-random on most issues. Agentic explorers (Claude Code, OpenHands, mini-SWE-agent) did
  far better, with small differences between them.

**Codebase-Memory** (arXiv 2603.27277, 2026) [A]
- The closest analogue to this PoC: a tree-sitter knowledge graph over 66 languages, served over MCP. It answers
  call-graph, impact, hub and caller-ranking queries.
- Answer quality was 83%, against 92% for a file-exploring agent. It used about 10x fewer tokens and 2.1x fewer tool
  calls.

**Repo-level completion** [A]
- RepoCoder (2303.12570), RepoHyper (2403.06095), GraphCoder (2406.07003), DraCo (2405.19782) and CodePlan (2309.12499).
- Dataflow and control-dependence graphs help code completion, but are overkill for navigation.
- CodePlan, which plans edits using change-impact analysis over a dependency graph (including C#), is relevant if Haya
  later supports "what breaks if I change X".

### Natural-language summaries and multi-repo

**Multi-repo bug localization via NL summarization** (arXiv 2512.05908, ICSE 2026) [A]
- The only multi-repo or microservice evidence found.
- Summaries at file, directory and repo level, with two phases: route to a repo, then search top-down.
- On a proprietary system (46 repos, 1.1M lines of code): Pass@10 0.82 and MRR 0.50. It beat retrieval baselines and
  agentic RAG (Copilot, Cursor).
- The authors argue that matching natural language to natural language closes the gap between how bug reports and
  code are worded.

**Meta-RAG** (arXiv 2508.02611) [A]
- LLM summaries shrink the code base by 79.8%. Localization on SWE-bench Lite was 84.7% at file level and 53.0% at
  function level.

**Retrieval-oriented code representations** (arXiv 2607.11046) [A]
- Summaries that describe each file's role beat raw code on file-localization hit rate (Hit@5) and on cost.

**Aider repo-map** (aider.chat blog, 2023) [S]
- Definitions and references from tree-sitter, ranked PageRank-style and cut to a token budget (about 1K by default).
- No controlled benchmark.

### Context engineering and delivery

**Context files** [A]
- Gloaguen et al., "Evaluating AGENTS.md" (arXiv 2602.11988, ICLR 2026):
  - Context files, whether developer-written or LLM-generated, did not generally improve success, and raised cost by
    more than 20%.
  - Agents follow them: they explore and test more, but resolve no more issues.
  - Repo overviews were not useful. The files helped only for non-standard practices, such as custom tooling.
- Lulla et al. (arXiv 2601.20404): on Codex, AGENTS.md cut median runtime by 28.6% and output tokens by 16.6%.
  It measured efficiency only, on a small sample.
- Read together: non-inferable facts help, and restating what the agent can discover costs tokens.

**Long-context degradation** [A/S]
- Lost in the Middle (arXiv 2307.03172): accuracy is U-shaped by position, worst in the middle.
- RULER (2404.06654): many models' effective context length falls well below what they claim.
- NoLiMa (2502.05167): at 32K tokens, 10 of 12 models fell below 50% of their short-context score when the question
  and the needle shared no literal words.
- Chroma "Context Rot" (2025): across 18 models, distractors and low similarity to the query speed up degradation.

**Tool design** [S, Anthropic engineering blog]
- From "Writing effective tools for agents":
  - Use few tools that each do a whole job, not API wrappers.
  - Add a `response_format` parameter (concise or detailed). Concise was about a third of the tokens in their example,
    but keep IDs in it.
  - Paginate. When truncating, say how to narrow the query.
  - Namespace tool names and write precise descriptions.
- From "Code execution with MCP": progressive disclosure, i.e. load detail only when needed.
- Claude Code caps MCP output at 25K tokens by default. Aim for 1–3K per call.

**Code-graph and LSP MCP servers** [S/U]
- Serena and similar servers make token-saving claims that are qualitative or vendor-run.
- arXiv 2608.13568 ("Does a Language Server Save Tokens for Coding Agents?") frames the measurement gap.

### Semantic code search

**Models**
- jina-code-embeddings-0.5b and -1.5b (arXiv 2508.21290) [P]:
  - Built on Qwen2.5-Coder.
  - Average score over 25 code benchmarks: 78.4 (0.5B) and 79.0 (1.5B), against voyage-code-3 at 79.2 (Jina's numbers).
  - Needs instruction prefixes, e.g. `Find the most relevant code snippet given the following query:`.
  - **The licence is reportedly CC BY-NC 4.0 [U]. Check the model card before any commercial use.**
- nomic-embed-code (7B) [S]: on par with voyage on CodeSearchNet, but heavy to run locally.
- CodeRankEmbed (137M) [S]: 60.1 nDCG@10 on CoIR; practical to run locally.
- CoIR (arXiv 2407.02883) is the benchmark to compare models on.
- Ferret currently uses bge-small-en-v1.5, a general-purpose model, not a code model.

**Chunking**
- cAST (arXiv 2506.15655) [P]: chunk along the syntax tree with tree-sitter, splitting large nodes recursively, then
  merging siblings up to a size cap.
- Best size was about 2000–2500 non-whitespace characters. It gave +2–4 Recall@5 on RepoEval, and +2.7 Pass@1 on
  SWE-bench with Claude.
- The merge step is essential: nDCG was 53.5 with splitting only, against 71.1 with split then merge.
- Code: github.com/yilinjz/astchunk.

**Natural-language augmentation**
- Summaries attached to the document side show some evidence of improving retrieval. Query expansion (HyDE-style)
  looks weaker [S/U].
- Haya's responsibilities are human-written summaries at no generation cost.

**Hybrid search**
- Combine BM25 and dense rankings with reciprocal rank fusion (RRF), k=60 (Cormack et al., SIGIR 2009). This is the
  usual default.
- No code-specific fusion study was verified.

**Vendor and practitioner evidence**
- Cursor (cursor.com/blog/semsearch, Nov 2025) [S, vendor]:
  - +12.5% accuracy on its own benchmark.
  - In a live A/B, code retention rose 0.3%, or 2.6% on repos with more than 1,000 files.
  - Grep was available in both arms, and the embedding model was trained on agent traces.
- Claude Code: early versions used a vector index, and agentic search reportedly beat it "by a lot" [S, podcast,
  second-hand]. The reasons given were staleness, simplicity, security and privacy.
- Cline ("why we don't index", 2025) [S]: chunking separates calls from definitions, and indexes go stale. It concedes
  RAG saves tokens.
- Sourcegraph Cody dropped embeddings for enterprise in 2024 because of scale and security [S].
- A 50-task benchmark on Claude Code (AI Engineer talk) [S]:
  - Semantic search did not clearly help, and windowed reads alone raised precision.
  - Semantic search won on vocabulary mismatch and lost when an identifier was known.
  - The agent lacked a good policy for when to use the tool.

## What Haya brings

Two layers already exist in the code base.

**Code layer (no annotations needed).**
- `Roslyn.typeGraph` (`src/Haya.Roslyn/Roslyn.fs:273`) and `typeGraphOf` (`src/Haya.Bun/analyser/analyser.ts`)
  produce a `TypeNode` (`src/Haya.Core/Dependencies.fs`) for every class, struct, record and interface in the source,
  with its file and whether it is abstract.
- Edges (`TypeEdge`) come from semantically resolved references in type bodies, generic arguments included, and from
  base types and implemented interfaces.
- `Dependencies.attach` walks the graph once in `Haya.Core` and is shared by C# and TypeScript through Fable. It
  produces "via" chains, e.g. `PaymentsController → VisaPaymentService via PaymentService, CardGateway`.
- At `--detail members`: public constructors and members of annotated classes.

**Architecture layer (annotated).**
- Systems → apps → components, with plain-language responsibilities.
- Collaborators across apps and systems, with direction, protocol, data and relationship.
- Meta: owner, repository, tier, lifecycle, kind.
- Merging across repos from `describe` files (`Merge.fs`, `Catalog.fs`).

How this maps to the evidence:

| Evidence | Haya equivalent |
| --- | --- |
| LocAgent `SearchEntity` (largest factor) | Name search over `TypeNode`s plus responsibility text |
| LocAgent `TraverseGraph`, 1–2 hops | References and inheritance edges, `via` chains, collaborator edges |
| LocAgent `RetrieveEntity` | Component detail (file, members); line numbers needed |
| Multi-repo NL routing | Responsibilities and `Meta` across merged `describe` files |
| Context files: only non-inferable facts help | Ownership, edges across repos and intent cannot be grepped from one checkout |

Gaps, and how cheap each is to close:

| Gap | Effort |
| --- | --- |
| Line numbers | Low. The declaration's location is in hand where nodes are built (`decl.GetLocation()`, TS `getLineAndCharacterOfPosition`). |
| Method-level and call edges | Low to medium. The body walk already resolves each name with `GetSymbolInfo` and folds it into its owning type. Keeping member symbols gives call edges. |
| Namespaces or modules as containment | Low. They are available from the symbol. |
| Analysis on every run (MSBuildWorkspace) | An MCP server is long-running: load once, then update incrementally (workspace or file watcher, TS language service). |
| Plain-language layer needs annotations | Inherent. Coverage depends on how much code is annotated. The code layer still works without it. |
| Parity and output stability | Put new fields behind a flag or a separate output, so the outputs that `bun run parity` compares do not change. |

## Proposed PoC

**MCP server** (a CLI equivalent is useful for the comparison arms below), with 3–4 tools. Each returns compact tree
text with stable IDs, about 1–2K tokens by default, a hard cap around 5K, and a cursor for pagination.

1. `find(query, kind?, system?, owner?)`
   - BM25 over type and member names, responsibilities and meta (Ferret lexical, or Ferret.Core in process).
   - Returns ID, kind, one-line responsibility, and `repo:path:line`.
2. `neighbours(id, direction=out|in|both, edges?, depth<=2)`
   - Covers references, implements, contains and collaborators, as a tree.
   - Answers "what reaches `CardGateway`" and "what does this app call".
3. `get(id, detail=concise|full)`
   - Responsibilities, collaborators, owner, links, members and locations.
4. Optional `system(id)`: the apps in one system and the edges between them.

**CLAUDE.md pointer**, 3–5 lines saying when to use the tools: before changes that cross apps or contracts, and when
the location is unknown. Do not paste the graph.

**Phase 2 (only if evaluation shows conceptual-query misses):**
- Ferret hybrid search over code: chunk with cAST at about 2000 characters, fuse with RRF k=60, and use a code
  embedding model.
- Index the responsibility text as entity documents, so a semantic hit routes to a component first.

## Evaluation

Standard benchmarks (SWE-bench, ContextBench arXiv 2602.05892, Multi-SWE-bench 2504.02605) are single-repo. Use them
only for the code layer. Build a custom set:

- **Localization:** 20–30 tasks in this repo's examples plus one or two real repos. Measure recall@k and precision of
  file and function against known answers.
- **System questions:** 20–40 questions with verifiable answers. For example:
  - Who owns X?
  - What breaks if contract Y changes?
  - Which app is responsible for Z?
- **End to end:** about 10 real tickets.

Run Claude Code headless (`claude -p "<task>" --output-format json`, with `--mcp-config` and `--strict-mcp-config`;
check the flags against `claude --help`) on a fixed model with a fresh clone per run, 3–5 repeats per task.

Arms:
1. Baseline (grep and read).
2. Haya MCP.
3. The same facts as a static file or CLI output, to separate the content from the delivery.

Measure correctness, total tokens, cost, turns, tool calls and tool errors.

**Go/no-go:** continue if arm 2 matches baseline correctness with clearly fewer tokens or turns, or answers system
questions the baseline gets wrong.

## Risks

- Strong agents already localize well with grep, so the headroom in a single repo is small.
- Extra tools add token overhead for their definitions and can distract the agent. Keep the tool set small and the
  descriptions precise.
- Staleness: the in-memory model must track edits, or the agent will distrust it.
- Annotation coverage limits the architecture layer.
- Several figures here come from abstracts or secondary sources (see markers). In particular: the multi-repo study,
  Codebase-Memory, SWE-Explore, the Jina licence, and the Claude Code vector-index anecdote.

## Sources

- LocAgent arXiv 2503.09089 · RepoGraph 2410.14684 · CodexGraph 2408.03910 · OrcaLoca 2502.00350 · CoSIL 2503.22424
- Agentless 2407.01489 · AutoCodeRover 2404.05427 · SWE-agent 2405.15793 · SWE-Explore 2606.07297
- Codebase-Memory 2603.27277
- RepoCoder 2303.12570 · RepoHyper 2403.06095 · GraphCoder 2406.07003 · DraCo 2405.19782 · CodePlan 2309.12499
- Multi-repo NL localization 2512.05908 · Meta-RAG 2508.02611 · Retrieval-oriented representations 2607.11046
- AGENTS.md: 2602.11988, 2601.20404
- Lost in the Middle 2307.03172 · RULER 2404.06654 · NoLiMa 2502.05167
- trychroma.com/research/context-rot
- ContextBench 2602.05892 · SWE-ContextBench 2602.08316 · Multi-SWE-bench 2504.02605 · LSP tokens 2608.13568
- jina-code-embeddings 2508.21290 · cAST 2506.15655 · CoIR 2407.02883 · huggingface.co/nomic-ai/nomic-embed-code
- anthropic.com/engineering/writing-tools-for-agents · anthropic.com/engineering/code-execution-with-mcp
- cursor.com/blog/semsearch · cline.bot/blog/why-cline-doesnt-index-your-codebase-and-why-thats-a-good-thing
- aider.chat/2023/10/22/repomap.html
