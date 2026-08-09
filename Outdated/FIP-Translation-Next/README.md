# FIP Translation Next

This folder contains one self-contained translation tool and four newly
generated language mods:

- `FIP-Translation Russian`
- `FIP-Translation Chinese` (Simplified and Traditional Chinese)
- `FIP-Translation Japanese`
- `FIP-Translation Korean`

The tool scans the current FIP repository, installed FCP modules, FIP
compatibility targets, the saved playset list, and the currently active
non-Ludeon mods. It rebuilds the translation catalog from current source XML
instead of copying the old translation packs.

Run:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\Tool\Run-All.ps1
```

For a multi-hour unattended run:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\Tool\Run-Detached.ps1
```

The process is resumable. Translation results are committed to
`Tool/state/translation.sqlite3`; a stopped run can be started again without
losing completed work.

Translations are generated locally with the bundled Argos Translate runtime
and English-to-Russian, Chinese, Japanese, and Korean models under
`Tool/models`. Mod text is not uploaded to Google or another translation
service. Traditional Chinese is derived locally with OpenCC and then receives
its own Fallout terminology pass.

If the offline models ever need to be restored, run:

```powershell
python .\Tool\download_offline_models.py
```

Progress and completion evidence:

- `Tool/LAST_RUN_STATUS.txt`
- `Tool/logs/translation-run.log`
- `Tool/logs/progress.txt`
- `Tool/logs/validation-report.txt`
- `TRANSLATION_COMPLETE.txt` after a fully successful run

The four output mods intentionally do not use LoadFolders. Each language mod
contains every generated `DefInjected`, `Keyed`, and English source string-list
translation required for all resolved source mods. Personal-name pools are
included verbatim because translating proper names would corrupt them.
