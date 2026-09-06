# Native document metadata characterization v1

- Schema: `document-native-metadata-characterization-v1`
- Generated: 2026-09-06 21:43:42Z
- Sources: 17

Acquisition evidence only. No semantic verdict is recorded: whether a native title is usable is a review decision.

## EPUB

- Sources: 7
- With a native title: 7
- With no metadata at all: 0

| Source | Title | Contributors | Language | Publisher | Dates |
|---|---|---|---|---|---|
| Institution de la Religion Chretienne.epub | Institution de la Religion Chrétienne (French Edition) | Calvin, Jean | fr | Éditions ThéoTeX | 2023-04-20 |
| Jesus and the Eyewitnesses - The Gospels as Eyewitness Testimony - Richard Bauckham.epub | Jesus and the Eyewitnesses, 2nd ed.: The Gospels as Eyewitness Testimony | Bauckham, Richard | en | Eerdmans | 2017-04-28 |
| La Septante Grec-Francais - Ouvrage Collectif.epub | La Septante Grec-Français (French Edition) | Collectif, Ouvrage | fr | Éditions ThéoTeX | 2023-04-20 |
| Logic and Philosophy - William H. Brenner.epub | Logic and Philosophy | Brenner, William H. | en-US | University of Notre Dame Press | 1993-09-30 |
| The New Testament_ A Historical Introduction to the Early Christian Writings.epub | The New Testament: A Historical Introduction to the Early Christian Writings (8th ed.) | Bart D. Ehrman; Hugo Méndez; calibre (9.11.0) [https://calibre-ebook.com] | en | — | 0101-01-01T00:00:00+00:00 |
| The New Testament_ A Historical Introduction to the Early Christian Writings_OCR.epub | The New Testament: A Historical Introduction to the Early Christian Writings (8th ed.) | Bart D. Ehrman; Hugo Méndez; calibre (9.11.0) [https://calibre-ebook.com] | en | — | 0101-01-01T00:00:00+00:00 |
| habermas-case-for-resurrection.epub | The Case for the Resurrection of Jesus | Unknown | en-US | Kregel Publications | 2004-03-26 |

## PDF

- Sources: 10
- With a native title: 8
- With no metadata at all: 0

| Source | Title | Contributors | Language | Publisher | Dates |
|---|---|---|---|---|---|
| From Peter to Papal Supremacy: A Structured Inferential-Cost Analysis of the Roman Catholic Case V4 FR.pdf | — | — | — | — | D:20260904014728+02'00'; D:20260904014728+02'00' |
| Institution de la Religion Chretienne - Jean Calvin.pdf | Institution de la Religion Chrétienne (French Edition) | Jean Calvin | — | — | D:20260821141408+00'00'; D:20260821141408+00'00' |
| Jesus and the Eyewitnesses, 2nd ed._ The Gospels as Eyewitness Testimony - Richard Bauckham.pdf | Jesus and the Eyewitnesses, 2nd ed.: The Gospels as Eyewitness Testimony | Richard Bauckham | — | — | D:20260723165741+00'00'; D:20260723165741+00'00' |
| La Septante Grec-Francais (French Edition) - Ouvrage Collectif.pdf | La Septante Grec-Français (French Edition) | Ouvrage Collectif | — | — | D:20260821141357+00'00'; D:20260821141357+00'00' |
| Logic and Philosophy - William H. Brenner.pdf | Logic and Philosophy | William H. Brenner | — | — | D:20260821141541+00'00'; D:20260821141541+00'00' |
| Nicene and Post Nicene Fathers Series II Vol 4.pdf | NPNF2-04. Athanasius: Select Works and Letters | Athanasius | — | — | D:20180712055445; D:20180712055445 |
| The Case for the Resurrection of Jesus - Gary R. Habernas.pdf | The Case for the Resurrection of Jesus | Unknown | — | — | D:20260511075937+00'00' |
| Vers une écologie des émotions.pdf | — | — | — | — | D:20250728221222+00'00'; D:20250728221222+00'00' |
| the-new-testament-a-historical-introduction-to-the-early-christian-writings.pdf | The New Testament: A Historical Introduction to the Early Christian Writings (8th ed.) | Bart D. Ehrman and Hugo Méndez | — | — | D:20250130213731-06'00'; D:20250130213731-06'00' |
| the-new-testament-a-historical-introduction-to-the-early-christian-writings_OCR.pdf | The New Testament: A Historical Introduction to the Early Christian Writings (8th ed.) | Bart D. Ehrman and Hugo Méndez | — | — | D:20250130213731-06'00'; D:20250130213731-06'00' |

## Findings

Acquisition works on both formats, and the availability is far better than the
architecture audit assumed.

**A native title is present in 15 of 17 sources** — 7 of 7 EPUB, 8 of 10 PDF.
Not one source yields no metadata at all: even the two PDFs without a title
carry dates.

**The titles are real bibliographic titles, not converter artefacts.** No source
in this corpus produced a `Microsoft Word - …` style value. Publisher titles,
subtitle and edition included, arrive verbatim: "Jesus and the Eyewitnesses,
2nd ed.: The Gospels as Eyewitness Testimony", "The New Testament: A Historical
Introduction to the Early Christian Writings (8th ed.)". Both PDF and EPUB agree
on the title for the four works present in both formats.

**The two PDFs without a title are informative.** "From Peter to Papal Supremacy"
and "Vers une écologie des émotions" are documents produced outside a publishing
chain; the eight publisher-produced PDFs all carry one. Absence appears to track
provenance rather than format.

**Contributors are available on both formats**, in the representation's own
form: "Bauckham, Richard" in EPUB against "Richard Bauckham" in PDF, for the same
work. Normalizing that is reconciliation work, not acquisition work.

**PDF exposes no language and no publisher.** The information dictionary has no
field for either. EPUB supplies both — language on 7 of 7, publisher on 6 of 7.
This asymmetry is a property of the formats and will not be fixed by reading
harder.

**Two quality signals worth recording before the portable model is frozen.**
Calibre-produced EPUBs list the toolchain as a contributor — "calibre (9.11.0)
[https://calibre-ebook.com]" appears beside the real authors — so a contributor
list is not automatically a list of people. And the same files carry
`0101-01-01T00:00:00+00:00` as their date, a placeholder rather than a
publication date. Both are acquired verbatim, which is the point: a cleaning
step here would have hidden exactly the evidence that shows a date field cannot
be trusted on its own.

**Dates are raw representation strings.** PDF supplies `D:20260723165741+00'00'`;
EPUB supplies ISO-8601. Parsing them is interpretation and is deliberately not
performed in this slice.

## Conclusions retained for P1-01

These are the decisions this characterization supports. They constrain the
portable `DocumentMetadata` model and are recorded here so P1-01 inherits
evidence rather than assumption.

**1. Native Title is the primary source for V1 when present.**
15 of 17 sources carry one, no converter artefact appeared, and PDF and EPUB
agree on the title for every work present in both formats. The filename fallback
becomes a fallback, used only when no native title exists.

**2. Language and Publisher are optional in the portable model.**
The PDF information dictionary has no field for either. Requiring them would
make the model unsatisfiable for a format already in production. EPUB supplies
language on 7 of 7 sources and publisher on 6 of 7.

**3. Per-field provenance is necessary, not decorative.**
A single document can carry a trustworthy value and a worthless one in the same
field: the Calibre EPUBs list real authors beside the toolchain. Provenance
attached to the document as a whole cannot express that; provenance attached to
each value can.

**4. Contributor acquisition is not an author conclusion.**
`dc:creator`, `dc:contributor` and PDF `/Author` are statements the
representation makes, in its own form — "Bauckham, Richard" against "Richard
Bauckham" for one work, and a toolchain string among the people. Concluding who
the authors are is reconciliation work in P1-01, over these acquired statements.

**5. Dates need a type and a sentinel policy in reconciliation.**
Acquisition keeps them verbatim: PDF `D:20260723165741+00'00'`, EPUB ISO-8601,
and placeholders such as `0101-01-01T00:00:00+00:00`. P1-01 owns both parsing to
a typed value and deciding what a sentinel means. Neither belongs to acquisition,
and the placeholder is only visible because acquisition did not clean it.

**6. EPUB subtitle is deferred to refinement interpretation.**
EPUB 3 expresses a subtitle as a second `dc:title` bound by a `meta refines`
refinement. Resolving that binding is interpretation, so no subtitle is acquired
in this slice. It is cheap to add in P1-01 once refinement resolution has a
defined owner.

## Corpus note

`De Decretis`, listed in the SDD characterization gate, is not present in
`tests/document_corpus/pdf/full`. The other five named PDF controls — Habermas,
Ehrman, Bauckham, Calvin, Brenner — are all present and characterized.
