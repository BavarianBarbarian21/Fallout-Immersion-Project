#!/usr/bin/env python3
"""Validate Donaustahl's universal trader and biome pack-animal policy."""

from __future__ import annotations

from copy import deepcopy
from pathlib import Path
import re
import sys

from lxml import etree


REPOSITORY = Path(__file__).resolve().parents[2]
NEW_MODS = REPOSITORY / "New-Mods"
DONAUSTAHL = NEW_MODS / "FIP-Donaustahl"
PATCH_FILES = (
    DONAUSTAHL
    / "LoadFolders"
    / "Base"
    / "Patches"
    / "FIP-Donaustahl"
    / "PackAnimals"
    / "Donaustahl_PackAnimals.xml",
    DONAUSTAHL
    / "LoadFolders"
    / "FCP_Animals"
    / "Patches"
    / "FIP-Donaustahl"
    / "PackAnimals"
    / "Donaustahl_FCPPackAnimals.xml",
    DONAUSTAHL
    / "LoadFolders"
    / "Arktos"
    / "Patches"
    / "FIP-Donaustahl"
    / "PackAnimals"
    / "Donaustahl_ArktosPackAnimals.xml",
    DONAUSTAHL
    / "LoadFolders"
    / "Arktos_FCP_Animals"
    / "Patches"
    / "FIP-Donaustahl"
    / "PackAnimals"
    / "Donaustahl_ArktosFCPPackAnimals.xml",
)
PLAYABLE_MODULES = {
    "FIP.Arktos",
    "FIP.Corvega",
    "FIP.FutureTec",
    "FIP.Greenway",
    "FIP.HHTools",
    "FIP.Hubris",
    "FIP.Lucky38",
    "FIP.Poseidon",
    "FIP.Repconn",
    "FIP.RobCo",
    "FIP.Sunset",
    "FIP.WestTek",
    "FIP.Whitespring",
}
ECOSYSTEM_PACKAGE = re.compile(
    r"^(?:"
    r"rick\.fcp\."
    r"|vanillaexpanded\."
    r"|vanillaquestsexpanded\."
    r"|vanillaracesexpanded\."
    r"|oskarpotocki\.vfe\."
    r"|oskarpotocki\.vanillafactionsexpanded\."
    r"|oskarpotocki\.vanillavehiclesexpanded"
    r"|smashphil\.vehicleframework$"
    r")",
    re.IGNORECASE,
)


def parse(path: Path) -> etree._Element:
    return etree.parse(str(path)).getroot()


def apply_operation(document: etree._Element, operation: etree._Element) -> None:
    operation_class = operation.get("Class")
    xpath = operation.findtext("xpath", "").strip()

    if operation_class == "PatchOperationConditional":
        branch = operation.find("match") if document.xpath(xpath) else operation.find("nomatch")
        if branch is not None:
            apply_operation(document, branch)
        return

    try:
        targets = document.xpath(xpath)
    except etree.XPathError as error:
        raise ValueError(f"Invalid XPath in policy patch: {xpath}") from error
    if operation_class == "PatchOperationRemove":
        for target in targets:
            target.getparent().remove(target)
        return

    if operation_class == "PatchOperationAdd":
        value = operation.find("value")
        if value is None:
            raise ValueError(f"PatchOperationAdd without value: {xpath}")
        for target in targets:
            for child in value:
                target.append(deepcopy(child))
        return

    raise ValueError(f"Unsupported operation in policy test: {operation_class}")


def assert_single(
    parent: etree._Element,
    name: str,
    expected_text: str | None = None,
) -> None:
    nodes = parent.xpath(f"./{name}")
    if len(nodes) != 1:
        raise AssertionError(
            f"Expected exactly one {name} below {parent.tag}, got {len(nodes)}"
        )
    if expected_text is not None and (nodes[0].text or "").strip() != expected_text:
        raise AssertionError(
            f"Expected {name}={expected_text}, got {(nodes[0].text or '').strip()}"
        )


def make_fixture() -> etree._Element:
    return etree.fromstring(
        b"""
<Defs>
  <FactionDef>
    <defName>Fixture_ExistingCarriers</defName>
    <pawnGroupMakers>
      <li>
        <kindDef>Trader</kindDef>
        <carriers>
          <Muffalo>9</Muffalo>
          <Muffalo>4</Muffalo>
          <FCP_Animal_Brahmin>2</FCP_Animal_Brahmin>
          <FCP_Animal_Brahmin>3</FCP_Animal_Brahmin>
          <Alpaca>7</Alpaca>
        </carriers>
      </li>
    </pawnGroupMakers>
  </FactionDef>
  <FactionDef>
    <defName>Fixture_MissingCarriers</defName>
    <pawnGroupMakers>
      <li>
        <kindDef>Trader</kindDef>
      </li>
    </pawnGroupMakers>
  </FactionDef>
  <BiomeDef>
    <defName>Fixture_ExistingBiomeList</defName>
    <allowedPackAnimals>
      <li>Muffalo</li>
      <li>Muffalo</li>
      <li>FCP_Animal_Brahmin</li>
      <li>FCP_Animal_Brahmin</li>
      <li>Alpaca</li>
    </allowedPackAnimals>
  </BiomeDef>
  <BiomeDef>
    <defName>Fixture_RootWithoutList</defName>
  </BiomeDef>
  <BiomeDef Name="Arktos_Nature" Abstract="True">
    <allowedPackAnimals>
      <li>Horse</li>
      <li>Horse</li>
    </allowedPackAnimals>
  </BiomeDef>
  <BiomeDef ParentName="Arktos_Nature">
    <defName>Arktos_FixtureChild</defName>
  </BiomeDef>
  <BiomeDef Name="Fixture_ParentBiome" Abstract="True">
    <allowedPackAnimals>
      <li>Alpaca</li>
    </allowedPackAnimals>
  </BiomeDef>
  <BiomeDef ParentName="Fixture_ParentBiome">
    <defName>Fixture_InheritingChild</defName>
    <allowedPackAnimals>
      <li>Dromedary</li>
    </allowedPackAnimals>
  </BiomeDef>
  <BiomeDef ParentName="Fixture_ParentBiome">
    <defName>Fixture_NonInheritingChild</defName>
    <allowedPackAnimals Inherit="False">
      <li>Dromedary</li>
    </allowedPackAnimals>
  </BiomeDef>
</Defs>
"""
    )


def known_ecosystem_packages() -> set[str]:
    package_ids: set[str] = set()
    for path in NEW_MODS.rglob("*.xml"):
        try:
            root = parse(path)
        except (OSError, etree.XMLSyntaxError):
            continue
        for element in root.xpath("//packageId | //loadAfter/li"):
            value = "".join(element.itertext()).strip()
            if value and ECOSYSTEM_PACKAGE.match(value):
                package_ids.add(value)
    return package_ids


def main() -> int:
    failures: list[str] = []
    arktos_biomes: list[etree._Element] = []
    arktos_named_biomes: dict[str, etree._Element] = {}
    for path in (NEW_MODS / "FIP-Arktos").rglob("*.xml"):
        try:
            root = parse(path)
        except (OSError, etree.XMLSyntaxError):
            continue
        if root.tag != "Defs":
            continue
        for biome in root.xpath("/Defs/BiomeDef"):
            arktos_biomes.append(biome)
            parent_template = (biome.get("Name") or "").strip()
            if parent_template:
                arktos_named_biomes[parent_template] = biome

    direct_arktos_biomes = [
        biome
        for biome in arktos_biomes
        if (biome.findtext("defName") or "").strip().startswith("Arktos_")
    ]
    for biome in direct_arktos_biomes:
        def_name = (biome.findtext("defName") or "").strip()
        parent_name = (biome.get("ParentName") or "").strip()
        visited: set[str] = set()
        inherits_arktos_nature = False
        while parent_name and parent_name not in visited:
            if parent_name == "Arktos_Nature":
                inherits_arktos_nature = True
                break
            visited.add(parent_name)
            parent = arktos_named_biomes.get(parent_name)
            parent_name = (
                (parent.get("ParentName") or "").strip()
                if parent is not None
                else ""
            )
        if not inherits_arktos_nature:
            failures.append(
                f"Arktos biome does not inherit Arktos_Nature: {def_name}"
            )

    for path in PATCH_FILES:
        if not path.is_file():
            failures.append(f"Missing policy patch: {path.relative_to(REPOSITORY)}")

    if not failures:
        fixture = make_fixture()
        for path in PATCH_FILES:
            root = parse(path)
            for operation in root:
                if not isinstance(operation.tag, str):
                    continue
                apply_operation(fixture, operation)

        for carriers in fixture.xpath(
            "/Defs/FactionDef/pawnGroupMakers/li[kindDef='Trader']/carriers"
        ):
            try:
                assert_single(carriers, "Muffalo", "1")
                assert_single(carriers, "FCP_Animal_Brahmin", "100")
            except AssertionError as error:
                failures.append(str(error))

        for allowed in fixture.xpath(
            "/Defs/BiomeDef[not(@ParentName) "
            "or allowedPackAnimals[@Inherit='False']]/allowedPackAnimals"
        ):
            try:
                muffalo = allowed.xpath("./li[.='Muffalo']")
                brahmin = allowed.xpath("./li[.='FCP_Animal_Brahmin']")
                if len(muffalo) != 1 or len(brahmin) != 1:
                    raise AssertionError(
                        "Every explicit biome list must contain one Muffalo "
                        "and one FCP_Animal_Brahmin"
                    )
            except AssertionError as error:
                failures.append(str(error))

        inheriting_child = fixture.xpath(
            "/Defs/BiomeDef[defName='Fixture_InheritingChild']"
            "/allowedPackAnimals"
        )[0]
        for animal in ("Muffalo", "FCP_Animal_Brahmin"):
            if inheriting_child.xpath(f"./li[.='{animal}']"):
                failures.append(
                    f"{animal} was redundantly added to an inheriting child biome"
                )

        arktos = fixture.xpath(
            "/Defs/BiomeDef[@Name='Arktos_Nature']/allowedPackAnimals"
        )[0]
        for animal in (
            "Muffalo",
            "Horse",
            "FCP_Animal_Brahmin",
            "FCP_Animal_Bighorner",
            "FCP_Animal_Radstag",
        ):
            count = len(arktos.xpath(f"./li[.='{animal}']"))
            if count != 1:
                failures.append(
                    f"Arktos_Nature must contain one {animal}, got {count}"
                )

    about = parse(DONAUSTAHL / "About" / "About.xml")
    load_after = {
        "".join(element.itertext()).strip()
        for element in about.xpath("/ModMetaData/loadAfter/li")
    }
    expected_load_after = known_ecosystem_packages() | PLAYABLE_MODULES
    missing_order = sorted(expected_load_after - load_after)
    failures.extend(
        f"Donaustahl does not load after supported package: {package_id}"
        for package_id in missing_order
    )

    obsolete_paths = (
        REPOSITORY
        / "Development"
        / "Source"
        / "FIP-Arktos"
        / "FIP.Arktos.Urban"
        / "ArktosPackAnimalCompatibility.cs",
        NEW_MODS
        / "FIP-Arktos"
        / "LoadFolders"
        / "FCP_Animals"
        / "Patches"
        / "FIP-Arktos"
        / "Compatch"
        / "Arktos_FCP_Animals_Patch.xml",
    )
    failures.extend(
        f"Obsolete Arktos implementation remains: {path.relative_to(REPOSITORY)}"
        for path in obsolete_paths
        if path.exists()
    )

    print(
        f"policy_patches={len(PATCH_FILES)}, "
        f"load_after={len(load_after)}, "
        f"known_ecosystem_packages={len(known_ecosystem_packages())}, "
        f"arktos_biomes={len(direct_arktos_biomes)}, "
        f"failures={len(failures)}"
    )
    if failures:
        print("FAILED")
        for failure in failures:
            print(f"- {failure}")
        return 1

    print("PASS: Donaustahl pack-animal policy is normalized and ordered.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
