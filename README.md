# ady-kbd-ai-language-test

Adyghe AI language test

A small, versioned instrument for measuring whether widely used AI chat tools, in their
**free tiers**, can process Adyghe (ady) input and produce semantically adequate Adyghe
output — together with the raw data produced by running it.

Adyghe is a Northwest Caucasian language with limited digital presence. Claims about what
AI tools can or cannot do in such a language are usually made from scattered impressions.
This repository is an attempt to replace impressions with a repeatable procedure.

This repository is part of M. Uğur Nemlioğlu's doctoral research on digital language
planning for Adyghe and Kabardian.

## What is here

    protocol/         the frozen instrument, the coding scheme and the source paragraph
    data/raw/         raw answers, one file per tool per repetition
    data/workbooks/   the marking workbooks, labelled by the evaluators
    data/analysis/    the tables the tool produces from those workbooks
    tool/             the analysis tool (C#)

## Method in brief

Five tools, selected from the top of an independent usage ranking by one criterion: usable
**without payment**, with registration completed by e-mail and/or phone verification.
Tools that can only be joined through a social-platform account are excluded, because such
a link exposes the user's profile to the tool and could personalise the answer.

Seven questions, all asked in Adyghe, in fixed order, with no correction or follow-up in
between. Questions 4 and 5 form a copying probe: the Turkish paragraph given back in
question 5 is the translation of the Adyghe paragraph from question 4, with one deliberate
change. A tool that returns that place in its original form has copied rather than
processed.

One instrument, applied four times: two repetitions in each of two rounds. The two
repetitions of a round are run on consecutive days and measure how stable a tool is within the
same model version; the second round, about a month later, measures change over time. The two
repetitions of a round are not compared with each other — they are averaged, and it is the
round averages that are compared.

Two evaluation axes. **Linguistic marking** is recorded as fixed labels rather than free
comments: every sentence is labelled for syntax, faulty words are labelled for form,
choice, invention or code mixing, and each answer is labelled for language compliance and
semantic adequacy. Questions 4, 5 and 7 carry their own criterion, so that the copying
probe is measured rather than averaged away. **Character-level analysis** is run by a
purpose-built tool — palochka use, Latin look-alikes, mixed scripts, normalisation form.
Look-alike and mixed-script checks are applied within Cyrillic script runs only, so that
legitimately Latin passages (the Turkish translation asked for in question 4) are not
counted as errors.

Full detail: [`protocol/instrument.md`](protocol/instrument.md) and
[`protocol/coding-scheme.md`](protocol/coding-scheme.md).

## Versioning

    v1.0.0   pilot: Adyghe, five tools, four repetitions in two rounds

Later versions extend rather than replace. The planned next step is the same instrument
applied to Kabardian (kbd), so that two related languages with markedly different digital
resource volumes can be compared with one measure.

Each release is tagged. Cite a tag, not the main branch.

## On timestamps

The instrument was frozen on 12 September 2026, before the first session was run. Round 1's
two repetitions were run on 13 and 14–15 September 2026.

The raw answers of the first repetition were hashed on 13 September and committed on
14 September 2026, before any marking was carried out, and the same hashes still hold: compare
the first commit's `CHECKSUMS.txt` with the current one. The raw answers of the second
repetition were not committed before marking. They enter the repository for the first time
with the commit that reorganised its folders, after round 1 had been marked; for them the
record is the hashes in the current `CHECKSUMS.txt`.

Round 2's first repetition (Test 3) was run on 16–17 October 2026. Its raw answers were
hashed on 17 October 2026, before any marking was carried out, and the five hashes were
added to `CHECKSUMS.txt` at that point.
Round 2's second repetition (Test 4) was run on 17 October 2026 and its raw answers were
hashed the same evening, again before any marking.

The session files of repetition 2 record only the start time for tools 1–3; the end times
were not written down during the sessions. They were read afterwards from the timestamps in
each tool's chat history: tool 1 ended at 00:03, tool 2 at 00:04 and tool 3 at 00:04 on
15 September 2026. They are recorded here rather than in the session files, so that
`data/raw/` stays as it was collected. Tool 4's file and tool 5's repeated session
(`Test2-AI-Tool5-Result2.txt`) record both times.

The five sessions of a repetition are run in parallel, each tool in its own browser tab.
Each question is pasted into the five tools in tool order (1 → 5). A tool receives the next
question as soon as it has answered the previous one; the other tools are not waited for. If
a tool returns an error, the questions continue in the other tools, and at each pass through
the tool order the failed session is retried to see whether it can continue. When the
question round is complete, each tool's answers are copied into its own session file.

The session order field ("Oturum sırası") was left with its template text in both
repetitions of round 1. The order is the tool order, and it can be read from the start times
recorded in the same files:

| Repetition | Date | Tool 1 (ChatGPT) | Tool 2 (Claude) | Tool 3 (Gemini) | Tool 4 (DeepSeek) | Tool 5 (Grok) |
|---|---|---|---|---|---|---|
| 1 | 13 Sep 2026 | 18:48 | 18:49 | 18:49 | 18:49 | 18:50 |
| 2 | 14 Sep 2026 | 23:29 | 23:30 | 23:30 | 23:31 | 23:32 |

In repetition 2, tool 5's repeated session (00:20–00:30, 15 September) and the resumed part
of tool 4's session (00:31–00:52) were run one after the other, not in parallel. As with the
end times above, the order is recorded here so that `data/raw/` stays as it was collected.

This is **not** a pre-registration. The instrument was frozen before the test, and the first
repetition's raw data was committed before the marking — no more and no less than that. `CHECKSUMS.txt`
records a SHA-256 for every tracked file, and what those hashes attest to differs by folder.
`data/raw/` is unchanged since the answers were collected, and that is the claim the file
exists to support. `protocol/` holds the versions in force; `coding-scheme.md` carries its
own revision history. `data/workbooks/` is edited as marking proceeds, and `data/analysis/`
is produced from those workbooks by the tool and kept together with them: when a marking
decision changes, the workbook is corrected and the tables are regenerated, so the two always
match. Anyone holding both can run the tool and obtain the same numbers.

The hashes are taken with Windows line endings (CRLF), the form in which the raw answers were
saved; the repository itself stores text files with LF. A checkout made with Git for Windows'
default settings reproduces them; elsewhere, convert text files to CRLF before running
`sha256sum -c CHECKSUMS.txt`.

## Limitations

Stated plainly, because they shape what the numbers mean:

- **Consensus coding by two evaluators.** The pair read each answer together and record one
  agreed label, so inter-rater agreement cannot be computed from it. Measuring the pair's own stability over time, by
  re-marking from a clean file, was left outside this pilot's scope and is adopted as a
  requirement for non-pilot runs (see `protocol/coding-scheme.md`).
- **Evaluation is not blind**, deliberately. The reasoning is in `protocol/coding-scheme.md`.
- **Web search is on in all five tools**, because one of them offers no way to turn it off
  and the setting has to be identical across tools. What is measured is therefore the model
  together with web access, not the model alone.
- **The model behind an answer is not always knowable.** Some tools switch model on their
  own; some never show a model name at all. Recorded as unknown where that is the case.
  That a free user cannot know which model answered is itself one of the findings.
- **Free-tier ceilings differ.** In one tool the stronger routing modes are paid-only, so a
  free user is confined to the weakest one.
- **Adyghe only, although the tool handles both languages.** `tool/profiles/` carries the
  Kabardian alphabet source and language profile, and `--profile kbd` works; there is no
  Kabardian data here. What is missing is not software but evaluators. Consensus coding needs
  a native speaker of the target language plus a second evaluator responsible for the
  reference translations, and no such pair was available for Kabardian within this study. A
  Kabardian arm would need an inter-institutional collaboration; it is named as future work
  rather than left as an unfinished corner of this one.

## People

Instrument, sessions and analysis: Mehmet Uğur Nemlioğlu (ORCID 0000-0002-1969-2356)

Linguistic evaluation, by consensus: Dr. Saida Abregova Nemlioğlu
(ORCID 0000-0002-2467-6692), native speaker of Adyghe, and Mehmet Uğur Nemlioğlu.
Authority over the target language rests with the first; the Turkish reference translation
used in question 4 rests with the second. Every label is the pair's joint judgement.

## Licence

Instrument, protocol, coding scheme, documentation and result tables: **CC BY 4.0**
(`LICENSE`). The analysis code is **MIT** (`tool/LICENSE`).

Raw tool outputs in `data/` were generated by third-party services. They are included so
the analysis can be checked and reproduced, and they are not covered by the licences above.
Before redistributing them further, check the terms of the provider concerned.
