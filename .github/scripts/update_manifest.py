import hashlib
import json
import sys
import zipfile
from pathlib import Path


def main():
    release_path = Path(sys.argv[1])
    release_directory = Path(sys.argv[2])
    with release_path.open(encoding="utf-8") as file:
        release = json.load(file)

    builds = {
        "_jf10.11.zip": ("manifest.json", "10.11.0.0"),
        "_jf12.zip": ("manifest-jf12.json", "12.2.0.0"),
    }
    found_abis = set()

    for asset in release["assets"]:
        suffix = next((item for item in builds if asset["name"].endswith(item)), None)
        if suffix is None:
            continue

        manifest_name, expected_abi = builds[suffix]
        package_path = release_directory / asset["name"]
        if not package_path.is_file():
            raise FileNotFoundError(f"Release asset not downloaded: {package_path}")

        with zipfile.ZipFile(package_path) as package:
            meta_name = next((name for name in package.namelist() if name.endswith("meta.json")), None)
            if meta_name is None:
                raise RuntimeError(f"No meta.json found in {asset['name']}.")
            raw_metadata = json.loads(package.read(meta_name))
        metadata = {key.lower(): value for key, value in raw_metadata.items()}

        version = metadata["version"]
        target_abi = metadata["targetabi"]
        if target_abi != expected_abi:
            raise RuntimeError(
                f"{asset['name']} declares ABI {target_abi}; expected {expected_abi}."
            )

        checksum = hashlib.md5()
        with package_path.open("rb") as package_file:
            for chunk in iter(lambda: package_file.read(1024 * 1024), b""):
                checksum.update(chunk)

        manifest_path = Path(manifest_name)
        with manifest_path.open(encoding="utf-8") as file:
            manifest = json.load(file)

        plugin = next(
            (item for item in manifest if item.get("guid") == metadata["guid"]),
            None,
        )
        if plugin is None:
            raise RuntimeError(f"Pawfin Backend entry is missing from {manifest_name}.")

        versions = plugin.get("versions")
        if not isinstance(versions, list):
            raise RuntimeError(f"Pawfin Backend entry in {manifest_name} has no versions array.")

        version_entry = {
            "version": version,
            "changelog": release.get("body") or "",
            "targetAbi": target_abi,
            "sourceUrl": asset["browser_download_url"],
            "checksum": checksum.hexdigest(),
            "timestamp": release["published_at"],
        }
        versions[:] = [entry for entry in versions if entry.get("version") != version]
        versions.insert(0, version_entry)

        with manifest_path.open("w", encoding="utf-8") as file:
            json.dump(manifest, file, indent=2, ensure_ascii=False)
            file.write("\n")
        found_abis.add(target_abi)

    if found_abis != {"10.11.0.0", "12.2.0.0"}:
        raise RuntimeError(
            "Expected release ZIPs for both Jellyfin 10.11 and 12; "
            f"found ABI targets: {sorted(found_abis)}."
        )


if __name__ == "__main__":
    main()
