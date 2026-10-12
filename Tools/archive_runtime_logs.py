"""원시 로그만 Temp에 보관한다. Git/네트워크/삭제 기능은 제공하지 않는다."""

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import stat
import sys
import zipfile


RAW_NAMES = frozenset({"RuntimeLog.txt", "RuntimeLog_device.txt",
                       "RuntimeLog_device_2.txt", "RuntimeLog_host.txt",
                       "RuntimeLog_client.txt"})
LOG_ROOT = PurePosixPath("Assets/_Project/Docs/_Logs")
OUTPUT_ROOT = PurePosixPath("Temp/log-archive")
BLOCK = 1024 * 1024


def utc_now():
    return datetime.now(timezone.utc).isoformat()


def raw_path(name):
    """허용 basename과 실제 날짜 경로만 인정하고 문서/meta는 제외한다."""
    path = PurePosixPath(name)
    if "\\" in name or path.is_absolute() or path.as_posix() != name:
        return False
    if ".." in path.parts or path.name not in RAW_NAMES:
        return False
    try:
        parts = path.relative_to(LOG_ROOT).parts
        if parts[0] == "_editor":
            if len(parts) != 3 or parts[-1] != "RuntimeLog.txt":
                return False
            date = parts[1]
        else:
            if len(parts) < 3:
                return False
            date = parts[0]
        return datetime.strptime(date, "%Y-%m-%d").strftime("%Y-%m-%d") == date
    except (ValueError, IndexError):
        return False


def no_links(path):
    """Windows junction을 포함한 모든 상위 reparse 경로를 거부한다."""
    for item in (path, *path.parents):
        if not item.exists() and not item.is_symlink():
            continue
        info = item.lstat()
        if stat.S_ISLNK(info.st_mode) or getattr(info, "st_file_attributes", 0) & 1024:
            raise ValueError("symlink/reparse path rejected")


def source_path(root, name):
    if not raw_path(name):
        raise ValueError("non-raw or unsafe entry path rejected")
    path = root.joinpath(*PurePosixPath(name).parts)
    no_links(path)
    if not path.resolve().is_relative_to(root):
        raise ValueError("source escaped repository")
    return path


def select_paths(root):
    start = root.joinpath(*LOG_ROOT.parts)
    no_links(start)
    if not start.is_dir():
        raise ValueError("log root missing")
    selected = []
    for folder, directories, files in os.walk(start, followlinks=False):
        for directory in directories:
            no_links(Path(folder) / directory)
        for filename in files:
            path = Path(folder) / filename
            name = path.relative_to(root).as_posix()
            if raw_path(name):
                source_path(root, name)
                selected.append(name)
            elif filename in RAW_NAMES:
                raise ValueError("raw basename outside approved date path")
    if not selected or len(selected) != len(set(selected)):
        raise ValueError("empty or duplicate selection")
    return sorted(selected)


def stream_digest(stream, destination=None):
    digest = hashlib.sha256()
    size = 0
    while True:
        data = stream.read(BLOCK)
        if not data:
            break
        digest.update(data)
        size += len(data)
        if destination is not None:
            destination.write(data)
    return size, digest.hexdigest()


def signature(info):
    return info.st_size, info.st_mtime_ns, info.st_dev, info.st_ino


def capture(root, name):
    path = source_path(root, name)
    before = path.stat()
    if not stat.S_ISREG(before.st_mode):
        raise ValueError("non-regular source rejected")
    with path.open("rb") as stream:
        if signature(os.fstat(stream.fileno())) != signature(before):
            raise ValueError("source replaced before capture")
        size, digest = stream_digest(stream)
        if signature(os.fstat(stream.fileno())) != signature(before):
            raise ValueError("source changed during capture")
    if signature(path.stat()) != signature(before) or size != before.st_size:
        raise ValueError("source changed during capture")
    return {"path": name, "bytes": size, "sha256": digest,
            "mtime_ns": before.st_mtime_ns, "device": before.st_dev,
            "inode": before.st_ino, "captured_at_utc": utc_now()}


def check_record(root, record):
    current = capture(root, record["path"])
    for field in ("path", "bytes", "sha256", "mtime_ns", "device", "inode"):
        if current[field] != record[field]:
            raise ValueError("source changed since frozen snapshot")


def fingerprint(records):
    # 순서까지 명시한다. 이전 PowerShell/culture 정렬값과 무조건 같다고 주장하지 않는다.
    canonical = "".join(f'{r["path"]}\t{r["bytes"]}\t{r["sha256"]}\n'
                        for r in sorted(records, key=lambda item: item["path"]))
    return hashlib.sha256(canonical.encode("utf-8")).hexdigest()


def output_path(root, name):
    if not re.fullmatch(r"\d{4}-\d{2}-\d{2}_기존로그(?:_part\d{2})?\.zip", name):
        raise ValueError("unsafe archive filename rejected")
    datetime.strptime(name[:10], "%Y-%m-%d")
    folder = root.joinpath(*OUTPUT_ROOT.parts)
    no_links(folder)
    path = folder / name
    no_links(path)
    if not path.resolve().is_relative_to(root.joinpath(*OUTPUT_ROOT.parts).resolve()):
        raise ValueError("output escaped Temp")
    return path


def pack(root, archive, records):
    """고정된 목록만 쓰며 실패 시 산출물을 지우거나 성공으로 표시하지 않는다."""
    with zipfile.ZipFile(archive, "x", compression=zipfile.ZIP_DEFLATED,
                         compresslevel=6, allowZip64=True) as bundle:
        for index, record in enumerate(records, 1):
            path = source_path(root, record["path"])
            before = path.stat()
            expected = (record["bytes"], record["mtime_ns"], record["device"], record["inode"])
            if signature(before) != expected:
                raise ValueError("source changed before compression")
            info = zipfile.ZipInfo(record["path"])
            info.compress_type = zipfile.ZIP_DEFLATED
            info.file_size = record["bytes"]
            with path.open("rb") as source, bundle.open(info, "w", force_zip64=True) as entry:
                if signature(os.fstat(source.fileno())) != expected:
                    raise ValueError("source replaced before compression")
                size, digest = stream_digest(source, entry)
                if signature(os.fstat(source.fileno())) != expected:
                    raise ValueError("source changed during compression")
            if signature(path.stat()) != expected or (size, digest) != (record["bytes"], record["sha256"]):
                raise ValueError("source changed during compression")
            if index % 16 == 0 or index == len(records):
                print(f"compressed {index}/{len(records)}", flush=True)


def verify_zip(archive, records):
    """실제 ZIP을 재개방해 원래 full-path 집합과 CRC/bytes/SHA를 모두 읽는다."""
    expected = {record["path"]: record for record in records}
    if len(expected) != len(records) or not all(raw_path(name) for name in expected):
        raise ValueError("invalid frozen entry set")
    with zipfile.ZipFile(archive, "r") as bundle:
        infos = bundle.infolist()
        names = [info.filename for info in infos]
        if len(names) != len(set(names)) or set(names) != set(expected):
            raise ValueError("ZIP missing/extra/duplicate paths")
        for index, info in enumerate(infos, 1):
            record = expected[info.filename]
            if info.is_dir() or info.file_size != record["bytes"]:
                raise ValueError("ZIP entry size mismatch")
            with bundle.open(info) as entry:
                size, digest = stream_digest(entry)
            if (size, digest) != (record["bytes"], record["sha256"]):
                raise ValueError("ZIP entry content mismatch")
            if index % 16 == 0 or index == len(records):
                print(f"verified {index}/{len(records)}", flush=True)
    with archive.open("rb") as stream:
        size, digest = stream_digest(stream)
    return {"status": "LOCAL_VERIFIED", "verified_at_utc": utc_now(),
            "entries": len(records), "uncompressed_bytes": sum(r["bytes"] for r in records),
            "zip_bytes": size, "zip_sha256": digest,
            "path_set_match": True, "entry_bytes_sha256_match": True, "crc_read_complete": True}


def prepare(root, name, expected_count, expected_bytes):
    archive = output_path(root, name)
    manifest_path = archive.with_suffix(".manifest.json")
    no_links(manifest_path)
    if archive.exists() or manifest_path.exists():
        raise ValueError("existing output will not be overwritten")
    started = utc_now()
    names = select_paths(root)
    records = [capture(root, path) for path in names]
    total = sum(record["bytes"] for record in records)
    if (expected_count is not None and len(records) != expected_count
            or expected_bytes is not None and total != expected_bytes):
        raise ValueError("approved inventory count/bytes changed")
    print(f"frozen count={len(records)} bytes={total}", flush=True)
    if select_paths(root) != names:
        raise ValueError("selection changed during snapshot")
    manifest = {"schema": "runtime-log-local-archive-v1", "capture_started_at_utc": started,
                "capture_completed_at_utc": utc_now(), "entries": records,
                "file_count": len(records), "original_bytes": total,
                "inventory_sha256": fingerprint(records),
                "fingerprint_format": "UTF8; path Unicode-codepoint sorted; path TAB bytes TAB lowercase SHA256 LF",
                "archive_path": archive.relative_to(root).as_posix(),
                "local_verification": {"status": "LOCAL_UNVERIFIED"},
                "remote": {"status": "NOTVERIFIED", "release_url": None, "download_url": None},
                "session_role": "NOT_INSPECTED", "build_status": "NOT_INFERRED",
                "limits": ["Local ZIP verification is not remote upload/download verification.",
                           "No Git changes or source deletion authorized by this receipt."]}
    archive.parent.mkdir(parents=True, exist_ok=True)
    # 배타적으로 직접 생성한 manifest에만 receipt를 추가하고 기존 파일은 덮어쓰지 않는다.
    with manifest_path.open("x", encoding="utf-8", newline="\n") as receipt:
        json.dump(manifest, receipt, ensure_ascii=False, indent=2)
        receipt.flush()
        try:
            pack(root, archive, records)
            verification = verify_zip(archive, records)
            if select_paths(root) != names:
                raise ValueError("source selection changed after compression")
            for record in records:
                check_record(root, record)
            verification["source_recheck_match"] = True
            manifest["local_verification"] = verification
        except (OSError, ValueError, zipfile.BadZipFile, RuntimeError):
            manifest["local_verification"] = {"status": "FAILED", "failed_at_utc": utc_now()}
            raise
        finally:
            receipt.seek(0)
            receipt.truncate()
            json.dump(manifest, receipt, ensure_ascii=False, indent=2)
    print(json.dumps({"manifest": manifest_path.relative_to(root).as_posix(),
                      "inventory_sha256": manifest["inventory_sha256"],
                      "local_verification": manifest["local_verification"],
                      "remote": "NOTVERIFIED"}, ensure_ascii=False), flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--name", default="2026-10-12_기존로그.zip")
    parser.add_argument("--expected-count", type=int)
    parser.add_argument("--expected-bytes", type=int)
    args = parser.parse_args()
    try:
        no_links(args.root.absolute())
        prepare(args.root.resolve(), args.name, args.expected_count, args.expected_bytes)
    except (OSError, ValueError, zipfile.BadZipFile, RuntimeError) as error:
        print(f"archive aborted: {type(error).__name__}: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
