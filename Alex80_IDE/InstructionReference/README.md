# Instruction reference and observed clocks

The Registers pane contains a compact **Istruzione al PC** card. It describes the
**next** instruction at a confirmed stop, not the instruction just executed.
A Z80 register dump in Debug mode, an extended log row reporting debugger WAIT,
or a halted eZ80 register dump requests up to 16 bytes from the board via the
existing `rb` protocol. This read has its own destination: it never replaces the
Memory pane, and Step/ordinary memory reads wait for it to finish. CPU reset,
resume, disconnect and Step invalidate the previous card. Read errors/timeouts
show an unavailable state instead of retaining an obsolete instruction.

The short read wraps correctly at the address-space boundary: 16 bits for Z80,
MBASE:PC within a 64 KiB bank for an eZ80 in Z80 mode, or 24 bits for ADL. The
mode comes from the register snapshot, not from the editor's assembly target.
The read can temporarily stop the manager clock but does not execute user code.
On eZ80, `rb` uses the existing ZDI routine, which restores PC afterwards.

## Manual clock

**Un colpo di clock** is available in Board Settings and the bus log when the
manager advertises `CLKPULSE:ON`. Stop the automatic Arduino clock and disable
Debug first. With **Aggiorna automaticamente i registri** off, each click sends
`clk pulse`, generates exactly one LOW–HIGH–LOW cycle using the normal memory
service, and leaves CLK low, without WAIT or register injection.
The configured frequency determines pulse width;
the interval between clicks determines how slowly the program advances.

The manager replies with one `!PULSE<base64>;` frame, carrying the same nine-byte
sample as `!LOG`. This is also the sole table row for that pulse, even with
logging disabled; ordinary logging does not duplicate it. The IDE blocks another
pulse until the reply and any instruction-byte read finish, and handles errors
or a missing reply with a five-second timeout for this basic pulse.

With the automatic register flag on (the saved default) and `REGUPDATE:ON`
advertised, the IDE sends `clk pulse regs`. A pulse reaching a held first-opcode
read adds the debugger's register-read clocks, restores PC/SP and stops CLK low
again. It returns the original `!PULSE`, an optional `!DUMP`, then `!PULSEEND`.
Pulses inside an instruction do not seek ahead to obtain registers. The IDE
waits for the final marker; the timeout allows for injection at the configured
frequency. Full logging identifies all additional clocks as register reads.
After a step, the same flag selects the normal step/dump or `s noregs` (`!STEP`
completion). Older managers keep their original commands; manual snapshots
require the updated library manager sketch.

The card becomes **Istruzione sul bus**: the first recognizable M1 identifies
the current instruction, and a short `rb` updates its bytes without generating
more clock edges. Intermediate operand, refresh and prefix cycles keep the same
card until the next instruction fetch. If no boundary has been observed yet,
the card asks to advance to a fetch. Between register snapshots the Registers
pane marks its values stale. INC/DEC result predictions are suppressed during
manual clocking, where the observed instruction can already be in progress.
Starting the automatic clock, reset, or disconnect invalidates the card.
This capability is currently implemented by the library's ALEX80µ manager; the
legacy managers and the eZ80 ZDI bridge do not advertise manual clock support.

## Reference information

`InstructionDecoder` is stateless and decodes documented Z80 opcode groups and
CB/ED/DD/FD prefixes, including indexed displacements and repeated prefixes.
It also recognizes eZ80 immediate widths, mode suffixes, and many additional
instructions (IN0/OUT0, MLT, LEA/PEA, mixed-mode controls and fixed encodings in
the eZ80 catalog). Byte sequences beyond the 16-byte window, undefined opcodes
and unsupported variants are explicitly reported; they do not get fabricated
lengths or timings.

`InstructionSemantics` supplies authored Italian operations, descriptions and
flag explanations for instruction families. Block variants link to the manual
for their detailed flag behavior. INC/DEC on 8-bit registers additionally show
a predicted result from the last register snapshot. This prediction changes no
register. The expandable details include a link to the appropriate manual.

**Apri manuale** opens the first page of the instruction's description and
shows its one-based PDF page number on the button. `ManualPages.tsv` records
the destinations from the official Z80/eZ80 PDF bookmarks; printed page labels
are not used as PDF indices. For example, Z80 `JP nn` is printed page 262 but
PDF page 276. Indexed, conditional, 8/16-bit and eZ80 mode-suffix variants are
matched to the corresponding instruction heading, independently of whether a
nominal timing is available. Undocumented/unmatched or incomplete instructions
retain the general manual link instead of inventing a page. The browser's PDF
viewer receives the standard `#page=N` fragment.

`extract_manual_pages.py` regenerates this small catalog from the official
Z80 UM008011-0816 and eZ80 UM007715-0415 PDFs using pypdf. It extracts only
instruction headings and page destinations; neither PDFs nor an extraction
dependency are required in the installed IDE.

Z80 nominal T-states come from the opcode encoding, with separate conditional
and repeated/terminal cases. Machine-cycle counts are shown when the path is
fixed. The ideal millisecond value uses the configured Arduino clock frequency;
it excludes WAIT, debugger instructions and delays due to serial backpressure.
It is not a measurement of execution time.

`Ez80Timings.tsv` records numeric Attributes-table facts from Zilog's
[eZ80 CPU User Manual UM007715-0415](https://www.zilog.com/docs/UM0077.pdf).
The eZ80 manual reports **bus cycles**, not Z80 T-states. ADL-dependent values
and conditional values are kept separate; repeat formulas are identified as
variable totals. Unmatched/unverified catalog variants show unavailable timing
with a manual link. No Z80 timing is substituted for an eZ80 instruction.
The checked-in extraction helper can regenerate the facts from the official PDF;
it is a development tool, not an application runtime dependency. Descriptions
are authored independently rather than copying manual pages.

## Trace metadata

The library manager extends `!LOG<base64>;` from four to nine decoded bytes:

| Offset | Meaning |
| --- | --- |
| 0..3 | Existing address, data and control/phase bits, unchanged |
| 4 bit 0 | WAIT asserted by the debugger |
| 4 bit 1 | Full logging mode |
| 4 bit 2 | First M1 of an instruction (excluding block-repeat iterations), retained through that fetch |
| 4 bit 3 | CPU HALT signal active |
| 5..8 | Big-endian 32-bit physical falling-edge counter since manager boot |

The counter stays internal to the protocol (there is no Clock # column). It
advances even if logging is disabled or compact mode suppresses a
row, and includes reset pulses and injected instructions. It does not advance
while serial output holds the clock level. Older IDEs still read the first four
bytes; this IDE also accepts the old four-byte records, displaying **—** for
T-states that were never supplied.

`ClockTraceTracker` reconstructs instruction-relative T-states only after a
recognizable instruction boundary and with consecutive numbered samples.
WAIT extensions are Tw1, Tw2, ...; the final released WAIT does not become an
extra nominal T-state. Internal register reads, reset and interrupt acknowledge
are kept out of the program's nominal count. Missing samples invalidate the
relative count until the next known boundary. **T?** and **Tw?** mark incomplete
intervals. Clearing the table or changing serial session resets reconstruction.
The summary reports the last complete program interval, WAITs, and subsequent
debugger clocks separately. eZ80 ZDI does not provide a measured bus-clock trace.

Tests in `InstructionReferenceTests` exercise byte decoding, timing variants,
eZ80 mode widths, trace reconstruction and the actual serial-read routing. The
library tests verify counter order, prefix metadata and preservation of the old
four-byte log fields. Physical-board timing still requires hardware validation.
