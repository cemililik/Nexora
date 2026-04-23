#!/usr/bin/env python3
"""
Schema drift detector for Nexora's dev tenant database.

Compares each EF entity's public settable properties against the actual PostgreSQL
columns in the dev tenant schema. Exits 0 on a clean match, 1 when drift is found.

Usage:
  python3 tools/check-schema-drift.py [--schema <name>]

Defaults to the dev tenant schema `tenant_00000000-0000-0000-0000-000000000001`.

Why: the project has no EF Core migration files. Schema evolution happens manually in
`src/Nexora.Host/DevelopmentSeed.cs → ApplySchemaUpdatesAsync`. When a developer adds
a new property/entity and forgets the matching ALTER/CREATE there, this script surfaces
the drift before runtime `relation/column does not exist` errors do.

Only flags DB tables/columns that SHOULD exist — entity-mapped settable properties and
entity tables declared in `IEntityTypeConfiguration<T>`. `Id`, audit-base columns
(`CreatedAt`, `UpdatedAt`, `IsDeleted`, etc.) and readonly/expression-bodied properties
are ignored.
"""

import os, re, subprocess, sys, glob, argparse

SCHEMA_DEFAULT = 'tenant_00000000-0000-0000-0000-000000000001'
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', 'src'))
CONFIG_PATTERNS = [
    f'{ROOT}/Modules/*/Infrastructure/Configurations/*.cs',
    f'{ROOT}/Nexora.Infrastructure/**/*.cs',
]

# Columns contributed by base classes (AuditableEntity<T>, Entity<T>) that won't appear
# on the concrete entity file being scanned.
BASE_COLS = {'Id', 'CreatedAt', 'CreatedBy', 'UpdatedAt', 'UpdatedBy',
             'IsDeleted', 'DeletedAt', 'DeletedBy'}


def psql(sql, schema):
    r = subprocess.run(
        ['docker', 'compose', 'exec', '-T', 'postgres',
         'psql', '-U', 'nexora', '-d', 'nexora', '-tAc', sql],
        cwd=os.path.abspath(os.path.join(os.path.dirname(__file__), '..')),
        capture_output=True, text=True, timeout=15
    )
    return [l.strip() for l in r.stdout.splitlines() if l.strip()]


def find_entity_file(name):
    patterns = [
        f'{ROOT}/Modules/*/Domain/Entities/{name}.cs',
        f'{ROOT}/Nexora.Infrastructure/**/{name}.cs',
        f'{ROOT}/Nexora.SharedKernel/**/{name}.cs',
    ]
    for pat in patterns:
        matches = glob.glob(pat, recursive=True)
        if matches:
            return matches[0]
    return None


def get_props(entity_file):
    """Return (settable_props, readonly_props) from a given entity .cs file."""
    with open(entity_file) as f:
        content = f.read()
    settable = set(re.findall(
        r'public\s+[A-Za-z0-9_<>?, .]+?\s+([A-Z][A-Za-z0-9_]*)\s*\{\s*get;\s*(?:private\s+)?(?:init|set);',
        content, re.MULTILINE,
    ))
    readonly = set(re.findall(
        r'public\s+[A-Za-z0-9_<>?, .]+?\s+([A-Z][A-Za-z0-9_]*)\s*\{\s*get;\s*\}',
        content,
    ))
    readonly |= set(re.findall(
        r'public\s+[A-Za-z0-9_<>?, .]+?\s+([A-Z][A-Za-z0-9_]*)\s*=>',
        content,
    ))
    return settable, readonly


def extract_config(config_file):
    with open(config_file) as f:
        c = f.read()
    table_m = re.search(r'ToTable\("([^"]+)"', c)
    entity_m = re.search(r'IEntityTypeConfiguration<\s*([A-Za-z0-9_]+)', c)
    if not (table_m and entity_m):
        return None
    return table_m.group(1), entity_m.group(1)


def main():
    parser = argparse.ArgumentParser(description=__doc__.split('\n\n')[0])
    parser.add_argument('--schema', default=SCHEMA_DEFAULT,
                        help=f'Target schema (default: {SCHEMA_DEFAULT})')
    args = parser.parse_args()

    configs = []
    for pat in CONFIG_PATTERNS:
        configs += glob.glob(pat, recursive=True)
    configs = [c for c in configs
               if 'Configuration' in os.path.basename(c)
               and '/Configurations/' in c]

    drift = []
    for cfg in sorted(configs):
        info = extract_config(cfg)
        if not info:
            continue
        table, entity = info
        ef = find_entity_file(entity)
        if not ef:
            drift.append((table, entity, ['<entity .cs not found>'], []))
            continue

        props, readonly = get_props(ef)
        cols = set(psql(
            "SELECT column_name FROM information_schema.columns "
            f"WHERE table_schema='{args.schema}' AND table_name='{table}';",
            args.schema,
        ))
        if not cols:
            drift.append((table, entity, ['<table missing>'], []))
            continue

        missing = sorted(props - cols - readonly)
        extra = sorted(cols - props - BASE_COLS)
        if missing or extra:
            drift.append((table, entity, missing, extra))

    if not drift:
        print(f"✓ All entity properties match DB columns in schema '{args.schema}'. No drift.")
        return 0

    print(f"✗ Drift detected in schema '{args.schema}':\n")
    for table, entity, missing, extra in drift:
        print(f"  {table}  ({entity})")
        if missing:
            print(f"    missing in DB: {', '.join(missing)}")
        if extra:
            print(f"    extra in DB (not mapped): {', '.join(extra)}")
    print("\nFix drift by adding an entry to DevelopmentSeed.cs → ApplySchemaUpdatesAsync")
    print("per docs/standards/schema-migration.md (or the /add-schema-migration skill).")
    return 1


if __name__ == '__main__':
    sys.exit(main())
