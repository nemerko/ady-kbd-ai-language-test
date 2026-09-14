# Coding scheme

## Linguistic marking — done by the philologist

Errors are marked in Word using Review → New Comment, in the form:

    CODE  weight  —  short reason

| Code | Category | Covers |
|---|---|---|
| SNT | syntax | word order, clause construction, wrong choice of construction |
| MRF | morphology | wrong or missing affix, person prefix, case marking |
| SMN | semantics | wrong word choice, shift of meaning, omission, invention/addition |

| Weight | Meaning |
|---|---|
| 1 | minor — still understandable |
| 2 | marked — changes the meaning, or understanding it takes effort |
| 3 | severe — not understandable, or says the opposite |

Error counts are normalised per 100 words, so that a tool giving longer answers is not
penalised for length.

Marking runs question by question, not tool by tool: the same question is marked across
all five tools in sequence. One week later a subset is re-marked to measure intra-rater
consistency.

The criterion for S4 is the Turkish reference translation, for S5 the Adyghe reference
translation. Both were written before any tool output was seen.

## Character analysis — done by script, no judgement involved

For every answer:

- palochka accuracy: counts of U+04C0 and U+04CF against look-alikes
  (U+0406, U+0456, U+0049 `I`, U+006C `l`, U+007C `|`, U+0031 `1`)
- non-Cyrillic characters inside Adyghe words
- words mixing two writing systems
- Unicode normalisation form (NFC / NFD)
- invisible and formatting characters
- word and character counts, for normalisation
- similarity to the source paragraph — for S5 only

The similarity column is read in reverse: an independent translation is equivalent in
meaning but different in wording. Near-identity is a reason for suspicion, not a mark of
success. It is interpreted together with the S7 answer.

## Why the evaluation is not blind

Blinding was considered and deliberately not applied. At A2–B1 level the criteria are
largely objective — is the word spelled correctly, is the sentence well formed, is the
meaning consistent — and the character counts come from a script rather than from
judgement. Beyond that, S1 asks the tool to introduce itself, so some tools name
themselves in their answers; hiding identity would have meant altering the very text
being marked. Output length and formatting habits also differ enough between tools that
blinding would not have held in practice. Claiming blinding on paper while not achieving
it is worse than evaluating openly.

Two compensating measures are applied: marking proceeds question by question rather than
tool by tool, and a subset is re-marked a week later to measure intra-rater consistency.

Blinding will be reconsidered for a scaled study.
