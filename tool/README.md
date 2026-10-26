# ady-kbd AI Language Testing Tool

Command-line tool behind the Adyghe/Kabardian AI chat test. It reads the raw session logs of
a test round, analyses them at the character and word level, builds the marking workbook the
human evaluators fill in, and turns the marked workbook back into report tables.

Version `1.0.0` · CLI command `akt` · .NET 10 · MIT

---

## What it does, and what it does not

**Does.** Parses the session logs; normalises and repairs the text (look-alike palochka,
invisible characters); splits it into sentences; runs six per-token checks (palochka
correctness and case, non-alphabet characters, confusables, mixed script); counts words;
produces a three-language marking workbook; and, once a human has marked it, aggregates the
marks into tables.

**Does not.** It does not judge language quality. Everything that requires linguistic
judgement — whether a sentence is correct, whether a word is the wrong word, whether a loan
is an error — is entered by human evaluators in the workbook. The tool supplies the
denominators and the mechanical counts; the coding is human.

The coding scheme those evaluators work from is
[`../protocol/coding-scheme.md`](../protocol/coding-scheme.md). Read it before using the
workbook: the tool cannot enforce the scheme, only the arithmetic around it.

---

## Scope: this is a seven-question instrument, not a general harness

The seven-question structure is **fixed in the code**, not a configuration value. Parsing, the
completeness check, both workbook sheets, the report tables and the "total responses" figure
all assume exactly seven. Two cases behave very differently:

- **Fewer than seven questions** — the parser looks for `CEVAP S7:`, does not find it, and
  stops with a format error naming the file. A loud failure.
- **More than seven questions** — the parser stops after the seventh. **The eighth answer is
  read by nobody and no warning is issued.** Numbers come out looking normal, computed from
  part of the data. Do not run this tool on a longer instrument expecting it to notice.

Even with the count made configurable, three questions carry logic of their own and would
have to be reconsidered:

- **Q4** is scored against a Turkish reference translation written before the answers were seen
- **Q5** is scored against an Adyghe reference, and carries the textual copying probe
- **Q7** has its own verdict field — the human leg of that same probe

So the instrument is this instrument. Adapting it to a different question set is a design job,
not a parameter change: decide what each new question measures, then change the code and the
coding scheme together. The `protocol/` documents are the place to start, not the source.

---

## Requirements

- .NET SDK 10.0 or later
- Nothing else. No database, no network access, no external service.

Packages used (all MIT, pinned centrally in `Directory.Packages.props`):
`DocumentFormat.OpenXml` for the workbook, and MSTest for the test suite.

```
dotnet build          # 0 warnings, 0 errors (warnings are errors here)
dotnet test           # runs the full suite, including under tr-TR culture
```

---

## Input: the raw session logs

One file per tool per test, named exactly:

```
Test{n}-AI-Tool{m}-Result[k].txt
```

- `{n}` — test number (1, 2, …)
- `{m}` — tool number. The numbering is fixed: 1 ChatGPT (OpenAI), 2 Claude (Anthropic),
  3 Google Gemini, 4 DeepSeek, 5 Grok (xAI). A number belongs to the tool, not to its place
  in a list; if a tool is skipped its number is not reused.
- `[k]` — optional attempt number, for a repeated session. When several attempts exist the
  tool picks the one with all seven answers present and says which it used.

Files must be UTF-8. The format of the file itself — the header block, the seven
`CEVAP S{n}:` sections and their `====` rules — is defined by the session-log template in
`data/raw/`. A file that does not parse is named in the error rather than skipped silently.

---

## Commands

Run from the `tool/` directory:

```
dotnet run --project src/AdyKbdTest.Cli -- <command> [options]
```

or publish once and call `akt` directly.

| Command | What it does |
|---|---|
| `akt analyze --raw <dir> --test <n>` | Character/word analysis of one test. Writes `test{n}-analysis.json` and `.tsv`. |
| `akt workbook --raw <dir> --test <n>` | Builds the marking workbook `test{n}-marking.xlsx` (four sheets: Info, Responses, Sentences, Summary). |
| `akt report --workbook <file.xlsx> --analysis <dir>` | Reads the **marked** workbook and writes the report tables. |
| `akt summary --analysis <dir> [--out <dir>] [--report-out <dir>]` | Adds up the per-test `task-type` tables (raw counts, ratios recomputed from the sums) and averages the per-test scores; then writes the results page `report.html` / `report.tr.html` / `report.ru.html` to `--report-out` (default `data/report`, see *Report page*). Reads what `akt report` wrote; run it deliberately, after the reports. Errors if there is no `test*-task-type.tsv`, or no `summary-scores.tsv` for the page. |
| `akt verify --raw <dir>` | Checks that every result file in a directory parses and has all seven answers. Run this before anything else. |
| `akt version` | Prints name and version. |

Common options: `--profile ady|kbd` (default `ady`), `--profiles <dir>` (default `profiles`),
`--out <dir>` (defaults: `data/analysis`, `data/workbooks`).

Exit codes: `0` success, `1` user error, `2` unexpected error.

---

## Typical run

```
# 0. check the raw files parse
akt verify   --raw ../data/raw/round1

# 1. analysis
akt analyze  --raw ../data/raw/round1 --test 1 --out ../data/analysis
akt analyze  --raw ../data/raw/round1 --test 2 --out ../data/analysis

# 2. build the marking workbooks
akt workbook --raw ../data/raw/round1 --test 1 --out ../data/workbooks
akt workbook --raw ../data/raw/round1 --test 2 --out ../data/workbooks

# 3. --- humans mark the workbooks in Excel ---
#     Two evaluators, one agreed label per cell. Instructions are on the Info sheet,
#     in English, Turkish and Russian. See ../protocol/coding-scheme.md.

# 4. reports from the marked workbook
akt report   --workbook ../data/workbooks/test1-marking.xlsx \
             --analysis ../data/analysis --out ../data/analysis
```

Step 3 is the slow one and it is not automatable — that is the point of the instrument.

### Copy-paste: the whole pipeline on Windows (PowerShell)

**Run it from `tool/`.** The working directory matters: `--profiles` defaults to `profiles`,
which sits in `tool/`, so calling the executable from its own `bin` folder fails to find the
alphabet sources.

```powershell
# Build once. The executable lands in src\AdyKbdTest.Cli\bin\Debug\net10.0\akt.exe
cd <repo>\tool
dotnet build

# A shortcut for this session, so the long path is typed once
function akt { & "$PWD\src\AdyKbdTest.Cli\bin\Debug\net10.0\akt.exe" @args }

# 0. do the raw files parse?
akt verify   --raw ..\data\raw\round1

# 1. analysis
akt analyze  --raw ..\data\raw\round1 --test 1 --out ..\data\analysis
akt analyze  --raw ..\data\raw\round1 --test 2 --out ..\data\analysis

# 2. marking workbooks
akt workbook --raw ..\data\raw\round1 --test 1 --out ..\data\workbooks
akt workbook --raw ..\data\raw\round1 --test 2 --out ..\data\workbooks

# 3. --- humans mark the workbooks in Excel; see ../protocol/coding-scheme.md ---

# 4. reports from the marked workbooks
akt report   --workbook ..\data\workbooks\test1-marking.xlsx --analysis ..\data\analysis --out ..\data\analysis
akt report   --workbook ..\data\workbooks\test2-marking.xlsx --analysis ..\data\analysis --out ..\data\analysis
```

`dotnet run --project src\AdyKbdTest.Cli -- <command> …` does the same thing without the
shortcut, and works the same on Linux and macOS with forward slashes.

**Step 2 on a second run.** Once a workbook carries marks, `akt workbook` will not overwrite
it: it keeps the file and writes a numbered alternate beside it. So re-running the whole block
is safe, but it leaves `test1-marking-2.xlsx` behind — skip step 2 unless you actually want a
fresh, empty workbook.

---

## Outputs

`analyze` writes, per test:

- `test{n}-analysis.json` / `.tsv` — per-response character and word metrics

`report` writes, per test:

| File | Contents |
|---|---|
| `test{n}-tool-x-category.tsv` | word-code counts per tool (`frm`, `cho`, `inv`, `mix`, `trm`) and errors per 100 words |
| `test{n}-tool-x-question.tsv` | per tool × question breakdown |
| `test{n}-sentence-verdicts.tsv` | `C1`/`C2`/`C3` counts per tool |
| `test{n}-language-compliance.tsv` | language-compliance counts per tool |
| `test{n}-contact-languages.tsv` | words mixed in, per source language, per tool |
| `test{n}-q7-verdict.tsv` | the copying-probe verdict per tool |
| `test{n}-copy-probe.tsv` | the textual copying heuristic on question 5 |
| `test{n}-character-summary.tsv` | palochka and character findings |
| `test{n}-integrity-warnings.tsv` | **read this one** — marking mistakes the tool can detect |
| `test{n}-report-summary.txt` | one-screen summary |
| `test{n}-task-type.tsv` | per tool × question group (`q1_3`, `q4`, `q5`, `q6_7`, `all`): raw counts (`error_words`, `words`, `sentences`, `correct`, `excluded_responses`) and the ratios derived from them |
| `test{n}-main-table.tsv` | the same, wide: one row per tool, four columns per group — the report's main table |
| `test{n}-scores.tsv` | the score of each tool in the test, per group, overall, with rank and tie flag (rule below) |
| `test{n}-notes.tsv` | the evaluators' notes from the workbook, exactly as written: every non-empty Responses note, and every non-empty Sentences note with its sentence number (see *Report page*) |

`summary` writes, over every test it finds (it never reads a workbook):

| File | Contents |
|---|---|
| `summary-task-type.tsv` | `test{n}-task-type.tsv` summed over the tests (a leading `tests` column lists them); ratios recomputed from the sums, never averaged |
| `summary-main-table.tsv` | the wide form of the same |
| `summary-scores.tsv` | per tool, the score of each test, their mean as the overall score, and the rank |

The `task-type` tables use only evaluated sentences: a response with no evaluable content and a
sentence without a verdict are outside every count (only `excluded_responses` records the former).
A ratio whose denominator is zero is an empty cell, not 0.

### The score

Fixed in advance (`puanlama-kurali-2026-10-26.md`) and computed by the tool, not by hand:

1. **Question score** (0–100) = `100 − error words × 100 / evaluated words`, using the
   `error_word_count` and `evaluated_word_count` of `tool-x-question`. An excluded response scores
   0; so does a response that is not excluded but has no evaluated words (which should not happen
   and is reported as `SCORE_NO_WORDS`).
2. **Group score** = the mean of its questions' scores, for the four groups `q1_3`, `q4`, `q5`, `q6_7`.
3. **Test score** = the mean of the four group scores, equally weighted.
4. **Rank** = 1–5 by test score, highest first; equal scores are ordered by tool number and carry
   `tie = true`. The overall score in `summary-scores.tsv` is the mean of the test scores.

Rounding happens only when a value is written; every mean is taken over unrounded values.
`akt summary` recomputes the scores from the per-question counts rather than reading the rounded
per-test scores. It uses `test{n}-scores.tsv` only as a cross-check: a disagreement is reported as
`SCORE_DRIFT` (the file was written by another version of the tool), and a test with a scores
file but no `test{n}-tool-x-question.tsv` is an error. Its warnings go to the console and to
`summary-integrity-warnings.tsv`.

### The integrity warnings are not optional

Excel's data validation only fires on typed entry; a pasted value bypasses it. The warnings
file is the second line of defence and catches, among others: a sentence marked `C1` that
also has error-coded words, word codes summing past the sentence's word count, contact-language
counts that do not add up to the code-mixing count, non-numeric content in a numeric column,
and rows left unmarked. Run `akt report` after each marking session, not only at the end.

---

## Report page

`akt summary` also writes the results page, three files in `data/report/` (`--report-out`):
`report.html` (English), `report.tr.html` (Turkish), `report.ru.html` (Russian). Each is a single,
self-contained file: no external resource of any kind (no CDN, web font or image), inline SVG,
a `system-ui` font stack, light and dark mode. It opens offline.

- **Everything on it comes from the tool's tables** — `summary-scores.tsv`, `test{n}-scores.tsv`,
  `summary-task-type.tsv`, `test{n}-tool-x-question.tsv`, `test{n}-excluded-responses.tsv`,
  `test{n}-notes.tsv` — and the score tables are **embedded in the page** as a
  `<script type="application/json" id="data">` block (column names kept), so a reader who saves the
  page has the data too. The source line at the bottom names the files read and the first 12
  characters of their SHA-256.
- **Sections 1 (overall ranking) and 2 (score card) are written into the HTML**; the script only draws
  section 3 (deviation from the five-tool mean) and the tooltips. There is no separate footnote file.
- **Footnotes are derived from the tables**: every excluded response (`excluded-responses.tsv`),
  every question group containing a question that scored 0, every `tie = true` rank.
- **Evaluator notes come from the workbooks.** `akt report` writes `test{n}-notes.tsv`
  (`sheet key tool question sentence_no note`): every non-empty Note of the Responses sheet, and of
  the Sentences sheet with its sentence number. A note's backslash, tab and line breaks are written
  as `\\`, `\t`, `\n`. `akt summary` shows the Responses notes as written, and splits the Sentences
  notes: text that occurs on more than one row is a *standard note* (counted per tool and test),
  text that occurs on exactly one row is quoted with its key and sentence number. Notes are shown
  in the language they were written in, HTML-escaped, never translated.
- No `summary-scores.tsv`, no page: `akt summary` stops with an error.
- Interface text lives in one table, `ReportStrings` (key → text per language); category words
  (language compliance, semantic adequacy, sentence verdicts, error codes) are the workbooks' own,
  from `CodingSchemeText`.

---

## Behaviour worth knowing before you run it

**The workbook is never silently overwritten.** If `test{n}-marking.xlsx` already contains
evaluator marks, `akt workbook` keeps it and writes a numbered alternate instead, and says so.
There is no override flag. This is deliberate: a regenerated workbook is empty, and marking is
hours of human work.

**The alphabet sources are verified at load time.** `profiles/{ady,kbd}-Cyrl-alphabet.json`
are checked against their recorded SHA-256 every run; a mismatch stops the run rather than
producing numbers from an altered alphabet.

**Culture.** Every number written to a file goes through `InvariantCulture`, and string
comparisons that matter use `Ordinal`. The test suite runs the whole assembly under `tr-TR`
to keep it that way — the Turkish dotless-i is a live hazard in this codebase.

**Correct palochka is counted on the pre-repair text.** Repair exists to make tokenising
robust, not to credit a tool for a character the look-alike check already counted against it.

**Word count.** A whitespace-separated item with no letter in it (an emoji, a lone dash, a
bare punctuation mark) is not a word. Word count is a denominator, so this is not cosmetic.

---

## Validation

The end-to-end tests run the real pipeline against `data/raw/round1` and reproduce the
documented findings exactly:

- Test 1, tool 2, excluding question 4: **38 correct palochka, 52 written as Latin capital I**
- Test 2, same conditions: **67 correct, 0 Latin I**
- Sentence counts: **235** (test 1) and **201** (test 2), across all 35 responses of each

If a change moves these numbers, something is wrong.

**What the tests cannot check.** The build environment has no Excel or LibreOffice, so nothing
in the test suite verifies how the workbook *looks* — row heights, whether a header is clipped,
whether a column is wide enough to read. Those were checked by opening the file, separately.
A schema-valid workbook is not the same as a usable one; if you change the workbook builder,
open the result and look at it.

---

## Repository layout

```
tool/
  src/AdyKbdTest.Cli/          command line
  src/AdyKbdTest.Instrument/   workbook, reports, coding scheme text
  src/AdyKbdTest.TextAnalysis/ normalisation, repair, sentence splitting, per-token checks
  tests/                       MSTest suite, incl. end-to-end runs on the real data
  profiles/                    alphabet sources and per-language tool profiles
```

The tool never writes into `data/raw/`. Every generated artefact goes under `data/`.

---

## Licence and citation

MIT — see `LICENSE`. If you use this tool or the data, cite the repository; see
`../CITATION.cff`.
