# Round 1 — session conditions

Run on 13 September 2026. The five sessions were opened in five browser tabs and the
questions were asked in parallel, in order, so that all five tools answered the same
question within the same time window. Response speed is not part of what this test
measures; start and end times were recorded only to document that the sessions were
parallel.

## Environment

| | |
|---|---|
| Browser | Firefox, dedicated profile created for this test |
| Account | a spare e-mail account with no personalisation history |
| Interface language | English, in all five tools |
| Web search | ON in all five tools |
| Reasoning modes | ON wherever the free tier offers them |
| Memory / history | turned off wherever the tool has it |
| Provider training setting | turned off wherever the tool has it |
| Non-essential cookies | rejected |

## Why web search is on

The rule is that the setting must be identical across the five tools. One tool offers no
way to turn web search off, so the shared condition became "on". What is therefore
measured is not the model alone but the model together with web access. The copying probe
still holds: neither paragraph used in the test has ever been published, so neither can be
found by searching.

## Why reasoning modes are on

This study measures the level of success the tools can reach, not their failure. A person
trying to communicate in Adyghe would use every capability the free tier offers. Defaults
differ arbitrarily between tools — on in one, off in another — so "all on" is both uniform
and expressible in one sentence.

Two costs follow, and both are reported as limitations. Some tools switch model on their
own when they judge a question complex, so which model produced an answer cannot always be
known; in those cases the model name is recorded as unknown. And with reasoning on, the
variation between answers to the same question grows.

One exception: mechanisms that silently switch model when a safety filter triggers were
turned off where the tool allows it. These add no capability. With them off the chat pauses
instead, and a paused chat is an observable event, whereas a silent substitution corrupts
the measurement invisibly.

## Per-tool notes

Session records for each tool — model name, whether a model name was shown at all, which
reasoning mode was active, which settings could and could not be turned off — are in the
header of each `tool<n>-round1-raw.txt` file.

Field names in those files are in Turkish. A short glossary:

| Turkish | English |
|---|---|
| Araç no ve adı | tool number and name |
| Model adı görünüyor mu | is a model name shown |
| Arayüzdeki model adı | model name as shown in the interface |
| Model seçimi | model selection |
| Akıl yürütme kipi | reasoning mode |
| Sessiz model değişimi | silent model switching |
| Bellek / geçmiş | memory / history |
| Eğitim / veri kullanımı | training / data use |
| Web araması | web search |
| Konum bilgisi | location data |
| Sohbet silindi mi | was the chat deleted |
| Kotaya takıldı mı | was a usage limit hit |
| CEVAP S1..S7 | answer to question 1..7 |
