# Changelog

Repository-level history: what a release contains and what changed between
releases. The coding scheme keeps its own revision history in
`protocol/coding-scheme.md`; the analysis tool reports its own version with
`akt version`.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versioning follows [Semantic Versioning](https://semver.org/). Any entry that
changes a reported number carries the marker ⚠ **Affects output**.

---

## [Unreleased]

Planned as the first release, 1.0.0, once round 2 has been run and marked.
It will not be tagged before then. Round 2 entries will be added here.

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
