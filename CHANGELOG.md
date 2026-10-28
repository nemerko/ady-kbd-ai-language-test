# Changelog

Repository-level history: what a release contains and what changed between
releases. The coding scheme keeps its own revision history in
`protocol/coding-scheme.md`; the analysis tool reports its own version with
`akt version`.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versioning follows [Semantic Versioning](https://semver.org/). Any entry that
changes a reported number carries the marker ⚠ **Affects output**.

---

## [1.0.0] – 2026-10-27

First release: the pilot — Adyghe, five tools, four repetitions in two rounds.

Archived on Zenodo: DOI [10.5281/zenodo.23729296](https://doi.org/10.5281/zenodo.23729296).

### Added

- `data/raw/round2/Test3-AI-Tool*-Result.txt`: raw answers of round 2,
  repetition 1 (16–17 October 2026). Hashed on 17 October 2026, before any
  marking; the hashes are in `CHECKSUMS.txt`. See README → On timestamps.
- `data/raw/round2/Test4-AI-Tool*-Result.txt`: raw answers of round 2,
  repetition 2 (17 October 2026). Hashed on 17 October 2026, before any
  marking; the hashes are in `CHECKSUMS.txt`.
- `protocol/s5-second-change-note.md`: how the second control change in S5
  (round 2) is recorded during marking. Written and hashed on 17 October 2026,
  before any answer of round 2 was marked.
- `data/raw/round1/Test2-AI-Tool*-Result*.txt`: raw answers of round 1,
  repetition 2 (14–15 September 2026). Committed here for the first time,
  after round 1 had been marked; see README → On timestamps.
- `data/workbooks/`: marking workbooks for both repetitions of round 1.
- `data/analysis/`: the tables the tool produces from those workbooks.
- `tool/`: the analysis tool, `akt` 1.0.0 (.NET 10, MIT), with `tool/LICENSE`.
- `.zenodo.json`, `CHANGELOG.md`.
- Tables by task type (revision 16): `test{n}-task-type.tsv` and `test{n}-main-table.tsv`
  group the questions as S1–S3, S4, S5, S6–S7 and the combined S1–S7, with errors per
  100 words, correct-sentence percentage, words and mean sentence length per group
  (evaluated sentences only), so a tool's result is no longer one blended rate.
- Score tables (revision 16): `test{n}-scores.tsv`. Question score = percentage of
  error-free words, an excluded response scores 0; group score = mean of its questions;
  test score = mean of the four groups; overall score = mean of the tests. New integrity
  warning `SCORE_NO_WORDS`.
- `akt summary` (revision 16): sums the per-test task-type tables over the tests (raw counts,
  ratios recomputed, never averaged) and writes `summary-task-type.tsv`,
  `summary-main-table.tsv` and `summary-scores.tsv`. It is a separate command, so re-running one
  test's report does not silently change the totals.
- `report.html` / `report.tr.html` / `report.ru.html` under `data/report/`: a self-contained
  results page written by `akt summary` — ranking, score card, deviation
  from the five-tool mean, evaluator notes from the workbooks; data embedded (revision 18).
- `test{n}-notes.tsv`: the evaluators' notes from the Responses sheet and counts of the
  standard sentence notes (revision 18).

### Changed

- Folder layout reorganised:
  - `data/round1/tool<n>-round1-raw.txt` → `data/raw/round1/Test1-AI-Tool<n>-Result.txt`.
    Content unchanged: the SHA-256 of each file is the one recorded on
    13 September 2026.
  - `protocol/instrument-v1.0.0.md` → `protocol/instrument.md`. The file name
    was wrong and is corrected; "v1.0.0" is removed from the title, and a note
    on the known spelling error in S6 is added. The question text is unchanged.
  - `analysis/LICENSE` → `tool/LICENSE` (identical file).
- `protocol/coding-scheme.md`: rewritten before the first marking; it carries
  its own revision history.
- `README.md`, `LICENSE` (now the full CC BY 4.0 legal code), `CITATION.cff`
  and `CHECKSUMS.txt` updated for the new layout.
- `README.md` → On timestamps: the end times of three repetition-2 sessions
  (tools 1–3), read afterwards from the tools' chat histories, are recorded
  there rather than in `data/raw/`.
- `README.md` → On timestamps: how the five sessions of a repetition are run
  (in parallel, in tool order, with failed sessions retried), and the session order of
  round 1, read from the start times in the session files, whose session order
  field was left with its template text. `data/raw/` unchanged.

### Removed

- `data/round1/conditions.md`: the parts still needed were moved into
  `README.md` and into the thesis.

### Fixed

- The Sentences sheet now shows the response's original characters; the repaired copy
  is used only for counting and is never displayed (revision 14).
- Palochka repair now judges each hyphen-separated part of a word on its own, so
  `AI-компьютерым` is no longer "repaired" into `AӀ-компьютерым`. The same rule is
  applied to the Latin-in-Cyrillic count. ⚠ **Affects output**: test 1, tool 2 character
  summary (look-alike palochka 57 → 56, palochka accuracy 40.63 → 41.05, mixed script
  proper-noun 1 → 2, interleaved 1 → 0, Latin in Cyrillic 1 → 0), and one finding fewer
  for T1-A2-S1 in the test 1 analysis.
- The words-per-100 denominator now counts only the words of sentences that carry a verdict,
  so unlabeled (non-target-language) sentences of a partly compliant response no longer
  inflate it (revision 15). ⚠ **Affects output**: test 4, tool 4, errors per 100 words
  21.4286 → 28.0645 (words 406 → 310).
- Added: `evaluated_word_count` column in `tool-x-question`, `WORD_COUNT_MISMATCH`
  integrity check, and an unlabeled-words line in the report summary.
- `akt summary` now recomputes the overall scores from the per-question counts instead of
  averaging the rounded per-test scores (revision 17). ⚠ **Affects output**:
  `summary-scores.tsv`, tool 4, 77.8905 → 77.8904; ranks unchanged. New integrity warning
  `SCORE_DRIFT` and file `summary-integrity-warnings.tsv`.
