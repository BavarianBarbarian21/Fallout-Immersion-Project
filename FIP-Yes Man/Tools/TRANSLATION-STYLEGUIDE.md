# FIP translation style guide

## Target locales

| RimWorld folder | BCP-47 target | Script/region |
|---|---|---|
| `Japanese` | `ja-JP` | Japanese |
| `Korean` | `ko-KR` | Korean |
| `ChineseSimplified` | `zh-Hans` | Simplified Han |
| `ChineseTraditional` | `zh-Hant` | Traditional Han |
| `Russian` | `ru-RU` | Cyrillic, Russian |

RimWorld folder names are technical API values and must not be replaced by
BCP-47 tags. The tags define the linguistic target for translation and QA.

## Non-translatable content

- XML element names, DefNames, package IDs, file paths and technical IDs.
- Placeholders such as `{0}`, `{PAWN_nameDef}`, `[NAME]`, `%s` and escaped
  control sequences.
- XML/Rich Text tags and established abbreviations marked `keep_original` in
  `translation-glossary.csv`.

Never add, remove, rename or reorder placeholders. Translate complete visible
sentences rather than reconstructing them from English fragments.

## Terminology and names

`translation-glossary.csv` is authoritative. Add a term there before changing
it repeatedly in generated files. Fictional species and forms use the approved
transliteration or established localized form. Descriptive titles may be
translated; technical codes remain unchanged.

## Encoding and output

- All generated XML and text files use UTF-8 without a legacy code page.
- XML output includes an explicit UTF-8 declaration.
- Source and target XML must contain the same identity keys.
- Simplified and Traditional Chinese are separate translations, not folder
  aliases of one generic Chinese output.

## QA expectations

Every build validates UTF-8 decoding, XML syntax, duplicate identities,
missing files, placeholder sequences, replacement characters/mojibake and
untranslated source text. A character inventory is generated per locale for
later in-game font coverage testing. Font rendering, CJK line breaking and UI
overflow still require a final RimWorld test.
