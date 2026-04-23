#!/usr/bin/env python3
"""
Schema drift detector for Nexora's dev tenant database.

Positioning: this is a **post-change drift checker**, not a migration-authoring guide.
Compare each EF entity's public settable properties against the actual PostgreSQL
columns in the dev tenant schema; exit 0 on a clean match, 1 when drift is found.
How you author schema changes is out of scope for this tool — see
`docs/standards/schema-migration.md` (development) or the forthcoming production
migration ADR (production).

Usage:
  python3 tools/check-schema-drift.py [--schema <name>]

Defaults to the dev tenant schema `tenant_00000000-0000-0000-0000-000000000001`.

Heuristics:
  - Readonly/expression-bodied properties and inherited audit columns
    (`Id`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, etc.) are ignored.
  - `[NotMapped]` attributes and collection/navigation property types
    (`IEnumerable<>`, `ICollection<>`, `List<>`, `HashSet<>`) are excluded so
    EF-unmapped properties do not trigger false positives.
"""

import os
import pathlib
import re
import subprocess
import sys
import glob
import argparse

SCHEMA_DEFAULT = 'tenant_00000000-0000-0000-0000-000000000001'
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', 'src'))
REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))

# Single source of truth for the EF configuration folder/segment name. Every scan
# below pivots on this, so a repo-wide rename only touches one line.
CONFIG_DIR_NAME = 'Configurations'

CONFIG_PATTERNS = [
    f'{ROOT}/Modules/*/Infrastructure/{CONFIG_DIR_NAME}/*.cs',
    f'{ROOT}/Nexora.Infrastructure/**/*.cs',
]

# Columns contributed by base classes (AuditableEntity<T>, Entity<T>) that won't
# appear on the concrete entity file being scanned.
BASE_COLS = {'Id', 'CreatedAt', 'CreatedBy', 'UpdatedAt', 'UpdatedBy',
             'IsDeleted', 'DeletedAt', 'DeletedBy'}

# Identifier allow-list — schema / table names passed through to SQL must only
# contain these characters or we refuse to build the query (defence-in-depth even
# though the tool is invoked locally by developers).
SAFE_IDENT = re.compile(r'^[A-Za-z0-9_\-]+$')

# Property types we treat as unmapped navigation collections.
_COLLECTION_TYPES = (
    'IEnumerable<', 'ICollection<', 'IReadOnlyCollection<',
    'IList<', 'IReadOnlyList<', 'List<', 'HashSet<',
)


# ----- SQL boundary ---------------------------------------------------------


def _validate_identifier(value: str, label: str) -> None:
    if not SAFE_IDENT.match(value):
        raise ValueError(
            f"{label} '{value}' contains characters outside [A-Za-z0-9_-]; refusing to build SQL.")


def psql_columns(schema: str, table: str) -> set[str]:
    """Return the set of column names for <schema>.<table>. Validates identifiers
    and surfaces psql failures with stderr instead of silently returning empty."""
    _validate_identifier(schema, 'schema')
    _validate_identifier(table, 'table')

    # psql's `:'name'` variable substitution is only available in -f / interactive
    # mode, not in -tAc one-shot. With identifiers already validated against
    # [A-Za-z0-9_-]+ (no quotes, no spaces, no semicolons), string-interpolating
    # into the query is safe — the validator is the injection barrier.
    sql = (
        "SELECT column_name FROM information_schema.columns "
        f"WHERE table_schema='{schema}' AND table_name='{table}';"
    )
    try:
        proc = subprocess.run(
            ['docker', 'compose', 'exec', '-T', 'postgres',
             'psql', '-U', 'nexora', '-d', 'nexora', '-tAc', sql],
            cwd=REPO_ROOT, capture_output=True, text=True, timeout=15, check=True,
        )
    except subprocess.CalledProcessError as ex:
        # Surface the actual psql error so a failing container, missing DB, or
        # bad credentials don't hide behind an "empty result" read as drift.
        stderr = (ex.stderr or '').strip() or '<no stderr>'
        raise RuntimeError(
            f"psql failed with code {ex.returncode} for {schema}.{table}: {stderr}") from ex

    return {line.strip() for line in proc.stdout.splitlines() if line.strip()}


# ----- File-side scanning ---------------------------------------------------


def collect_configs() -> list[str]:
    """Discover EF Entity Type Configuration source files."""
    configs: list[str] = []
    for pat in CONFIG_PATTERNS:
        configs += glob.glob(pat, recursive=True)
    configs = [c for c in configs
               if 'Configuration' in os.path.basename(c)
               and CONFIG_DIR_NAME in pathlib.PurePath(c).parts]
    return sorted(configs)


def extract_config(config_file: str) -> tuple[str, str] | None:
    """Return (table, entity) tuple or None when the file is not a config."""
    with open(config_file) as f:
        content = f.read()
    table_m = re.search(r'ToTable\("([^"]+)"', content)
    entity_m = re.search(r'IEntityTypeConfiguration<\s*([A-Za-z0-9_]+)', content)
    if not (table_m and entity_m):
        return None
    return table_m.group(1), entity_m.group(1)


def find_entity_file(config_file: str, entity_name: str) -> str | None:
    """
    Locate the entity source file. First try the same module the config file
    lives in; fall back to a global search. Raises when multiple matches exist
    so we don't silently pick the wrong file.
    """
    # Module-scoped search: climb from the config path to the module root
    # (`.../Modules/Nexora.Modules.X/...`) and look only there.
    module_root = None
    head = os.path.dirname(config_file)
    while head and head != ROOT:
        if os.path.basename(os.path.dirname(head)).startswith('Nexora.Modules.'):
            module_root = os.path.dirname(head)
            break
        head = os.path.dirname(head)

    if module_root:
        module_matches = glob.glob(
            f'{module_root}/**/{entity_name}.cs', recursive=True)
        # Exclude configuration files themselves.
        module_matches = [m for m in module_matches if CONFIG_DIR_NAME not in pathlib.PurePath(m).parts]
        if len(module_matches) == 1:
            return module_matches[0]
        if len(module_matches) > 1:
            raise RuntimeError(
                f"Multiple module-local matches for entity '{entity_name}': {module_matches}")

    # Fallback: global search across the whole src/ tree.
    patterns = [
        f'{ROOT}/Modules/**/{entity_name}.cs',
        f'{ROOT}/Nexora.Infrastructure/**/{entity_name}.cs',
        f'{ROOT}/Nexora.SharedKernel/**/{entity_name}.cs',
    ]
    all_matches: list[str] = []
    for pat in patterns:
        all_matches += [m for m in glob.glob(pat, recursive=True)
                        if CONFIG_DIR_NAME not in pathlib.PurePath(m).parts]
    all_matches = list(dict.fromkeys(all_matches))  # de-dup preserving order
    if len(all_matches) == 1:
        return all_matches[0]
    if len(all_matches) > 1:
        raise RuntimeError(
            f"Multiple global matches for entity '{entity_name}': {all_matches}")
    return None


def _strip_notmapped_props(content: str) -> set[str]:
    """Return property names annotated with [NotMapped] (those we should exclude)."""
    notmapped: set[str] = set()
    # Match [NotMapped] (on its own line or inline) followed by a property declaration.
    pat = re.compile(
        r'\[\s*NotMapped\s*\]\s*(?:\[[^\]]*\]\s*)*'
        r'public\s+[A-Za-z0-9_<>?, .]+?\s+([A-Z][A-Za-z0-9_]*)\s*[\{=]',
        re.MULTILINE,
    )
    for m in pat.finditer(content):
        notmapped.add(m.group(1))
    return notmapped


def _is_collection_type(type_fragment: str) -> bool:
    return any(tok in type_fragment for tok in _COLLECTION_TYPES)


def get_props(entity_file: str) -> tuple[set[str], set[str]]:
    """Return (settable_props, readonly_or_unmapped_props) for the entity file.
    Properties that look like nav collections or are annotated [NotMapped] go
    into the "readonly_or_unmapped" bucket so they never trigger "missing in DB"."""
    with open(entity_file) as f:
        content = f.read()

    notmapped = _strip_notmapped_props(content)

    settable_raw = re.findall(
        r'public\s+([A-Za-z0-9_<>?, .]+?)\s+([A-Z][A-Za-z0-9_]*)\s*\{\s*get;\s*(?:private\s+)?(?:init|set);',
        content,
    )
    readonly_raw = re.findall(
        r'public\s+([A-Za-z0-9_<>?, .]+?)\s+([A-Z][A-Za-z0-9_]*)\s*\{\s*get;\s*\}',
        content,
    )
    expr_readonly_raw = re.findall(
        r'public\s+([A-Za-z0-9_<>?, .]+?)\s+([A-Z][A-Za-z0-9_]*)\s*=>',
        content,
    )

    settable: set[str] = set()
    readonly: set[str] = set()

    for type_frag, name in settable_raw:
        if name in notmapped or _is_collection_type(type_frag):
            readonly.add(name)
        else:
            settable.add(name)

    for _, name in readonly_raw + expr_readonly_raw:
        readonly.add(name)

    return settable, readonly


# ----- Drift orchestration --------------------------------------------------


def check_config_drift(cfg: str, schema: str):
    """Returns (table, entity, missing, extra) when drift exists; None otherwise."""
    info = extract_config(cfg)
    if not info:
        return None
    table, entity = info
    ef = find_entity_file(cfg, entity)
    if ef is None:
        return (table, entity, ['<entity .cs not found>'], [])

    props, readonly = get_props(ef)
    cols = psql_columns(schema, table)
    if not cols:
        return (table, entity, ['<table missing>'], [])

    missing = sorted(props - cols - readonly)
    extra = sorted(cols - props - BASE_COLS)
    if missing or extra:
        return (table, entity, missing, extra)
    return None


def report_drift(drift: list, schema: str) -> int:
    if not drift:
        print(f"✓ All entity properties match DB columns in schema '{schema}'. No drift.")
        return 0
    print(f"✗ Drift detected in schema '{schema}':\n")
    for table, entity, missing, extra in drift:
        print(f"  {table}  ({entity})")
        if missing:
            print(f"    missing in DB: {', '.join(missing)}")
        if extra:
            print(f"    extra in DB (not mapped): {', '.join(extra)}")
    print("\nDrift reported. Author the schema change via the path your environment uses")
    print("(dev: docs/standards/schema-migration.md; prod: the forthcoming migration ADR).")
    return 1


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.split('\n\n')[0])
    parser.add_argument('--schema', default=SCHEMA_DEFAULT,
                        help=f'Target schema (default: {SCHEMA_DEFAULT})')
    args = parser.parse_args()

    _validate_identifier(args.schema, 'schema')

    configs = collect_configs()
    drift = [r for r in (check_config_drift(cfg, args.schema) for cfg in configs)
             if r is not None]
    return report_drift(drift, args.schema)


if __name__ == '__main__':
    sys.exit(main())
