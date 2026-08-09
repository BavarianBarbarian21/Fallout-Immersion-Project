#!/usr/bin/env python3
"""
Build the shared FIP equipment-tag compatibility layer.

The script deliberately reads the installed RimWorld 1.6, Vanilla Expanded,
and Fallout Collaboration Project definitions but writes only inside the FIP
workspace.  It is kept in Guidelines/Tools so the generated patch set and
coverage report can be reproduced after any source-mod update.

Shared item tags and ordinary faction profiles belong to FIP-H&HTools.
Royalty/Empire, Deserter, and Enclave rank profiles belong to FIP-Whitespring.
FIP-Donaustahl intentionally owns none of this compatibility layer.
"""

from __future__ import annotations

import argparse
import copy
import csv
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable


WORKSPACE = Path(__file__).resolve().parents[2]
RIMWORLD = Path(r"D:\Steam\steamapps\common\RimWorld")
VANILLA_ROOT = RIMWORLD / "Data"
LOCAL_MOD_ROOT = RIMWORLD / "Mods"
WORKSHOP_ROOT = Path(r"D:\Steam\steamapps\workshop\content\294100")

REPORT_DIR = WORKSPACE / "Guidelines"
HHTOOLS_PATCH_ROOT = WORKSPACE / "FIP-H&HTools" / "LoadFolders"
WHITESPRING_PATCH_ROOT = WORKSPACE / "FIP-Whitespring" / "LoadFolders"
PATCH_ROOT = HHTOOLS_PATCH_ROOT

WHITESPRING_ITEM_FOLDERS = {
    "OskarPotocki.VFE.Deserters": "Deserters",
    "OskarPotocki.VFE.Empire": "Empire",
}


@dataclass(frozen=True)
class ModSource:
    package_id: str
    name: str
    root: Path
    family: str

    @property
    def folder_key(self) -> str:
        key = re.sub(r"[^A-Za-z0-9]+", "_", self.package_id).strip("_")
        return key or "Unknown"


@dataclass
class Thing:
    def_name: str
    label: str
    description: str
    source: ModSource
    source_file: Path
    element_tag: str
    parent_name: str
    abstract: bool
    direct: ET.Element
    lineage: list[ET.Element] = field(default_factory=list)
    kind: str = ""
    original_weapon_tags: set[str] = field(default_factory=set)
    original_apparel_tags: set[str] = field(default_factory=set)
    tech_level: str = ""
    market_value: float | None = None
    category: str = ""
    tradeability: str = ""
    thing_categories: set[str] = field(default_factory=set)
    destroy_on_drop: bool = False
    menu_hidden: bool = False


def text_at(node: ET.Element, path: str, default: str = "") -> str:
    found = node.find(path)
    if found is None or found.text is None:
        return default
    return found.text.strip()


def clean_display_text(value: str) -> str:
    """Repair the common Windows-1252-as-UTF-8 sequences found in old mods."""
    if any(marker in value for marker in ("\u00e2", "\u00c3", "\u00c2")):
        try:
            value = value.encode("cp1252").decode("utf-8")
        except (UnicodeEncodeError, UnicodeDecodeError):
            pass
    return value


def all_li_text(node: ET.Element, path: str) -> set[str]:
    found = node.find(path)
    if found is None:
        return set()
    return {
        child.text.strip()
        for child in found
        if child.tag == "li" and child.text and child.text.strip()
    }


def parse_about(path: Path) -> tuple[str, str] | None:
    try:
        root = ET.parse(path).getroot()
    except (ET.ParseError, OSError):
        return None
    package_id = text_at(root, "packageId")
    name = text_at(root, "name")
    if not package_id:
        return None
    return package_id, name or package_id


def discover_mods() -> list[ModSource]:
    mods: dict[str, ModSource] = {}

    vanilla_ids = {
        "Core": ("Ludeon.RimWorld", "RimWorld Core"),
        "Royalty": ("Ludeon.RimWorld.Royalty", "RimWorld Royalty"),
        "Ideology": ("Ludeon.RimWorld.Ideology", "RimWorld Ideology"),
        "Biotech": ("Ludeon.RimWorld.Biotech", "RimWorld Biotech"),
        "Anomaly": ("Ludeon.RimWorld.Anomaly", "RimWorld Anomaly"),
        "Odyssey": ("Ludeon.RimWorld.Odyssey", "RimWorld Odyssey"),
    }
    for folder, (package_id, name) in vanilla_ids.items():
        root = VANILLA_ROOT / folder
        if root.exists():
            mods[package_id.lower()] = ModSource(package_id, name, root, "Vanilla")

    for about in LOCAL_MOD_ROOT.glob("*/About/About.xml"):
        parsed = parse_about(about)
        if not parsed:
            continue
        package_id, name = parsed
        if package_id.lower().startswith("rick.fcp."):
            mods[package_id.lower()] = ModSource(
                package_id, name, about.parent.parent, "FCP"
            )

    if WORKSHOP_ROOT.exists():
        for about in WORKSHOP_ROOT.glob("*/About/About.xml"):
            parsed = parse_about(about)
            if not parsed:
                continue
            package_id, name = parsed
            lower_id = package_id.lower()
            lower_name = name.lower()
            is_ve = (
                "vanillaexpanded" in lower_id
                or lower_id.startswith("oskarpotocki.vfe")
                or lower_id.startswith("oskarpotocki.vanillafactions")
                or ("vanilla" in lower_name and "expanded" in lower_name)
            )
            if is_ve:
                mods[lower_id] = ModSource(
                    package_id, name, about.parent.parent, "Vanilla Expanded"
                )

    return sorted(mods.values(), key=lambda mod: (mod.family, mod.package_id.lower()))


def def_xml_files(mod: ModSource) -> list[Path]:
    """
    Return 1.6-relevant Def XML files without pulling in old version folders.

    We intentionally include every Defs folder selected by a mod's LoadFolders
    design. Conditional DLC subfolders are safe at inventory time; generated
    patches guard every target with PatchOperationConditional.
    """
    files: set[Path] = set()
    root_defs = mod.root / "Defs"
    if root_defs.exists():
        files.update(root_defs.rglob("*.xml"))

    version_defs = mod.root / "1.6" / "Defs"
    if version_defs.exists():
        files.update(version_defs.rglob("*.xml"))

    loadfolders = mod.root / "LoadFolders"
    if loadfolders.exists():
        for defs_dir in loadfolders.glob("*/Defs"):
            files.update(defs_dir.rglob("*.xml"))

    # Core/DLC use versioned Defs in some releases, and local FCP may use
    # Common/1.6 layouts without a LoadFolders directory.
    for relative in ("Common/Defs", "Common/1.6/Defs"):
        defs_dir = mod.root / relative
        if defs_dir.exists():
            files.update(defs_dir.rglob("*.xml"))

    return sorted(files)


def parse_def_elements(mods: Iterable[ModSource]) -> tuple[list[tuple[ModSource, Path, ET.Element]], list[str]]:
    elements: list[tuple[ModSource, Path, ET.Element]] = []
    errors: list[str] = []
    for mod in mods:
        for path in def_xml_files(mod):
            try:
                root = ET.parse(path).getroot()
            except (ET.ParseError, OSError) as exc:
                errors.append(f"{path}: {exc}")
                continue
            if root.tag != "Defs":
                continue
            for child in root:
                if isinstance(child.tag, str):
                    elements.append((mod, path, child))
    return elements, errors


def is_thing_def_element(node: ET.Element) -> bool:
    lowered = node.tag.lower()
    return (
        node.tag == "ThingDef"
        or "thingdef" in lowered
        or lowered.endswith("appareldef")
    )


def parent_lineage(
    mod: ModSource,
    node: ET.Element,
    named_nodes: dict[str, list[tuple[ModSource, ET.Element]]],
) -> list[ET.Element]:
    lineage: list[ET.Element] = [node]
    seen: set[str] = set()
    parent = node.attrib.get("ParentName", "")
    while parent and parent not in seen:
        seen.add(parent)
        candidates = named_nodes.get(parent, [])
        if not candidates:
            break
        same_mod = [candidate for source, candidate in candidates if source == mod]
        core = [
            candidate
            for source, candidate in candidates
            if source.package_id == "Ludeon.RimWorld"
        ]
        parent_node = (same_mod or core or [candidate for _, candidate in candidates])[-1]
        lineage.append(parent_node)
        parent = parent_node.attrib.get("ParentName", "")
    return lineage


def inherited_text(lineage: list[ET.Element], path: str) -> str:
    for node in lineage:
        value = text_at(node, path)
        if value:
            return value
    return ""


def inherited_li(lineage: list[ET.Element], path: str) -> set[str]:
    result: set[str] = set()
    for node in reversed(lineage):
        container = node.find(path)
        if container is None:
            continue
        if container.attrib.get("Inherit", "").lower() == "false":
            result.clear()
        result.update(all_li_text(node, path))
    return result


def looks_like_weapon(thing: Thing) -> bool:
    lineage_names = " ".join(
        [
            thing.parent_name,
            *(node.attrib.get("Name", "") for node in thing.lineage),
            *(node.attrib.get("ParentName", "") for node in thing.lineage),
        ]
    ).lower()
    if inherited_text(thing.lineage, "equipmentType"):
        return True
    if inherited_li(thing.lineage, "weaponTags") and any(
        node.find("verbs") is not None for node in thing.lineage
    ):
        return True
    return bool(
        re.search(r"(weapon|makeablegun|basegun|melee.*base|personaweapon)", lineage_names)
    )


def looks_like_apparel(thing: Thing) -> bool:
    lineage_names = " ".join(
        [
            thing.parent_name,
            *(node.attrib.get("Name", "") for node in thing.lineage),
            *(node.attrib.get("ParentName", "") for node in thing.lineage),
        ]
    ).lower()
    return (
        any(node.find("apparel") is not None for node in thing.lineage)
        or "apparel" in lineage_names
        or "appareldef" in thing.element_tag.lower()
    )


def build_inventory(
    mods: list[ModSource],
) -> tuple[list[Thing], list[str]]:
    elements, errors = parse_def_elements(mods)

    # Named abstract parents are globally addressable in RimWorld XML.
    named_nodes: dict[str, list[tuple[ModSource, ET.Element]]] = defaultdict(list)
    for mod, _, node in elements:
        if not is_thing_def_element(node):
            continue
        name = node.attrib.get("Name")
        if name:
            named_nodes[name].append((mod, node))

    things: list[Thing] = []
    for mod, path, node in elements:
        if not is_thing_def_element(node):
            continue
        def_name = text_at(node, "defName")
        if not def_name:
            continue
        thing = Thing(
            def_name=def_name,
            label=clean_display_text(text_at(node, "label", def_name)),
            description=clean_display_text(text_at(node, "description")),
            source=mod,
            source_file=path,
            element_tag=node.tag,
            parent_name=node.attrib.get("ParentName", ""),
            abstract=node.attrib.get("Abstract", "").lower() == "true",
            direct=node,
        )
        thing.lineage = parent_lineage(mod, node, named_nodes)
        if thing.abstract:
            continue
        weapon = looks_like_weapon(thing)
        apparel = looks_like_apparel(thing)
        if weapon and apparel:
            thing.kind = "weapon+apparel"
        elif weapon:
            thing.kind = "weapon"
        elif apparel:
            thing.kind = "apparel"
        else:
            continue
        thing.original_weapon_tags = inherited_li(thing.lineage, "weaponTags")
        thing.original_apparel_tags = inherited_li(thing.lineage, "apparel/tags")
        thing.tech_level = inherited_text(thing.lineage, "techLevel")
        thing.category = inherited_text(thing.lineage, "category")
        thing.tradeability = inherited_text(thing.lineage, "tradeability")
        thing.thing_categories = inherited_li(thing.lineage, "thingCategories")
        thing.destroy_on_drop = (
            inherited_text(thing.lineage, "destroyOnDrop").lower() == "true"
        )
        thing.menu_hidden = (
            inherited_text(thing.lineage, "menuHidden").lower() == "true"
        )
        market_text = inherited_text(thing.lineage, "statBases/MarketValue")
        try:
            thing.market_value = float(market_text) if market_text else None
        except ValueError:
            thing.market_value = None
        things.append(thing)

    # A defName can intentionally be declared twice by broken source mods.
    # Keep both declarations in the report; generation de-duplicates per source.
    things.sort(
        key=lambda thing: (
            thing.source.family,
            thing.source.package_id.lower(),
            thing.kind,
            thing.def_name.lower(),
        )
    )
    return things, errors


@dataclass
class Assignment:
    tags: set[str] = field(default_factory=set)
    factions: set[str] = field(default_factory=set)
    status: str = "unassigned_candidate"
    reason: str = ""

    def assign(self, faction: str, *tags: str) -> None:
        self.factions.add(faction)
        self.tags.update(tags)
        self.status = "assigned"


FCP_BALLISTIC_T0 = {
    "FCP_Gun_Pipe_Pistol",
    "FCP_Gun_Pipe_Revolver",
    "FCP_Gun_Pipe_Rifle",
    "FCP_Gun_Pipe_BoltAction",
    "FCP_Gun_Pipe_Shotgun",
    "FCP_Gun_Pipe_Sniper",
    "FCP_Gun_SawnOff_Shotgun",
    "FCP_Gun_Single_Shotgun",
}

FCP_BALLISTIC_T1 = {
    "FCP_Gun_10mm_Pistol",
    "FCP_Gun_Silenced_10mm_Pistol",
    "FCP_Gun_Chinese_Pistol",
    "FCP_Gun_9mm_Pistol",
    "FCP_Gun_Silenced_22_Pistol",
    "FCP_Gun_Colt_6520_Pistol",
    "FCP_Gun_45_Auto_Pistol",
    "FCP_Gun_357_Revolver",
    "FCP_Gun_44Magnum_Revolver",
    "FCP_Gun_Hunting_Revolver",
    "FCP_Gun_Police_Pistol",
    "FCP_Gun_BB_Gun",
    "FCP_Gun_Brush_Gun",
    "FCP_Lever_Action_Rifle",
    "FCP_Gun_Cowboy_Repeater",
    "FCP_Gun_Trail_Carbine",
    "FCP_Gun_32_Hunting_Rifle",
    "FCP_Gun_Varmint_Rifle",
    "FCP_Gun_223_Rangemaster",
    "FCP_Gun_223_Rifle",
    "FCP_Gun_Caravan_Shotgun",
    "FCP_Gun_Hunting_Shotgun",
    "FCP_Gun_LeverAction_Shotgun",
    "FCP_Gun_10mm_SMG",
    "FCP_Gun_9mm_SMG",
    "FCP_Gun_Silenced_22_SMG",
    "FCP_Gun_45_Auto_SMG",
}

FCP_BALLISTIC_T2 = {
    "FCP_Gun_556_Pistol",
    "FCP_Gun_127mm_Pistol",
    "FCP_Gun_10mm_Pistol_MkII",
    "FCP_Gun_14mm_Pistol",
    "FCP_Gun_223_Pistol",
    "FCP_Gun_Deagle",
    "FCP_Gun_R91_Rifle",
    "FCP_Gun_Chinese_Assault_Rifle",
    "FCP_Gun_Marksman_Carbine",
    "FCP_Gun_Assault_Carbine",
    "FCP_Gun_AK_112_AR",
    "FCP_Gun_Automatic_Rifle",
    "FCP_Gun_Combat_Rifle_Standard",
    "FCP_Gun_Battle_Rifle",
    "FCP_Gun_Combat_Shotgun",
    "FCP_Gun_Riot_Shotgun",
    "FCP_Gun_Citykiller",
    "FCP_Gun_Combat_Shotgun_MkII",
    "FCP_Gun_Jackhammer",
    "FCP_Gun_HK_10_SMG",
    "FCP_Gun_127mm_SMG",
    "FCP_Gun_DKS_501_Sniper_Rifle",
    "FCP_Gun_Scoped_Hunting_Rifle",
    "FCP_Gun_Anti_Material_Rifle",
    "FCP_Gun_DKS_500_Sniper_Rifle",
}

FCP_BALLISTIC_T3 = {
    "FCP_Gun_Power_Armor_Assault_Rifle",
    "FCP_Gun_G11_AR",
    "FCP_Gun_Infiltrator",
    "FCP_Gun_Minigun",
    "FCP_Gun_Light_Machine_Gun",
    "FCP_Gun_ChunkyBoi",
    "FCP_Gun_MkI_Avenger",
    "FCP_Gun_Vindicator",
    "FCP_Gun_50_Cal_MG",
    "FCP_Gun_K900_Cyberdog_Gun",
    "FCP_Gun_Rockwell_CZ54_Minigun",
    "FCP_Gun_Shoulder_Mounted_MG",
}

FCP_UNIQUE_WEAPONS = {
    "FCP_Gun_Lucky",
    "FCP_Gun_Maria",
    "FCP_Gun_AbileneKidLEBBGun",
    "FCP_Gun_AllAmerican",
    "FCP_Gun_BigBoomer",
    "FCP_Gun_Deliverer",
    "FCP_Gun_HHToolsNailgun",
    "FCP_Gun_LilDevil",
    "FCP_Gun_LincolnsRepeater",
    "FCP_Gun_MkIIAvenger",
    "FCP_Gun_Vances9mmSMG",
    "FCP_Gun_GobiCampaignScoutRifle",
    "FCP_Gun_MedicineStick",
    "FCP_Gun_Bozar",
    "FCP_Gun_DinnerBell",
    "FCP_Gun_Ratslayer",
    "FCP_Gun_Wanda",
    "FCP_Gun_OlPainless",
    "FCP_Gun_VictoryRifle",
    "FCP_Gun_ReservistsRifle",
    "FCP_Gun_FIDO",
    "FCP_Gun_Light_Shining_In_Darkness",
    "FCP_Gun_Paciena",
    "FCP_Gun_Sleepy_Time",
    "FCP_Gun_Sturdy_Caravan_Shotgun",
    "FCP_Gun_Survivalist_Rifle",
    "FCP_Gun_This_Machine",
    "FCP_Gun_Weathered_10mm_Pistol",
    "FCP_Gun_Heavy_Incinerator",
    "FCP_Gun_M72_A2_Gauss_Rifle",
    "FCP_Gun_AER14_Prototype",
    "FCP_Gun_MF_Hyperbreeder_Alpha",
    "FCP_Gun_Pew_Pew",
    "FCP_Gun_Sprtel_Wood_9700_Unique",
    "FCP_Gun_Wattz_2000_Laser_Rifle",
    "FCP_Gun_Q35_Matter_Modulator",
    "FCP_Gun_Ranger_Ricks_Big_Iron_Revolver",
}

FCP_SIMPLE_LASERS = {
    "FCP_Gun_Laser_Pistol",
    "FCP_Gun_Laser_Rifle",
    "FCP_Gun_Laser_Carbine",
    "FCP_Gun_Recharger_Rifle",
    "FCP_Gun_Recharger_Pistol",
    "FCP_Gun_Wattz_1000_Laser_Pistol",
}

FCP_ENERGY_T2 = FCP_SIMPLE_LASERS | {
    "FCP_Gun_Flamer_MkI",
    "FCP_Gun_Flamer_MkII",
}

FCP_ENERGY_T3 = {
    "FCP_Gun_Tri_Beam_Laser_Rifle",
    "FCP_Gun_Gatling_Laser",
    "FCP_Gun_Laser_RCW",
    "FCP_Gun_Gatling_Laser_MkI",
}

FCP_ENERGY_T4 = {
    "FCP_Gun_Plasma_Pistol",
    "FCP_Gun_Plasma_Rifle",
    "FCP_Gun_Multiplas_Rifle",
    "FCP_Gun_Plasma_Caster",
    "FCP_Gun_Plasma_Defender",
    "FCP_Gun_Plasma_Caster_MkI",
    "FCP_Gun_Glock_86_Plasma_Pistol",
    "FCP_Gun_P_94_Plasma_Rifle",
    "FCP_Gun_Plasma_Machine_Gun",
    "FCP_Gun_PPK12_Gauss_Pistol",
    "FCP_Gun_M72_Gauss_Rifle",
    "FCP_Gun_Gauss_Minigun",
    "FCP_Gun_Gauss_Sniper_Rifle",
    "FCP_Gun_YK32_Pulse_Pistol",
    "FCP_Gun_YK42B_Pulse_Rifle",
}

FCP_MELEE_T0 = {
    "FCP_Melee_9_Iron",
    "FCP_Melee_Bat",
    "FCP_Melee_Bat_Aluminum",
    "FCP_Melee_Bat_Spiked",
    "FCP_Melee_Board_Spiked",
    "FCP_Melee_Meat_Cleaver",
    "FCP_Melee_Pool_Cue",
    "FCP_Melee_Rebar_Club",
    "FCP_Melee_Rolling_Pin",
    "FCP_Melee_Scrap_Axe",
    "FCP_Melee_Scrap_Machete",
    "FCP_Melee_Scrap_Pipe",
    "FCP_Melee_Sledgehammer",
}

FCP_MELEE_T2 = {
    "FCP_Melee_Bat_Saw",
    "FCP_Melee_Bumper_Sword",
    "FCP_Melee_Chainsaw",
    "FCP_Melee_Mr_Handy_Buzz_Blade",
    "FCP_Melee_Ripper",
    "FCP_Melee_Shishkebab",
}


def is_internal_equipment(thing: Thing) -> bool:
    return (
        thing.destroy_on_drop
        and thing.tradeability.lower() == "none"
    ) or thing.menu_hidden


def is_equipment_utility(thing: Thing) -> bool:
    if thing.kind.startswith("weapon"):
        real_categories = {"WeaponsMelee", "WeaponsRanged", "Grenades"}
        if thing.thing_categories & real_categories:
            return False
        if thing.def_name in {
            "ElephantTusk",
            "ThrumboHorn",
            "AlphaThrumboHorn",
            "MastodonTusk",
        }:
            return False
        return not thing.original_weapon_tags
    return bool(
        re.search(
            r"(OrbitalTargeter|TornadoGenerator|Psychic.*Lance|BiomutationLance|"
            r"Declassifier|CerebrexNode)",
            thing.def_name,
            re.IGNORECASE,
        )
    )


def infer_weapon_role(thing: Thing) -> str:
    haystack = " ".join(
        [thing.def_name, thing.label, *sorted(thing.original_weapon_tags)]
    ).lower()
    role_patterns = [
        ("Pulse", r"pulse"),
        ("Gauss", r"gauss"),
        ("Plasma", r"plasma|multiplas"),
        ("Laser", r"laser|ultracite|charge|beam|fletcher|fletchling"),
        ("Flame", r"flame|flamer|inciner|molotov|firebomb|shishkebab"),
        ("Heavy", r"minigun|machine gun|lmg|cannon|rocket|bozar|avenger|heavy"),
        ("Sniper", r"sniper|anti.?materi|scoped|marksman"),
        ("Shotgun", r"shotgun|scattergun|jackhammer"),
        ("SMG", r"\bsmg\b|submachine|machine pistol|rcw"),
        ("Sidearm", r"pistol|revolver|sequoia|big iron"),
        ("Assault", r"assault|automatic rifle|combat rifle|battle rifle"),
        ("Rifle", r"rifle|carbine|repeater|musket|arquebus|bow|arbalest|sling"),
        (
            "Melee",
            r"melee|sword|mace|axe|spear|knife|club|hammer|staff|"
            r"glove|fist|bat|shiv|cleaver|ripper|chainsaw|pike|flail|stake",
        ),
    ]
    for role, pattern in role_patterns:
        if re.search(pattern, haystack):
            return role
    return "Other"


def add_weapon_tier_and_role(
    assignment: Assignment,
    thing: Thing,
    tier: int,
) -> None:
    assignment.tags.add(f"FIPW_T{tier}_{['Improvised', 'Wasteland', 'Service', 'Advanced', 'Elite'][tier]}")
    assignment.tags.add(f"FIPW_Role_{infer_weapon_role(thing)}")


def assign_low_weapon_pools(assignment: Assignment, *, tribal: bool = True) -> None:
    assignment.assign("generic wastelanders", "FIPW_Pool_Wastelander")
    assignment.assign("settlements", "FIPW_Pool_Settler")
    assignment.assign("raiders", "FIPW_Pool_Raider")
    assignment.assign("ghoul settlements", "FIPW_Pool_Ghoul")
    assignment.assign("caravan companies", "FIPW_Pool_Caravan")
    assignment.assign("escaped slaves and beggars", "FIPW_Pool_SlaveLowTech")
    if tribal:
        assignment.assign("regional tribes", "FIPW_Pool_Tribal")
        assignment.assign("Numen", "FIPW_Pool_Numen")
        assignment.assign("S'Lanters", "FIPW_Pool_Slanter")
        assignment.assign("Mothman and Wendigo cults", "FIPW_Pool_CultPrimitive")


def assign_service_weapon_pools(assignment: Assignment) -> None:
    assignment.assign("settlement guards", "FIPW_Pool_SettlerGuard")
    assignment.assign("rough settlements", "FIPW_Pool_RoughSettler")
    assignment.assign("controlled and crazed vaults", "FIPW_Pool_VaultHighTech")
    assignment.assign("Whitespring Enclave", "FIPW_Pool_Enclave")
    assignment.assign("Enclave remnants", "FIPW_Pool_EnclaveRemnant")
    assignment.assign("Circle of Steel", "FIPW_Pool_CircleOfSteel")
    assignment.assign("US Army-equipped forces", "FIPW_Pool_USArmy")
    assignment.assign("super mutant forces", "FIPW_Pool_SuperMutant")


def classify_fcp_weapon(thing: Thing, assignment: Assignment) -> bool:
    package = thing.source.package_id
    name = thing.def_name

    if name in FCP_UNIQUE_WEAPONS:
        assignment.tags.update({"FIPW_Unique", f"FIPW_Role_{infer_weapon_role(thing)}"})
        assignment.status = "unique_or_quest_only"
        assignment.reason = "Named FCP weapon; deliberately excluded from random faction pools."
        return True

    if package == "Rick.FCP.BallisticWeapons":
        if name in FCP_BALLISTIC_T0:
            add_weapon_tier_and_role(assignment, thing, 0)
            assign_low_weapon_pools(assignment)
            assignment.assign("Nuevo Texico raiders", "FIPW_Pool_NuevoTexicoRaider")
            assignment.assign("Arizona raiders", "FIPW_Pool_ArizonaRaider")
        elif name in FCP_BALLISTIC_T1:
            add_weapon_tier_and_role(assignment, thing, 1)
            assign_low_weapon_pools(assignment, tribal=False)
            assignment.assign("Great Khans", "FIPW_Pool_GreatKhan")
            assignment.assign("California raiders", "FIPW_Pool_CaliforniaRaider")
            assignment.assign("Arizona raiders", "FIPW_Pool_ArizonaRaider")
            assignment.assign("Nuevo Texico raiders", "FIPW_Pool_NuevoTexicoRaider")
            if re.search(r"(Cowboy|Trail|Lever|Brush|357|44Magnum|Hunting)", name):
                assignment.assign("Nova Arizona frontier", "FIPW_Pool_ArizonaCowboy")
                assignment.assign("Nuevo Texico frontier", "FIPW_Pool_NuevoTexicoFrontier")
        elif name in FCP_BALLISTIC_T2:
            add_weapon_tier_and_role(assignment, thing, 2)
            assign_service_weapon_pools(assignment)
            assignment.assign("NCR", "FIPW_Pool_NCRNative")
            assignment.assign("California settlements", "FIPW_Pool_NCRRegional")
            assignment.assign("Caesar's Legion", "FIPW_Pool_LegionNative")
            assignment.assign("Arizona settlements", "FIPW_Pool_LegionRegional")
            assignment.assign("Cascadia Forged raiders", "FIPW_Pool_CascadiaRaider")
        elif name in FCP_BALLISTIC_T3:
            add_weapon_tier_and_role(assignment, thing, 3)
            assign_service_weapon_pools(assignment)
            if name == "FCP_Gun_K900_Cyberdog_Gun":
                assignment.factions = {"cyberdogs"}
                assignment.tags.difference_update(
                    {tag for tag in assignment.tags if tag.startswith("FIPW_Pool_")}
                )
                assignment.tags.add("FIPW_Pool_CyberdogOnly")
                assignment.reason = "Species-restricted K9000 weapon."
        else:
            assignment.reason = "FCP ballistic definition was not present in the verified tier catalogue."
        return True

    if package == "Rick.FCP.EnergyWeapons":
        if name in FCP_ENERGY_T2:
            add_weapon_tier_and_role(assignment, thing, 2)
            assign_service_weapon_pools(assignment)
            if name in FCP_SIMPLE_LASERS:
                assignment.assign(
                    "Two California, Cascadia, and Nuevo Texico tribes",
                    "FIPW_Pool_SimpleLaserTribal",
                )
            else:
                assignment.assign("Cascadia Forged raiders", "FIPW_Pool_CascadiaRaider")
                assignment.assign("Caesar's Legion specialists", "FIPW_Pool_LegionElite")
        elif name in FCP_ENERGY_T3:
            add_weapon_tier_and_role(assignment, thing, 3)
            assign_service_weapon_pools(assignment)
        elif name in FCP_ENERGY_T4:
            add_weapon_tier_and_role(assignment, thing, 4)
            assignment.assign("controlled and crazed vaults", "FIPW_Pool_VaultHighTech")
            assignment.assign("Whitespring Enclave", "FIPW_Pool_Enclave")
            assignment.assign("Enclave remnants", "FIPW_Pool_EnclaveRemnant")
            assignment.assign("Circle of Steel", "FIPW_Pool_CircleOfSteel")
        else:
            assignment.reason = "FCP energy weapon was not present in the verified non-unique catalogue."
        return True

    if package == "Rick.FCP.MeleeWeapons":
        if name in FCP_MELEE_T0:
            add_weapon_tier_and_role(assignment, thing, 0)
            assign_low_weapon_pools(assignment)
            assignment.assign("Great Khans", "FIPW_Pool_GreatKhan")
            assignment.assign("all regional raiders", "FIPW_Pool_RegionalRaider")
            assignment.assign("super mutant forces", "FIPW_Pool_SuperMutant")
        elif name in FCP_MELEE_T2:
            add_weapon_tier_and_role(assignment, thing, 2)
            assignment.assign("settlement guards", "FIPW_Pool_SettlerGuard")
            assignment.assign("raiders", "FIPW_Pool_Raider")
            assignment.assign("Great Khans", "FIPW_Pool_GreatKhan")
            assignment.assign("Caesar's Legion", "FIPW_Pool_LegionNative")
            assignment.assign("Arizona raiders", "FIPW_Pool_ArizonaRaider")
            assignment.assign("Cascadia Forged raiders", "FIPW_Pool_CascadiaRaider")
            assignment.assign("super mutant forces", "FIPW_Pool_SuperMutant")
        elif name == "FCP_Melee_Super_Sledge":
            add_weapon_tier_and_role(assignment, thing, 3)
            assignment.assign("controlled and crazed vaults", "FIPW_Pool_VaultHighTech")
            assignment.assign("Circle of Steel", "FIPW_Pool_CircleOfSteel")
            assignment.assign("Whitespring Enclave", "FIPW_Pool_Enclave")
            assignment.assign("super mutant forces", "FIPW_Pool_SuperMutant")
        return True

    if package == "Rick.FCP.UnarmedWeapons":
        if name == "FCP_Unarmed_Boxing_Glove":
            add_weapon_tier_and_role(assignment, thing, 0)
            assignment.assign("raiders and arena families", "FIPW_Pool_Raider")
        elif name == "FCP_Unarmed_Industrial_Fist":
            add_weapon_tier_and_role(assignment, thing, 2)
            assignment.assign("raiders and arena families", "FIPW_Pool_Raider")
            assignment.assign("super mutant forces", "FIPW_Pool_SuperMutant")
        else:
            tier = 4 if name in {"FCP_Unarmed_Displacer_Glove", "FCP_Unarmed_Zap_Glove"} else 3
            add_weapon_tier_and_role(assignment, thing, tier)
            assignment.assign("controlled and crazed vaults", "FIPW_Pool_VaultHighTech")
            assignment.assign("Circle of Steel", "FIPW_Pool_CircleOfSteel")
            assignment.assign("Whitespring Enclave", "FIPW_Pool_Enclave")
            assignment.assign("super mutant forces", "FIPW_Pool_SuperMutant")
        return True

    if package == "Rick.FCP.BOS":
        add_weapon_tier_and_role(
            assignment,
            thing,
            4 if "Plasma" in thing.label or "Ultracite" in thing.label else 3,
        )
        assignment.assign("FCP Brotherhood of Steel", "FIPW_Pool_BrotherhoodNative")
        assignment.assign("Circle of Steel", "FIPW_Pool_CircleOfSteel")
        return True

    if package == "Rick.FCP.Enclave":
        add_weapon_tier_and_role(assignment, thing, 4)
        assignment.assign("FCP Enclave", "FIPW_Pool_EnclaveNative")
        assignment.assign("Whitespring Enclave", "FIPW_Pool_Enclave")
        assignment.assign("Enclave remnants", "FIPW_Pool_EnclaveRemnant")
        return True

    if package == "Rick.FCP.NCR":
        if name == "FCP_Gun_Ranger_Ricks_Big_Iron_Revolver":
            assignment.tags.update({"FIPW_Unique", "FIPW_Role_Sidearm"})
            assignment.status = "unique_or_quest_only"
            assignment.reason = "Named Ranger Rick weapon."
        else:
            tier = 1 if name == "FCP_Gun_NCR_Hunting_Rifle" else 2
            add_weapon_tier_and_role(assignment, thing, tier)
            assignment.assign("FCP NCR", "FIPW_Pool_NCRNative")
            assignment.assign("California settlements", "FIPW_Pool_NCRRegional")
            if name in {"FCP_Gun_Service_Rifle", "FCP_Gun_NCR_Hunting_Rifle"}:
                assignment.assign(
                    "Two California tribes",
                    "FIPW_Pool_NCRSurplus",
                    "FIPW_Pool_SimpleFirearmTribal",
                )
            else:
                assignment.tags.add("FIPW_Pool_NCRRegionalElite")
        return True

    return False


def classify_generic_weapon(thing: Thing, assignment: Assignment) -> None:
    name = thing.def_name
    package = thing.source.package_id
    haystack = f"{name} {thing.label} {' '.join(thing.original_weapon_tags)}".lower()

    if (
        name.endswith("_Unique")
        or "bladelink" in haystack
        or "persona" in haystack
        or package == "VanillaExpanded.VPersonaWeaponsE"
    ):
        assignment.tags.update({"FIPW_Unique", f"FIPW_Role_{infer_weapon_role(thing)}"})
        assignment.status = "unique_or_quest_only"
        assignment.reason = "Unique/persona weapon; not suitable for random faction generation."
        return

    if name == "VFEM2_MeleeWeapon_Standard":
        assignment.reason = "Heraldic standard has no Fallout faction loadout role."
        return

    primitive = (
        thing.tech_level == "Neolithic"
        or any(tag.startswith("Neolithic") for tag in thing.original_weapon_tags)
        or package in {"OskarPotocki.VFE.Tribals", "VanillaExpanded.VWETB"}
    )
    medieval_melee = (
        thing.tech_level == "Medieval"
        and (
            any("Melee" in tag for tag in thing.original_weapon_tags)
            or bool(
                re.search(
                    r"axe|blade|club|flail|gladius|halberd|hammer|knife|"
                    r"longsword|mace|pike|spear|sword",
                    haystack,
                )
            )
        )
        and "eltex" not in haystack
        and "plasma" not in haystack
    )
    ultra = thing.tech_level in {"Spacer", "Ultra", "Archotech"} or bool(
        re.search(r"charge|crypto|beam|plasmasword|mono|zeus|mass lance", haystack)
    )
    gun = (
        "gun" in thing.original_weapon_tags
        or "simplegun" in thing.original_weapon_tags
        or bool(re.search(r"gun_|_gun_|rifle|pistol|revolver|shotgun|musket|arquebus|handcannon", haystack))
    )

    if package == "VanillaExpanded.VPsycastsE" or "eltex" in haystack:
        add_weapon_tier_and_role(assignment, thing, 2)
        assignment.assign("Mothman and Wendigo cults", "FIPW_Pool_CultPrimitive")
        return

    if package == "vanillaquestsexpanded.cryptoforge":
        add_weapon_tier_and_role(assignment, thing, 4)
        assignment.assign("controlled and crazed vaults", "FIPW_Pool_VaultHighTech")
        assignment.assign("Circle of Steel", "FIPW_Pool_CircleOfSteel")
        assignment.assign("Whitespring Enclave", "FIPW_Pool_Enclave")
        return

    if package == "vanillaquestsexpanded.deadlife":
        add_weapon_tier_and_role(assignment, thing, 2)
        assignment.assign("controlled and crazed vaults", "FIPW_Pool_VaultHighTech")
        assignment.assign("Whitespring Enclave", "FIPW_Pool_Enclave")
        assignment.assign("US Army-equipped forces", "FIPW_Pool_USArmy")
        return

    if package in {
        "OskarPotocki.VFE.Deserters",
        "OskarPotocki.VFE.Empire",
        "vanillaexpanded.gravship",
    }:
        add_weapon_tier_and_role(assignment, thing, 4 if ultra else 3)
        assignment.assign("controlled and crazed vaults", "FIPW_Pool_VaultHighTech")
        assignment.assign("Circle of Steel", "FIPW_Pool_CircleOfSteel")
        assignment.assign("Whitespring Enclave", "FIPW_Pool_Enclave")
        return

    if primitive or medieval_melee:
        add_weapon_tier_and_role(assignment, thing, 0)
        assign_low_weapon_pools(assignment)
        assignment.assign("all regional raiders", "FIPW_Pool_RegionalRaider")
        if package == "Ludeon.RimWorld" and name in {
            "MeleeWeapon_BreachAxe",
            "MeleeWeapon_Club",
            "MeleeWeapon_Knife",
            "MeleeWeapon_Spear",
        }:
            assignment.assign(
                "super mutant improvised weapons",
                "FIPW_Pool_SuperMutant",
            )
        if package == "Ludeon.RimWorld" and name == "MeleeWeapon_Knife":
            assignment.assign(
                "Enclave emergency sidearms",
                "FIPW_Pool_EnclaveFallback",
            )
        return

    if package == "OskarPotocki.VFE.Medieval2" and gun:
        # Black-powder weapons are Fallout-compatible frontier pieces, unlike
        # the mod's heraldic/plate equipment.
        add_weapon_tier_and_role(assignment, thing, 1)
        assign_low_weapon_pools(assignment, tribal=False)
        assignment.assign("Nuevo Texico frontier", "FIPW_Pool_NuevoTexicoFrontier")
        assignment.assign("Nova Arizona frontier", "FIPW_Pool_ArizonaCowboy")
        return

    if ultra:
        add_weapon_tier_and_role(assignment, thing, 4)
        assignment.assign("controlled and crazed vaults", "FIPW_Pool_VaultHighTech")
        assignment.assign("Whitespring Enclave", "FIPW_Pool_Enclave")
        assignment.assign("Circle of Steel", "FIPW_Pool_CircleOfSteel")
        return

    if gun or "grenade" in haystack:
        advanced = bool(
            re.search(
                r"assault|chain shotgun|heavy smg|lmg|minigun|sniper|hellcat|"
                r"incinerator|charge|toxblade",
                haystack,
            )
        )
        tier = 2 if advanced else 1
        add_weapon_tier_and_role(assignment, thing, tier)
        assign_low_weapon_pools(assignment, tribal=False)
        if tier == 2:
            assign_service_weapon_pools(assignment)
        if "revolver" in haystack or "hunting rifle" in haystack:
            assignment.assign("Nova Arizona frontier", "FIPW_Pool_ArizonaCowboy")
            assignment.assign("Nuevo Texico frontier", "FIPW_Pool_NuevoTexicoFrontier")
        return

    if name in {"ElephantTusk", "ThrumboHorn", "AlphaThrumboHorn", "MastodonTusk"}:
        add_weapon_tier_and_role(assignment, thing, 0)
        assignment.assign("regional tribes", "FIPW_Pool_Tribal")
        assignment.assign("Numen", "FIPW_Pool_Numen")
        assignment.assign("S'Lanters", "FIPW_Pool_Slanter")
        return

    if name == "VQEA_MeleeWeapon_Scalpel":
        add_weapon_tier_and_role(assignment, thing, 0)
        assign_low_weapon_pools(assignment, tribal=False)
        assignment.assign("settlement medics", "FIPW_Pool_Settler")
        return

    assignment.reason = "No safe random-faction weapon role was identified."


def classify_weapon(thing: Thing) -> Assignment:
    assignment = Assignment()
    if is_internal_equipment(thing):
        assignment.status = "internal_not_trade_item"
        assignment.reason = "Internal turret, artillery, mechanoid, or destroy-on-drop weapon."
        return assignment
    if is_equipment_utility(thing):
        assignment.status = "utility_not_pawn_gear"
        assignment.reason = "Equippable resource, drink, or utility object; not a PawnKind weapon."
        return assignment
    if classify_fcp_weapon(thing, assignment):
        if assignment.status == "unassigned_candidate" and assignment.factions:
            assignment.status = "assigned"
        return assignment
    classify_generic_weapon(thing, assignment)
    return assignment


def classify_fcp_apparel(thing: Thing, assignment: Assignment) -> bool:
    package = thing.source.package_id
    name = thing.def_name
    tags = thing.original_apparel_tags

    if package == "Rick.FCP.BOS":
        assignment.assign("FCP Brotherhood of Steel", "FIPA_Pool_BrotherhoodNative")
        assignment.assign("Circle of Steel", "FIPA_Pool_CircleOfSteel")
        if "Power_Armor" in name:
            assignment.tags.add("FIPA_Class_PowerArmor")
        return True

    if package == "Rick.FCP.Enclave":
        assignment.assign("FCP Enclave", "FIPA_Pool_EnclaveNative")
        if "Power_Armor" not in name:
            assignment.assign("Enclave remnants", "FIPA_Pool_EnclaveRemnant")
        if "Scientist" in name:
            assignment.assign("Whitespring scientists", "FIPA_Pool_EnclaveScientist")
        elif "Officer" in name:
            assignment.assign("Whitespring officers", "FIPA_Pool_EnclaveOfficer")
        elif "Trooper" in name:
            assignment.assign("Whitespring troopers", "FIPA_Pool_EnclaveTrooper")
        elif "Combat_Armor" in name:
            assignment.assign(
                "Whitespring armored troops", "FIPA_Pool_EnclaveCombatArmor"
            )
        elif "Power_Armor" in name:
            assignment.assign(
                "Whitespring power-armored soldiers",
                "FIPA_Pool_EnclavePowerArmor",
                "FIPA_Class_PowerArmor",
            )
            if "Hellfire" in name:
                assignment.tags.add("FIPA_Pool_EnclavePowerArmor_Hellfire")
            elif "Tesla" in name:
                assignment.tags.add("FIPA_Pool_EnclavePowerArmor_Tesla")
            elif "X02" in name:
                assignment.tags.add("FIPA_Pool_EnclavePowerArmor_X02")
            else:
                assignment.tags.add("FIPA_Pool_EnclavePowerArmor_APA")
        return True

    if package == "Rick.FCP.GreatKhans":
        assignment.assign("FCP Great Khans", "FIPA_Pool_GreatKhanNative")
        if "Suit_Armor" in name:
            assignment.assign(
                "California raider leaders", "FIPA_Pool_CaliforniaRaiderLeader"
            )
        else:
            assignment.assign("California raiders", "FIPA_Pool_CaliforniaRaider")
        return True

    if package == "Rick.FCP.Legion":
        assignment.assign("FCP Caesar's Legion", "FIPA_Pool_LegionNative")
        elite_terms = (
            "Caesar_",
            "Centurion",
            "Legate",
            "Praetorian",
            "Vexillarius",
        )
        if not any(term in name for term in elite_terms):
            assignment.assign("Nova Arizona factions", "FIPA_Pool_LegionRegional")
        else:
            assignment.tags.add("FIPA_Class_FactionElite")
        return True

    if package == "Rick.FCP.NCR":
        assignment.assign("FCP NCR", "FIPA_Pool_NCRNative")
        forbidden = (
            "Ambassador",
            "General",
            "President",
            "Ranger_Ricks",
        )
        if any(term in name for term in forbidden):
            assignment.tags.add("FIPA_Class_FactionUnique")
        elif "Veteran_Ranger" in name or "Salvaged_Power" in name:
            assignment.assign(
                "California elite fighters", "FIPA_Pool_NCRRegionalElite"
            )
        elif "NCRCF" not in name and "Powder_Ganger" not in name:
            assignment.assign("California settlements", "FIPA_Pool_NCRRegional")
        return True

    if package == "Rick.FCP.PowerArmor":
        assignment.assign(
            "controlled and crazed vaults",
            "FIPA_Pool_VaultHighTech",
            "FIPA_Class_PowerArmor",
        )
        return True

    if package == "Rick.FCP.Raiders":
        assignment.assign("raiders", "FIPA_Pool_Raider")
        assignment.tags.add("FIPA_Class_PowerArmor" if "Power_Armor" in name else "FIPA_Class_Raider")
        if re.search(r"Fiend|Badlands|Painspike|Psycho", name):
            assignment.assign("Arizona Fiend raiders", "FIPA_Pool_ArizonaRaider")
        elif re.search(r"Blastmaster|Arclight|Armor_Heavy", name):
            assignment.assign("Cascadia Forged raiders", "FIPA_Pool_CascadiaRaider")
        elif re.search(r"Wastehound|Skull|Sack|Tire", name):
            assignment.assign(
                "Nuevo Texico Viper/Scorpion/Jackal raiders",
                "FIPA_Pool_NuevoTexicoRaider",
            )
        else:
            assignment.tags.add("FIPA_Pool_RegionalRaider")
        return True

    if package == "Rick.FCP.Wastelanders":
        if "FCP_Pre_War_Costume" in tags or name == "FCP_Apparel_Benny_Pre_War_Fancy_Suit_Clean":
            assignment.status = "unique_or_quest_only"
            assignment.tags.add("FIPA_Special_Costume")
            assignment.reason = "Collectible or named pre-war costume; excluded from random pawns."
            return True
        if "FCP_Pipboy" in tags:
            assignment.assign("controlled and crazed vaults", "FIPA_Pool_VaultHighTech")
            return True
        if any("Slave" in tag or "Slaves" in tag for tag in tags):
            assignment.assign("slavers and forced laborers", "FIPA_Pool_Slave")
            return True
        if any("Ghoul" in tag or "Bright" in tag for tag in tags):
            assignment.assign("ghoul settlements", "FIPA_Pool_Ghoul")
            assignment.assign("wasteland cults", "FIPA_Pool_Cult")
            return True
        if "FCP_Radiation_Apparel" in tags:
            assignment.assign("controlled and crazed vaults", "FIPA_Pool_VaultHighTech")
            assignment.assign("Whitespring scientists", "FIPA_Pool_EnclaveScientist")
            return True
        if "Gas_Ranger" in name:
            assignment.assign("wasteland rangers", "FIPA_Pool_SettlerGuard")
            assignment.assign("ghoul settlements", "FIPA_Pool_Ghoul")
            return True
        if "Sheriff" in name:
            assignment.assign("settlement leaders and guards", "FIPA_Pool_Settler")
            assignment.assign("settlement leaders and guards", "FIPA_Pool_SettlerGuard")
            assignment.assign("Nova Arizona lawmen", "FIPA_Pool_ArizonaCowboy")
            return True
        if any("Combat_Armor" in tag or tag == "FCP_Combat_Armors" for tag in tags):
            assignment.assign("settlement guards", "FIPA_Pool_SettlerGuard")
            assignment.assign("raider fighters", "FIPA_Pool_Raider")
            assignment.assign("controlled and crazed vaults", "FIPA_Pool_VaultHighTech")
            assignment.assign("US Army-equipped forces", "FIPA_Pool_USArmy")
            return True
        if "Army_" in name or "Military_Fatigues" in name:
            assignment.assign("US Army-equipped forces", "FIPA_Pool_USArmy")
            assignment.assign("Whitespring non-power-armored soldiers", "FIPA_Pool_EnclaveTrooper")
        if "FCP_Raider_Armor" in tags or "FCP_Raider_Apparel" in tags:
            assignment.assign("raiders", "FIPA_Pool_Raider")
        if "FCP_Wastelander_Apparel" in tags:
            assignment.assign("generic wastelanders", "FIPA_Pool_WastelanderGeneric")
            assignment.assign("settlements", "FIPA_Pool_Settler")
            assignment.assign("raiders", "FIPA_Pool_Raider")
        if any("Prostitute" in tag for tag in tags):
            assignment.assign("saloon families", "FIPA_Pool_Saloon")
        if assignment.factions:
            return True
        assignment.reason = "Specialized FCP wastelander apparel without a safe random faction role."
        return True

    if package == "Rick.FCP.Core.Tools":
        if "Pipboy" in name:
            assignment.assign("controlled and crazed vaults", "FIPA_Pool_VaultHighTech")
        else:
            assignment.status = "unique_or_quest_only"
            assignment.tags.add("FIPA_Special_MysteriousStranger")
            assignment.reason = "Mysterious Stranger outfit is a named special set."
        return True

    return False


def classify_generic_apparel(thing: Thing, assignment: Assignment) -> None:
    name = thing.def_name
    label = thing.label.lower()
    package = thing.source.package_id
    tags = thing.original_apparel_tags
    haystack = f"{name} {label} {' '.join(tags)}".lower()

    if package == "OskarPotocki.VFE.Empire":
        if "Deserter" in name:
            assignment.assign("Circle of Steel", "FIPA_Pool_CircleOfSteel")
        elif "Techfriar" in name:
            assignment.assign("Whitespring scientists", "FIPA_Pool_EnclaveScientist")
        else:
            assignment.assign("Whitespring Enclave fallback", "FIPA_Pool_EnclaveFallback")
        return

    if name == "Apparel_TortureCrown":
        assignment.assign("raiders", "FIPA_Pool_Raider")
        assignment.assign("Wendigo cults", "FIPA_Pool_Cult")
        return

    if package == "Ludeon.RimWorld.Royalty" and name in {
        "Apparel_Coronet",
        "Apparel_Corset",
        "Apparel_Crown",
        "Apparel_CrownStellic",
        "Apparel_RobeRoyal",
    }:
        assignment.assign(
            "Whitespring Enclave fallback",
            "FIPA_Pool_EnclaveFallback",
        )
        if name != "Apparel_Corset":
            assignment.assign("Whitespring officers", "FIPA_Pool_EnclaveOfficer")
        return

    medieval_exclusions = bool(
        re.search(
            r"plate.?armor|plate.?helmet|king.?s|dame|jester|chaperon|tabard|"
            r"heraldic|plague.?mask|stellic.?robe|coronet|crown|royal.?robe|"
            r"corset|heater.?shield|padded.?armor|padded.?helmet",
            haystack,
        )
    )
    if medieval_exclusions:
        assignment.reason = (
            "Medieval/royal visual language has no retained Fallout faction; "
            "candidate for trader removal."
        )
        return

    if package == "OskarPotocki.VFE.Medieval2":
        if re.search(r"leather.?armor|leather.?helmet|round.?shield|torch.?belt", haystack):
            assignment.assign("regional tribes", "FIPA_Pool_Tribal")
            assignment.assign("raiders", "FIPA_Pool_Raider")
            if "leather" in haystack:
                assignment.assign("Numen", "FIPA_Pool_Numen")
            return
        if name == "VFEM2_Apparel_Cap":
            assignment.assign("generic wastelanders", "FIPA_Pool_WastelanderGeneric")
            assignment.assign("settlements", "FIPA_Pool_Settler")
            assignment.assign("raiders", "FIPA_Pool_Raider")
            return
        assignment.reason = "Remaining VFE Medieval apparel has no Fallout faction role."
        return

    if package == "OskarPotocki.VFE.Tribals":
        assignment.assign("regional tribes", "FIPA_Pool_Tribal")
        assignment.assign("Numen", "FIPA_Pool_Numen")
        assignment.assign("S'Lanters", "FIPA_Pool_Slanter")
        return

    if package == "OskarPotocki.VFE.Deserters":
        assignment.assign("Circle of Steel", "FIPA_Pool_CircleOfSteel")
        return

    if package == "vanillaquestsexpanded.cryptoforge":
        assignment.assign("controlled and crazed vaults", "FIPA_Pool_VaultHighTech")
        assignment.assign("Circle of Steel", "FIPA_Pool_CircleOfSteel")
        return

    if package == "vanillaquestsexpanded.deadlife":
        assignment.assign("US Army-equipped forces", "FIPA_Pool_USArmy")
        assignment.assign("Whitespring Enclave", "FIPA_Pool_EnclaveTrooper")
        assignment.assign("controlled and crazed vaults", "FIPA_Pool_VaultHighTech")
        return

    if package in {
        "vanillaquestsexpanded.ancients",
        "vanillaquestsexpanded.generator",
    }:
        assignment.assign("controlled and crazed vaults", "FIPA_Pool_VaultHighTech")
        return

    if package == "VanillaExpanded.VAnomalyEInsanity":
        assignment.assign("slavers and hostile cults", "FIPA_Pool_Slave")
        assignment.assign("Wendigo cults", "FIPA_Pool_Cult")
        return

    if package == "VanillaExpanded.VIEHAR":
        if re.search(r"deer.?skull|horse.?mask|headbag|spiked", haystack):
            assignment.assign("raiders", "FIPA_Pool_Raider")
            assignment.assign("wasteland cults", "FIPA_Pool_Cult")
        elif "bishop" in haystack or "beads" in haystack:
            assignment.assign("Mothman cults", "FIPA_Pool_Cult")
        elif "commisar" in haystack or "militarycap" in haystack:
            assignment.assign("US Army-equipped forces", "FIPA_Pool_USArmy")
            assignment.assign("settlement guards", "FIPA_Pool_SettlerGuard")
        elif "rags" in haystack:
            assignment.assign("raiders", "FIPA_Pool_Raider")
            assignment.assign("escaped slaves and beggars", "FIPA_Pool_Slave")
        else:
            assignment.assign("generic wastelanders", "FIPA_Pool_WastelanderGeneric")
            assignment.assign("settlements", "FIPA_Pool_Settler")
            assignment.assign("raiders", "FIPA_Pool_Raider")
        return

    if package == "VanillaExpanded.VAEAccessories":
        if "Quiver" in name or "BattleBanner" in name:
            assignment.assign("regional tribes", "FIPA_Pool_Tribal")
            assignment.assign("Numen", "FIPA_Pool_Numen")
        elif "Ressurector" in name:
            assignment.assign("controlled and crazed vaults", "FIPA_Pool_VaultHighTech")
        else:
            assignment.assign("generic wastelanders", "FIPA_Pool_WastelanderGeneric")
            assignment.assign("settlements", "FIPA_Pool_Settler")
            assignment.assign("raiders", "FIPA_Pool_Raider")
        return

    if package == "VanillaExpanded.VPsycastsE":
        assignment.assign("Mothman and Wendigo cults", "FIPA_Pool_Cult")
        return

    if package == "vanillaexpanded.gravship":
        assignment.assign("controlled and crazed vaults", "FIPA_Pool_VaultHighTech")
        assignment.assign("Whitespring Enclave", "FIPA_Pool_EnclaveTrooper")
        return

    if any(tag in tags for tag in {"Horaxian", "HoraxianCeremonial"}):
        assignment.assign("Mothman and Wendigo cults", "FIPA_Pool_Cult")
        return

    if any(tag.startswith("Psychic") for tag in tags) or "eltex" in haystack:
        assignment.assign("Mothman and Wendigo cults", "FIPA_Pool_Cult")
        return

    if re.search(
        r"armor.?helmet.?(recon|marine|cataphract|locust)|"
        r"armor.?(recon|marine|cataphract|locust)|power.?armor|vacsuit|"
        r"mechlord|mechcommander|airwire|array.?headset|integrator.?headset|"
        r"bandwidth.?pack|control.?pack",
        haystack,
    ):
        assignment.assign("controlled and crazed vaults", "FIPA_Pool_VaultHighTech")
        return

    if re.search(r"labcoat|lab.?coat", haystack):
        assignment.assign("controlled and crazed vaults", "FIPA_Pool_VaultHighTech")
        assignment.assign("Whitespring scientists", "FIPA_Pool_EnclaveScientist")
        return

    if re.search(r"gas.?mask|bandolier|tox.?pack|deadlife|turret.?pack|disruptor", haystack):
        assignment.assign("raiders", "FIPA_Pool_Raider")
        assignment.assign("settlement guards", "FIPA_Pool_SettlerGuard")
        return

    if name == "Apparel_Shadecone":
        assignment.assign("generic wastelanders", "FIPA_Pool_WastelanderGeneric")
        assignment.assign("settlements", "FIPA_Pool_Settler")
        assignment.assign("regional tribes", "FIPA_Pool_Tribal")
        return

    if name == "Apparel_PackHunter":
        assignment.assign("generic wastelanders", "FIPA_Pool_WastelanderGeneric")
        assignment.assign("settlement hunters", "FIPA_Pool_Settler")
        assignment.assign("raiders", "FIPA_Pool_Raider")
        return

    if name == "Apparel_Beret":
        assignment.assign("US Army-equipped forces", "FIPA_Pool_USArmy")
        assignment.assign("Whitespring officers", "FIPA_Pool_EnclaveOfficer")
        assignment.assign("settlement guards", "FIPA_Pool_SettlerGuard")
        return

    if any(tag == "Neolithic" for tag in tags) or re.search(
        r"tribal|war.?mask|war.?veil|headdress|broadwrap|headwrap|visage", haystack
    ):
        assignment.assign("regional tribes", "FIPA_Pool_Tribal")
        assignment.assign("Numen", "FIPA_Pool_Numen")
        assignment.assign("S'Lanters", "FIPA_Pool_Slanter")
        assignment.assign("raiders", "FIPA_Pool_Raider")
        return

    if re.search(r"slave|blindfold|bodystrap|body.?strap|torture", haystack):
        assignment.assign("slavers and forced laborers", "FIPA_Pool_Slave")
        assignment.assign("raiders", "FIPA_Pool_Raider")
        return

    if any(
        tag in tags
        for tag in {
            "IndustrialMilitaryBasic",
            "IndustrialMilitaryAdvanced",
            "BeltDefensePop",
            "BeltDefense",
            "Gunlink",
            "PackJump",
        }
    ):
        assignment.assign("settlement guards", "FIPA_Pool_SettlerGuard")
        assignment.assign("raider fighters", "FIPA_Pool_Raider")
        assignment.assign("controlled and crazed vaults", "FIPA_Pool_VaultHighTech")
        assignment.assign("US Army-equipped forces", "FIPA_Pool_USArmy")
        assignment.assign(
            "Whitespring Enclave fallback",
            "FIPA_Pool_EnclaveFallback",
        )
        return

    if any(tag in tags for tag in {"IndustrialBasic", "IndustrialAdvanced", "Western", "Outlander"}):
        assignment.assign("generic wastelanders", "FIPA_Pool_WastelanderGeneric")
        assignment.assign("settlements", "FIPA_Pool_Settler")
        assignment.assign("raiders", "FIPA_Pool_Raider")
        return

    if re.search(
        r"shirt|pants|jacket|parka|hat|cap|hood|robe|duster|dress|vest|"
        r"mask|sash|burka|wrap|tuque|sombrero|bandana|uniform|coat|gown",
        haystack,
    ):
        assignment.assign("generic wastelanders", "FIPA_Pool_WastelanderGeneric")
        assignment.assign("settlements", "FIPA_Pool_Settler")
        assignment.assign("raiders", "FIPA_Pool_Raider")
        return

    assignment.reason = "No retained faction can use this apparel without breaking its visual theme."


def classify_apparel(thing: Thing) -> Assignment:
    assignment = Assignment()
    if is_internal_equipment(thing):
        assignment.status = "internal_not_trade_item"
        assignment.reason = "Internal/destroy-on-drop apparel helper."
        return assignment
    if is_equipment_utility(thing):
        assignment.status = "utility_not_pawn_gear"
        assignment.reason = "Targeter, lance, or one-use utility; not random clothing."
        return assignment
    if classify_fcp_apparel(thing, assignment):
        return assignment
    classify_generic_apparel(thing, assignment)
    return assignment


def classify_all(things: list[Thing]) -> dict[tuple[str, str, str], Assignment]:
    assignments: dict[tuple[str, str, str], Assignment] = {}
    for thing in things:
        key = (thing.source.package_id, thing.def_name, thing.kind)
        if thing.kind == "weapon":
            assignments[key] = classify_weapon(thing)
        elif thing.kind == "apparel":
            assignments[key] = classify_apparel(thing)
        else:
            # VFE Medieval shields are ThingDefs that are both equippable and
            # wearable. Apparel governs PawnKind selection for these items.
            assignments[key] = classify_apparel(thing)
    return assignments


def write_inventory(things: list[Thing], errors: list[str]) -> Path:
    output = REPORT_DIR / "FIP_Equipment_Source_Inventory.tsv"
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, delimiter="\t", lineterminator="\n")
        writer.writerow(
            [
                "family",
                "packageId",
                "mod",
                "kind",
                "defName",
                "label",
                "techLevel",
                "marketValue",
                "category",
                "tradeability",
                "thingCategories",
                "destroyOnDrop",
                "menuHidden",
                "weaponTags",
                "apparelTags",
                "sourceFile",
            ]
        )
        for thing in things:
            writer.writerow(
                [
                    thing.source.family,
                    thing.source.package_id,
                    thing.source.name,
                    thing.kind,
                    thing.def_name,
                    thing.label,
                    thing.tech_level,
                    "" if thing.market_value is None else thing.market_value,
                    thing.category,
                    thing.tradeability,
                    ",".join(sorted(thing.thing_categories)),
                    str(thing.destroy_on_drop).lower(),
                    str(thing.menu_hidden).lower(),
                    ",".join(sorted(thing.original_weapon_tags)),
                    ",".join(sorted(thing.original_apparel_tags)),
                    str(thing.source_file),
                ]
            )

    error_output = REPORT_DIR / "FIP_Equipment_Inventory_Parse_Errors.txt"
    error_output.write_text(
        "\n".join(errors) + ("\n" if errors else "No XML parse errors.\n"),
        encoding="utf-8",
    )
    return output


def write_assignment_reports(
    things: list[Thing],
    assignments: dict[tuple[str, str, str], Assignment],
) -> tuple[Path, Path]:
    complete = REPORT_DIR / "FIP_Equipment_Faction_Assignments.tsv"
    with complete.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, delimiter="\t", lineterminator="\n")
        writer.writerow(
            [
                "family",
                "packageId",
                "mod",
                "kind",
                "defName",
                "label",
                "status",
                "factions",
                "addedTags",
                "existingWeaponTags",
                "existingApparelTags",
                "reason",
                "sourceFile",
            ]
        )
        for thing in things:
            assignment = assignments[
                (thing.source.package_id, thing.def_name, thing.kind)
            ]
            writer.writerow(
                [
                    thing.source.family,
                    thing.source.package_id,
                    thing.source.name,
                    thing.kind,
                    thing.def_name,
                    thing.label,
                    assignment.status,
                    "; ".join(sorted(assignment.factions)),
                    ",".join(sorted(assignment.tags)),
                    ",".join(sorted(thing.original_weapon_tags)),
                    ",".join(sorted(thing.original_apparel_tags)),
                    assignment.reason,
                    str(thing.source_file),
                ]
            )

    candidates = [
        (thing, assignments[(thing.source.package_id, thing.def_name, thing.kind)])
        for thing in things
        if assignments[(thing.source.package_id, thing.def_name, thing.kind)].status
        == "unassigned_candidate"
    ]
    unassigned = REPORT_DIR / "FIP_Unassigned_Trader_Removal_Candidates.txt"
    lines = [
        "FIP UNASSIGNED EQUIPMENT / TRADER-REMOVAL CANDIDATES",
        "=====================================================",
        "",
        "Generated from installed RimWorld 1.6, Vanilla Expanded, and FCP Defs.",
        "These items received no random-faction pool. They have NOT been removed.",
        "Internal turret guns, one-use utility items, drinks/resources, and named",
        "Unique weapons are deliberately listed in the complete TSV instead and",
        "are not mixed into this trader-removal candidate list.",
        "",
        f"Total candidates: {len(candidates)}",
        "",
    ]
    grouped: dict[tuple[str, str], list[tuple[Thing, Assignment]]] = defaultdict(list)
    for thing, assignment in candidates:
        grouped[(thing.source.package_id, thing.source.name)].append((thing, assignment))
    for (package_id, mod_name), group in sorted(grouped.items()):
        lines.extend(
            [
                f"{mod_name}",
                f"packageId: {package_id}",
                "-" * max(20, len(mod_name)),
            ]
        )
        for thing, assignment in sorted(group, key=lambda item: item[0].def_name):
            lines.append(
                f"- {thing.def_name} | {thing.label} | {thing.kind}"
                + (f" | {assignment.reason}" if assignment.reason else "")
            )
        lines.append("")
    unassigned.write_text("\n".join(lines).rstrip() + "\n", encoding="utf-8")
    return complete, unassigned


def xml_escape(value: str) -> str:
    return (
        value.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
    )


def weapon_tag_operation(def_name: str, tags: list[str]) -> list[str]:
    xpath = f'/Defs/*[defName="{xml_escape(def_name)}"]'
    values = [f"          <li>{xml_escape(tag)}</li>" for tag in tags]
    return [
        '  <Operation Class="PatchOperationConditional">',
        f"    <xpath>{xpath}/weaponTags</xpath>",
        '    <match Class="PatchOperationAdd">',
        f"      <xpath>{xpath}/weaponTags</xpath>",
        "      <value>",
        *values,
        "      </value>",
        "    </match>",
        '    <nomatch Class="PatchOperationAdd">',
        f"      <xpath>{xpath}</xpath>",
        "      <value>",
        "        <weaponTags>",
        *[line.replace("          ", "          ", 1) for line in values],
        "        </weaponTags>",
        "      </value>",
        "    </nomatch>",
        "  </Operation>",
    ]


def apparel_tag_operation(def_name: str, tags: list[str]) -> list[str]:
    xpath = f'/Defs/*[defName="{xml_escape(def_name)}"]'
    values = [f"            <li>{xml_escape(tag)}</li>" for tag in tags]
    return [
        '  <Operation Class="PatchOperationConditional">',
        f"    <xpath>{xpath}/apparel/tags</xpath>",
        '    <match Class="PatchOperationAdd">',
        f"      <xpath>{xpath}/apparel/tags</xpath>",
        "      <value>",
        *[line.replace("            ", "        ", 1) for line in values],
        "      </value>",
        "    </match>",
        '    <nomatch Class="PatchOperationConditional">',
        f"      <xpath>{xpath}/apparel</xpath>",
        '      <match Class="PatchOperationAdd">',
        f"        <xpath>{xpath}/apparel</xpath>",
        "        <value>",
        "          <tags>",
        *values,
        "          </tags>",
        "        </value>",
        "      </match>",
        '      <nomatch Class="PatchOperationAdd">',
        f"        <xpath>{xpath}</xpath>",
        "        <value>",
        "          <apparel>",
        "            <tags>",
        *[line.replace("            ", "              ", 1) for line in values],
        "            </tags>",
        "          </apparel>",
        "        </value>",
        "      </nomatch>",
        "    </nomatch>",
        "  </Operation>",
    ]


def generate_equipment_patches(
    mods: list[ModSource],
    things: list[Thing],
    assignments: dict[tuple[str, str, str], Assignment],
) -> tuple[list[ModSource], list[Path]]:
    mod_lookup = {mod.package_id: mod for mod in mods}
    by_mod: dict[str, dict[tuple[str, str], set[str]]] = defaultdict(
        lambda: defaultdict(set)
    )
    for thing in things:
        assignment = assignments[
            (thing.source.package_id, thing.def_name, thing.kind)
        ]
        if not assignment.tags:
            continue
        if thing.kind == "weapon":
            tags = {tag for tag in assignment.tags if tag.startswith("FIPW_")}
            target_kind = "weapon"
        else:
            tags = {tag for tag in assignment.tags if tag.startswith("FIPA_")}
            target_kind = "apparel"
        if tags:
            by_mod[thing.source.package_id][(thing.def_name, target_kind)].update(tags)

    generated_files: list[Path] = []
    relevant_mods: list[ModSource] = []
    for package_id, entries in sorted(by_mod.items()):
        mod = mod_lookup[package_id]
        relevant_mods.append(mod)
        whitespring_folder = WHITESPRING_ITEM_FOLDERS.get(package_id)
        if whitespring_folder:
            folder = WHITESPRING_PATCH_ROOT / whitespring_folder
            patch_owner = "FIP-Whitespring"
            filename_owner = "Whitespring"
        else:
            folder_name = (
                "Equipment_Core"
                if package_id == "Ludeon.RimWorld"
                else f"Equipment_{mod.folder_key}"
            )
            folder = HHTOOLS_PATCH_ROOT / folder_name
            patch_owner = "FIP-H&HTools"
            filename_owner = "HHTools"
        patch_dir = folder / "Patches" / patch_owner / "EquipmentTags"
        patch_dir.mkdir(parents=True, exist_ok=True)
        output = patch_dir / f"{filename_owner}_{mod.folder_key}_EquipmentTags.xml"
        lines = [
            '<?xml version="1.0" encoding="utf-8"?>',
            "<Patch>",
            f"  <!-- Generated additive tags for {xml_escape(mod.name)} ({xml_escape(package_id)}). -->",
            "  <!-- Original source tags are preserved. Missing targets are skipped safely. -->",
            "",
        ]
        for (def_name, target_kind), tags in sorted(entries.items()):
            if target_kind == "weapon":
                lines.extend(weapon_tag_operation(def_name, sorted(tags)))
            else:
                lines.extend(apparel_tag_operation(def_name, sorted(tags)))
            lines.append("")
        lines.append("</Patch>")
        output.write_text("\n".join(lines) + "\n", encoding="utf-8")
        generated_files.append(output)

    return relevant_mods, generated_files


def add_replace_list_operation(
    patch: ET.Element,
    xpath: str,
    list_name: str,
    tags: Iterable[str],
) -> None:
    operation = ET.SubElement(patch, "Operation", {"Class": "PatchOperationReplace"})
    ET.SubElement(operation, "xpath").text = xpath
    value = ET.SubElement(operation, "value")
    replacement = ET.SubElement(value, list_name, {"Inherit": "False"})
    for tag in tags:
        ET.SubElement(replacement, "li").text = tag


def add_set_list_operation(
    patch: ET.Element,
    def_name: str,
    list_name: str,
    tags: Iterable[str],
) -> None:
    """Replace a PawnKind list if present, otherwise add an explicit override."""
    parent_xpath = f'/Defs/PawnKindDef[defName="{def_name}"]'
    conditional = ET.SubElement(
        patch, "Operation", {"Class": "PatchOperationConditional"}
    )
    ET.SubElement(conditional, "xpath").text = f"{parent_xpath}/{list_name}"

    match = ET.SubElement(
        conditional, "match", {"Class": "PatchOperationReplace"}
    )
    ET.SubElement(match, "xpath").text = f"{parent_xpath}/{list_name}"
    match_value = ET.SubElement(match, "value")
    match_list = ET.SubElement(match_value, list_name, {"Inherit": "False"})
    for tag in tags:
        ET.SubElement(match_list, "li").text = tag

    nomatch = ET.SubElement(
        conditional, "nomatch", {"Class": "PatchOperationAdd"}
    )
    ET.SubElement(nomatch, "xpath").text = parent_xpath
    nomatch_value = ET.SubElement(nomatch, "value")
    nomatch_list = ET.SubElement(nomatch_value, list_name, {"Inherit": "False"})
    for tag in tags:
        ET.SubElement(nomatch_list, "li").text = tag


def set_pawnkind_profile(
    patch: ET.Element,
    def_names: Iterable[str],
    *,
    weapons: Iterable[str] | None = None,
    apparel: Iterable[str] | None = None,
    clear_apparel_requirements: bool = False,
) -> None:
    for def_name in def_names:
        if weapons is not None:
            add_set_list_operation(patch, def_name, "weaponTags", weapons)
        if apparel is not None:
            add_set_list_operation(patch, def_name, "apparelTags", apparel)
        if clear_apparel_requirements:
            add_set_list_operation(patch, def_name, "apparelRequired", [])
            add_set_list_operation(
                patch, def_name, "specificApparelRequirements", []
            )


def pawnkind_xpath(def_names: Iterable[str], child: str) -> str:
    names = list(def_names)
    predicate = " or ".join(f'defName="{name}"' for name in names)
    return f"/Defs/PawnKindDef[{predicate}]/{child}"


def write_xml_document(path: Path, root: ET.Element) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    ET.indent(root, space="  ")
    path.write_text(
        '<?xml version="1.0" encoding="utf-8"?>\n'
        + ET.tostring(root, encoding="unicode", short_empty_elements=True)
        + "\n",
        encoding="utf-8",
    )


def generate_hhtools_profile_patches() -> list[Path]:
    """
    Connect the shared equipment pools to H&H Tools PawnKinds.

    California and Nova Arizona already have dedicated PawnKinds, so their
    existing lists can be replaced in-place. Cascadia, Nuevo Texico, and the
    mutant factions share generic parent PawnKinds; those factions receive
    lightweight child PawnKinds and private pawnGroupMakers so regional gear
    cannot leak into an unrelated faction.
    """
    source = (
        WORKSPACE
        / "FIP-H&HTools"
        / "LoadFolders"
        / "Base"
        / "Defs"
        / "FIP-H&HTools"
        / "Factions"
        / "HHTools_Faction_BaseDef.xml"
    )
    pawnkind_source = (
        WORKSPACE
        / "FIP-H&HTools"
        / "LoadFolders"
        / "Base"
        / "Defs"
        / "FIP-H&HTools"
        / "Pawns"
        / "HHTools_PawnKindDef.xml"
    )
    if not source.exists() or not pawnkind_source.exists():
        return []

    folder = HHTOOLS_PATCH_ROOT / "Equipment_FIP_HHTools"
    generated: list[Path] = []

    settlement_roles = [
        "Civilian",
        "Fighter",
        "Trader",
        "Leader",
        "Child",
        "Miner",
        "Hunter",
        "Logger",
        "Farmer",
    ]
    raider_roles = [
        "Civilian",
        "Fighter",
        "Trader",
        "Leader",
        "Miner",
        "Hunter",
        "Logger",
        "Farmer",
    ]
    tribal_roles = [
        "Civilian",
        "Fighter",
        "Trader",
        "Leader",
        "Child",
        "Miner",
        "Hunter",
        "Logger",
        "Farmer",
    ]

    base_kinds = {
        "settlement": {
            role: f"HHTools_Settlement_{role}" for role in settlement_roles
        },
        "raider": {role: f"HHTools_Raider_{role}" for role in raider_roles},
        "tribal": {role: f"HHTools_Tribal_{role}" for role in tribal_roles},
    }
    pawnkind_root = ET.parse(pawnkind_source).getroot()
    pawnkind_templates = {
        node.attrib["Name"]: node
        for node in pawnkind_root.findall("./PawnKindDef")
        if node.attrib.get("Name")
    }
    missing_templates = sorted(
        {
            parent_name
            for family_kinds in base_kinds.values()
            for parent_name in family_kinds.values()
            if parent_name not in pawnkind_templates
        }
    )
    if missing_templates:
        raise RuntimeError(
            "Missing H&H Tools PawnKind templates: "
            + ", ".join(missing_templates)
        )
    guard_roles = {"Fighter", "Trader", "Leader"}

    # (key, concrete faction, family, normal weapon pools, normal apparel pools,
    #  extra pools for guards/leaders)
    profiles = [
        (
            "CascadiaCivil",
            "HHTools_Cascadia_Civil",
            "settlement",
            ["FIPW_Pool_Wastelander", "FIPW_Pool_Settler"],
            ["FIPA_Pool_WastelanderGeneric", "FIPA_Pool_Settler"],
            ["FIPW_Pool_SettlerGuard"],
            ["FIPA_Pool_SettlerGuard"],
        ),
        (
            "CascadiaRough",
            "HHTools_Cascadia_Rough",
            "settlement",
            [
                "FIPW_Pool_Wastelander",
                "FIPW_Pool_Settler",
                "FIPW_Pool_RoughSettler",
            ],
            ["FIPA_Pool_WastelanderGeneric", "FIPA_Pool_Settler"],
            ["FIPW_Pool_SettlerGuard"],
            ["FIPA_Pool_SettlerGuard"],
        ),
        (
            "CascadiaRaider",
            "HHTools_Cascadia_Raider",
            "raider",
            [
                "FIPW_Pool_Wastelander",
                "FIPW_Pool_Raider",
                "FIPW_Pool_CascadiaRaider",
            ],
            [
                "FIPA_Pool_WastelanderGeneric",
                "FIPA_Pool_Raider",
                "FIPA_Pool_CascadiaRaider",
            ],
            [],
            [],
        ),
        (
            "CascadiaTribal",
            "HHTools_Cascadia_Tribal",
            "tribal",
            ["FIPW_Pool_Tribal", "FIPW_Pool_SimpleLaserTribal"],
            ["FIPA_Pool_Tribal"],
            [],
            [],
        ),
        (
            "NuevoTexicoCivil",
            "HHTools_NuevoTexico_Civil",
            "settlement",
            [
                "FIPW_Pool_Wastelander",
                "FIPW_Pool_Settler",
                "FIPW_Pool_NuevoTexicoFrontier",
            ],
            ["FIPA_Pool_WastelanderGeneric", "FIPA_Pool_Settler"],
            ["FIPW_Pool_SettlerGuard"],
            ["FIPA_Pool_SettlerGuard"],
        ),
        (
            "NuevoTexicoRough",
            "HHTools_NuevoTexico_Rough",
            "settlement",
            [
                "FIPW_Pool_Wastelander",
                "FIPW_Pool_Settler",
                "FIPW_Pool_RoughSettler",
                "FIPW_Pool_NuevoTexicoFrontier",
            ],
            ["FIPA_Pool_WastelanderGeneric", "FIPA_Pool_Settler"],
            ["FIPW_Pool_SettlerGuard"],
            ["FIPA_Pool_SettlerGuard"],
        ),
        (
            "NuevoTexicoRaider",
            "HHTools_NuevoTexico_Raider",
            "raider",
            [
                "FIPW_Pool_Wastelander",
                "FIPW_Pool_Raider",
                "FIPW_Pool_NuevoTexicoRaider",
                "FIPW_Pool_NuevoTexicoFrontier",
            ],
            [
                "FIPA_Pool_WastelanderGeneric",
                "FIPA_Pool_Raider",
                "FIPA_Pool_NuevoTexicoRaider",
            ],
            [],
            [],
        ),
        (
            "NuevoTexicoTribal",
            "HHTools_NuevoTexico_Tribal",
            "tribal",
            [
                "FIPW_Pool_Tribal",
                "FIPW_Pool_SimpleLaserTribal",
                "FIPW_Pool_NuevoTexicoFrontier",
            ],
            ["FIPA_Pool_Tribal"],
            [],
            [],
        ),
        (
            "GhoulSettlement",
            "HHTools_Mutant_Ghoul",
            "settlement",
            ["FIPW_Pool_Ghoul", "FIPW_Pool_Settler"],
            [
                "FIPA_Pool_Ghoul",
                "FIPA_Pool_WastelanderGeneric",
                "FIPA_Pool_Settler",
            ],
            ["FIPW_Pool_SettlerGuard"],
            ["FIPA_Pool_SettlerGuard"],
        ),
        (
            "SuperMutantStronghold",
            "HHTools_Mutant_SuperMutant",
            "settlement",
            ["FIPW_Pool_SuperMutant"],
            ["FIPA_Pool_Raider"],
            [],
            [],
        ),
        (
            "SuperMutantArmy",
            "HHTools_Mutant_Army",
            "raider",
            ["FIPW_Pool_SuperMutant", "FIPW_Pool_Raider"],
            ["FIPA_Pool_Raider"],
            [],
            [],
        ),
        (
            "SlanterTribal",
            "HHTools_Mutant_Slanter",
            "tribal",
            ["FIPW_Pool_Slanter"],
            ["FIPA_Pool_Slanter", "FIPA_Pool_Tribal"],
            [],
            [],
        ),
        (
            "NumenTribal",
            "HHTools_Mutant_Numen",
            "tribal",
            ["FIPW_Pool_Numen"],
            ["FIPA_Pool_Numen", "FIPA_Pool_Tribal"],
            [],
            [],
        ),
        (
            "CritterTribal",
            "HHTools_Mutant_Critter",
            "tribal",
            ["FIPW_Pool_Numen", "FIPW_Pool_Slanter"],
            ["FIPA_Pool_Numen", "FIPA_Pool_Slanter", "FIPA_Pool_Tribal"],
            [],
            [],
        ),
    ]

    defs = ET.Element("Defs")
    profile_kind_maps: dict[str, dict[str, str]] = {}
    for (
        key,
        _faction,
        family,
        weapon_pools,
        apparel_pools,
        guard_weapon_pools,
        guard_apparel_pools,
    ) in profiles:
        kind_map: dict[str, str] = {}
        for role, parent_name in base_kinds[family].items():
            def_name = f"FIPD_{key}_{role}"
            kind_map[parent_name] = def_name
            # Cross-mod ParentName inheritance from H&H Tools is not reliable
            # during RimWorld's XML inheritance pass. Clone the H&H Tools
            # template while retaining its vanilla ParentName instead.
            pawn = copy.deepcopy(pawnkind_templates[parent_name])
            pawn.attrib.pop("Name", None)
            def_name_node = pawn.find("defName")
            if def_name_node is None:
                def_name_node = ET.Element("defName")
                pawn.insert(0, def_name_node)
            def_name_node.text = def_name
            for existing in list(pawn):
                if existing.tag in {"weaponTags", "apparelTags"}:
                    pawn.remove(existing)
            weapons = ET.SubElement(pawn, "weaponTags", {"Inherit": "False"})
            if role != "Child":
                for tag in weapon_pools + (
                    guard_weapon_pools if role in guard_roles else []
                ):
                    ET.SubElement(weapons, "li").text = tag
            apparel = ET.SubElement(pawn, "apparelTags", {"Inherit": "False"})
            for tag in apparel_pools + (
                guard_apparel_pools if role in guard_roles else []
            ):
                ET.SubElement(apparel, "li").text = tag
            defs.append(pawn)
        profile_kind_maps[key] = kind_map

    defs_path = (
        folder
        / "Defs"
        / "FIP-H&HTools"
        / "EquipmentProfiles"
        / "HHTools_Equipment_PawnKinds.xml"
    )
    write_xml_document(defs_path, defs)
    generated.append(defs_path)

    source_root = ET.parse(source).getroot()
    parent_by_family = {
        "settlement": "HHTools_Settler_Democratic",
        "raider": "HHTools_Raider",
        "tribal": "FalloutTribalClan",
    }
    inherited_groups: dict[str, ET.Element] = {}
    for family, parent_name in parent_by_family.items():
        parent = source_root.find(f"./FactionDef[@Name='{parent_name}']")
        if parent is None:
            raise RuntimeError(f"Missing H&H Tools faction parent: {parent_name}")
        groups = parent.find("pawnGroupMakers")
        if groups is None:
            raise RuntimeError(f"Missing pawnGroupMakers on {parent_name}")
        inherited_groups[family] = groups

    faction_patch = ET.Element("Patch")
    for key, faction, family, *_rest in profiles:
        operation = ET.SubElement(
            faction_patch, "Operation", {"Class": "PatchOperationAdd"}
        )
        ET.SubElement(operation, "xpath").text = (
            f'/Defs/FactionDef[defName="{faction}"]'
        )
        value = ET.SubElement(operation, "value")
        groups = copy.deepcopy(inherited_groups[family])
        groups.set("Inherit", "False")
        kind_map = profile_kind_maps[key]
        for descendant in groups.iter():
            if descendant.tag in kind_map:
                descendant.tag = kind_map[descendant.tag]
        value.append(groups)

    faction_patch_path = (
        folder
        / "Patches"
        / "FIP-H&HTools"
        / "EquipmentProfiles"
        / "HHTools_Equipment_FactionProfiles.xml"
    )
    write_xml_document(faction_patch_path, faction_patch)
    generated.append(faction_patch_path)

    # Existing generic, California, and Nova Arizona PawnKinds can be made
    # strict in place. This also strips the old broad FCP_NCR/FCP_Legion tags
    # from tribal children and civilians.
    patch = ET.Element("Patch")
    base_sets = [
        (
            [f"HHTools_Settlement_{role}" for role in ["Civilian", "Miner", "Hunter", "Logger", "Farmer"]],
            "weaponTags",
            ["FIPW_Pool_Wastelander", "FIPW_Pool_Settler"],
        ),
        (
            [f"HHTools_Settlement_{role}" for role in ["Fighter", "Trader", "Leader"]],
            "weaponTags",
            ["FIPW_Pool_Wastelander", "FIPW_Pool_Settler", "FIPW_Pool_SettlerGuard"],
        ),
        (
            [f"HHTools_Settlement_{role}" for role in settlement_roles],
            "apparelTags",
            ["FIPA_Pool_WastelanderGeneric", "FIPA_Pool_Settler"],
        ),
        (
            [f"HHTools_Raider_{role}" for role in raider_roles],
            "weaponTags",
            ["FIPW_Pool_Wastelander", "FIPW_Pool_Raider"],
        ),
        (
            [f"HHTools_Raider_{role}" for role in raider_roles],
            "apparelTags",
            ["FIPA_Pool_WastelanderGeneric", "FIPA_Pool_Raider"],
        ),
        (
            [f"HHTools_Tribal_{role}" for role in tribal_roles if role != "Child"],
            "weaponTags",
            ["FIPW_Pool_Tribal"],
        ),
        (
            [f"HHTools_Tribal_{role}" for role in tribal_roles],
            "apparelTags",
            ["FIPA_Pool_Tribal"],
        ),
    ]
    for names, list_name, tags in base_sets:
        add_replace_list_operation(
            patch, pawnkind_xpath(names, list_name), list_name, tags
        )

    def add_regional_sets(
        prefix: str,
        family: str,
        normal_weapon: list[str],
        guard_weapon: list[str],
        normal_apparel: list[str],
        guard_apparel: list[str],
    ) -> None:
        roles = {
            "settlement": settlement_roles,
            "raider": raider_roles,
            "tribal": tribal_roles,
        }[family]
        normal_roles = [
            role for role in roles if role not in guard_roles and role != "Child"
        ]
        guarded_roles = [role for role in roles if role in guard_roles]
        if normal_roles:
            add_replace_list_operation(
                patch,
                pawnkind_xpath(
                    [f"HHTools_{prefix}_{family.title()}_{role}" for role in normal_roles],
                    "weaponTags",
                ),
                "weaponTags",
                normal_weapon,
            )
        if guarded_roles:
            add_replace_list_operation(
                patch,
                pawnkind_xpath(
                    [f"HHTools_{prefix}_{family.title()}_{role}" for role in guarded_roles],
                    "weaponTags",
                ),
                "weaponTags",
                guard_weapon,
            )
        all_apparel_names = [
            f"HHTools_{prefix}_{family.title()}_{role}" for role in roles
        ]
        add_replace_list_operation(
            patch,
            pawnkind_xpath(all_apparel_names, "apparelTags"),
            "apparelTags",
            normal_apparel,
        )
        if guarded_roles and guard_apparel != normal_apparel:
            add_replace_list_operation(
                patch,
                pawnkind_xpath(
                    [f"HHTools_{prefix}_{family.title()}_{role}" for role in guarded_roles],
                    "apparelTags",
                ),
                "apparelTags",
                guard_apparel,
            )

    add_regional_sets(
        "California",
        "settlement",
        ["FIPW_Pool_Wastelander", "FIPW_Pool_Settler", "FIPW_Pool_NCRRegional"],
        [
            "FIPW_Pool_Wastelander",
            "FIPW_Pool_Settler",
            "FIPW_Pool_SettlerGuard",
            "FIPW_Pool_NCRRegional",
            "FIPW_Pool_NCRRegionalElite",
        ],
        [
            "FIPA_Pool_WastelanderGeneric",
            "FIPA_Pool_Settler",
            "FIPA_Pool_NCRRegional",
        ],
        [
            "FIPA_Pool_WastelanderGeneric",
            "FIPA_Pool_Settler",
            "FIPA_Pool_SettlerGuard",
            "FIPA_Pool_NCRRegional",
            "FIPA_Pool_NCRRegionalElite",
        ],
    )
    add_regional_sets(
        "California",
        "raider",
        ["FIPW_Pool_Wastelander", "FIPW_Pool_Raider", "FIPW_Pool_CaliforniaRaider"],
        ["FIPW_Pool_Wastelander", "FIPW_Pool_Raider", "FIPW_Pool_CaliforniaRaider"],
        [
            "FIPA_Pool_WastelanderGeneric",
            "FIPA_Pool_Raider",
            "FIPA_Pool_CaliforniaRaider",
        ],
        [
            "FIPA_Pool_WastelanderGeneric",
            "FIPA_Pool_Raider",
            "FIPA_Pool_CaliforniaRaider",
        ],
    )
    add_regional_sets(
        "California",
        "tribal",
        ["FIPW_Pool_Tribal", "FIPW_Pool_SimpleLaserTribal", "FIPW_Pool_NCRSurplus"],
        ["FIPW_Pool_Tribal", "FIPW_Pool_SimpleLaserTribal", "FIPW_Pool_NCRSurplus"],
        ["FIPA_Pool_Tribal"],
        ["FIPA_Pool_Tribal"],
    )
    add_regional_sets(
        "Arizona",
        "settlement",
        ["FIPW_Pool_Wastelander", "FIPW_Pool_Settler", "FIPW_Pool_ArizonaCowboy"],
        [
            "FIPW_Pool_Wastelander",
            "FIPW_Pool_Settler",
            "FIPW_Pool_ArizonaCowboy",
            "FIPW_Pool_SettlerGuard",
        ],
        ["FIPA_Pool_WastelanderGeneric", "FIPA_Pool_Settler"],
        [
            "FIPA_Pool_WastelanderGeneric",
            "FIPA_Pool_Settler",
            "FIPA_Pool_SettlerGuard",
            "FIPA_Pool_LegionRegional",
        ],
    )
    add_regional_sets(
        "Arizona",
        "raider",
        [
            "FIPW_Pool_Wastelander",
            "FIPW_Pool_Raider",
            "FIPW_Pool_ArizonaRaider",
            "FIPW_Pool_ArizonaCowboy",
        ],
        [
            "FIPW_Pool_Wastelander",
            "FIPW_Pool_Raider",
            "FIPW_Pool_ArizonaRaider",
            "FIPW_Pool_ArizonaCowboy",
        ],
        [
            "FIPA_Pool_WastelanderGeneric",
            "FIPA_Pool_Raider",
            "FIPA_Pool_ArizonaRaider",
            "FIPA_Pool_LegionRegional",
        ],
        [
            "FIPA_Pool_WastelanderGeneric",
            "FIPA_Pool_Raider",
            "FIPA_Pool_ArizonaRaider",
            "FIPA_Pool_LegionRegional",
        ],
    )
    add_regional_sets(
        "Arizona",
        "tribal",
        ["FIPW_Pool_Tribal", "FIPW_Pool_ArizonaCowboy"],
        ["FIPW_Pool_Tribal", "FIPW_Pool_ArizonaCowboy"],
        ["FIPA_Pool_Tribal"],
        ["FIPA_Pool_Tribal"],
    )

    required_operation = ET.SubElement(
        patch, "Operation", {"Class": "PatchOperationReplace"}
    )
    ET.SubElement(required_operation, "xpath").text = (
        '/Defs/PawnKindDef[defName="HHTools_TribalChiefBase"]/apparelRequired'
    )
    required_value = ET.SubElement(required_operation, "value")
    required = ET.SubElement(required_value, "apparelRequired", {"Inherit": "False"})
    ET.SubElement(required, "li").text = "Apparel_TribalHeaddress"

    strict_path = (
        folder
        / "Patches"
        / "FIP-H&HTools"
        / "EquipmentProfiles"
        / "HHTools_StrictPawnKindPools.xml"
    )
    write_xml_document(strict_path, patch)
    generated.append(strict_path)
    return generated


def generate_special_faction_profile_patches() -> list[Path]:
    """
    Assign retained vanilla/DLC/VE factions to the same equipment doctrine.

    Removed world factions are intentionally absent. Native FCP factions keep
    their own source tags, which the item patches preserve.
    """
    generated: list[Path] = []

    def write_patch(
        folder_key: str,
        filename: str,
        patch: ET.Element,
        *,
        owner: str = "hhtools",
    ) -> None:
        if owner == "whitespring":
            root = WHITESPRING_PATCH_ROOT
            patch_owner = "FIP-Whitespring"
        else:
            root = HHTOOLS_PATCH_ROOT
            patch_owner = "FIP-H&HTools"
        path = (
            root
            / folder_key
            / "Patches"
            / patch_owner
            / "FactionEquipmentProfiles"
            / filename
        )
        write_xml_document(path, patch)
        generated.append(path)

    core = ET.Element("Patch")
    set_pawnkind_profile(
        core,
        ["AncientSoldier"],
        weapons=["FIPW_Pool_VaultHighTech"],
        apparel=["FIPA_Pool_VaultHighTech"],
    )
    ancient_budget = ET.SubElement(
        core, "Operation", {"Class": "PatchOperationReplace"}
    )
    ET.SubElement(ancient_budget, "xpath").text = (
        '/Defs/PawnKindDef[defName="AncientSoldier"]/weaponMoney'
    )
    ancient_budget_value = ET.SubElement(ancient_budget, "value")
    ET.SubElement(ancient_budget_value, "weaponMoney").text = "355~900"
    write_patch(
        "Equipment_Core",
        "HHTools_Core_RetainedFactionPools.xml",
        core,
    )

    royalty = ET.Element("Patch")
    set_pawnkind_profile(
        royalty,
        ["Refugee"],
        weapons=["FIPW_Pool_SlaveLowTech"],
        apparel=["FIPA_Pool_Slave", "FIPA_Pool_WastelanderGeneric"],
    )
    set_pawnkind_profile(
        royalty,
        ["Slave_Empire"],
        weapons=["FIPW_Pool_SlaveLowTech"],
        apparel=["FIPA_Pool_Slave"],
    )
    set_pawnkind_profile(
        royalty,
        ["Empire_Common_Lodger", "Empire_Common_Laborer", "Empire_Common_Trader"],
        weapons=["FIPW_Pool_Enclave"],
        apparel=["FIPA_Pool_EnclaveFallback"],
    )
    empire_troopers = [
        "Empire_Fighter_Trooper",
        "Empire_Fighter_Grenadier",
        "Empire_Fighter_Janissary",
        "Empire_Fighter_Champion",
    ]
    empire_power_armor = [
        "Empire_Fighter_Cataphract",
        "Empire_Fighter_StellicGuardRanged",
        "Empire_Fighter_StellicGuardMelee",
    ]
    empire_royals = [
        "Empire_Royal_NobleWimp",
        "Empire_Royal_Yeoman",
        "Empire_Royal_Acolyte",
        "Empire_Royal_Knight",
        "Empire_Royal_Praetor",
        "Empire_Royal_Baron",
        "Empire_Royal_Count",
        "Empire_Royal_Duke",
        "Empire_Royal_Consul",
        "Empire_Royal_Stellarch",
        "Empire_Royal_Bestower",
    ]
    set_pawnkind_profile(
        royalty,
        empire_troopers + empire_power_armor,
        weapons=["FIPW_Pool_Enclave"],
        apparel=["FIPA_Pool_EnclaveFallback"],
    )
    set_pawnkind_profile(
        royalty,
        empire_royals,
        apparel=["FIPA_Pool_EnclaveFallback", "FIPA_Pool_EnclaveOfficer"],
    )
    # The vanilla Empire trader group otherwise still mixes in generic
    # Villagers. They become regular troopers in the Whitespring escort.
    guard_replace = ET.SubElement(
        royalty, "Operation", {"Class": "PatchOperationReplace"}
    )
    ET.SubElement(guard_replace, "xpath").text = (
        '/Defs/FactionDef[defName="Empire"]/pawnGroupMakers/'
        'li[kindDef="Trader"]/guards/Villager'
    )
    guard_value = ET.SubElement(guard_replace, "value")
    ET.SubElement(guard_value, "Empire_Fighter_Trooper").text = "3"
    write_patch(
        "Base",
        "Whitespring_Royalty_RetainedFactionPools.xml",
        royalty,
        owner="whitespring",
    )

    biotech = ET.Element("Patch")
    set_pawnkind_profile(
        biotech,
        ["Sanguophage", "SanguophageThrall"],
        weapons=["FIPW_Pool_CultPrimitive"],
        apparel=[
            "FIPA_Pool_Cult",
            "FIPA_Pool_Tribal",
            "FIPA_Pool_WastelanderGeneric",
        ],
    )
    write_patch(
        "Equipment_Ludeon_RimWorld_Biotech",
        "HHTools_Biotech_RetainedFactionPools.xml",
        biotech,
    )

    ideology = ET.Element("Patch")
    set_pawnkind_profile(
        ideology,
        ["Beggar"],
        weapons=["FIPW_Pool_SlaveLowTech"],
        apparel=["FIPA_Pool_Slave", "FIPA_Pool_WastelanderGeneric"],
    )
    set_pawnkind_profile(
        ideology,
        ["PovertyPilgrim"],
        weapons=["FIPW_Pool_CultPrimitive"],
        apparel=["FIPA_Pool_Cult", "FIPA_Pool_Tribal"],
    )
    write_patch(
        "Equipment_Ludeon_RimWorld_Ideology",
        "HHTools_Ideology_RetainedFactionPools.xml",
        ideology,
    )

    anomaly = ET.Element("Patch")
    set_pawnkind_profile(
        anomaly,
        ["Horaxian_Underthrall", "Horaxian_Highthrall", "Horaxian_Gunner"],
        weapons=["FIPW_Pool_CultPrimitive"],
        apparel=["FIPA_Pool_Cult"],
    )
    write_patch(
        "Equipment_Ludeon_RimWorld_Anomaly",
        "HHTools_Anomaly_RetainedFactionPools.xml",
        anomaly,
    )

    odyssey = ET.Element("Patch")
    trader_kinds = [
        "TradersGuild_Slasher",
        "TradersGuild_Gunner",
        "TradersGuild_Elite",
        "TradersGuild_Heavy",
        "TradersGuild_Citizen",
        "TradersGuild_Child",
    ]
    set_pawnkind_profile(
        odyssey,
        [name for name in trader_kinds if not name.endswith("Child")],
        weapons=[
            "FIPW_Pool_EnclaveRemnant",
            "FIPW_Pool_EnclaveFallback",
        ],
        apparel=[
            "FIPA_Pool_EnclaveRemnant",
            "FIPA_Pool_EnclaveFallback",
        ],
    )
    set_pawnkind_profile(
        odyssey,
        ["TradersGuild_Child"],
        weapons=[],
        apparel=[
            "FIPA_Pool_EnclaveRemnant",
            "FIPA_Pool_EnclaveFallback",
        ],
    )
    set_pawnkind_profile(
        odyssey,
        ["Salvager_Pirate", "Salvager_Scrapper", "Salvager_Elite"],
        weapons=[
            "FIPW_Pool_Enclave",
            "FIPW_Pool_EnclaveFallback",
        ],
        apparel=[
            "FIPA_Pool_EnclaveFallback",
            "FIPA_Pool_EnclaveTrooper",
            "FIPA_Pool_EnclaveCombatArmor",
        ],
    )
    write_patch(
        "Equipment_Ludeon_RimWorld_Odyssey",
        "HHTools_Odyssey_RetainedFactionPools.xml",
        odyssey,
    )

    odyssey_fcp = ET.Element("Patch")
    set_pawnkind_profile(
        odyssey_fcp,
        [name for name in trader_kinds if not name.endswith("Child")],
        apparel=["FIPA_Pool_EnclaveRemnant"],
        clear_apparel_requirements=True,
    )
    set_pawnkind_profile(
        odyssey_fcp,
        ["TradersGuild_Child"],
        weapons=[],
        apparel=["FIPA_Pool_EnclaveRemnant"],
        clear_apparel_requirements=True,
    )
    set_pawnkind_profile(
        odyssey_fcp,
        ["Salvager_Pirate", "Salvager_Scrapper", "Salvager_Elite"],
        apparel=[
            "FIPA_Pool_EnclaveTrooper",
            "FIPA_Pool_EnclaveCombatArmor",
        ],
        clear_apparel_requirements=True,
    )
    write_patch(
        "Equipment_Odyssey_FCP_Enclave",
        "HHTools_Odyssey_FCPEnclave_RankPools.xml",
        odyssey_fcp,
    )

    vfee = ET.Element("Patch")
    set_pawnkind_profile(
        vfee,
        ["VFEE_Deserter"],
        weapons=["FIPW_Pool_CircleOfSteel"],
        apparel=["FIPA_Pool_CircleOfSteel"],
    )
    set_pawnkind_profile(
        vfee,
        ["VFEE_Empire_Fighter_Absolver"],
        weapons=["FIPW_Pool_Enclave"],
        apparel=[
            "FIPA_Pool_EnclaveFallback",
            "FIPA_Pool_EnclaveCombatArmor",
        ],
    )
    set_pawnkind_profile(
        vfee,
        ["VFEE_Empire_Royal_Techfriar"],
        weapons=["FIPW_Pool_Enclave"],
        apparel=[
            "FIPA_Pool_EnclaveFallback",
            "FIPA_Pool_EnclaveScientist",
        ],
    )
    set_pawnkind_profile(
        vfee,
        [
            "VFEE_Empire_Royal_Archcount",
            "VFEE_Empire_Royal_Marquess",
            "VFEE_Empire_Royal_Archduke",
            "VFEE_Empire_Royal_Magister",
            "VFEE_Empire_Royal_Despot",
            "VFEE_Empire_Royal_HighStellarch",
            "VFEE_Empire_Royal_Emperor",
        ],
        apparel=["FIPA_Pool_EnclaveFallback", "FIPA_Pool_EnclaveOfficer"],
    )
    write_patch(
        "Empire",
        "Whitespring_VFEEmpire_RetainedFactionPools.xml",
        vfee,
        owner="whitespring",
    )

    medieval = ET.Element("Patch")
    merchant_guards = [
        "VFEM2_Knight",
        "VFEM2_Handgunner",
        "VFEM2_Raider",
        "VFEM2_Militia",
    ]
    merchant_civilians = ["VFEM2_Merchant", "VFEM2_Trader", "VFEM2_Guildmaster"]
    set_pawnkind_profile(
        medieval,
        merchant_guards,
        weapons=["FIPW_Pool_Caravan", "FIPW_Pool_SettlerGuard"],
        apparel=[
            "FIPA_Pool_WastelanderGeneric",
            "FIPA_Pool_Settler",
            "FIPA_Pool_SettlerGuard",
        ],
        clear_apparel_requirements=True,
    )
    set_pawnkind_profile(
        medieval,
        merchant_civilians,
        weapons=["FIPW_Pool_Caravan"],
        apparel=["FIPA_Pool_WastelanderGeneric", "FIPA_Pool_Settler"],
        clear_apparel_requirements=True,
    )
    write_patch(
        "Equipment_OskarPotocki_VFE_Medieval2",
        "HHTools_VFEMedieval2_MerchantGuildPools.xml",
        medieval,
    )

    # FCP Enclave apparel is optional. These two composite folders remove the
    # fallback pool only when the matching FCP source actually exists.
    whitespring_fcp = ET.Element("Patch")
    set_pawnkind_profile(
        whitespring_fcp,
        ["Empire_Common_Lodger", "Empire_Common_Laborer", "Empire_Common_Trader"],
        apparel=["FIPA_Pool_EnclaveTrooper"],
        clear_apparel_requirements=True,
    )
    set_pawnkind_profile(
        whitespring_fcp,
        empire_troopers,
        apparel=[
            "FIPA_Pool_EnclaveTrooper",
            "FIPA_Pool_EnclaveCombatArmor",
        ],
        clear_apparel_requirements=True,
    )
    set_pawnkind_profile(
        whitespring_fcp,
        empire_power_armor,
        apparel=["FIPA_Pool_EnclavePowerArmor"],
        clear_apparel_requirements=True,
    )
    set_pawnkind_profile(
        whitespring_fcp,
        empire_royals,
        apparel=["FIPA_Pool_EnclaveOfficer"],
        clear_apparel_requirements=True,
    )
    write_patch(
        "FCP_Enclave",
        "Whitespring_FCPEnclave_RankPools.xml",
        whitespring_fcp,
        owner="whitespring",
    )

    whitespring_vfee_fcp = ET.Element("Patch")
    set_pawnkind_profile(
        whitespring_vfee_fcp,
        ["VFEE_Empire_Fighter_Absolver"],
        apparel=["FIPA_Pool_EnclaveCombatArmor"],
        clear_apparel_requirements=True,
    )
    set_pawnkind_profile(
        whitespring_vfee_fcp,
        ["VFEE_Empire_Royal_Techfriar"],
        apparel=["FIPA_Pool_EnclaveScientist"],
        clear_apparel_requirements=True,
    )
    set_pawnkind_profile(
        whitespring_vfee_fcp,
        [
            "VFEE_Empire_Royal_Archcount",
            "VFEE_Empire_Royal_Marquess",
            "VFEE_Empire_Royal_Archduke",
            "VFEE_Empire_Royal_Magister",
            "VFEE_Empire_Royal_Despot",
            "VFEE_Empire_Royal_HighStellarch",
            "VFEE_Empire_Royal_Emperor",
        ],
        apparel=["FIPA_Pool_EnclaveOfficer"],
        clear_apparel_requirements=True,
    )
    write_patch(
        "Empire_FCP_Enclave",
        "Whitespring_VFEEmpire_FCPEnclave_RankPools.xml",
        whitespring_vfee_fcp,
        owner="whitespring",
    )
    return generated


def validate_generated_output(
    mods: list[ModSource],
    things: list[Thing],
    assignments: dict[tuple[str, str, str], Assignment],
    relevant_mods: list[ModSource],
    generated: list[Path],
    inventory_errors: list[str],
) -> tuple[Path, bool]:
    """Perform deterministic checks over the generated compatibility layer."""
    report = REPORT_DIR / "Donaustahl_Equipment_Validation.txt"
    mod_root = WORKSPACE / "FIP-Donaustahl"
    loadfolders_path = mod_root / "LoadFolders.xml"
    about_path = mod_root / "About" / "About.xml"

    checks: list[tuple[str, bool, str]] = []
    xml_errors: list[str] = []
    xml_paths = sorted(
        {
            *mod_root.rglob("*.xml"),
            loadfolders_path,
            about_path,
        },
        key=lambda path: str(path).lower(),
    )
    for path in xml_paths:
        try:
            ET.parse(path)
        except (ET.ParseError, OSError) as exc:
            xml_errors.append(f"{path}: {exc}")
    checks.append(
        (
            "All FIP-Donaustahl XML files parse",
            not xml_errors,
            f"{len(xml_paths)} files checked; {len(xml_errors)} errors",
        )
    )

    loadfolders_root = ET.parse(loadfolders_path).getroot()
    configured_folders: set[str] = set()
    missing_folders: list[str] = []
    configured_packages: set[str] = set()
    for entry in loadfolders_root.findall("./v1.6/li"):
        if entry.text and entry.text.strip():
            relative = entry.text.strip().replace("/", "\\")
            configured_folders.add(relative.lower())
            if not (mod_root / Path(relative)).is_dir():
                missing_folders.append(relative)
        for attribute in ("IfModActive", "IfModActiveAll"):
            for package_id in entry.attrib.get(attribute, "").split(","):
                if package_id.strip():
                    configured_packages.add(package_id.strip().lower())
    checks.append(
        (
            "Every configured LoadFolder exists",
            not missing_folders,
            f"{len(configured_folders)} folders checked; "
            f"{len(missing_folders)} missing",
        )
    )

    equipment_dirs = {
        str(path.relative_to(mod_root)).replace("/", "\\").lower()
        for path in (mod_root / "LoadFolders").glob("Equipment_*")
        if path.is_dir()
    }
    stale_equipment_dirs = sorted(equipment_dirs - configured_folders)
    checks.append(
        (
            "No generated equipment folder is omitted from LoadFolders.xml",
            not stale_equipment_dirs,
            f"{len(equipment_dirs)} equipment folders; "
            f"{len(stale_equipment_dirs)} omitted",
        )
    )

    about_root = ET.parse(about_path).getroot()
    load_after = {
        child.text.strip().lower()
        for child in about_root.findall("./loadAfter/li")
        if child.text and child.text.strip()
    }
    fip_load_graph: dict[str, set[str]] = {}
    fip_package_names: dict[str, str] = {}
    for other_about in WORKSPACE.glob("FIP-*/**/About/About.xml"):
        try:
            other_root = ET.parse(other_about).getroot()
        except (ET.ParseError, OSError):
            continue
        other_package_text = text_at(other_root, "packageId")
        other_package = other_package_text.lower()
        if not other_package.startswith("fip."):
            continue
        fip_package_names[other_package] = other_package_text
        fip_load_graph[other_package] = {
            child.text.strip().lower()
            for child in other_root.findall("./loadAfter/li")
            if child.text
            and child.text.strip().lower().startswith("fip.")
        }

    cycle_keys: set[tuple[str, ...]] = set()
    visit_state: dict[str, int] = {}
    visit_stack: list[str] = []

    def visit_load_order(package_id: str) -> None:
        visit_state[package_id] = 1
        visit_stack.append(package_id)
        for dependency in sorted(fip_load_graph.get(package_id, set())):
            if dependency not in fip_load_graph:
                continue
            state = visit_state.get(dependency, 0)
            if state == 0:
                visit_load_order(dependency)
            elif state == 1:
                start = visit_stack.index(dependency)
                cycle = visit_stack[start:]
                rotations = [
                    tuple(cycle[index:] + cycle[:index])
                    for index in range(len(cycle))
                ]
                cycle_keys.add(min(rotations))
        visit_stack.pop()
        visit_state[package_id] = 2

    for package_id in sorted(fip_load_graph):
        if visit_state.get(package_id, 0) == 0:
            visit_load_order(package_id)
    fip_load_cycles = [
        " -> ".join(
            [
                *(fip_package_names.get(package, package) for package in cycle),
                fip_package_names.get(cycle[0], cycle[0]),
            ]
        )
        for cycle in sorted(cycle_keys)
    ]
    checks.append(
        (
            "FIP loadAfter graph contains no cycle",
            not fip_load_cycles,
            f"{len(fip_load_cycles)} cycles",
        )
    )
    optional_packages = {
        mod.package_id.lower()
        for mod in relevant_mods
        if mod.package_id != "Ludeon.RimWorld"
    }
    missing_conditions = sorted(optional_packages - configured_packages)
    missing_load_after = sorted(optional_packages - load_after)
    checks.append(
        (
            "Every optional equipment source has an IfModActive condition",
            not missing_conditions,
            f"{len(optional_packages)} optional sources; "
            f"{len(missing_conditions)} missing",
        )
    )
    checks.append(
        (
            "Every optional equipment source is listed in loadAfter",
            not missing_load_after,
            f"{len(optional_packages)} optional sources; "
            f"{len(missing_load_after)} missing",
        )
    )

    expected_targets: set[tuple[str, str, str]] = set()
    for thing in things:
        assignment = assignments[
            (thing.source.package_id, thing.def_name, thing.kind)
        ]
        target_kind = "weapon" if thing.kind == "weapon" else "apparel"
        prefix = "FIPW_" if target_kind == "weapon" else "FIPA_"
        if any(tag.startswith(prefix) for tag in assignment.tags):
            expected_targets.add(
                (thing.source.package_id, thing.def_name, target_kind)
            )

    folder_packages = {
        (
            "Equipment_Core"
            if mod.package_id == "Ludeon.RimWorld"
            else f"Equipment_{mod.folder_key}"
        ).lower(): mod.package_id
        for mod in relevant_mods
    }
    actual_targets: set[tuple[str, str, str]] = set()
    equipment_tag_files = [
        path
        for path in generated
        if "EquipmentTags" in path.parts
    ]
    for path in equipment_tag_files:
        relative = path.relative_to(PATCH_ROOT)
        package_id = folder_packages.get(relative.parts[0].lower())
        if not package_id:
            continue
        root = ET.parse(path).getroot()
        for operation in root.findall("./Operation"):
            xpath = text_at(operation, "xpath")
            target = re.search(r'defName="([^"]+)"', xpath)
            if not target:
                continue
            target_kind = "weapon" if xpath.endswith("/weaponTags") else "apparel"
            actual_targets.add((package_id, target.group(1), target_kind))
    missing_item_patches = sorted(expected_targets - actual_targets)
    unexpected_item_patches = sorted(actual_targets - expected_targets)
    checks.append(
        (
            "Every assigned item has exactly one generated tag target",
            not missing_item_patches and not unexpected_item_patches,
            f"expected={len(expected_targets)}, actual={len(actual_targets)}, "
            f"missing={len(missing_item_patches)}, "
            f"unexpected={len(unexpected_item_patches)}",
        )
    )

    assigned_pool_tags = {
        tag
        for assignment in assignments.values()
        for tag in assignment.tags
        if re.fullmatch(r"FIP[WA]_Pool_.+", tag)
    }
    consumed_pool_tags: set[str] = set()
    for path in generated:
        if not {
            "EquipmentProfiles",
            "FactionEquipmentProfiles",
        }.intersection(path.parts):
            continue
        root = ET.parse(path).getroot()
        for entry in root.iter("li"):
            if entry.text and re.fullmatch(r"FIP[WA]_Pool_.+", entry.text.strip()):
                consumed_pool_tags.add(entry.text.strip())
    empty_consumed_pools = sorted(consumed_pool_tags - assigned_pool_tags)
    checks.append(
        (
            "Every pool used by a PawnKind contains assigned equipment",
            not empty_consumed_pools,
            f"{len(consumed_pool_tags)} pools consumed; "
            f"{len(empty_consumed_pools)} empty",
        )
    )

    known_defs: set[str] = set()
    source_xml: set[Path] = set()
    for mod in mods:
        source_xml.update(def_xml_files(mod))
    for fip_dir in WORKSPACE.glob("FIP-*"):
        if not fip_dir.is_dir():
            continue
        # Only Defs can provide defNames. Skipping Languages and other XML
        # avoids reparsing thousands of translation files during validation.
        for defs_dir in fip_dir.rglob("Defs"):
            if defs_dir.is_dir():
                source_xml.update(defs_dir.rglob("*.xml"))
    for path in sorted(source_xml, key=lambda item: str(item).lower()):
        try:
            root = ET.parse(path).getroot()
        except (ET.ParseError, OSError):
            continue
        for node in root.iter("defName"):
            if node.text and node.text.strip():
                known_defs.add(node.text.strip())

    targeted_defs: set[str] = set()
    referenced_fipd_defs: set[str] = set()
    for path in generated:
        root = ET.parse(path).getroot()
        if root.tag != "Patch":
            continue
        for xpath_node in root.iter("xpath"):
            if not xpath_node.text:
                continue
            targeted_defs.update(
                re.findall(r'defName="([^"]+)"', xpath_node.text)
            )
        for node in root.iter():
            if (
                isinstance(node.tag, str)
                and node.tag.startswith("FIPD_")
            ):
                referenced_fipd_defs.add(node.tag)
    unresolved_targets = sorted(targeted_defs - known_defs)
    unresolved_fipd_refs = sorted(referenced_fipd_defs - known_defs)
    external_hhtools_parents: list[str] = []
    for path in generated:
        try:
            root = ET.parse(path).getroot()
        except (ET.ParseError, OSError):
            continue
        if root.tag != "Defs":
            continue
        for pawn_kind in root.findall("./PawnKindDef"):
            parent_name = pawn_kind.attrib.get("ParentName", "")
            if parent_name.startswith("HHTools_"):
                def_name = text_at(pawn_kind, "defName", "(unnamed)")
                external_hhtools_parents.append(
                    f"{def_name} -> {parent_name}"
                )
    checks.append(
        (
            "Every exact defName patch target resolves in the inspected sources",
            not unresolved_targets,
            f"{len(targeted_defs)} targets; {len(unresolved_targets)} unresolved",
        )
    )
    checks.append(
        (
            "Every generated FIPD PawnKind reference resolves",
            not unresolved_fipd_refs,
            f"{len(referenced_fipd_defs)} references; "
            f"{len(unresolved_fipd_refs)} unresolved",
        )
    )
    checks.append(
        (
            "Generated PawnKinds do not use cross-mod HHTools ParentName inheritance",
            not external_hhtools_parents,
            f"{len(external_hhtools_parents)} unsafe parent references",
        )
    )
    checks.append(
        (
            "Source inventory contains no XML parse errors",
            not inventory_errors,
            f"{len(inventory_errors)} source errors",
        )
    )

    status_counts = Counter(
        assignment.status for assignment in assignments.values()
    )
    passed = all(success for _, success, _ in checks)
    lines = [
        "FIP-Donaustahl equipment compatibility validation",
        "=" * 54,
        "",
        f"Overall result: {'PASS' if passed else 'FAIL'}",
        f"Source equipment declarations: {len(things)}",
        f"Generated XML files this run: {len(generated)}",
        "Assignment status: "
        + ", ".join(
            f"{status}={count}"
            for status, count in sorted(status_counts.items())
        ),
        "",
        "Checks",
        "------",
    ]
    for name, success, detail in checks:
        lines.append(f"[{'PASS' if success else 'FAIL'}] {name}: {detail}")

    detail_sections = [
        ("XML errors", xml_errors),
        ("Missing LoadFolders", missing_folders),
        ("Omitted equipment directories", stale_equipment_dirs),
        ("Missing IfModActive conditions", missing_conditions),
        ("Missing loadAfter entries", missing_load_after),
        ("FIP load-order cycles", fip_load_cycles),
        ("Missing generated item patches", [" | ".join(item) for item in missing_item_patches]),
        ("Unexpected generated item patches", [" | ".join(item) for item in unexpected_item_patches]),
        ("Consumed but empty pools", empty_consumed_pools),
        ("Unresolved exact patch targets", unresolved_targets),
        ("Unresolved generated PawnKind references", unresolved_fipd_refs),
        ("Unsafe cross-mod HHTools parents", external_hhtools_parents),
    ]
    for title, values in detail_sections:
        if not values:
            continue
        lines.extend(["", title, "-" * len(title), *values])
    report.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return report, passed


def validate_distributed_output(
    mods: list[ModSource],
    things: list[Thing],
    assignments: dict[tuple[str, str, str], Assignment],
    relevant_mods: list[ModSource],
    generated: list[Path],
    inventory_errors: list[str],
) -> tuple[Path, bool]:
    """Validate the H&H Tools/Whitespring split without depending on Donaustahl."""
    report = REPORT_DIR / "FIP_Equipment_Validation.txt"
    checks: list[tuple[str, bool, str]] = []

    xml_errors: list[str] = []
    for path in generated:
        try:
            ET.parse(path)
        except (ET.ParseError, OSError) as exc:
            xml_errors.append(f"{path}: {exc}")
    checks.append(
        (
            "All generated equipment XML files parse",
            not xml_errors,
            f"{len(generated)} files checked; {len(xml_errors)} errors",
        )
    )

    donaustahl_generated = [
        str(path)
        for path in generated
        if "FIP-Donaustahl" in path.parts
    ]
    donaustahl_dirs = sorted(
        str(path)
        for path in (WORKSPACE / "FIP-Donaustahl" / "LoadFolders").glob(
            "Equipment_*"
        )
        if path.is_dir()
    )
    donaustahl_loadfolders = (
        WORKSPACE / "FIP-Donaustahl" / "LoadFolders.xml"
    ).read_text(encoding="utf-8")
    checks.append(
        (
            "Donaustahl owns no equipment compatibility output",
            not donaustahl_generated
            and not donaustahl_dirs
            and "LoadFolders/Equipment_" not in donaustahl_loadfolders,
            f"generated={len(donaustahl_generated)}, "
            f"directories={len(donaustahl_dirs)}",
        )
    )

    for mod_name in ("FIP-H&HTools", "FIP-Whitespring"):
        mod_root = WORKSPACE / mod_name
        configured: set[str] = set()
        missing: list[str] = []
        root = ET.parse(mod_root / "LoadFolders.xml").getroot()
        for entry in root.findall("./v1.6/li"):
            if not entry.text or not entry.text.strip():
                continue
            relative = entry.text.strip().replace("/", "\\")
            configured.add(relative.lower())
            if not (mod_root / Path(relative)).is_dir():
                missing.append(relative)
        checks.append(
            (
                f"{mod_name} LoadFolders exist",
                not missing,
                f"{len(configured)} configured; {len(missing)} missing",
            )
        )

    expected_targets: set[tuple[str, str, str]] = set()
    for thing in things:
        assignment = assignments[
            (thing.source.package_id, thing.def_name, thing.kind)
        ]
        target_kind = "weapon" if thing.kind == "weapon" else "apparel"
        prefix = "FIPW_" if target_kind == "weapon" else "FIPA_"
        if any(tag.startswith(prefix) for tag in assignment.tags):
            expected_targets.add(
                (thing.source.package_id, thing.def_name, target_kind)
            )

    source_by_key = {mod.folder_key: mod.package_id for mod in relevant_mods}
    actual_targets: set[tuple[str, str, str]] = set()
    duplicate_targets: list[str] = []
    seen_targets: set[tuple[str, str, str]] = set()
    for path in generated:
        if "EquipmentTags" not in path.parts:
            continue
        package_id = ""
        for folder_key, candidate in source_by_key.items():
            if f"_{folder_key}_EquipmentTags.xml" in path.name:
                package_id = candidate
                break
        if not package_id:
            continue
        root = ET.parse(path).getroot()
        for operation in root.findall("./Operation"):
            xpath = text_at(operation, "xpath")
            target = re.search(r'defName="([^"]+)"', xpath)
            if not target:
                continue
            target_kind = "weapon" if xpath.endswith("/weaponTags") else "apparel"
            key = (package_id, target.group(1), target_kind)
            if key in seen_targets:
                duplicate_targets.append(" | ".join(key))
            seen_targets.add(key)
            actual_targets.add(key)
    missing_item_patches = sorted(expected_targets - actual_targets)
    unexpected_item_patches = sorted(actual_targets - expected_targets)
    checks.append(
        (
            "Every assigned item has one generated tag target",
            not missing_item_patches
            and not unexpected_item_patches
            and not duplicate_targets,
            f"expected={len(expected_targets)}, actual={len(actual_targets)}, "
            f"missing={len(missing_item_patches)}, "
            f"unexpected={len(unexpected_item_patches)}, "
            f"duplicates={len(duplicate_targets)}",
        )
    )

    assigned_pool_tags = {
        tag
        for assignment in assignments.values()
        for tag in assignment.tags
        if re.fullmatch(r"FIP[WA]_Pool_.+", tag)
    }
    consumed_pool_tags: set[str] = set()
    for path in generated:
        if not {
            "EquipmentProfiles",
            "FactionEquipmentProfiles",
        }.intersection(path.parts):
            continue
        root = ET.parse(path).getroot()
        for entry in root.iter("li"):
            if entry.text and re.fullmatch(
                r"FIP[WA]_Pool_.+", entry.text.strip()
            ):
                consumed_pool_tags.add(entry.text.strip())
    empty_consumed_pools = sorted(consumed_pool_tags - assigned_pool_tags)
    checks.append(
        (
            "Every consumed equipment pool has assigned items",
            not empty_consumed_pools,
            f"{len(consumed_pool_tags)} consumed; "
            f"{len(empty_consumed_pools)} empty",
        )
    )

    external_hhtools_parents: list[str] = []
    for path in generated:
        root = ET.parse(path).getroot()
        if root.tag != "Defs":
            continue
        for pawn_kind in root.findall("./PawnKindDef"):
            parent_name = pawn_kind.attrib.get("ParentName", "")
            if parent_name.startswith("HHTools_"):
                external_hhtools_parents.append(
                    f"{text_at(pawn_kind, 'defName', '(unnamed)')} -> "
                    f"{parent_name}"
                )
    checks.append(
        (
            "Generated PawnKinds avoid cross-mod ParentName inheritance",
            not external_hhtools_parents,
            f"{len(external_hhtools_parents)} unsafe parents",
        )
    )
    checks.append(
        (
            "Source inventory contains no XML parse errors",
            not inventory_errors,
            f"{len(inventory_errors)} source errors",
        )
    )

    status_counts = Counter(
        assignment.status for assignment in assignments.values()
    )
    passed = all(success for _, success, _ in checks)
    lines = [
        "FIP distributed equipment compatibility validation",
        "=" * 50,
        "",
        f"Overall result: {'PASS' if passed else 'FAIL'}",
        f"Source equipment declarations: {len(things)}",
        f"Generated XML files this run: {len(generated)}",
        "Assignment status: "
        + ", ".join(
            f"{status}={count}"
            for status, count in sorted(status_counts.items())
        ),
        "",
        "Checks",
        "------",
    ]
    lines.extend(
        f"[{'PASS' if success else 'FAIL'}] {name}: {detail}"
        for name, success, detail in checks
    )
    details = [
        ("XML errors", xml_errors),
        ("Generated Donaustahl paths", donaustahl_generated),
        ("Donaustahl equipment directories", donaustahl_dirs),
        (
            "Missing generated item patches",
            [" | ".join(item) for item in missing_item_patches],
        ),
        (
            "Unexpected generated item patches",
            [" | ".join(item) for item in unexpected_item_patches],
        ),
        ("Duplicate generated item patches", duplicate_targets),
        ("Consumed but empty pools", empty_consumed_pools),
        ("Unsafe cross-mod HHTools parents", external_hhtools_parents),
    ]
    for title, values in details:
        if values:
            lines.extend(["", title, "-" * len(title), *values])
    report.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return report, passed


def print_summary(
    mods: list[ModSource],
    things: list[Thing],
    errors: list[str],
    assignments: dict[tuple[str, str, str], Assignment],
) -> None:
    counts: dict[tuple[str, str, str], int] = defaultdict(int)
    for thing in things:
        counts[(thing.source.family, thing.source.package_id, thing.kind)] += 1

    print(f"Discovered source mods: {len(mods)}")
    print(f"Equipment declarations: {len(things)}")
    print(f"XML parse errors: {len(errors)}")
    status_counts = Counter(assignment.status for assignment in assignments.values())
    print(
        "Assignment status: "
        + ", ".join(f"{status}={count}" for status, count in sorted(status_counts.items()))
    )
    for mod in mods:
        weapon_count = sum(
            count
            for (family, package_id, kind), count in counts.items()
            if package_id == mod.package_id and "weapon" in kind
        )
        apparel_count = sum(
            count
            for (family, package_id, kind), count in counts.items()
            if package_id == mod.package_id and "apparel" in kind
        )
        if weapon_count or apparel_count:
            print(
                f"{mod.family}\t{mod.package_id}\t"
                f"weapons={weapon_count}\tapparel={apparel_count}\t{mod.name}"
            )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--inventory-only",
        action="store_true",
        help="Discover and report source equipment without generating patches.",
    )
    args = parser.parse_args()

    mods = discover_mods()
    things, errors = build_inventory(mods)
    output = write_inventory(things, errors)
    assignments = classify_all(things)
    complete_report, unassigned_report = write_assignment_reports(things, assignments)
    print_summary(mods, things, errors, assignments)
    print(f"Inventory: {output}")
    print(f"Assignments: {complete_report}")
    print(f"Unassigned candidates: {unassigned_report}")

    if not args.inventory_only:
        relevant_mods, generated = generate_equipment_patches(
            mods, things, assignments
        )
        generated.extend(generate_hhtools_profile_patches())
        generated.extend(generate_special_faction_profile_patches())
        print(f"Generated patch files: {len(generated)}")
        print("H&H Tools LoadFolder entries required:")
        for mod in relevant_mods:
            if mod.package_id in WHITESPRING_ITEM_FOLDERS:
                print(
                    f"  Whitespring/{WHITESPRING_ITEM_FOLDERS[mod.package_id]} "
                    f"owns {mod.package_id}"
                )
                continue
            folder_name = (
                "Equipment_Core"
                if mod.package_id == "Ludeon.RimWorld"
                else f"Equipment_{mod.folder_key}"
            )
            if mod.package_id == "Ludeon.RimWorld":
                print(f"  <li>LoadFolders/{folder_name}</li>")
            else:
                print(
                    f'  <li IfModActive="{mod.package_id}">'
                    f"LoadFolders/{folder_name}</li>"
                )
        print("  <li>LoadFolders/Equipment_FIP_HHTools</li>")
        print(
            '  <li IfModActiveAll="Ludeon.RimWorld.Odyssey,'
            'Rick.FCP.Enclave">'
            "LoadFolders/Equipment_Odyssey_FCP_Enclave</li>"
        )
        validation_report, validation_passed = validate_distributed_output(
            mods,
            things,
            assignments,
            relevant_mods,
            generated,
            errors,
        )
        print(f"Validation: {validation_report}")
        if not validation_passed:
            return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
