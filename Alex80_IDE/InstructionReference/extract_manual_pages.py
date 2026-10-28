"""Extract instruction destinations from the official manuals' PDF bookmarks.

Usage: python extract_manual_pages.py z80-um0080.pdf ez80-um0077.pdf ManualPages.tsv
Requires pypdf only when regenerating the checked-in data.
Page numbers are one-based PDF positions, not printed page labels.
"""
import sys
from pathlib import Path
from pypdf import PdfReader


def destinations(reader, outline):
    for node in outline:
        if isinstance(node, list):
            yield from destinations(reader, node)
        else:
            yield node.title, reader.get_destination_page_number(node) + 1


rows = []
for cpu, filename, first, last in [
    ("Z80", sys.argv[1], "LD r, r'", "Customer Support"),
    ("eZ80", sys.argv[2], "ADC A, (HL)", "Opcode Maps"),
]:
    reader = PdfReader(filename)
    active = False
    for title, page in destinations(reader, reader.outline):
        if title == last:
            break
        active |= title == first
        if not active or "Group" in title:
            continue
        title = title.split("—also")[0].strip()
        title = title.replace("DJNZ,", "DJNZ").replace("IN r (C)", "IN r, (C)")
        rows.append(f"{cpu}\t{title}\t{page}")

header = ("# Instruction headings and one-based PDF destinations from official bookmarks.\n"
          "# Z80 UM008011-0816: https://www.zilog.com/docs/z80/um0080.pdf\n"
          "# eZ80 UM007715-0415: https://www.zilog.com/docs/UM0077.pdf\n"
          "# Regenerate with extract_manual_pages.py; no manual prose is included.\n"
          "# CPU\tinstruction pattern\tPDF page\n")
Path(sys.argv[3]).write_text(header + "\n".join(rows) + "\n")
print(f"Extracted {len(rows)} instruction destinations")
