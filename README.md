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

Registers with manual clock

With the Arduino clock stopped, a counted instruction step always stops before
the next program T1. With manager 1.3.0 (`REGBND:ON`), **Aggiorna automaticamente
i registri** also reads registers after the last T-state. The firmware supplies
the register-read program instead of the next original opcode, restores PC/SP
and stops on the injected jump's final T-state. The next user pulse starts the
original instruction at T1. Its 94 injected clocks are reported separately;
no extra program T1/T2 or WAIT rows are added. The refresh register R advances.
The instruction card keeps the completed instruction and is labelled
**ISTRUZIONE COMPLETATA**, while the Registers pane shows the next PC. The card
changes to the next instruction only when its first T1 appears on the bus.
Firmware without `REGBND` stops without automatic register reads, marking the
last snapshot stale. Automatic refresh continues to apply to Debug/WAIT steps.

On ALEX80µ, **Rileggi dalla scheda** uses the new read at a completed boundary
when `REGBND:ON` is available. With older firmware (`REGSNAP:ON`, manager 1.1.0),
or when already at T1, it can explicitly request the legacy read. This request
reaches the next opcode fetch's T1/T2, reads registers at a temporary 1 kHz and
stops the clock again. The trace preserves T1/T2 and separates injection clocks
and WAIT release from program T-states. The configured frequency is preserved.
Single-clock pulses generate one pulse each. Older firmware
keeps its previous behavior; manual-clock snapshots require updating the
`ALEX80u_manager_terminal` example from the ALEX80u library.

Projects

When no document is open, the center of the IDE offers **Apri un file**,
**Nuovo file**, **Crea un progetto** and **Apri un progetto**, including the
existing platform keyboard shortcuts. With a project already open, it offers
**Aggiungi file al progetto** and reminds you that sources can also be dragged
onto the Project file list. Writing and assembling do not require a connected board.

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

Board state on connection

On opening a serial connection, the IDE sends `cc` and requests `info` to read
the board's current clock source/frequency/running state, memory source, log,
debug and monitor state. It updates the controls without sending these settings
back to the board. Log/debug are cleared for the old session before requesting
the new snapshot, rather than overwriting the received state after connection.
Configuration changes in the IDE also request a fresh snapshot for confirmation.

The bottom status bar shows the connected board's firmware version from the
optional `FWVER` field in `!INFO`. For ALEX80µ, this IDE requires manager firmware
**1.0.0 or newer** (`RequiredMicroFirmwareVersion` in
`MainViewModel.Firmware.cs`). An older version shows **aggiornamento richiesto**;
a missing or malformed version shows **versione da verificare**. Both are
highlighted, and the tooltip states the required version. Existing firmware
remains usable; the warning does not disable board commands. No micro firmware
requirement is applied to modular ALEX80 or experimental eZ80 boards.

The library's `ALEX80u_manager_terminal` example reports `LOG:ON/OFF`,
`DEBUG:ON/OFF` and `MONITOR:ON/OFF` in the existing `!INFO` frame. Use the updated
IDE and upload that updated example to synchronize the log state. Older firmware
remains accepted; missing fields mean the firmware does not report that state,
so it cannot be recovered from `info`. The board's monitor status is read-only.

The bus-log panel has a single toolbar for recording, single-clock pulses,
instruction advance, auto-scroll, following the listing and clearing the log.
Toggle icons show their current state and each icon has an action tooltip.
Following execution is enabled only while an assembled listing with a valid
address map is open; editing or closing that listing disables it immediately,
and assembling again restores availability.
The instruction card sits to the right of the table with a resizable divider,
independent scrolling and expandable details; it is no longer in Registers.
It remains available for eZ80 instruction inspection even though ZDI does not
provide a bus log. There is no **Tutti i clock** selector: the IDE displays the
samples sent by the firmware and preserves compatibility with existing full
and compact frames. FASE still identifies debugger/reset rows, whose bytes
are excluded from program disassembly.

`dotnet run --project AssemblerTests` includes board-state parsing and UI
synchronization tests in addition to assembler regressions. These tests simulate
INFO messages without opening or reconfiguring physical hardware.

Execution in the listing

After assembling, **Segui esecuzione nel listing** in the bus-log panel highlights
the last instruction fetch received from the board and scrolls its listing line
into view. Enable bus logging to receive these updates. Data accesses, refresh
cycles and injected register reads do not move the highlight; reset clears it.
The highlight follows received logs, so serial buffering can delay it at high
clock rates.

Select a log row to inspect its instruction in the latest assembled listing;
operand and data-access rows refer to the preceding instruction fetch. Selecting
a row suspends following. Re-enable **Segui esecuzione nel listing** to return to
the latest fetch. Only logs received after that assembly can navigate its listing.
Editing or reloading the listing invalidates its address map until the next
assembly. The highlight also works in the separate editor window.

Instruction reference while stopped

The Registers pane now starts with **Istruzione al PC**: opcode bytes, operation,
nominal timing, flag effects and a manual link for the next instruction. It reads
the bytes from the connected board at a confirmed stop, including eZ80 ADL and
MBASE addressing, without replacing the Memory pane. The Z80 card distinguishes
nominal T-states from observed clocks; eZ80 uses its own bus-cycle catalog.
Unavailable or unverified variants are clearly indicated.

The log's **T / WAIT** column separates instruction T-states from WAIT extensions.
Scrolling the bus log with the wheel/trackpad, scrollbar or navigation keys turns
off **Scorrimento automatico** so new samples do not pull the view to the bottom.
The switch saves your manual on/off choice for the next startup; an automatic
browsing pause affects only the current session, including when other settings
or the window layout are saved. Turn the switch back on to follow new samples.
The board's falling-edge counter stays internal to detect missing samples.
Continuous logging (**Abilita log continuo**) and manual clock samples use the
same counter: an observed first T-state synchronizes it even at the start of
recording or after a gap. Prefix fetches do not restart the instruction count.
**T?**, **Tw?** and **—**
prevent incomplete/legacy traces from being mistaken for measured timings.
The port selector remembers the ten most recently used ports on which an ALEX80
or eZ80 board identified itself. It preselects the most recent available port at
startup or when devices reappear, preserving an explicit manual selection and
the currently open port. Each port also remembers the baud rate of its last
identified connection and restores it when selected. Failed connection attempts
do not replace that value; older history entries use the existing baud selection
until their first successful connection. Preselection never opens a connection or assumes a board
type: **Connetti** and the board's INFO response are still required. A changed
USB port name must be selected again before it can be remembered.

The top toolbar provides **Clock da Arduino**, **Memoria da Arduino**, the
clock frequency in Hz and a clock start/stop button showing its current state.
Spaced vertical dividers separate connection, clock and action controls. **Assembla**
follows the clock controls, then **Reset**, **NMI** (**Avvia** for eZ80) and
**1 istruzione**. Instruction advance remains available with the Bus Log pane
closed, with a stop button during counted manual stepping. With a running clock,
Z80 stepping uses Debug/WAIT where supported; with the Arduino clock stopped and
Debug off, it completes the instruction using manual pulses.
Frequency and start/stop are enabled only with an Arduino clock source; Arduino
controls are disabled for eZ80. The connected board and firmware are highlighted
in the status bar. A non-modal notification shows connection progress until the
board identifies itself, then confirms its name for 2.5 seconds. If identification
retries finish without a response, it shows a short diagnostic message; the
status bar continues to indicate that the serial port is waiting for a board.
**Debug WAIT** sits beside the manual stepping controls in
Bus Log. CPU mode is below the CPU selector in **Assemblatore**, leaving the
right dock dedicated to Registers.

With the updated library manager, **Un colpo di clock** sends one complete pulse
while the Arduino clock is stopped and Debug is off. It always sends `clk pulse`,
even with automatic register updates enabled: no WAIT or register-read clocks
are added. The instruction card
updates from the bus fetch without WAIT or extra injected clock cycles, even
with logging off.

**1 istruzione** in the top toolbar and Bus Log uses the same command and
availability rules: Debug/WAIT with a running clock, counted manual pulses with
the Arduino clock stopped and Debug off, or the existing ZDI step on eZ80.
In manual-clock mode, it completes the current
instruction using individually acknowledged `clk pulse` requests, leaving CLK
low after each pulse and stopping on the current instruction's final T-state,
before the next T1. From a NOP's T1 it emits T2, T3 and T4 only. On the next click,
one pulse reaches the new T1; the IDE reads fresh opcode bytes with CLK stopped,
then sends only the remaining clocks. The existing Z80 timing decoder includes
prefix costs. Conditional JR/CALL/RET and DJNZ require a current register snapshot
at the instruction's PC to select the duration; repeated blocks and other unknown
durations are reported without guessing or advancing to the next fetch. With an
unknown bus position, use single pulses to synchronize at T1. All pulses appear in the bus log,
including with continuous logging off. **Ferma avanzamento** stops after the
pending pulse; errors, HALT, reset and disconnection also end the operation.
With `REGBND:ON` and automatic register updates enabled, completion requests a
94-clock snapshot and restores PC/SP before the next program T1. Older managers
and a disabled automatic flag leave registers stale without extra clocks.
It requires the same manual-clock capability, stopped automatic clock and
disabled Debug as a single pulse; eZ80 continues using its existing ZDI controls.
External clock cannot be pulsed or stopped by the IDE: it keeps
the existing Debug/WAIT step commands, only where supported by the firmware.

In the Registers pane, **Aggiorna automaticamente i registri** (enabled by
default and saved between sessions) applies register snapshots after steps in
Debug mode with WAIT and after counted instructions with `REGBND:ON`. A raw
single pulse keeps the last snapshot marked as needing an update. The extended
`clk pulse regs` protocol remains readable for compatibility, including its
separate debugger clocks; the manual buttons no longer request it. Missing
program samples still show `T?` rather than inventing a position.

With the flag off, the updated manager steps without injection and manual
clocking uses exactly one complete pulse. **Rileggi dalla scheda** remains
available independently of this flag when the board permits a manual read.
Older firmware keeps its existing commands and may still inject after steps;
the IDE leaves those automatic snapshots unapplied when the flag is off.
The modular ALEX80's destructive monitor dump remains unavailable. eZ80 keeps
the existing ZDI step/dump protocol. Serial polling runs while a step/register
request is pending, including on USB ports with delayed receive notifications;
conflicting reads/steps are disabled until completion, error, reset or timeout.
See [the instruction-reference guide](Alex80_IDE/InstructionReference/README.md)
for timing sources, supported metadata and validation.

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
