# Parameter snapshot — Fresh Air Intake Louvre (HWL-00082/83/84)

**Taken:** 2026-08-20, before Ajmal changes the family category.
**Model:** `4355-BHVD-3D-60P00-BL006A.rvt` (Revit 2020.2.9)
**Family:** `TCM_FAL_T001_FreshAirIntakeLouvre` · Type `Standard` · TypeId `5742116`
**Current category:** Duct Accessories → **being changed to Air Terminals**

Captured live through the bridge with `filter-by-current-selection.cs` + a full parameter dump.
133 instance parameters and 29 type parameters per element; 64 instance parameters carried a value,
69 were empty.

---

## Why this file exists

Changing a loadable family's category in the Family Editor and reloading **drops the existing
instances** — they cannot stay as Duct Accessories once the family is an Air Terminal. Every value
below would be lost and would have to be retyped by hand across 3 elements. This is the record to
restore from.

---

## TYPE parameters (shared by all three — same type)

| Parameter | Value |
|---|---|
| Description | `Fresh Air Intake Louvre - Rectangular` |
| Type Comments | `EAL` |
| Flange Thickness | 5.000 mm (raw 0.0164042 ft) |
| Flange Width | 30.000 mm (raw 0.0984252 ft) |
| Default Elevation | 0.000 |
| Workset | `Family  : Duct Accessories : TCM_FAL_T001_FreshAirIntakeLouvre` (raw 684) — read-only, will change with the category |

Empty type parameters: Assembly Code, Classification.Uniclass.Pr.Description / .Number,
Classification.Uniclass.Ss.Description / .Number, Cost, IfcDescription, Keynote, Manufacturer,
Model, Type Mark, URL.

---

## INSTANCE parameters — identical across all three

These are the ones that must be retyped. All writable unless marked.

| Parameter | Value |
|---|---|
| Client Tag | `127-80-NSS-00001` |
| Comments | `Galvanized Steel` |
| CWA | `60P00` |
| Discipline | `HVAC` |
| Duct Height | `750` (raw 2.46062992 ft) |
| Duct Width | `750` (raw 2.46062992 ft) |
| Elevation from Level | `1325.000` mm (raw 4.34711286 ft) |
| ID_Level Name | `EL. +100.150 TOC` |
| Length | `100.000` (raw 0.32808399 ft) |
| Level | `EL. +100.150 TOC` (Id 7557726) |
| Loss Method | `8baf7d75-8b9b-46d0-b8ce-3ad1c19e6b19` |
| LV00_WBS Area Code | `P` |
| LV00_WBS Sub-Area Code | `00` |
| LV00_WBS Unit Code | `60` |
| LV01_Material Work Group | `A` |
| LV01_Object Code | `BL` |
| LV01_Sequence Number | `006` |
| LV02_Material Work Group | `H` |
| LV02_Object Code | `45` |
| LV02_Sequence Number | `003` |
| LV03_Material Work Group | `H` |
| LV03_Object Code | `HWL` |
| Main Item Tag | `60P00-BL006A` |
| MM_Discipline Code | `H` |
| MM_Main Document Definition | `Drawing` |
| MM_Main Drawing Number | `4355-BH-VD-VL224880700` |
| MM_Main Drawing Revision | `DD` |
| MM_Main Drawing Statement | `NA` |
| MM_NP System Type | `NPD` |
| Phase Created | `New Construction` (Id 86961) |
| Phase Demolished | (none) |
| Port Area Code | `127` |
| STI_C_Position | `GROUND FLOOR` |
| Sub-Discipline | `BH` |
| Use Annotation Scale | `No` (raw 0) |
| V_Airflow | `2247.0 m³/h` (raw 22.04223781) |
| V_Pressure Drop | `0.0 Pa` (raw 0) |
| WBS | `60P00` |
| Workset | `3.0_HVAC` (raw 981) |

**Read-only — these come back on their own** from geometry and the duct connection, no need to
retype: Free Size, Insulation Thickness, Lining Thickness, Overall Size, Pressure Drop, Size,
Symbol Width, System Abbreviation (`FA`), System Classification (`Supply Air`),
System Type (`TCM_M_CDN_FA - Fresh Air`, Id 1055654), Volume.

---

## INSTANCE parameters — different per element

| | HWL-00082 | HWL-00083 | HWL-00084 |
|---|---|---|---|
| Element Id | 8092750 | 8161681 | 8403332 |
| Equipment Tag | `HWL-00082` | `HWL-00083` | `HWL-00084` |
| Item Tag | `127-45-HWL-00082` | `127-45-HWL-00083` | `127-45-HWL-00084` |
| LV03_Sequence Number | `00082` | `00083` | `00084` |
| Mark | `395` | `399` | `413` |
| System Name (read-only) | `FA 4` | `FA 5` | `FA 2` |

### Placement (needed to re-place them in the same spot)

All three share Y = 22769.5 mm, Z = 101475 mm, rotation 4.18879 rad,
facing `0,0,1`, hand `0,-1,0`.

| | X (mm) | X (ft) |
|---|---|---|
| HWL-00082 | 31308.1 | 102.71674 |
| HWL-00083 | 37304.9 | 122.391406 |
| HWL-00084 | 43301.9 | 142.066601 |

### Duct connections (both connectors 750×750 mm rectangular, HVAC)

| | Connector 1 | Connector 2 |
|---|---|---|
| HWL-00082 | free | connected → Duct 8092770 (Duct System 8092776) |
| HWL-00083 | connected → Duct 8161689 (Duct System 8161698) | free |
| HWL-00084 | free | connected → Duct 8403337 (Duct System 8403385) |

Each is connected on **one side only** — already behaving as an end device, which is why moving it
to Air Terminals is the right call.

---

## The 69 empty instance parameters

Recorded so a later check can tell "was never filled" from "got wiped": Additional Tag, Area,
ClassificationCode(2), ClassificationCode(3), Contractor Identification Code, Dimensions/Sizes,
Document Revision, Edited by, Employer Identification Code, Engineering_Status, EQUI, Existing,
Family Name, Filter By, FRMW, Host Family, ID_Coordinate X, ID_Coordinate Y, ID_Coordinate Z,
ID_Description, ID_Element ID, ID_Family Name, ID_Level, ID_Room Name, ID_Room Number, ID_Slope,
ID_Type Name, Image, Insulation Type, Issue_Date, Lining Type, Loss Method Settings,
LV00_WBS Sub-Unit Code, LV04_Material Work Group, LV04_Object Code, LV04_Sequence Number,
Marka Code, MM_Installation UM, MM_ITP Project Document Number, MM_NP System Activity Description,
MM_NP System Code, MM_Owner, MM_PO Number, MM_Sub-Discipline Description, MTO Notes, MTO Type,
Object_Test, PDS Model Name, Price Code, Price Code Combined, PTY_01, PTY_02, PTY_03, PTY_04,
PTY_05, SBFR, SITE, STI_C_BoQ Zone, STI_C_Building, STI_C_Material, STI_C_WBS_02, STI_C_WBS_04,
STRU, Sub-Contractor, SUBE, Tag Reference Drawing, Type Name, Vendor, ZONE.

---

## The other louvre family, not yet snapshotted

`TCM_EAL_T100_J002_ExhaustLouvre1` — 4 instances in **Mechanical Equipment**, tags HGH-00374/375/
376/377 (three at 300×300, one at 600×450). Snapshot it the same way before changing that one too.

---

## OUTCOME — verified after the change, 2026-08-20

Ajmal changed the family category to **Air Terminals**. Read back live:

**All three converted IN PLACE — same element IDs (8092750, 8161681, 8403332).** The instances were
not deleted, contrary to the warning above. Re-placing was not needed.

| | Result |
|---|---|
| Filled parameters kept | 61 of 64 |
| Parameters that no longer exist | 8 |
| Of those, a real loss | 1 — `ID_Level Name` |
| `V_Airflow` | survived, 2247.0 m³/h |
| Built-in `Flow` (new) | 2247.0 m³/h — family drives it from V_Airflow |
| Duct connections | intact, no open ends |

**Seven of the eight are no loss** — `Free Size`, `Overall Size`, `Insulation Thickness`,
`Lining Thickness`, `Pressure Drop`, `Loss Method`, `Use Annotation Scale` are Duct-Accessory
built-ins an Air Terminal does not have. All were read-only or auto-calculated.

**The one real loss: `ID_Level Name`** (was `EL. +100.150 TOC`). A project parameter bound to exactly
one category — Duct Accessories. No air terminal in this model carries it, so it may be
Duct-Accessory-only by design; if it is wanted, add Air Terminals to that project parameter's
categories and the value can be written back.

**Side effects observed:**

- Old ducts 8092770 / 8161689 / 8403337 were **deleted** and replaced by 8437505 / 8437632 / 8437818.
- Duct systems fragmented — new single-element systems FA 3, FA 7, FA 8 (all 0 m³/h) now sit alongside
  the originals FA 2, FA 4, FA 5.
- **HWL-00082 moved 53.3 mm in +Y** (22769.5 → 22822.8). The other two did not move.
- The airflow is **still on the unconnected connector** — unchanged by the category swap, because
  connector flow assignment lives inside the family. This is why the new systems read 0 m³/h.

Technique written up as [`knowledge/live-model/family-category-change.md`](../../knowledge/live-model/family-category-change.md).
