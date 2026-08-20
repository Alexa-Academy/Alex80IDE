Alex80 IDE

Alex80 IDE is an open-source desktop application built with Avalonia UI for developing software for the ALEX80 educational Z80 computer system.

The project aims to provide a modern cross-platform development environment for learning, experimenting with, and programming Z80-based systems.

Features

* Cross-platform support (Windows, macOS, and Linux)
* Source code editor
* Z80 assembler integration (Macro80 and TASM syntax)
* Projects: several source files assembled into a single binary or Arduino array
* Serial communication with ALEX80 hardware
* Memory visualization tools
* Assembly listing view
* Designed for educational use and experimentation

Projects

A program made of several files is described by a project (`.a80proj`), managed from the
panel on the left: add the sources, reorder them with the arrows, and mark one as the main
file with the star. **Assembla** builds every file and merges the
result into a single memory image, which can be written to the board or exported as an
Arduino array. Without a project open, the same button assembles the selected source alone.

Where each file ends up:

* a file with its own `ORG` goes to that address;
* a file without one continues right after the previous file;
* the main file is assembled first, so with no `ORG` anywhere the program starts at 0000;
* everything in between is filled with the configurable fill byte (`00` by default, `FF` for
  a typical EPROM image).

The **simboli condivisi fra i file** setting decides how the files relate to each other:

* off (default) — each file is a self-contained assembly unit. Two files can define the same
  label without clashing, and a stray `END` only ends its own file. This is what you want to
  combine programs that were written independently.
* on — the files are assembled as one source in list order, so a label defined in one file is
  visible in all the others. This is what you want for one program split into main + libraries.

Either way a file can pull in others with `INCLUDE` (or the TASM spelling `.INCLUDE`):

    ; main.asm
            .ORG    $8000
            .INCLUDE "hardware.inc"
            .END

Relative paths are resolved starting from the project folder, then from the folders of the
other sources. Files open in the editor are assembled from the editor content, so unsaved
changes are included too. Overlapping blocks and missing files are reported as errors instead
of silently producing a wrong binary.

Keyboard shortcuts

`Cmd` on macOS, `Ctrl` on Windows and Linux — the modifier is picked from the platform, and
the same shortcuts work in the main window and in the separate editor window.

| Shortcut | Action |
| --- | --- |
| `Cmd/Ctrl + N` | New tab |
| `Cmd/Ctrl + O` | Open file |
| `Cmd/Ctrl + S` | Save |
| `Cmd/Ctrl + Shift + S` | Save as |
| `Cmd/Ctrl + B` | Assemble |
| `Cmd/Ctrl + W` | Close the current tab |

TASM syntax

The **Sintassi TASM** setting (on by default, in the project panel) makes the assembler
accept the syntax used by TASM in addition to the Macro80 one it already supported, so both
styles can be mixed freely:

* Directives with a dot prefix: `.EQU`, `.ORG`, `.END`, `.INCLUDE`, `.DB`, `.DW`, `.DS`...
* The TASM specific pseudo-ops `.BYTE`, `.WORD`, `.TEXT`, `.BLOCK` and `.FILL`
* Labels in the first column don't need a trailing colon
* Hexadecimal numbers with a `$` prefix (`$8000`); a lone `$` is still the location counter
* C style operators: `&`, `|`, `^`, `~`, `<<`, `>>`
* Names such as `TYPE` or `HIGH`, which Macro80 treats as operators, can be used as ordinary
  symbols once the source defines them

With a project open the setting belongs to the project and is saved in the `.a80proj` file;
with no project it is the default applied to single files.

Project Status

Alex80 IDE is currently under active development. Features and user interface may change as the project evolves.

Building

Requirements

* .NET 9 SDK or later

Build

dotnet build

Run

dotnet run

Contributing

Contributions, bug reports, feature requests, and suggestions are welcome.

Please open an issue before starting major changes so that the proposed work can be discussed.

About ALEX80

ALEX80 is an educational project designed to teach the architecture and programming of classic 8-bit microprocessor systems based on the Z80 CPU.

The goal is to make computer architecture, digital electronics, and low-level programming accessible through practical experimentation.

The Alexa Academy YouTube channel describes the Alex80 project and its educational goals.

https://www.youtube.com/@alexaacademyit

License

This project is licensed under the MIT License. See the LICENSE file for details.
