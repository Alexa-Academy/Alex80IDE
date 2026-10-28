using System;
using System.Collections.Generic;
using System.Linq;

namespace Alex80_IDE.InstructionReference;

// Stateless byte decoder: independent of the streaming log disassembler and editor.
public static class InstructionDecoder
{
    public static InstructionInfo Decode(uint address, IReadOnlyList<byte> bytes, bool ez80 = false, bool adl = false)
    {
        var r = new Reader(bytes, address, ez80, adl);
        try { return r.Decode(); }
        catch (IndexOutOfRangeException)
        {
            return new(address, "Byte incompleti", bytes.ToArray(), "", "Servono altri byte per decodificare l'istruzione.",
                "", "Non disponibile", "—", null, "Nessun tempo dedotto da una sequenza incompleta.",
                ez80 ? Ez80Timing.Manual : Z80Manual, false) { AddressDigits = ez80 ? 6 : 4 };
        }
    }
    public const string Z80Manual = "https://www.zilog.com/docs/z80/um0080.pdf";

    private sealed class Reader
    {
        private readonly IReadOnlyList<byte> data;
        private readonly uint address;
        private readonly bool ez80, adl;
        private bool wideImmediate;
        private int at, prefixes;
        private string index = "HL", suffix = "";
        private int? displacement;
        private string mnemonic = "", operation = "";
        private int t, alternate, machines;
        private bool block, known = true;
        private static readonly string[] Reg = { "B", "C", "D", "E", "H", "L", "(HL)", "A" };
        private static readonly string[] Pair = { "BC", "DE", "HL", "SP" };
        private static readonly string[] Conditions = { "NZ", "Z", "NC", "C", "PO", "PE", "P", "M" };
        public Reader(IReadOnlyList<byte> data, uint address, bool ez80, bool adl)
        { this.data = data; this.address = address; this.ez80 = ez80; this.adl = adl; wideImmediate = ez80 && adl; }
        private byte B() { if (at >= data.Count || at >= 16) throw new IndexOutOfRangeException(); return data[at++]; }
        private string N() => $"{B():X2}h";
        private string W()
        {
            uint n = B(); n |= (uint)B() << 8;
            if (wideImmediate) n |= (uint)B() << 16;
            return n.ToString(wideImmediate ? "X6" : "X4") + "h";
        }
        private string Relative()
        {
            int d = (sbyte)B(); uint mask = ez80 && adl ? 0xFFFFFFu : 0xFFFFu;
            uint target = ((address + (uint)at + unchecked((uint)d)) & mask) | (address & ~mask);
            return target.ToString(ez80 ? "X6" : "X4") + "h";
        }
        private string Indexed()
        {
            displacement ??= (sbyte)B();
            return $"({index}{(displacement < 0 ? "-" : "+")}{Math.Abs(displacement.Value):X2}h)";
        }
        private string R(int n, bool plain = false) => n == 6 ? index == "HL" ? "(HL)" : Indexed()
            : !plain && index != "HL" && n is 4 or 5 ? index + (n == 4 ? "H" : "L") : Reg[n];
        private string P(int n, bool af = false) => n == 2 ? index : n == 3 && af ? "AF" : Pair[n];
        private void Set(string text, int clocks, int m = 0, int other = 0)
        { mnemonic = text; t = clocks; machines = m; alternate = other; }

        public InstructionInfo Decode()
        {
            if (ez80 && Ez80Timing.TrySpecific(data, adl) is { } specific)
            {
                at = specific.Length; Set(specific.Mnemonic, 0); return Build();
            }
            byte op = B();
            if (ez80 && op is 0x40 or 0x49 or 0x52 or 0x5B)
            {
                suffix = op switch { 0x40 => ".SIS", 0x49 => ".LIS", 0x52 => ".SIL", _ => ".LIL" };
                wideImmediate = (op & 0x10) != 0; op = B();
            }
            while (op is 0xDD or 0xFD) { index = op == 0xDD ? "IX" : "IY"; prefixes++; op = B(); }
            if (op == 0xCB) DecodeCb();
            else if (op == 0xED) { index = "HL"; DecodeEd(); }
            else DecodeBase(op);
            if (op != 0xCB) t += 4 * prefixes;
            if (alternate > 0 && op != 0xCB) alternate += 4 * prefixes;
            if (prefixes > 0 && machines > 0 && op != 0xCB) machines += prefixes;
            if (suffix != "")
            {
                int space = mnemonic.IndexOf(' ');
                mnemonic = space < 0 ? mnemonic + suffix : mnemonic.Insert(space, suffix);
            }
            return Build();
        }
        private InstructionInfo Build()
        {
            var bytes = data.Take(at).ToArray();
            var semantics = InstructionSemantics.For(mnemonic, operation);
            string timing = alternate == 0 ? $"{t} T-state nominali" : $"{t} / {alternate} T-state ({(block ? "ripetizione / ultima iterazione" : "condizione vera / falsa")})";
            string note = "Tempi Z80 senza WAIT. La stima usa la frequenza impostata; il log può allungare i periodi. Clock osservati e cicli del debugger sono conteggiati separatamente.";
            string machineText = machines > 0 && alternate == 0 ? $"{machines} {(machines == 1 ? "ciclo" : "cicli")} macchina" : "Dipende dal percorso eseguito";
            int? nominal = known && alternate == 0 ? t : null;
            string manual = Z80Manual;
            if (ez80)
            {
                var entry = Ez80Timing.Find(mnemonic, bytes, adl);
                timing = entry is null ? "Cicli bus: dato non disponibile" : $"{entry.Cycles} {(entry.Cycles == "1" ? "ciclo bus nominale" : "cicli bus nominali")}";
                machineText = "eZ80 · " + (adl ? "modo ADL" : "modo Z80"); nominal = null;
                note = "I cicli bus del manuale eZ80 non sono i T-state dello Z80. Clock effettivi dipendono da memoria, WAIT e pipeline; ZDI non fornisce una misura dei clock.";
                manual = entry?.Url ?? Ez80Timing.Manual;
            }
            if (!known) { timing = "Non disponibile"; machineText = "—"; nominal = null; }
            return new(address, mnemonic, bytes, semantics.Operation, semantics.Description, semantics.Flags,
                timing, machineText, nominal, note, manual, known) { AddressDigits = ez80 ? 6 : 4 };
        }
        private void DecodeBase(byte op)
        {
            int x = op >> 6, y = (op >> 3) & 7, z = op & 7, p = y >> 1, q = y & 1;
            bool indexed = index != "HL";
            if (x == 0)
            {
                switch (z)
                {
                    case 0:
                        if (y == 0) Set("NOP", 4, 1);
                        else if (y == 1) Set("EX AF,AF'", 4, 1);
                        else if (y == 2) Set("DJNZ " + Relative(), 13, 0, 8);
                        else if (y == 3) Set("JR " + Relative(), 12, 3);
                        else Set("JR " + Conditions[y - 4] + "," + Relative(), 12, 0, 7);
                        break;
                    case 1:
                        if (q == 0) Set("LD " + P(p) + "," + W(), 10, 3);
                        else Set("ADD " + index + "," + P(p), 11, 3);
                        break;
                    case 2:
                        string operand = p switch { 0 => "(BC)", 1 => "(DE)", _ => "(" + W() + ")" };
                        string register = p == 2 ? index : "A";
                        Set("LD " + (q == 0 ? operand + "," + register : register + "," + operand), p < 2 ? 7 : p == 2 ? 16 : 13, p < 2 ? 2 : p == 2 ? 5 : 4);
                        break;
                    case 3: Set((q == 0 ? "INC " : "DEC ") + P(p), 6, 1); break;
                    case 4: Set("INC " + R(y), y == 6 ? indexed ? 19 : 11 : 4, y == 6 ? indexed ? 5 : 3 : 1); break;
                    case 5: Set("DEC " + R(y), y == 6 ? indexed ? 19 : 11 : 4, y == 6 ? indexed ? 5 : 3 : 1); break;
                    case 6: Set("LD " + R(y) + "," + N(), y == 6 ? indexed ? 15 : 10 : 7, y == 6 ? indexed ? 4 : 3 : 2); break;
                    case 7: Set(new[] { "RLCA", "RRCA", "RLA", "RRA", "DAA", "CPL", "SCF", "CCF" }[y], 4, 1); break;
                }
            }
            else if (x == 1)
            {
                if (op == 0x76) Set("HALT", 4, 1);
                else Set("LD " + R(y, z == 6) + "," + R(z, y == 6), y == 6 || z == 6 ? indexed ? 15 : 7 : 4, y == 6 || z == 6 ? indexed ? 4 : 2 : 1);
            }
            else if (x == 2) Alu(y, R(z), z == 6 ? indexed ? 15 : 7 : 4, z == 6 ? indexed ? 4 : 2 : 1);
            else switch (z)
            {
                case 0: Set("RET " + Conditions[y], 11, 0, 5); break;
                case 1:
                    if (q == 0) Set("POP " + P(p, true), 10, 3);
                    else switch (p)
                    { case 0: Set("RET", 10, 3); break; case 1: Set("EXX", 4, 1); break; case 2: Set("JP (" + index + ")", 4, 1); break; case 3: Set("LD SP," + index, 6, 1); break; }
                    break;
                case 2: Set("JP " + Conditions[y] + "," + W(), 10, 3); break;
                case 3:
                    switch (y)
                    { case 0: Set("JP " + W(), 10, 3); break;
                      case 2: Set("OUT (" + N() + "),A", 11, 3); break;
                      case 3: Set("IN A,(" + N() + ")", 11, 3); break;
                      case 4: Set("EX (SP)," + index, 19, 5); break;
                      case 5: Set("EX DE,HL", 4, 1); break;
                      case 6: Set("DI", 4, 1); break;
                      case 7: Set("EI", 4, 1); break;
                      default: Unknown(op); break; }
                    break;
                case 4: Set("CALL " + Conditions[y] + "," + W(), 17, 0, 10); break;
                case 5:
                    if (q == 0) Set("PUSH " + P(p, true), 11, 3);
                    else if (p == 0) Set("CALL " + W(), 17, 5);
                    else Unknown(op);
                    break;
                case 6: Alu(y, N(), 7, 2); break;
                case 7: Set($"RST {y * 8:X2}h", 11, 3); break;
            }
        }
        private void Alu(int y, string value, int clocks, int m) => Set(new[] { "ADD A,", "ADC A,", "SUB ", "SBC A,", "AND ", "XOR ", "OR ", "CP " }[y] + value, clocks, m);
        private void DecodeCb()
        {
            string? target = index == "HL" ? null : Indexed();
            byte op = B(); int x = op >> 6, y = (op >> 3) & 7, z = op & 7;
            target ??= Reg[z];
            string text = x == 0 ? new[] { "RLC", "RRC", "RL", "RR", "SLA", "SRA", "SLL", "SRL" }[y] + " " + target
                : new[] { "", "BIT", "RES", "SET" }[x] + $" {y}," + target;
            if (index != "HL" && x != 1 && z != 6) text += "," + Reg[z]; // undocumented copy form, decoded faithfully
            Set(text, index != "HL" ? x == 1 ? 20 : 23 : z == 6 ? x == 1 ? 12 : 15 : 8,
                index != "HL" ? x == 1 ? 5 : 6 : z == 6 ? x == 1 ? 3 : 4 : 2);
            if (prefixes > 1) t += 4 * (prefixes - 1);
        }
        private void DecodeEd()
        {
            byte op = B(); int y = (op >> 3) & 7, z = op & 7, p = y >> 1;
            if (ez80)
            {
                if ((op & 0xCF) is 0x02 or 0x03 && p < 3) {
                    string reg = (op & 1) == 0 ? "IX" : "IY"; int d = (sbyte)B();
                    Set($"LEA {Pair[p]},{reg}{(d < 0 ? "-" : "+")}{Math.Abs(d):X2}h", 0); return;
                }
                if ((op & 0xC7) == 0 && y != 6) { Set("IN0 " + Reg[y] + ",(" + N() + ")", 0); return; }
                if ((op & 0xC7) == 1 && y != 6) { Set("OUT0 (" + N() + ")," + Reg[y], 0); return; }
                if (op == 0x64) { Set("TST " + N(), 0); return; }
                if (op == 0x74) { Set("TSTIO " + N(), 0); return; }
                if ((op & 0xC7) == 4 && op < 0x40) { Set("TST " + Reg[y], 0); return; }
                if ((op & 0xCF) == 0x4C) { Set("MLT " + Pair[p], 0); return; }
                var exclusive = new Dictionary<byte,string> { [0x76]="SLP",[0x7D]="STMIX",[0x7E]="RSMIX",[0x83]="OTIM",[0x8B]="OTDM",[0x93]="OTIMR",[0x9B]="OTDMR" };
                if (exclusive.TryGetValue(op, out var name)) { Set(name, 0); return; }
            }
            if ((op & 0xC0) == 0x40)
            {
                switch (z)
                { case 0: Set(y == 6 ? "IN (C)" : "IN " + Reg[y] + ",(C)", 12, 3); break;
                  case 1: Set("OUT (C)," + (y == 6 ? "0" : Reg[y]), 12, 3); break;
                  case 2: Set(((op & 8) == 0 ? "SBC HL," : "ADC HL,") + Pair[p], 15, 4); break;
                  case 3: string n = "(" + W() + ")"; Set("LD " + ((op & 8) == 0 ? n + "," + Pair[p] : Pair[p] + "," + n), 20, 6); break;
                  case 4: Set("NEG", 8, 2); break;
                  case 5: Set(op == 0x4D ? "RETI" : "RETN", 14, 4); break;
                  case 6: Set("IM " + new[] { 0, 0, 1, 2, 0, 0, 1, 2 }[y], 8, 2); break;
                  case 7: var names = new[] { "LD I,A", "LD R,A", "LD A,I", "LD A,R", "RRD", "RLD" }; if (y < 6) Set(names[y], y < 4 ? 9 : 18, y < 4 ? 2 : 5); else Unknown(op); break; }
            }
            else if ((op & 0xE4) == 0xA0)
            {
                string[,] names = { { "LDI", "CPI", "INI", "OUTI" }, { "LDD", "CPD", "IND", "OUTD" }, { "LDIR", "CPIR", "INIR", "OTIR" }, { "LDDR", "CPDR", "INDR", "OTDR" } };
                block = (op & 0x10) != 0;
                Set(names[(op >> 3) & 3, op & 3], block ? 21 : 16, block ? 0 : 4, block ? 16 : 0);
            }
            else Unknown(op);
        }
        private void Unknown(byte op) { Set($"DB {op:X2}h", 0); known = false; }
    }
}
