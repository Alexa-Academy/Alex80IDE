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

The counter advances even if logging is disabled or compact mode suppresses a
row, and includes reset pulses and injected instructions. It does not advance
while serial output holds the clock level. Older IDEs still read the first four
bytes; this IDE also accepts the old four-byte records, displaying **—** for
clock numbers and T-states that were never supplied.

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
