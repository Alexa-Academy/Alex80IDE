"""Development helper: extract numeric facts, never prose, from UM007715-0415.
Usage: python extract_ez80_timings.py official-manual.pdf Ez80Timings.tsv
Requires pdfplumber only when regenerating the checked-in data.
"""
import re
import sys
from pathlib import Path
import pdfplumber

rows = []
pattern = re.compile(r"^([A-Z][A-Z0-9]*(?:\.[A-Z]+)?)\s+(?:(.*?)\s+)?(X|0/1|0|1)\s+([0-9]+(?:/[0-9]+)*)\s+(.+)$")
with pdfplumber.open(sys.argv[1]) as document:
    for index in range(87, 383):
        text = document.pages[index].extract_text() or ""
        header = "Mnemonic Operand ADL Mode Cycle Opcode (hex)"
        if header not in text:
            continue
        for line in text.split(header)[-1].splitlines():
            match = pattern.match(line)
            if not match:
                continue
            name, operands, mode, cycles, opcodes = match.groups()
            operands = (operands or "").strip()
            # PDF glyph extraction reads R as I on the LD A,R page.
            if name == "LD" and opcodes == "ED, 5F":
                operands = "A,R"
            rows.append([name, operands, mode, cycles, str(index - 8), opcodes])
# The conditional RET table has a wrapped header and textual cycle condition.
# Preserve both cases rather than feeding them through the numeric-row parser.
rows += [["RET", "cc", "0", "2 falsa / 6 vera", "295", "kk"],
         ["RET", "cc", "1", "2 falsa / 7 vera", "295", "kk"]]
header = ("# Numeric instruction facts from Zilog UM007715-0415 Attributes tables.\n"
          "# https://www.zilog.com/docs/UM0077.pdf\n"
          "# Regenerate with extract_ez80_timings.py; descriptions are authored separately.\n"
          "# mnemonic\toperands\tADL mode\tbus cycles\tprinted page\topcode pattern\n")
Path(sys.argv[2]).write_text(header + "\n".join("\t".join(row) for row in rows) + "\n")
print(f"Extracted {len(rows)} timing records")
