import hashlib
import json
import os
import sys
from pathlib import Path


def main():
    release_path = Path(sys.argv[1])
    release_directory = Path(sys.argv[2])
    version = os.environ["PLUGIN_VERSION"]
    target_abi = os.environ["TARGET_ABI"]

    with release_path.open(encoding="utf-8") as file:
        release = json.load(file)

    zip_assets = [
        asset for asset in release["assets"]
        if asset["name"].endswith(".zip")
    ]
    if len(zip_assets) != 1:
        raise RuntimeError(
            f"Expected exactly one ZIP release asset; found {len(zip_assets)}."
        )

    asset = zip_assets[0]
    package_path = release_directory / asset["name"]
    if not package_path.is_file():
        raise FileNotFoundError(f"Downloaded release asset not found: {package_path}")

    checksum = hashlib.sha256()
    with package_path.open("rb") as package_file:
        for chunk in iter(lambda: package_file.read(1024 * 1024), b""):
            checksum.update(chunk)
    digest = checksum.hexdigest()
    manifest_path = Path("manifest.json")
    with manifest_path.open(encoding="utf-8") as file:
        manifest = json.load(file)

    plugin = next(
        (
            item for item in manifest
            if item.get("guid") == "7c1f3a52-9b0e-4d7e-8a65-2f4c9e1d3b70"
        ),
        None,
    )
    if plugin is None:
        raise RuntimeError("Pawfin Backend entry is missing from manifest.json.")

    versions = plugin.get("versions")
    if not isinstance(versions, list):
        raise RuntimeError("Pawfin Backend manifest entry must have a versions array.")

    version_entry = {
        "version": version,
        "changelog": release.get("body") or "",
        "targetAbi": target_abi,
        "sourceUrl": asset["browser_download_url"],
        "checksum": digest,
        "timestamp": release["published_at"],
    }
    versions[:] = [
        entry for entry in versions if entry.get("version") != version
    ]
    versions.insert(0, version_entry)

    with manifest_path.open("w", encoding="utf-8") as file:
        json.dump(manifest, file, indent=2, ensure_ascii=False)
        file.write("\n")


if __name__ == "__main__":
    main()
