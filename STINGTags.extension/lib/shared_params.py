# -*- coding: utf-8 -*-
"""
shared_params.py
Complete parameter GUID map extracted from MR_PARAMETERS.txt.
All GUIDs are real values — no placeholders remain.

Two binding passes in LoadSharedParams:
  Pass 1 (UNIVERSAL_PARAMS)    — 17 ASS_MNG parameters → all 53 categories.
  Pass 2 (DISCIPLINE_PARAMS)   — all discipline-specific tag containers and
                                  category-specific source params → their
                                  correct category subsets only.

Known data-type defect in MR_PARAMETERS.txt:
  PLM_EQP_TAG_01_TXT and PLM_EQP_TAG_02_TXT are declared as NUMBER instead
  of TEXT (lines 854-855 of the file). Binding will succeed in Revit, but
  Naviate Combine Param Values cannot assemble a NUMBER field into a tag formula.
  Fix: open MR_PARAMETERS.txt in a text editor and change NUMBER to TEXT on
  both lines, then reload the shared parameter file in Revit.
"""

import os

# ── Network path — update per project ────────────────────────────────────────
SHARED_PARAM_FILE = r'\\server\BIM\SharedParams\MR_PARAMETERS.txt'

# ── NUM zero-padding width ─────────────────────────────────────────────────────
NUM_PAD = 4   # e.g. 0001

# ── Separator for assembled tags ───────────────────────────────────────────────
SEPARATOR = '-'

# ── CONFIG_SOURCE (populated at runtime by tag_config.py) ────────────────────
CONFIG_SOURCE = 'defaults'

# ═════════════════════════════════════════════════════════════════════════════
# COMPLETE GUID MAP  —  sourced directly from MR_PARAMETERS.txt
# ═════════════════════════════════════════════════════════════════════════════
PARAM_GUIDS = {

    # ── PASS 1: Universal source tokens (ASS_MNG, group 2) ───────────────────
    'ASS_DISCIPLINE_COD_TXT':  '8c7dcfd7-f922-52d0-b859-81cae8d17dc0',
    'ASS_LOC_TXT':             'b7469c27-c80e-5b59-b999-1a99ba620cd1',
    'ASS_ZONE_TXT':            'dc0d940f-e4ce-5e73-a0a7-fc7094148c84',
    'ASS_LVL_COD_TXT':         'b1e51fab-fa88-50df-8b2f-bcdbe48e7c78',
    'ASS_SYSTEM_TYPE_TXT':     '2b3658d9-bfc6-56db-9df5-901337fde0f5',
    'ASS_FUNC_TXT':            '1ddff9a8-6e66-4a93-88fe-f3b94fbd5710',
    'ASS_PRODCT_COD_TXT':      '082a2a05-3387-5501-b355-51dd45e23e9f',
    'ASS_SEQ_NUM_TXT':         'bbe1cd55-247b-48bd-94ba-a08031f06d5b',
    'ASS_STATUS_TXT':          'b97665a8-5e34-585b-9674-fbdd83d7637f',
    'ASS_INST_DETAIL_NUM_TXT': '73f74429-76da-4ae8-ae90-66884bad8a06',
    # MNT_TYPE_TXT is in group 5 (LTG_CONTROLS) but treated as universal
    'MNT_TYPE_TXT':            '9358b203-900a-52c5-9e80-3a4d67dc5c51',

    # ── PASS 1: T1 / T2 assembled tag containers (ASS_MNG, group 2) ──────────
    'ASS_TAG_1_TXT':           '1eeb577d-342d-5039-97f1-f1dd8d80c8c4',
    'ASS_TAG_2_TXT':           'bf6cb687-478f-459a-8c5d-ece073c24831',
    'ASS_TAG_3_TXT':           '068abd3b-650e-4a7c-ac07-82e556160de0',
    'ASS_TAG_4_TXT':           '9432c6e4-8912-4a0b-bf04-2ee1cf3afadf',
    'ASS_TAG_5_TXT':           'c875d795-d764-4a8b-826a-2677635b15b9',
    'ASS_TAG_6_TXT':           '6fcfc27b-a42d-4f54-92ad-66c875f9be38',

    # ─────────────────────────────────────────────────────────────────────────
    # PASS 2: Discipline-specific tag containers
    # All GUIDs confirmed present in MR_PARAMETERS.txt
    # ─────────────────────────────────────────────────────────────────────────

    # T3-H: HVAC Equipment (HVC_SYSTEMS, group 7)
    'HVC_EQP_TAG_01_TXT':      '62f66802-8bd6-4861-aad0-f23e5b1905d2',
    'HVC_EQP_TAG_02_TXT':      'c3974615-09ac-46ee-8c69-589df04a35c0',
    'HVC_EQP_TAG_03_TXT':      '59605dc9-0029-4704-8dff-cf3bd25b749d',

    # T3-D: Ducts / Duct Fittings / Flex Ducts / Air Terminals (HVC_SYSTEMS, group 7)
    'HVC_DCT_TAG_01_TXT':      '9751957e-f2c7-4f9a-8f3a-dd432af861f5',
    'HVC_DCT_TAG_02_TXT':      '0a3ab853-91c0-4e01-9c93-68c7fb333a85',
    'HVC_DCT_TAG_03_TXT':      '7ed52b35-4486-428d-b7ee-8f01c0140763',
    'HVC_FLX_TAG_01_TXT':      'cd984834-8273-4d2c-89c4-dfe33520e2d2',

    # T3-E: Electrical Equipment (ELC_PWR, group 9)
    'ELC_EQP_TAG_01_TXT':      'd9441c7a-025c-4034-a7da-dc960df4a57c',
    'ELC_EQP_TAG_02_TXT':      '64805f01-8548-44c5-8091-ffcf04d35d7d',

    # T3-F: Electrical Fixtures + Lighting Fixtures (ELC_PWR, group 9)
    'ELE_FIX_TAG_1_TXT':       '5b5c4c6c-c420-4f30-a67e-bdeca6944577',
    'ELE_FIX_TAG_2_TXT':       '6b727a38-f448-4b8e-81fb-a2570637ee57',

    # T3-L: Lighting Fixtures (LTG_CONTROLS, group 5)
    'LTG_FIX_TAG_01_TXT':      '647a8916-560f-4c0c-b6ca-f2ee23a97fb0',
    'LTG_FIX_TAG_02_TXT':      '7783ce57-766b-4903-8e9f-bde24b706b42',

    # T3-P: Pipework (PLM_DRN, group 8)
    # NOTE: these are declared as NUMBER in MR_PARAMETERS.txt (should be TEXT).
    # Binding will succeed but Naviate formula assembly will not work until
    # the datatype is corrected to TEXT in MR_PARAMETERS.txt.
    'PLM_EQP_TAG_01_TXT':      'e4f6bae0-bc3b-42f3-a3c9-ef31a23a80d0',
    'PLM_EQP_TAG_02_TXT':      '4a31aa91-9511-4fa4-8024-632f64b6a2ae',

    # T3-S: Fire & Life Safety (FLS_LIFE_SFTY, group 13)
    'FLS_DEV_TAG_01_TXT':      'c611d904-3f4f-45c6-81e6-11f3cc64759d',
    'FLS_DEV_TAG_02_TXT':      '86a39b3f-6ef2-46d1-a9ab-a0620f1761ac',

    # T3: Conduits (ELC_PWR, group 9) — source + container
    'ELC_CDT_SZ_MM':           '3ab3a279-b143-42ae-ace3-8b3997dcc8a4',
    'ELC_CDT_MAT_TXT':         '1d4c7468-66e8-4ee7-a13f-25fd28d4a875',
    'ELC_CDT_INSTALL_METHOD_TXT': 'a6443de3-f5b7-4279-b348-1e6fbac12e88',
    'ELC_CDT_CBL_FILL_PCT':    '2fdc3987-8e87-4d81-ada8-47514e7b50f2',
    'ELC_CDT_TAG_01_TXT':      'f867c60f-1fda-4f3f-a052-cf81268c0600',
    'ELC_CDT_TAG_02_TXT':      'aab93953-601b-4bab-8d52-079193f71524',

    # T3: Cable Trays (ELC_PWR, group 9) — source + container
    'ELC_CTR_WIDTH_MM':        'd1f2feb5-c03b-44df-8b3d-41adf0720aa7',
    'ELC_CTR_DEPTH_MM':        '60f19e3f-1cc9-431d-b041-22be14800ecb',
    'ELC_CTR_MAT_TXT':         'e9384354-be81-4930-8dbf-c4a790cb7944',
    'ELC_CTR_FILL_PCT':        '0e7a96cb-9218-4e4c-a1d8-15c110902e94',
    'ELC_CTR_TAG_01_TXT':      'aed9d3de-71ff-4a73-aba3-5bbd16af7526',

    # T3: Low-voltage / Communications (COM_DAT, group 4)
    'COM_DEV_TAG_01_TXT':      '7775b009-25a6-43b9-b0ec-31ac063a9183',
    'SEC_DEV_ZONE_TXT':        '7d356ae1-4b0e-442d-a3bb-8082c04ba1a8',
    'SEC_CBL_NR_TXT':          'dea07e5e-cd31-4beb-8b33-bb2c01faee6f',
    'SEC_DEV_TAG_01_TXT':      'e892601e-68a8-4f68-9618-223326a2cac4',
    'NCL_ZONE_TXT':            '4077d6d9-5914-4ec1-a8bc-6242f45bc0e1',
    'NCL_CALL_TYPE_TXT':       'bb14a823-b485-45b0-b6ae-a564826c86c3',
    'NCL_DEV_TAG_01_TXT':      '34b75836-a597-4bc6-b7ee-6713a5caf6a5',
    'ICT_OUTLET_TYPE_TXT':     'a440a436-44a3-47f8-a8bf-6c218698e32d',
    'ICT_PORT_NR_TXT':         '3b7b1bdf-f50d-44b1-b3ab-3ae2586d47f2',
    'ICT_PATCH_PANEL_TXT':     '951f8832-2b1c-496d-b73c-4f622591fb5b',
    'ICT_DEV_TAG_01_TXT':      'ec3cc840-731f-47f7-943e-465a027fea74',

    # ─────────────────────────────────────────────────────────────────────────
    # MATERIAL TAGS (mat_tag_architecture.docx)
    # ─────────────────────────────────────────────────────────────────────────

    # T1 Universal material tags (MAT_INFO, group 10)
    'MAT_TAG_1_TXT':           '6b6c3ada-95bb-4a7b-aa9c-beb47c639928',
    'MAT_TAG_2_TXT':           '0d78850d-45b1-4179-9079-3e3ec525a4ef',
    'MAT_TAG_3_TXT':           '04883c90-411f-4ab5-b937-2909c54fdf65',
    'MAT_TAG_4_TXT':           '1d5e82d6-e988-4e9d-8be1-e195f2782041',
    # ISO standard tags — group 10 and group 1 (PER_SUST)
    'MAT_TAG_5_TXT':           'c7efed83-bbd2-4a2e-b79a-292a1cfa3a18',
    'MAT_TAG_6_TXT':           'd0d35a3a-abea-4898-9deb-4cdca865e8ee',

    # T2 Performance tags (MAT_INFO, group 10)
    'MAT_PERF_TAG_1_TXT':      'cc56b572-0538-4c37-b158-db6dbd51d3de',
    'MAT_PERF_TAG_2_TXT':      '28487745-251e-4555-bf56-b1d06c933cc5',
    'MAT_PERF_TAG_3_TXT':      '011ce782-1772-4477-bf4e-4a29a95c507a',

    # T3 Finish tags (BLE_ELES, group 14)
    'FIN_WALL_TAG_TXT':        '456cd8aa-e875-458c-b778-7abbe2f0563d',
    'FIN_FLR_TAG_TXT':         'a2b19e19-d4ad-44b4-97d4-8e80dc981228',
    'FIN_CEIL_TAG_TXT':        'cfbedb01-9783-403d-a273-2f2e4e215907',

    # T3 Envelope tags (BLE_ELES, group 14)
    'ENV_FAC_TAG_TXT':         '3eda6808-aa94-46d4-bd09-71fa6a26c090',
    'ENV_ROOF_TAG_TXT':        '9a1ef97d-12f7-419d-93f9-60f3773e55ef',
    'ENV_WIN_TAG_TXT':         'a204d24a-e981-44b4-bf39-afe03ddfa8dc',

    # T3 Structural tags (BLE_ELES, group 14)
    'STR_CONC_TAG_TXT':        '04a3dc69-9931-4154-809f-896df5584100',
    'STR_STEEL_TAG_TXT':       'e4f2fcf1-4764-4f8c-9b8d-e8512834e1aa',

    # T3 Sustainability tags (PER_SUST, group 1)
    'SUST_MAT_TAG_1_TXT':      '5491e7e3-472a-4b7a-9497-2e5170ce870b',
    'SUST_MAT_TAG_2_TXT':      '7f2ca957-dd29-4a9a-835a-c39dbdfed8d9',

    # T3 Compliance tags (MAT_INFO, group 10)
    'COMP_MAT_TAG_1_TXT':      '58757592-bbc4-4cfd-8228-c89c89bb0d4a',
    'COMP_MAT_TAG_2_TXT':      '6eef14b4-c51c-4918-bd6c-dd05aca45813',

    # ISO standard source params (MAT_INFO / PER_SUST)
    'MAT_PROP_DICT_GUID_TXT':  '8d8d9310-7d16-4e70-bf7e-3f328816a223',
    'MAT_EPD_REF_TXT':         '9858e380-29bb-430b-82d0-7f2898d2fd3d',
}

# ═════════════════════════════════════════════════════════════════════════════
# PASS 1 — 17 universal parameters → all 53 categories
# ═════════════════════════════════════════════════════════════════════════════
UNIVERSAL_PARAMS = [
    'ASS_DISCIPLINE_COD_TXT',
    'ASS_LOC_TXT',
    'ASS_ZONE_TXT',
    'ASS_LVL_COD_TXT',
    'ASS_SYSTEM_TYPE_TXT',
    'ASS_FUNC_TXT',
    'ASS_PRODCT_COD_TXT',
    'ASS_SEQ_NUM_TXT',
    'ASS_TAG_1_TXT',
    'ASS_TAG_2_TXT',
    'ASS_TAG_3_TXT',
    'ASS_TAG_4_TXT',
    'ASS_TAG_5_TXT',
    'ASS_TAG_6_TXT',
    'ASS_STATUS_TXT',
    'ASS_INST_DETAIL_NUM_TXT',
    'MNT_TYPE_TXT',
]

# ═════════════════════════════════════════════════════════════════════════════
# PASS 2 — Discipline tag containers and category-specific source params
# {parameter_name: [OST_BuiltInCategory string, ...]}
# ═════════════════════════════════════════════════════════════════════════════
DISCIPLINE_PARAMS = {

    # ── T3-H: HVAC Equipment ─────────────────────────────────────────────────
    'HVC_EQP_TAG_01_TXT': ['OST_MechanicalEquipment'],
    'HVC_EQP_TAG_02_TXT': ['OST_MechanicalEquipment'],
    'HVC_EQP_TAG_03_TXT': ['OST_MechanicalEquipment'],

    # ── T3-D: Ducts, Duct Fittings, Flex Ducts, Air Terminals ────────────────
    'HVC_DCT_TAG_01_TXT': ['OST_DuctCurves', 'OST_DuctFitting', 'OST_FlexDuctCurves'],
    'HVC_DCT_TAG_02_TXT': ['OST_DuctCurves', 'OST_DuctFitting'],
    'HVC_DCT_TAG_03_TXT': ['OST_DuctTerminal'],                      # Air Terminals only
    'HVC_FLX_TAG_01_TXT': ['OST_FlexDuctCurves'],                    # Flex Ducts only

    # ── T3-E: Electrical Equipment ────────────────────────────────────────────
    'ELC_EQP_TAG_01_TXT': ['OST_ElectricalEquipment'],
    'ELC_EQP_TAG_02_TXT': ['OST_ElectricalEquipment'],

    # ── T3-F: Electrical Fixtures (circuit traceability) ─────────────────────
    'ELE_FIX_TAG_1_TXT':  ['OST_ElectricalFixtures', 'OST_LightingFixtures'],
    'ELE_FIX_TAG_2_TXT':  ['OST_ElectricalFixtures', 'OST_LightingFixtures'],

    # ── T3-L: Lighting Fixtures ───────────────────────────────────────────────
    'LTG_FIX_TAG_01_TXT': ['OST_LightingFixtures'],
    'LTG_FIX_TAG_02_TXT': ['OST_LightingFixtures'],

    # ── T3-P: Pipework (NOTE: NUMBER datatype defect — see file header) ───────
    'PLM_EQP_TAG_01_TXT': ['OST_PipeCurves', 'OST_PipeFitting',
                            'OST_PipeAccessory', 'OST_FlexPipeCurves'],
    'PLM_EQP_TAG_02_TXT': ['OST_PipeCurves', 'OST_PlumbingFixtures'],

    # ── T3-S: Fire & Life Safety ──────────────────────────────────────────────
    'FLS_DEV_TAG_01_TXT': ['OST_FireAlarmDevices', 'OST_Sprinklers',
                            'OST_GenericModel', 'OST_SpecialityEquipment'],
    'FLS_DEV_TAG_02_TXT': ['OST_Sprinklers'],

    # ── T3: Conduits — source params + containers ─────────────────────────────
    'ELC_CDT_SZ_MM':              ['OST_Conduit', 'OST_ConduitFitting'],
    'ELC_CDT_MAT_TXT':            ['OST_Conduit', 'OST_ConduitFitting'],
    'ELC_CDT_INSTALL_METHOD_TXT': ['OST_Conduit', 'OST_ConduitFitting'],
    'ELC_CDT_CBL_FILL_PCT':       ['OST_Conduit'],
    'ELC_CDT_TAG_01_TXT':         ['OST_Conduit', 'OST_ConduitFitting'],
    'ELC_CDT_TAG_02_TXT':         ['OST_Conduit'],

    # ── T3: Cable Trays — source params + containers ──────────────────────────
    'ELC_CTR_WIDTH_MM':    ['OST_CableTray', 'OST_CableTrayFitting'],
    'ELC_CTR_DEPTH_MM':    ['OST_CableTray', 'OST_CableTrayFitting'],
    'ELC_CTR_MAT_TXT':     ['OST_CableTray', 'OST_CableTrayFitting'],
    'ELC_CTR_FILL_PCT':    ['OST_CableTray'],
    'ELC_CTR_TAG_01_TXT':  ['OST_CableTray', 'OST_CableTrayFitting'],

    # ── T3: Communication / BMS devices ──────────────────────────────────────
    'COM_DEV_TAG_01_TXT':  ['OST_CommunicationDevices', 'OST_GenericModel'],

    # ── T3: Security devices ──────────────────────────────────────────────────
    'SEC_DEV_ZONE_TXT':    ['OST_SecurityDevices'],
    'SEC_CBL_NR_TXT':      ['OST_SecurityDevices'],
    'SEC_DEV_TAG_01_TXT':  ['OST_SecurityDevices'],

    # ── T3: Nurse call devices ────────────────────────────────────────────────
    'NCL_ZONE_TXT':        ['OST_NurseCallDevices'],
    'NCL_CALL_TYPE_TXT':   ['OST_NurseCallDevices'],
    'NCL_DEV_TAG_01_TXT':  ['OST_NurseCallDevices'],

    # ── T3: ICT / data devices ────────────────────────────────────────────────
    'ICT_OUTLET_TYPE_TXT': ['OST_DataDevices', 'OST_TelephoneDevices'],
    'ICT_PORT_NR_TXT':     ['OST_DataDevices', 'OST_TelephoneDevices'],
    'ICT_PATCH_PANEL_TXT': ['OST_DataDevices', 'OST_TelephoneDevices'],
    'ICT_DEV_TAG_01_TXT':  ['OST_DataDevices', 'OST_TelephoneDevices'],

    # ── Material tags (from mat_tag_architecture.docx) ────────────────────────
    # T1/T2 universal material tags — host element categories
    'MAT_TAG_1_TXT':        ['OST_Walls', 'OST_Floors', 'OST_Ceilings', 'OST_Roofs',
                              'OST_StructuralFraming', 'OST_StructuralColumns',
                              'OST_StructuralFoundation', 'OST_Windows', 'OST_Doors'],
    'MAT_TAG_2_TXT':        ['OST_Walls', 'OST_Floors', 'OST_Ceilings', 'OST_Roofs',
                              'OST_StructuralFraming', 'OST_StructuralColumns',
                              'OST_StructuralFoundation', 'OST_Windows', 'OST_Doors'],
    'MAT_TAG_3_TXT':        ['OST_Walls', 'OST_Floors', 'OST_Ceilings', 'OST_Roofs',
                              'OST_StructuralFraming', 'OST_StructuralColumns',
                              'OST_StructuralFoundation', 'OST_Windows', 'OST_Doors'],
    'MAT_TAG_4_TXT':        ['OST_Walls', 'OST_Floors', 'OST_Ceilings', 'OST_Roofs',
                              'OST_StructuralFraming', 'OST_StructuralColumns',
                              'OST_StructuralFoundation', 'OST_Windows', 'OST_Doors'],
    'MAT_TAG_5_TXT':        ['OST_Walls', 'OST_Floors', 'OST_Ceilings', 'OST_Roofs',
                              'OST_StructuralFraming', 'OST_StructuralColumns',
                              'OST_Windows', 'OST_Doors'],
    'MAT_TAG_6_TXT':        ['OST_Walls', 'OST_Floors', 'OST_Ceilings', 'OST_Roofs',
                              'OST_StructuralFraming', 'OST_StructuralColumns',
                              'OST_Windows'],

    # T2 Performance tags
    'MAT_PERF_TAG_1_TXT':   ['OST_Walls', 'OST_Floors', 'OST_Ceilings', 'OST_Roofs',
                              'OST_Windows'],
    'MAT_PERF_TAG_2_TXT':   ['OST_Walls', 'OST_Floors', 'OST_Ceilings'],
    'MAT_PERF_TAG_3_TXT':   ['OST_Walls', 'OST_Floors', 'OST_Ceilings', 'OST_Roofs'],

    # T3 Finish tags — single-category
    'FIN_WALL_TAG_TXT':     ['OST_Walls'],
    'FIN_FLR_TAG_TXT':      ['OST_Floors'],
    'FIN_CEIL_TAG_TXT':     ['OST_Ceilings'],

    # T3 Envelope tags — single-category
    'ENV_FAC_TAG_TXT':      ['OST_Walls', 'OST_CurtainWallPanels'],
    'ENV_ROOF_TAG_TXT':     ['OST_Roofs'],
    'ENV_WIN_TAG_TXT':      ['OST_Windows'],

    # T3 Structural tags
    'STR_CONC_TAG_TXT':     ['OST_StructuralColumns', 'OST_StructuralFraming',
                              'OST_StructuralFoundation', 'OST_Walls', 'OST_Floors'],
    'STR_STEEL_TAG_TXT':    ['OST_StructuralColumns', 'OST_StructuralFraming'],

    # T3 Sustainability tags
    'SUST_MAT_TAG_1_TXT':   ['OST_Walls', 'OST_Floors', 'OST_Ceilings', 'OST_Roofs',
                              'OST_StructuralFraming', 'OST_StructuralColumns'],
    'SUST_MAT_TAG_2_TXT':   ['OST_Walls', 'OST_Floors', 'OST_Ceilings', 'OST_Roofs',
                              'OST_StructuralFraming', 'OST_StructuralColumns'],

    # T3 Compliance / procurement tags
    'COMP_MAT_TAG_1_TXT':   ['OST_Walls', 'OST_Floors', 'OST_Ceilings', 'OST_Roofs',
                              'OST_StructuralFraming', 'OST_StructuralColumns',
                              'OST_Windows', 'OST_Doors'],
    'COMP_MAT_TAG_2_TXT':   ['OST_Walls', 'OST_Floors', 'OST_Ceilings', 'OST_Roofs',
                              'OST_StructuralFraming', 'OST_StructuralColumns',
                              'OST_Windows', 'OST_Doors'],

    # ISO source params
    'MAT_PROP_DICT_GUID_TXT': ['OST_Walls', 'OST_Floors', 'OST_Ceilings', 'OST_Roofs',
                                'OST_StructuralFraming', 'OST_StructuralColumns'],
    'MAT_EPD_REF_TXT':        ['OST_Walls', 'OST_Floors', 'OST_Ceilings', 'OST_Roofs',
                                'OST_StructuralFraming', 'OST_StructuralColumns',
                                'OST_Windows'],
}

# ═════════════════════════════════════════════════════════════════════════════
# All 53 categories targeted by Pass 1
# ═════════════════════════════════════════════════════════════════════════════
ALL_CATEGORIES = [
    'OST_MechanicalEquipment',
    'OST_DuctCurves',
    'OST_DuctFitting',
    'OST_DuctAccessory',
    'OST_DuctTerminal',
    'OST_FlexDuctCurves',
    'OST_PipeCurves',
    'OST_PipeFitting',
    'OST_PipeAccessory',
    'OST_FlexPipeCurves',
    'OST_Sprinklers',
    'OST_PlumbingFixtures',
    'OST_ElectricalEquipment',
    'OST_ElectricalFixtures',
    'OST_LightingFixtures',
    'OST_LightingDevices',
    'OST_Conduit',
    'OST_ConduitFitting',
    'OST_CableTray',
    'OST_CableTrayFitting',
    'OST_FireAlarmDevices',
    'OST_CommunicationDevices',
    'OST_DataDevices',
    'OST_NurseCallDevices',
    'OST_SecurityDevices',
    'OST_TelephoneDevices',
    'OST_Doors',
    'OST_Windows',
    'OST_Walls',
    'OST_Floors',
    'OST_Ceilings',
    'OST_Roofs',
    'OST_Rooms',
    'OST_Furniture',
    'OST_FurnitureSystems',
    'OST_Casework',
    'OST_Columns',
    'OST_StructuralColumns',
    'OST_StructuralFraming',
    'OST_StructuralFoundation',
    'OST_StructuralStiffener',
    'OST_Railings',
    'OST_Stairs',
    'OST_Ramps',
    'OST_GenericModel',
    'OST_SpecialityEquipment',
    'OST_MedicalEquipment',
    'OST_Parking',
    'OST_Site',
    'OST_Mass',
    'OST_Parts',
    'OST_Assemblies',
    'OST_DetailComponents',
]

# ── Parameters with known data-type defects in MR_PARAMETERS.txt ─────────────
# LoadSharedParams will still bind these but will warn in the report.
DATATYPE_DEFECTS = {
    'PLM_EQP_TAG_01_TXT': 'Declared as NUMBER — must be TEXT for Naviate formula assembly',
    'PLM_EQP_TAG_02_TXT': 'Declared as NUMBER — must be TEXT for Naviate formula assembly',
}
