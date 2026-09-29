# Frozen test instrument

**Adyghe AI language test**
Prepared by: Mehmet Uğur Nemlioğlu (ORCID 0000-0002-1969-2356)
Approved by: Dr. Saida Abregova Nemlioğlu (ORCID 0000-0002-2467-6692), Adyghe philologist
Frozen on: 12 September 2026 — before the first session was run
Encoding: UTF-8

## What this instrument measures

Whether widely used AI chat tools, in their free tiers, can process Adyghe input and
produce semantically adequate Adyghe output, under conditions that exclude copying.
A second axis counts character-level correctness in the output: use of the palochka
letters (U+04C0, U+04CF) and contamination by visually similar Latin or Cyrillic
look-alikes.

It does **not** claim to measure whether a tool "understands" Adyghe. Searle's Chinese
room argument is constructed so that no behavioural test can settle it. What is measured
is functional adequacy, and the claim is limited to that.

## Language level

The questions and the translation paragraph are at the A2–B1 transition level. This was a
deliberate choice: at a higher level all tools would be expected to fail, and failure at
ceiling cannot distinguish between them.

## Design of the copying probe

S4 presents an Adyghe paragraph and asks for a Turkish translation. S5 gives back the
Turkish version of that same paragraph — with one deliberate change. A tool that returns
the changed place in its original form has copied what it saw in S4; a tool that carries
the change into Adyghe has processed the input. S7 then asks directly whether the two
paragraphs differ.

Three outcomes at S7, all of them findings:
1. States the difference correctly — strongest evidence both texts were processed.
2. Says "no difference" — the texts were not aligned.
3. Invents a difference that is not there — hallucination.

## Rounds

| Round | Timing | Measures |
|---|---|---|
| 1 and 2 | consecutive days | stability within the same model version |
| 3 and 4 | about one month later | change over time |

Two measurements at each time point make it possible to separate "the model improved"
from "the model already answers differently every time".

Rounds 1–2 and rounds 3–4 use different question sets: the second stage adds a second
control change, testing whether the tool carries a negative-conditional construction.

## The paragraph used

Written by the researcher and confirmed by the philologist to be at the A2–B1 transition
level. It has never been published. This matters: a published text would let the model
recall rather than translate.

Character profile of the source paragraph: 51 words, 11 short sentences, 12 lowercase
palochka (U+04CF), 1 uppercase palochka (U+04C0), zero Latin look-alikes, all Cyrillic,
NFC normalised, no invisible formatting characters. The input being clean in this respect
guarantees that any deviation in the output originates with the tool.

---

## A known defect in S6, deliberately not corrected

On 18 September 2026 a spelling error was found in the text of S6: the word that should
read `къыбгурыӏуагъэр` is written `къыбгрыӏуагъэр`, with one `у` missing from its root.
The error was found after the instrument had been frozen and after both sessions of
stage one had been run.

**The text has not been corrected.** This is the question the tools were actually asked,
and a frozen instrument that showed a question nobody was asked would invalidate the
record of the measurement. Stage two will be run with the same text, because comparing
the two stages depends on the stimulus staying identical. How the tools responded to
this question is reported separately among the study's findings.

# Stage one — questions for Round 1 and Round 2

The parenthesised Turkish after each Adyghe question is a gloss for the record; it is not
shown to the tool. Lines marked "Kontrol çevirisi" are the reference translations used as
marking criteria — also not shown to the tool.


```
S1: Уимафэ шӏу, о ухэт? Кӏэкӏэу къэпӏошъущта? (İyi günler, sen kimsin, kısaca söyleyebilir misin?)
```

```
S2: Ащыгъум о уӏэрышӏ акъыл шъыпкъ. Адэ, шъыпкъэу угупшысэшъущта? Гущыӏэухыгъитӏу-щыкӏэ джэуап къэт. (Demek gerçekten yapay zekasın. Peki, gerçekten düşünebiliyor musun? İki üç cümle ile yanıtla.)
```

```
S3: О зы къызэдзэкӏын ӏоф горэ къыостмэ пшӏышъущта? (Sana bir çeviri görevi versem yapabilir misin?)
```

```
S4: Ычӏэгъкӏэ къэтыгъэ параграфыр тыркубзэкӏэ къызэбдзэкӏына? (Aşağıdaki paragrafı Türkçeye çevirir misin?)
  
«Непэ мэфэ дах. Тищагу зэкӏэ тиунагъохэм тыщызэрэугъоигъ. Ӏахьылхэри тиблагъэхэри къэкӏощтых. Мы пчыхьэ нысащэ тиӏ. Сшынахьыжъ къыщагъ. Тинысэ дэхэ дэд. Ахэр дэгъоу зэдештэх. Ахэр илъэс хъугъэу зэдэгущыӏэщтыгъэх. Тӏури шӏу зэрэлъэгъухэзэ мы мэфэ дахэм къэсыгъэх. Блэкӏыгъэ илъэсым сшынахьыжъ иныбджэгъу инысащэ нэӏуасэ щызэфэхъугъагъэх. Тхьам унэгъо ӏужъ, унэгъо бай ышӏынхэу, амал дэгъухэр агъотынэу, насыпышӏонхэу сафэлъаӏо.»
(Kontrol çevirisi: Bugün güzel bir gün. Bahçemizde bütün aile toplandık. Akrabalarla yakınlarımızda gelecek. Bu akşam düğünümüz var. Abim gelin aldı. Gelinimiz çok güzel. Onlar birbirlerine yakışıyorlar. Onlar bir yıldır konuşuyorlardı. Her ikisi de birbirlerini severek bu güzel güne ulaştılar. Geçen sene abimin arkadaşının düğününde tanıştılar. Allahtan, sağlam, zengin bir aile olmalarını, {onlara} iyi imkanlar ve iyi nasipler vermesini dilerim.)
```

```
S5: Адэ мы тыркубзэ параграфыр адыгабзэкӏэ къызэбдзэкӏына? (Peki bu Türkçe paragrafı Adıgeceye çevirir misin?)
“Bugün güzel bir gün. Bahçemizde bütün aile toplandık. Akrabalarla yakınlarımızda gelecek. Bu akşam düğünümüz var. Abim gelin aldı. Gelinimiz çok güzel. Onlar birbirlerine yakışıyorlar. Onlar bir yıldır konuşuyorlardı. Her ikisi de birbirlerini severek bu güzel güne ulaştılar. Geçen sene abimin arkadaşının doğum gününde tanıştılar. Allahtan, sağlam, zengin bir aile olmalarını, onlara iyi imkanlar ve iyi nasipler vermesini dilerim.”
(Kontrol çevirisi: Непэ мэфэ дах. Тищагу зэкӏэ тиунагъохэм тыщызэрэугъоигъ. Ӏахьылхэри тиблагъэхэри къэкӏощтых. Мы пчыхьэ нысащэ тиӏ. Сшынахьыжъ къыщагъ. Тинысэ дэхэ дэд. Ахэр дэгъоу зэдештэх. Ахэр илъэс хъугъэу зэдэгущыӏэщтыгъэх. Тӏури шӏу зэрэлъэгъухэзэ мы мэфэ дахэм къэсыгъэх. Блэкӏыгъэ илъэсым сшынахьыжъ иныбджэгъу икъызыхъугъэ мафэмкӏэ нэӏуасэ щызэфэхъугъагъэх. Тхьам унэгъо ӏужъ, унэгъо бай ышӏынхэу, амал дэгъухэр агъотынэу, насыпышӏонхэу сафэлъаӏо.)
```

```
S6: Мы къызэбдзэкӏыгъэ параграфитӏумкӏэ сыд о къыбгрыӏуагъэр? Гущыӏэухыгъитӏукӏэ резюмэ къэтхыба? (Bu çevirdiğin iki paragraftan anladığın nedir? İki cümle ile özetler misin?)
```

```
S7: Мы параграфитӏум зэфэшъхьафыныгъэ яӏа? Сыда зэрэзэфэшъхьафыр? Щымыӏэмэ «зэфэшъхьафыныгъэ щыӏэп» къаӏо. (Bu iki paragraf arasında fark var mı? Varsa nedir? Yoksa "fark yok" de.)
```

---

# Stage two — questions for Round 3 and Round 4

Identical to stage one except for a second control change in the Turkish paragraph of S5
(see "Design of the copying probe" above).


```
S1: Уимафэ шӏу, о ухэт? Кӏэкӏэу къэпӏошъущта? (İyi günler, sen kimsin, kısaca söyleyebilir misin?)
```

```
S2: Ащыгъум о уӏэрышӏ акъыл шъыпкъ. Адэ, шъыпкъэу угупшысэшъущта? Гущыӏэухыгъитӏу-щыкӏэ джэуап къэт. (Demek gerçekten yapay zekasın. Peki, gerçekten düşünebiliyor musun? İki üç cümle ile yanıtla.)
```

```
S3: О зы къызэдзэкӏын ӏоф горэ къыостмэ пшӏышъущта? (Sana bir çeviri görevi versem yapabilir misin?)
```

```
S4: Ычӏэгъкӏэ къэтыгъэ параграфыр тыркубзэкӏэ къызэбдзэкӏына? (Aşağıdaki paragrafı Türkçeye çevirir misin?)
  
«Непэ мэфэ дах. Тищагу зэкӏэ тиунагъохэм тыщызэрэугъоигъ. Ӏахьылхэри тиблагъэхэри къэкӏощтых. Мы пчыхьэ нысащэ тиӏ. Сшынахьыжъ къыщагъ. Тинысэ дэхэ дэд. Ахэр дэгъоу зэдештэх. Ахэр илъэс хъугъэу зэдэгущыӏэщтыгъэх. Тӏури шӏу зэрэлъэгъухэзэ мы мэфэ дахэм къэсыгъэх. Блэкӏыгъэ илъэсым сшынахьыжъ иныбджэгъу инысащэ нэӏуасэ щызэфэхъугъагъэх. Тхьам унэгъо ӏужъ, унэгъо бай ышӏынхэу, амал дэгъухэр агъотынэу, насыпышӏонхэу сафэлъаӏо.»
(Kontrol çevirisi: Bugün güzel bir gün. Bahçemizde bütün aile toplandık. Akrabalarla yakınlarımızda gelecek. Bu akşam düğünümüz var. Abim gelin aldı. Gelinimiz çok güzel. Onlar birbirlerine yakışıyorlar. Onlar bir yıldır konuşuyorlardı. Her ikisi de birbirlerini severek bu güzel güne ulaştılar. Geçen sene abimin arkadaşının düğününde tanıştılar. Allahtan, sağlam, zengin bir aile olmalarını, {onlara} iyi imkanlar ve iyi nasipler vermesini dilerim.)
```

```
S5: Адэ мы тыркубзэ параграфыр адыгабзэкӏэ къызэбдзэкӏына? (Peki bu Türkçe paragrafı Adıgeceye çevirir misin?)
“Bugün güzel bir gün. Bahçemizde bütün aile toplandık. Akrabalarla yakınlarımızda gelecek. Bu akşam düğünümüz var. Abim gelin aldı. Gelinimiz çok güzel. Onlar birbirlerine yakışıyorlar. Onlar bir yıldır konuşuyorlardı. Her ikisi de birbirlerini sevmeselerdi bu güzel güne ulaşamazlardı. Geçen sene abimin arkadaşının doğum gününde tanıştılar. Allahtan, sağlam, zengin bir aile olmalarını, onlara iyi imkanlar ve iyi nasipler vermesini dilerim.”
(Kontrol çevirisi: Непэ мэфэ дах. Тищагу зэкӏэ тиунагъохэм тыщызэрэугъоигъ. Ӏахьылхэри тиблагъэхэри къэкӏощтых. Мы пчыхьэ нысащэ тиӏ. Сшынахьыжъ къыщагъ. Тинысэ дэхэ дэд. Ахэр дэгъоу зэдештэх. Ахэр илъэс хъугъэу зэдэгущыӏэщтыгъэх. Тӏури шӏу зэрэмылъэгъущтыгъэхэмэ мы мэфэ дахэм къэсышъущтыгъэхэп. Блэкӏыгъэ илъэсым сшынахьыжъ иныбджэгъу икъызыхъугъэ мафэмкӏэ нэӏуасэ щызэфэхъугъагъэх. Тхьам унэгъо ӏужъ, унэгъо бай ышӏынхэу, амал дэгъухэр агъотынэу, насыпышӏонхэу сафэлъаӏо.)
```

```
S6: Мы къызэбдзэкӏыгъэ параграфитӏумкӏэ сыд о къыбгрыӏуагъэр? Гущыӏэухыгъитӏукӏэ резюмэ къэтхыба? (Bu çevirdiğin iki paragraftan anladığın nedir? İki cümle ile özetler misin?)
```

```
S7: Мы параграфитӏум зэфэшъхьафыныгъэ яӏа? Сыда зэрэзэфэшъхьафыр? Щымыӏэмэ «зэфэшъхьафыныгъэ щыӏэп» къаӏо. (Bu iki paragraf arasında fark var mı? Varsa nedir? Yoksa "fark yok" de.)
```
