"""Check Britannia Solo's agent routes without a network or a running shard."""

import argparse
from datetime import date
import re
from pathlib import Path
import subprocess
from urllib.parse import unquote


SERVER = Path(__file__).resolve().parents[2]
WORKSPACE = SERVER.parent
CLIENT = WORKSPACE / "crossuo"
PAIRED = (CLIENT / "src").is_dir()
MEMORY = SERVER / ".memory"
GUIDE = SERVER / "dev-docs/uoex-development"
SKILLS = SERVER / ".agents/skills"
MIRRORS = SERVER / ".claude/skills"
SLUG = re.compile(r"[a-z0-9]+(?:-[a-z0-9]+)*")
LINK = re.compile(r"\]\((?:<([^>]+)>|([^()\s]+))\)")
NOTE_FIELDS = ("title", "feature", "type", "status", "verified")


def local_links(document):
    for match in LINK.finditer(document.read_text(encoding="utf-8")):
        target = match[1] or match[2]
        path = unquote(target.split("#", 1)[0])
        if path and ":" not in path:
            yield document.parent / path


def frontmatter(document):
    lines = document.read_text(encoding="utf-8").splitlines()
    if not lines or lines[0] != "---" or "---" not in lines[1:]:
        return {}, lines
    end = lines.index("---", 1)
    metadata = {}
    for line in lines[1:end]:
        key, sep, value = line.partition(":")
        if sep:
            metadata[key.strip()] = value.strip()
    return metadata, lines[end + 1:]


def check_router(router, children, limit, errors):
    if not router.is_file():
        errors.append(f"Missing router {router.relative_to(SERVER)}")
        return
    if len(router.read_text(encoding="utf-8").splitlines()) > limit:
        errors.append(f"Router exceeds {limit} lines: {router.relative_to(SERVER)}")
    targets = {link.resolve() for link in local_links(router)}
    for child in children:
        if child.resolve() not in targets:
            errors.append(f"{child.relative_to(SERVER)} is missing from {router.name}")


def check_memory(errors):
    features = sorted(p for p in MEMORY.iterdir() if p.is_dir() and not p.name.startswith("_"))
    check_router(MEMORY / "index.md", [feature / "README.md" for feature in features], 200, errors)
    docs = [MEMORY / "index.md", MEMORY / "_system/README.md"]
    for feature in features:
        topics = sorted(p for p in feature.iterdir() if p.is_dir())
        notes = sorted(feature.rglob("*.md"))
        unexpected = sorted(p for topic in topics for p in topic.iterdir() if p.is_dir())
        for directory in [feature, *topics]:
            router = directory / "README.md"
            children = [p for p in directory.glob("*.md") if p != router]
            if directory == feature:
                children.extend(p / "README.md" for p in topics)
            check_router(router, children, 80, errors)
        for directory in [feature, *topics]:
            if not SLUG.fullmatch(directory.name):
                errors.append(f"Invalid folder name: {directory.relative_to(MEMORY)}")
        for directory in unexpected:
            errors.append(f"Memory topic is nested too deeply: {directory.relative_to(MEMORY)}")
        for note in notes:
            docs.append(note)
            if note.name == "README.md":
                continue
            metadata, body = frontmatter(note)
            if not SLUG.fullmatch(note.stem) or len(body) > 60:
                errors.append(f"Invalid note name or body length: {note.relative_to(MEMORY)}")
            if any(not metadata.get(key) for key in NOTE_FIELDS):
                errors.append(f"Missing note metadata: {note.relative_to(MEMORY)}")
            if metadata.get("feature") != feature.name:
                errors.append(f"Feature mismatch: {note.relative_to(MEMORY)}")
            try:
                date.fromisoformat(metadata.get("verified", ""))
            except ValueError:
                errors.append(f"Use an ISO verification date: {note.relative_to(MEMORY)}")
            if not any(local_links(note)):
                errors.append(f"Missing source link: {note.relative_to(MEMORY)}")
    return docs


def check_skills(sync, errors):
    if sync:
        MIRRORS.mkdir(parents=True, exist_ok=True)
    skills = sorted(p for p in SKILLS.iterdir() if p.is_dir())
    catalog = SKILLS / "README.md"
    sources = [skill / "SKILL.md" for skill in skills]
    check_router(catalog, sources, 80, errors)
    for skill, source in zip(skills, sources):
        if not source.is_file():
            errors.append(f"Missing skill source: {source.relative_to(SERVER)}")
            continue
        metadata, _ = frontmatter(source)
        if metadata.get("name") != skill.name or not metadata.get("description"):
            errors.append(f"Invalid skill frontmatter: {source.relative_to(SERVER)}")
        mirror = MIRRORS / skill.name
        if sync and not mirror.exists() and not mirror.is_symlink():
            mirror.symlink_to(f"../../.agents/skills/{skill.name}", target_is_directory=True)
        if not mirror.is_symlink() or mirror.resolve() != skill.resolve():
            errors.append(f"Missing or incorrect Claude Code mirror: {mirror.relative_to(SERVER)}")
    if MIRRORS.is_dir():
        for mirror in sorted(MIRRORS.iterdir()):
            if mirror.is_symlink() and mirror.name not in {skill.name for skill in skills}:
                errors.append(f"Stale mirror: {mirror.relative_to(SERVER)}")
    return [catalog, *sources]


def check_links(documents, errors):
    for document in documents:
        if not document.is_file():
            errors.append(f"Missing document: {document}")
            continue
        for link in local_links(document):
            if link.exists():
                continue
            if not PAIRED and not link.resolve().is_relative_to(SERVER):
                continue
            errors.append(f"Broken local link in {document}: {link}")


def report_changed_sources(notes):
    edited = set()
    for fork in (SERVER, CLIENT) if PAIRED else (SERVER,):
        for args in (("diff", "--name-only", "-z", "HEAD"), ("ls-files", "--others", "--exclude-standard", "-z")):
            result = subprocess.check_output(["git", "-C", str(fork), *args], text=True)
            edited.update((fork / name).resolve() for name in result.split("\0") if name)
    for note in notes:
        if note.name not in ("README.md", "index.md") and any(path.resolve() in edited for path in local_links(note)):
            print(f"Recheck worktree evidence: {note.relative_to(SERVER)}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sync-skills", action="store_true", help="Create missing Claude Code links.")
    parser.add_argument("--status", action="store_true", help="Show Git status for available forks.")
    parser.add_argument("--changed", action="store_true", help="Report memory sources edited in a worktree.")
    args = parser.parse_args()
    errors = []
    notes = check_memory(errors)
    docs = [*notes, *check_skills(args.sync_skills, errors), *GUIDE.glob("*.md")]
    docs.append(SERVER / "AGENTS.md")
    if PAIRED:
        docs.extend((WORKSPACE / "AGENTS.md", WORKSPACE / "CLAUDE.md", CLIENT / "AGENTS.md", CLIENT / "CLAUDE.md"))
        for entry in (WORKSPACE, CLIENT):
            for name in (".agents", ".claude", ".memory"):
                path = entry / name
                if not path.is_symlink() or path.resolve() != SERVER / name:
                    errors.append(f"Missing shared workspace link: {path}")
    check_links(docs, errors)
    if args.status:
        for fork in (SERVER, CLIENT) if PAIRED else (SERVER,):
            print(f"\n{fork.name}", flush=True)
            subprocess.run(["git", "-C", str(fork), "status", "--short", "--branch"], check=True)
    if args.changed:
        report_changed_sources(notes)
    for error in errors:
        print(f"FAIL {error}")
    if errors:
        return 1
    print(f"Britannia Solo agent routes, memory, and skills OK ({'paired' if PAIRED else 'server-only'} workspace).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
