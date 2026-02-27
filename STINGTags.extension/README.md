# STINGTags v9.6 — Smart Tag Intelligence Engine

pyRevit extension for intelligent tag selection, organisation, placement, and ISO 19650 asset tagging within Autodesk Revit. Four-tab modeless panel: SELECT, ORGANISE, CREATE, VIEW.

---

## Extension structure

```
STINGTags.extension/
├── bundle.yaml                          extension metadata
├── extension.json                       pyRevit registration
├── README.md
├── lib/
│   ├── selection_engine.py              DBSCAN, KMeans, SA, GA, ForceEngine, QuadTree
│   ├── tag_config.py                    DISC/SYS/PROD/FUNC maps, reads project_config.json
│   ├── tag_logic.py                     get_str/set_str helpers, level code deriver
│   └── shared_params.py                 GUIDs and category list from MR_PARAMETERS.txt
├── config/
│   └── project_config.json              written by ProjectConfig button
├── data/
│   ├── param_db.json                    885-parameter lookup database
│   └── placement_history.json           generated at runtime (learning data)
└── STINGTags.tab/
    └── STINGTags.panel/
        └── STINGTags.pushbutton/
            ├── bundle.yaml              pushbutton metadata
            ├── script.py                main panel script (7000+ lines)
            └── STINGTagsPanel.xaml       WPF UI definition
```

---

## Installation

Copy the entire `STINGTags.extension` folder to your pyRevit extensions location:

```
%AppData%\pyRevit\Extensions\STINGTags.extension
```

Or for network deployment:

```
\\server\BIM\pyRevit\STINGTags.extension
```

Register the parent folder in pyRevit Settings > Custom Extension Directories if using a non-default location. Restart Revit after registration.

---

## Shared parameter file

`lib/shared_params.py` contains:

```python
SHARED_PARAM_FILE = r'\\server\BIM\SharedParams\MR_PARAMETERS.txt'
```

Update this path to match your server before first use.

---

## First-run sequence (new project)

1. Load Shared Params — binds ASS_MNG parameters to all 53 Revit categories.
2. Project Config — set project-specific LOC and ZONE codes.
3. Auto Populate Tokens — sets DISC, LVL, SYS, FUNC, PROD on all elements.
4. Assign Numbers — sequential ASS_SEQ_NUM_TXT per group.
5. Validate Tags — review the QA report.
6. Export Tag Register — .xlsx export for submissions.

---

## ISO 19650 tag structure

```
DISC - LOC - ZONE - LVL - SYS - FUNC - PROD - NUM
 M   - BLD1 - Z01 - L02 - HVAC - SUP - GRL  - 0001
```

---

## Panel features (228 buttons)

SELECT, ORGANISE, CREATE, VIEW tabs with full AI-powered selection, multi-algorithm tag organisation, ISO 19650 token management, and view-level graphic overrides.

---

## Version history

| Version | Change |
|---------|--------|
| 7.0     | Tag Factory panel only |
| 8.0     | Added ISO_Tagging tab; algorithms moved to lib/selection_engine.py |
| 9.0     | Renamed to STINGTags; unified single-panel design |
| 9.4     | Colouriser, viewport sync, tag text alignment, all 13 ISO setters |
| 9.6     | Fixed extension paths, param lookup, health/anomaly, filter toggles |
