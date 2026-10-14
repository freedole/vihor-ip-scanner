#!/usr/bin/env python3
"""Build compact vendor database (src/Vihor/Resources/oui.txt.gz) from IEEE registry CSVs.

Usage: build_oui.py OUT.gz CSV [CSV ...]
CSV files: oui.csv (MA-L), mam.csv (MA-M), oui36.csv / iab.csv (MA-S).
Output lines: <HEXPREFIX>\t<Vendor>   (prefix length 6/7/9 hex chars)
"""
import csv, gzip, sys, re

def clean(name: str) -> str:
    name = re.sub(r"\s+", " ", name).strip().strip('"')
    return name

def main():
    out, srcs = sys.argv[1], sys.argv[2:]
    rows = {}
    for src in srcs:
        with open(src, newline="", encoding="utf-8", errors="replace") as f:
            for r in csv.DictReader(f):
                a = (r.get("Assignment") or "").strip().upper()
                n = clean(r.get("Organization Name") or "")
                if not a or not n or not re.fullmatch(r"[0-9A-F]{6,9}", a):
                    continue
                if n.lower() == "private":
                    continue
                rows[a] = n
    lines = [f"{k}\t{v}" for k, v in sorted(rows.items())]
    with gzip.open(out, "wt", encoding="utf-8", compresslevel=9) as g:
        g.write("\n".join(lines) + "\n")
    print(f"{len(lines)} entries -> {out}")

if __name__ == "__main__":
    main()
