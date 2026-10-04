#!/usr/bin/env python3
"""Extract Markdown vocabulary with SudachiPy.

Run this script from a repository root that owns tools/lint/markdown-targets.json.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
import unicodedata
import warnings
from collections import Counter
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable


TARGET_CONFIG = Path("tools/lint/markdown-targets.json")
TARGET_SUFFIXES = (".md", ".txt")
DEPENDENCY_HINT = (
    "Missing dependency: {module}. Run `pip install -r tools/lint/requirements.txt` "
    "from the target repository root."
)
CHIKKARPY_DEPENDENCY_HINT = (
    "Missing dependency: {module}. Install ChikkarPy for synonym grouping, or pass "
    "`--synonyms none` to skip synonym grouping."
)

ENGLISH_RE = re.compile(r"(?<![A-Za-z0-9])[A-Za-z][A-Za-z0-9]*(?:[._-][A-Za-z0-9]+)*")
FENCE_RE = re.compile(r"^[ \t]*(```|~~~)[^\n]*(?:\n[\s\S]*?)?^[ \t]*\1[^\n]*(?=\n|$)", re.MULTILINE)
INLINE_CODE_RE = re.compile(r"`[^`\n]+`")
FOOTNOTE_DEF_RE = re.compile(r"^\[\^[^\]]+\]:.*$", re.MULTILINE)
URL_RE = re.compile(r"https?://[^\s)]+")
MAILTO_RE = re.compile(r"mailto:[^\s)]+")
HTML_COMMENT_RE = re.compile(r"<!--[\s\S]*?-->")
REFERENCE_LINK_RE = re.compile(r"^\[[^\]\n]+\]:\s+\S+.*$", re.MULTILINE)
INLINE_LINK_RE = re.compile(r"!?\[[^\]\n]+\]\([^)]+\)")
KATAKANA_RE = re.compile(r"[\u30A0-\u30FF]")
CJK_RE = re.compile(r"[\u3400-\u9FFF]")


@dataclass
class VocabularyEntry:
    kind: str
    surface: str
    normalized: str
    reading: str = ""
    part_of_speech: str = ""
    group_key: str = ""
    synonyms: tuple[str, ...] = field(default_factory=tuple)
    count: int = 0
    sources: Counter[str] = field(default_factory=Counter)


def main() -> int:
    args = parse_args()
    tokenizer_obj, split_mode = create_sudachi_tokenizer()
    root = Path.cwd()
    target_config = read_target_config(root)
    files = select_target_files(root, target_config, args.files, args.changed)

    vocabulary: dict[tuple[str, str, str, str], VocabularyEntry] = {}
    for file_path in files:
        relative_path = normalize_path(file_path.relative_to(root))
        text = file_path.read_text(encoding="utf-8")
        cleaned = strip_markdown_noise(text)
        collect_english_tokens(vocabulary, cleaned, relative_path)
        collect_japanese_tokens(vocabulary, tokenizer_obj, split_mode, cleaned, relative_path)

    if args.synonyms == "chikkarpy":
        apply_synonym_groups(list(vocabulary.values()), create_chikkarpy_grouper())

    entries = sorted(
        vocabulary.values(),
        key=lambda item: (-item.count, item.kind, item.group_key.casefold(), item.surface.casefold(), item.part_of_speech),
    )
    if args.format == "json":
        write_json(entries)
    else:
        write_tsv(entries)

    return 0


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Extract Markdown vocabulary with SudachiPy.")
    parser.add_argument("--files", nargs="+", help="Specific .md/.txt files to inspect.")
    parser.add_argument("--changed", action="store_true", help="Inspect changed .md/.txt files only.")
    parser.add_argument("--format", choices=("tsv", "json"), default="tsv", help="Output format.")
    parser.add_argument(
        "--synonyms",
        choices=("chikkarpy", "none"),
        default="chikkarpy",
        help="Candidate synonym grouping source.",
    )
    return parser.parse_args()


def create_sudachi_tokenizer():
    try:
        from sudachipy import dictionary
        from sudachipy import tokenizer as sudachi_tokenizer
    except ImportError as exc:
        print(DEPENDENCY_HINT.format(module=exc.name or "SudachiPy"), file=sys.stderr)
        raise SystemExit(2) from exc

    try:
        return dictionary.Dictionary().create(), sudachi_tokenizer.Tokenizer.SplitMode.C
    except Exception as exc:
        print(DEPENDENCY_HINT.format(module="SudachiPy dictionary"), file=sys.stderr)
        print(f"SudachiPy initialization failed: {exc}", file=sys.stderr)
        raise SystemExit(2) from exc


def read_target_config(root: Path) -> dict[str, list[str]]:
    config_path = root / TARGET_CONFIG
    if not config_path.exists():
        print(f"{config_path}: missing. Run this script from the target repository root.", file=sys.stderr)
        raise SystemExit(2)

    data = json.loads(config_path.read_text(encoding="utf-8"))
    return {
        "ignoreDirectories": list(data.get("ignoreDirectories") or []),
        "ignoredPrefixes": [normalize_prefix(prefix) for prefix in data.get("ignoredPrefixes") or []],
    }


def select_target_files(
    root: Path,
    target_config: dict[str, list[str]],
    explicit_files: list[str] | None,
    changed: bool,
) -> list[Path]:
    if explicit_files is not None:
        candidates = [resolve_candidate(root, file_name) for file_name in explicit_files]
    elif changed:
        candidates = [root / file_name for file_name in list_changed_files(root)]
    else:
        candidates = list_all_target_files(root, target_config)

    return sorted(
        {
            path.resolve()
            for path in candidates
            if path is not None and path.exists() and path.is_file() and is_target_file(path)
            and not is_ignored(normalize_path(path.resolve().relative_to(root.resolve())), target_config)
        },
        key=lambda item: normalize_path(item.relative_to(root.resolve())),
    )


def resolve_candidate(root: Path, file_name: str) -> Path | None:
    path = Path(file_name)
    if path.is_absolute():
        try:
            path.resolve().relative_to(root.resolve())
        except ValueError:
            return None
        return path
    return root / path


def list_changed_files(root: Path) -> set[str]:
    candidates: set[str] = set()
    git_commands = [
        ["diff", "--name-only", "--diff-filter=ACMR"],
        ["diff", "--cached", "--name-only", "--diff-filter=ACMR"],
        ["ls-files", "--others", "--exclude-standard"],
    ]
    for args in git_commands:
        result = subprocess.run(["git", *args], cwd=root, text=True, stdout=subprocess.PIPE, check=False)
        if result.returncode != 0:
            continue
        for line in result.stdout.splitlines():
            file_name = line.strip()
            if file_name:
                candidates.add(file_name)
    return candidates


def list_all_target_files(root: Path, target_config: dict[str, list[str]]) -> list[Path]:
    files: list[Path] = []
    ignore_directories = set(target_config["ignoreDirectories"])
    ignored_prefixes = target_config["ignoredPrefixes"]

    for current_root, directory_names, file_names in os.walk(root):
        current = Path(current_root)
        relative_current = normalize_path(current.relative_to(root)) if current != root else ""
        directory_names[:] = [
            directory_name
            for directory_name in directory_names
            if directory_name not in ignore_directories
            and not prefix_ignored(join_path(relative_current, directory_name) + "/", ignored_prefixes)
        ]
        for file_name in file_names:
            path = current / file_name
            relative_path = normalize_path(path.relative_to(root))
            if is_target_file(path) and not is_ignored(relative_path, target_config):
                files.append(path)
    return files


def is_target_file(path: Path) -> bool:
    return path.suffix.lower() in TARGET_SUFFIXES


def is_ignored(relative_path: str, target_config: dict[str, list[str]]) -> bool:
    segments = relative_path.split("/")
    return (
        any(segment in set(target_config["ignoreDirectories"]) for segment in segments)
        or prefix_ignored(relative_path, target_config["ignoredPrefixes"])
    )


def prefix_ignored(relative_path: str, ignored_prefixes: Iterable[str]) -> bool:
    return any(relative_path.startswith(prefix) for prefix in ignored_prefixes)


def normalize_prefix(prefix: str) -> str:
    return normalize_path(prefix)


def normalize_path(path: str | Path) -> str:
    return str(path).replace(os.sep, "/")


def join_path(left: str, right: str) -> str:
    return f"{left}/{right}" if left else right


def strip_markdown_noise(text: str) -> str:
    cleaned = FENCE_RE.sub(blank_preserving, text)
    cleaned = INLINE_CODE_RE.sub(blank_preserving, cleaned)
    cleaned = FOOTNOTE_DEF_RE.sub(blank_preserving, cleaned)
    cleaned = URL_RE.sub(blank_preserving, cleaned)
    cleaned = MAILTO_RE.sub(blank_preserving, cleaned)
    cleaned = HTML_COMMENT_RE.sub(blank_preserving, cleaned)
    cleaned = REFERENCE_LINK_RE.sub(strip_reference_link_address, cleaned)
    cleaned = INLINE_LINK_RE.sub(strip_inline_link_address, cleaned)
    return cleaned


def blank_preserving(value: str | re.Match[str]) -> str:
    text = value.group(0) if isinstance(value, re.Match) else value
    return re.sub(r"[^\n]", " ", text)


def strip_reference_link_address(match: re.Match[str]) -> str:
    value = match.group(0)
    return re.sub(r":\s+\S+.*$", lambda address: ":" + blank_preserving(address.group(0)[1:]), value)


def strip_inline_link_address(match: re.Match[str]) -> str:
    value = match.group(0)
    return re.sub(r"\([^)]+\)", blank_preserving, value)


def collect_english_tokens(vocabulary: dict[tuple[str, str, str, str], VocabularyEntry], text: str, source: str) -> None:
    for match in ENGLISH_RE.finditer(text):
        surface = match.group(0)
        if not should_check_english(surface):
            continue
        normalized = normalize_term(surface)
        add_entry(vocabulary, "english", surface, normalized, "", "", source)


def collect_japanese_tokens(
    vocabulary: dict[tuple[str, str, str, str], VocabularyEntry],
    tokenizer_obj,
    split_mode,
    text: str,
    source: str,
) -> None:
    for morpheme in tokenizer_obj.tokenize(text, split_mode):
        surface = morpheme.surface()
        if not should_collect_japanese(surface, morpheme):
            continue
        normalized = sudachi_value(morpheme, "normalized_form") or surface
        reading = sudachi_value(morpheme, "reading_form")
        part_of_speech = ",".join(str(part) for part in morpheme.part_of_speech() if part != "*")
        kind = "japanese" if CJK_RE.search(surface) else "katakana"
        add_entry(vocabulary, kind, surface, normalize_term(normalized), reading, part_of_speech, source)


def should_check_english(value: str) -> bool:
    return len(value) > 1 and not any(character.isdigit() for character in value)


def should_collect_japanese(surface: str, morpheme) -> bool:
    if not (KATAKANA_RE.search(surface) or CJK_RE.search(surface)):
        return False
    normalized = normalize_term(surface).replace("・", "").replace("ー", "").replace(".", "").replace("_", "").replace("-", "")
    if len(normalized) <= 1:
        return False
    part_of_speech = morpheme.part_of_speech()
    return bool(part_of_speech) and part_of_speech[0] == "名詞"


def sudachi_value(morpheme, name: str) -> str:
    value = getattr(morpheme, name, None)
    if value is None:
        return ""
    result = value()
    return "" if result == "*" else str(result)


class ChikkarpyGrouper:
    def __init__(self, chikkar) -> None:
        self.chikkar = chikkar
        self.cache: dict[str, tuple[str, ...]] = {}

    def find(self, value: str) -> tuple[str, ...]:
        normalized = normalize_term(value)
        if normalized in self.cache:
            return self.cache[normalized]

        try:
            raw_synonyms = self.chikkar.find(value)
        except Exception:
            raw_synonyms = []

        synonyms = tuple(
            sorted(
                {
                    str(synonym).strip()
                    for synonym in raw_synonyms
                    if str(synonym).strip() and normalize_term(str(synonym)) != normalized
                },
                key=lambda item: (normalize_term(item), item),
            )
        )
        self.cache[normalized] = synonyms
        return synonyms


def create_chikkarpy_grouper() -> ChikkarpyGrouper:
    try:
        with warnings.catch_warnings():
            warnings.filterwarnings("ignore", message="pkg_resources is deprecated.*", category=UserWarning)
            from chikkarpy import Chikkar
            from chikkarpy.dictionarylib import Dictionary
    except ImportError as exc:
        print(CHIKKARPY_DEPENDENCY_HINT.format(module=exc.name or "ChikkarPy"), file=sys.stderr)
        raise SystemExit(2) from exc

    try:
        chikkar = Chikkar()
        chikkar.add_dictionary(Dictionary())
        return ChikkarpyGrouper(chikkar)
    except Exception as exc:
        print(CHIKKARPY_DEPENDENCY_HINT.format(module="ChikkarPy dictionary"), file=sys.stderr)
        print(f"ChikkarPy initialization failed: {exc}", file=sys.stderr)
        raise SystemExit(2) from exc


def apply_synonym_groups(entries: list[VocabularyEntry], grouper: ChikkarpyGrouper) -> None:
    parent = list(range(len(entries)))
    candidate_sets: list[set[str]] = [set() for _ in entries]
    synonym_sets: list[set[str]] = [set() for _ in entries]
    candidate_to_entry_indexes: dict[str, list[int]] = {}

    for index, entry in enumerate(entries):
        candidate_sets[index].add(entry.normalized)
        if entry.kind in {"japanese", "katakana"}:
            lookup_values = {entry.surface, entry.normalized}
            for lookup_value in lookup_values:
                if not contains_japanese(lookup_value):
                    continue
                for synonym in grouper.find(lookup_value):
                    synonym_sets[index].add(synonym)
                    candidate_sets[index].add(normalize_term(synonym))

        for candidate in candidate_sets[index]:
            candidate_to_entry_indexes.setdefault(candidate, []).append(index)

    for indexes in candidate_to_entry_indexes.values():
        first_index = indexes[0]
        for index in indexes[1:]:
            union(parent, first_index, index)

    grouped_indexes: dict[int, list[int]] = {}
    for index in range(len(entries)):
        grouped_indexes.setdefault(find_parent(parent, index), []).append(index)

    for indexes in grouped_indexes.values():
        if not any(synonym_sets[index] for index in indexes):
            continue

        group_candidates = set[str]()
        for index in indexes:
            group_candidates.update(candidate_sets[index])
            entries[index].synonyms = tuple(
                sorted(synonym_sets[index], key=lambda item: (normalize_term(item), item))
            )

        group_id = select_group_identifier(group_candidates)
        for index in indexes:
            entries[index].group_key = f"synonym:{group_id}"


def find_parent(parent: list[int], index: int) -> int:
    if parent[index] != index:
        parent[index] = find_parent(parent, parent[index])
    return parent[index]


def union(parent: list[int], left: int, right: int) -> None:
    left_parent = find_parent(parent, left)
    right_parent = find_parent(parent, right)
    if left_parent != right_parent:
        parent[right_parent] = left_parent


def select_group_identifier(candidates: set[str]) -> str:
    sorted_candidates = sorted(candidates)
    for candidate in sorted_candidates:
        if contains_japanese(candidate):
            return candidate
    return sorted_candidates[0] if sorted_candidates else ""


def contains_japanese(value: str) -> bool:
    return bool(KATAKANA_RE.search(value) or CJK_RE.search(value))


def normalize_term(value: str) -> str:
    return unicodedata.normalize("NFKC", value).casefold()


def add_entry(
    vocabulary: dict[tuple[str, str, str, str], VocabularyEntry],
    kind: str,
    surface: str,
    normalized: str,
    reading: str,
    part_of_speech: str,
    source: str,
) -> None:
    key = (kind, normalized, reading, part_of_speech)
    entry = vocabulary.get(key)
    if entry is None:
        entry = VocabularyEntry(kind, surface, normalized, reading, part_of_speech, group_key(kind, normalized, reading))
        vocabulary[key] = entry
    entry.count += 1
    entry.sources[source] += 1


def group_key(kind: str, normalized: str, reading: str) -> str:
    if kind in {"japanese", "katakana"} and reading:
        return f"reading:{reading}"
    return f"normalized:{normalized}"


def write_tsv(entries: list[VocabularyEntry]) -> None:
    print("type\tsurface\tnormalized\treading\tpartOfSpeech\tgroupKey\tsynonyms\tcount\tsources")
    for entry in entries:
        sources = ",".join(f"{source}:{count}" for source, count in sorted(entry.sources.items()))
        synonyms = "|".join(entry.synonyms)
        print(
            "\t".join(
                [
                    entry.kind,
                    entry.surface,
                    entry.normalized,
                    entry.reading,
                    entry.part_of_speech,
                    entry.group_key,
                    synonyms,
                    str(entry.count),
                    sources,
                ]
            )
        )


def write_json(entries: list[VocabularyEntry]) -> None:
    print(
        json.dumps(
            [
                {
                    "type": entry.kind,
                    "surface": entry.surface,
                    "normalized": entry.normalized,
                    "reading": entry.reading,
                    "partOfSpeech": entry.part_of_speech,
                    "groupKey": entry.group_key,
                    "synonyms": list(entry.synonyms),
                    "count": entry.count,
                    "sources": dict(sorted(entry.sources.items())),
                }
                for entry in entries
            ],
            ensure_ascii=False,
            indent=2,
        )
    )


if __name__ == "__main__":
    raise SystemExit(main())
