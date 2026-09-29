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

### Removed

- `data/round1/conditions.md`: the parts still needed were moved into
  `README.md` and into the thesis.
