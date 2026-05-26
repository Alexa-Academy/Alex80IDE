using System;
using System.Collections.Generic;

namespace Alex80Supervisor;

public enum OpcodeType
{
    Address,  
    Value1,   
    Value2,   
    TwoOpcodes,  
    TwoOpcodesDisplacement, 
    TwoOpcodesDisplacementN,  
    TwoOpcodesDisplacementBit,  
    Bit,   
    None   
}

public struct Opcode
{
    public byte OpCode1 { get; set; }
    public byte OpCode2 { get; set; }  // Nel caso di istruzione multibyte
    public byte OpCode3 { get; set; }  // Si usa con le istruzioni da 4 byte
    public string Mnemonic { get; set; }
    public int Bytes { get; set; }
    public int TStates1 { get; set; }
    public int TStates2 { get; set; }
    public OpcodeType Type { get; set; }

    public Opcode(byte opCode1, byte opCode2, byte opCode3, string mnemonic, int bytes, int tStates1, int tStates2, OpcodeType type)
    {
        OpCode1 = opCode1;
        OpCode2 = opCode2;
        OpCode3 = opCode3;
        Mnemonic = mnemonic;
        Bytes = bytes;
        TStates1 = tStates1;
        TStates2 = tStates2;
        Type = type;
    }
}

public class Disassembler
{
    private static readonly List<Opcode> opcodes = new List<Opcode>
    {
        new(0x8e, 0, 0, "adc A, (HL)", 1, 7, 0, OpcodeType.None),
        new(0xdd, 0x8e, 0, "adc A, (IX+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xfd, 0x8e, 0, "adc A, (IY+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xce, 0, 0, "adc A, {0}", 2, 7, 0, OpcodeType.Value1),
        new(0x88, 0, 0, "adc A, B", 1, 4, 0, OpcodeType.None),
        new(0x89, 0, 0, "adc A, C", 1, 4, 0, OpcodeType.None),
        new(0x8a, 0, 0, "adc A, D", 1, 4, 0, OpcodeType.None),
        new(0x8b, 0, 0, "adc A, E", 1, 4, 0, OpcodeType.None),
        new(0x8c, 0, 0, "adc A, H", 1, 4, 0, OpcodeType.None),
        new(0x8d, 0, 0, "adc A, L", 1, 4, 0, OpcodeType.None),
        new(0x8f, 0, 0, "adc A, A", 1, 4, 0, OpcodeType.None),
        new(0xed, 0x4a, 0, "adc HL, BC", 2, 15, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0x5a, 0, "adc HL, DE", 2, 15, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0x6a, 0, "adc HL, HL", 2, 15, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0x7a, 0, "adc HL, SP", 2, 15, 0, OpcodeType.TwoOpcodes),

        new(0x86, 0, 0, "add A, (HL)", 1, 7, 0, OpcodeType.None),
        new(0xdd, 0x86, 0, "add A, (IX+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xfd, 0x86, 0, "add A, (IY+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xc6, 0, 0, "add A, {0}", 2, 7, 0, OpcodeType.Value1),
        new(0x80, 0, 0, "add A, B", 1, 4, 0, OpcodeType.None),
        new(0x81, 0, 0, "add A, C", 1, 4, 0, OpcodeType.None),
        new(0x82, 0, 0, "add A, D", 1, 4, 0, OpcodeType.None),
        new(0x83, 0, 0, "add A, E", 1, 4, 0, OpcodeType.None),
        new(0x84, 0, 0, "add A, H", 1, 4, 0, OpcodeType.None),
        new(0x85, 0, 0, "add A, L", 1, 4, 0, OpcodeType.None),
        new(0x87, 0, 0, "add A, A", 1, 4, 0, OpcodeType.None),
        new(0x09, 0, 0, "add HL, BC", 1, 11, 0, OpcodeType.None),
        new(0x19, 0, 0, "add HL, DE", 1, 11, 0, OpcodeType.None),
        new(0x29, 0, 0, "add HL, HL", 1, 11, 0, OpcodeType.None),
        new(0x39, 0, 0, "add HL, SP", 1, 11, 0, OpcodeType.None),
        new(0xdd, 0x09, 0, "add IX, BC", 2, 15, 0, OpcodeType.TwoOpcodes),
        new(0xdd, 0x19, 0, "add IX, DE", 2, 15, 0, OpcodeType.TwoOpcodes),
        new(0xdd, 0x29, 0, "add IX, IX", 2, 15, 0, OpcodeType.TwoOpcodes),
        new(0xdd, 0x39, 0, "add IX, SP", 2, 15, 0, OpcodeType.TwoOpcodes),
        new(0xfd, 0x09, 0, "add IY, BC", 2, 15, 0, OpcodeType.TwoOpcodes),
        new(0xfd, 0x19, 0, "add IY, DE", 2, 15, 0, OpcodeType.TwoOpcodes),
        new(0xfd, 0x29, 0, "add IY, IY", 2, 15, 0, OpcodeType.TwoOpcodes),
        new(0xfd, 0x39, 0, "add IY, SP", 2, 15, 0, OpcodeType.TwoOpcodes),

        new(0xa6, 0, 0, "and (HL)", 1, 7, 0, OpcodeType.None),
        new(0xdd, 0xa6, 0, "and A, (IX+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xfd, 0xa6, 0, "and A, (IY+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xe6, 0, 0, "and {0}", 2, 7, 0, OpcodeType.Value1),
        new(0xa0, 0, 0, "and A, B", 1, 4, 0, OpcodeType.None),
        new(0xa1, 0, 0, "and A, C", 1, 4, 0, OpcodeType.None),
        new(0xa2, 0, 0, "and A, D", 1, 4, 0, OpcodeType.None),
        new(0xa3, 0, 0, "and A, E", 1, 4, 0, OpcodeType.None),
        new(0xa4, 0, 0, "and A, H", 1, 4, 0, OpcodeType.None),
        new(0xa5, 0, 0, "and A, L", 1, 4, 0, OpcodeType.None),
        new(0xa7, 0, 0, "and A, A", 1, 4, 0, OpcodeType.None),

        new(0xcb, 0x46, 0, "bit {0}, (HL)", 2, 12, 0, OpcodeType.Bit),
        new(0xdd, 0xcb, 0x46, "bit {0}, (IX+{1})", 4, 20, 0, OpcodeType.TwoOpcodesDisplacementBit),
        new(0xfd, 0xcb, 0x46, "bit {0}, (IY+{1})", 4, 20, 0, OpcodeType.TwoOpcodesDisplacementBit),
        new(0xcb, 0x40, 0, "bit {0}, B", 2, 8, 0, OpcodeType.Bit),
        new(0xcb, 0x41, 0, "bit {0}, C", 2, 8, 0, OpcodeType.Bit),
        new(0xcb, 0x42, 0, "bit {0}, D", 2, 8, 0, OpcodeType.Bit),
        new(0xcb, 0x43, 0, "bit {0}, E", 2, 8, 0, OpcodeType.Bit),
        new(0xcb, 0x44, 0, "bit {0}, H", 2, 8, 0, OpcodeType.Bit),
        new(0xcb, 0x45, 0, "bit {0}, L", 2, 8, 0, OpcodeType.Bit),
        new(0xcb, 0x47, 0, "bit {0}, A", 2, 8, 0, OpcodeType.Bit),

        new(0xcd, 0, 0, "call {0}", 3, 17, 0, OpcodeType.Address),
        new(0xdc, 0, 0, "call C, {0}", 3, 17, 10, OpcodeType.Address),
        new(0xfc, 0, 0, "call M, {0}", 3, 17, 10, OpcodeType.Address),
        new(0xd4, 0, 0, "call NC, {0}", 3, 17, 10, OpcodeType.Address),
        new(0xc4, 0, 0, "call NZ, {0}", 3, 17, 10, OpcodeType.Address),
        new(0xf4, 0, 0, "call P, {0}", 3, 17, 10, OpcodeType.Address),
        new(0xec, 0, 0, "call PE, {0}", 3, 17, 10, OpcodeType.Address),
        new(0xe4, 0, 0, "call PO, {0}", 3, 17, 10, OpcodeType.Address),
        new(0xcc, 0, 0, "call Z, {0}", 3, 17, 10, OpcodeType.Address),

        new(0x3f, 0, 0, "ccf", 1, 4, 0, OpcodeType.None),

        new(0xbe, 0, 0, "cp (HL)", 1, 7, 0, OpcodeType.None),
        new(0xdd, 0xbe, 0, "cp (IX+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xfd, 0xbe, 0, "cp (IY+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xfe, 0, 0, "cp {0}", 2, 7, 0, OpcodeType.Value1),
        new(0xb8, 0, 0, "cp B", 1, 4, 0, OpcodeType.None),
        new(0xb9, 0, 0, "cp C", 1, 4, 0, OpcodeType.None),
        new(0xba, 0, 0, "cp D", 1, 4, 0, OpcodeType.None),
        new(0xbb, 0, 0, "cp E", 1, 4, 0, OpcodeType.None),
        new(0xbc, 0, 0, "cp H", 1, 4, 0, OpcodeType.None),
        new(0xbd, 0, 0, "cp L", 1, 4, 0, OpcodeType.None),
        new(0xbf, 0, 0, "cp A", 1, 4, 0, OpcodeType.None),
        new(0xed, 0xa9, 0, "cpd", 2, 16, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0xb9, 0, "cpdr", 2, 21, 16, OpcodeType.TwoOpcodes),
        new(0xed, 0xa1, 0, "cpi", 2, 16, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0xb1, 0, "cpir", 2, 21, 16, OpcodeType.TwoOpcodes),
        new(0x2f, 0, 0, "cpl", 1, 4, 0, OpcodeType.None),

        new(0x27, 0, 0, "daa", 1, 4, 0, OpcodeType.None),

        new(0x35, 0, 0, "dec (HL)", 1, 11, 0, OpcodeType.None),
        new(0xdd, 0x35, 0, "dec (IX+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xfd, 0x35, 0, "dec (IY+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0x3d, 0, 0, "dec A", 1, 4, 0, OpcodeType.None),
        new(0x05, 0, 0, "dec B", 1, 4, 0, OpcodeType.None),
        new(0x0b, 0, 0, "dec BC", 1, 6, 0, OpcodeType.None),
        new(0x0d, 0, 0, "dec C", 1, 4, 0, OpcodeType.None),
        new(0x15, 0, 0, "dec D", 1, 4, 0, OpcodeType.None),
        new(0x1b, 0, 0, "dec DE", 1, 6, 0, OpcodeType.None),
        new(0x1d, 0, 0, "dec E", 1, 4, 0, OpcodeType.None),
        new(0x25, 0, 0, "dec H", 1, 4, 0, OpcodeType.None),
        new(0x2b, 0, 0, "dec HL", 1, 6, 0, OpcodeType.None),
        new(0xdd, 0x2b, 0, "dec IX", 2, 10, 0, OpcodeType.TwoOpcodes),
        new(0xfd, 0x2b, 0, "dec IY", 2, 10, 0, OpcodeType.TwoOpcodes),
        new(0x2d, 0, 0, "dec L", 1, 4, 0, OpcodeType.None),
        new(0x3b, 0, 0, "dec SP", 1, 6, 0, OpcodeType.None),

        new(0xf3, 0, 0, "di", 1, 4, 0, OpcodeType.None),

        new(0x10, 0, 0, "djnz {0}", 2, 13, 8, OpcodeType.Address),

        new(0xfb, 0, 0, "ei", 1, 4, 0, OpcodeType.None),

        new(0xe3, 0, 0, "ex (SP), HL", 1, 19, 0, OpcodeType.None),
        new(0xdd, 0xe3, 0, "ex (SP), IX", 2, 23, 0, OpcodeType.TwoOpcodes),
        new(0xfd, 0xe3, 0, "ex (SP), IY", 2, 23, 0, OpcodeType.TwoOpcodes),
        new(0x08, 0, 0, "ex AF, AF'", 1, 4, 0, OpcodeType.None),
        new(0xeb, 0, 0, "ex DE, HL", 1, 4, 0, OpcodeType.None),
        new(0xd9, 0, 0, "exx", 1, 4, 0, OpcodeType.None),

        new(0x76, 0, 0, "halt", 1, 4, 0, OpcodeType.None),

        new(0xed, 0x46, 0, "IM 0", 2, 8, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0x56, 0, "IM 1", 2, 8, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0x5e, 0, "IM 2", 2, 8, 0, OpcodeType.TwoOpcodes),

        new(0xed, 0x78, 0, "in A, (C)", 2, 12, 0, OpcodeType.TwoOpcodes),
        new(0xdb, 0, 0, "in A, ({0})", 2, 11, 0, OpcodeType.Value1),
        new(0xed, 0x40, 0, "in B, (C)", 2, 12, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0x48, 0, "in C, (C)", 2, 12, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0x50, 0, "in D, (C)", 2, 12, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0x58, 0, "in E, (C)", 2, 12, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0x60, 0, "in H, (C)", 2, 12, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0x68, 0, "in L, (C)", 2, 12, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0x70, 0, "in F, (C)", 2, 12, 0, OpcodeType.TwoOpcodes),

        new(0x34, 0, 0, "inc (HL)", 1, 11, 0, OpcodeType.None),
        new(0xdd, 0x34, 0, "inc (IX+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xfd, 0x34, 0, "inc (IY+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0x3c, 0, 0, "inc A", 1, 4, 0, OpcodeType.None),
        new(0x04, 0, 0, "inc B", 1, 4, 0, OpcodeType.None),
        new(0x03, 0, 0, "inc BC", 1, 6, 0, OpcodeType.None),
        new(0x0c, 0, 0, "inc C", 1, 4, 0, OpcodeType.None),
        new(0x14, 0, 0, "inc D", 1, 4, 0, OpcodeType.None),
        new(0x13, 0, 0, "inc DE", 1, 6, 0, OpcodeType.None),
        new(0x1c, 0, 0, "inc E", 1, 4, 0, OpcodeType.None),
        new(0x24, 0, 0, "inc H", 1, 4, 0, OpcodeType.None),
        new(0x23, 0, 0, "inc HL", 1, 6, 0, OpcodeType.None),
        new(0xdd, 0x23, 0, "inc IX", 2, 10, 0, OpcodeType.TwoOpcodes),
        new(0xfd, 0x23, 0, "inc IY", 2, 10, 0, OpcodeType.TwoOpcodes),
        new(0x2c, 0, 0, "inc L", 1, 4, 0, OpcodeType.None),
        new(0x33, 0, 0, "inc SP", 1, 6, 0, OpcodeType.None),

        new(0xed, 0xaa, 0, "ind", 2, 16, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0xba, 0, "indr", 2, 21, 16, OpcodeType.TwoOpcodes),
        new(0xed, 0xa2, 0, "ini", 2, 16, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0xb2, 0, "inir", 2, 21, 16, OpcodeType.TwoOpcodes),

        new(0xc3, 0, 0, "jp {0}", 3, 10, 0, OpcodeType.Address),
        new(0xe9, 0, 0, "jp (HL)", 1, 4, 0, OpcodeType.None),
        new(0xdd, 0xe9, 0, "jp (IX)", 2, 8, 0, OpcodeType.TwoOpcodes),
        new(0xfd, 0xe9, 0, "jp (IY)", 2, 8, 0, OpcodeType.TwoOpcodes),
        new(0xda, 0, 0, "jp C, {0}", 3, 10, 0, OpcodeType.Address),
        new(0xfa, 0, 0, "jp M, {0}", 3, 10, 0, OpcodeType.Address),
        new(0xd2, 0, 0, "jp NC, {0}", 3, 10, 0, OpcodeType.Address),
        new(0xc2, 0, 0, "jp NZ, {0}", 3, 10, 0, OpcodeType.Address),
        new(0xf2, 0, 0, "jp P, {0}", 3, 10, 0, OpcodeType.Address),
        new(0xea, 0, 0, "jp PE, {0}", 3, 10, 0, OpcodeType.Address),
        new(0xe2, 0, 0, "jp PO, {0}", 3, 10, 0, OpcodeType.Address),
        new(0xca, 0, 0, "jp Z, {0}", 3, 10, 0, OpcodeType.Address),

        new(0x18, 0, 0, "jr {0}", 2, 12, 0, OpcodeType.Address),
        new(0x38, 0, 0, "jr C, {0}", 2, 12, 7, OpcodeType.Address),
        new(0x30, 0, 0, "jr NC, {0}", 2, 12, 7, OpcodeType.Address),
        new(0x20, 0, 0, "jr NZ, {0}", 2, 12, 7, OpcodeType.Address),
        new(0x28, 0, 0, "jr Z, {0}", 2, 12, 7, OpcodeType.Address),

        new(0x02, 0, 0, "ld (BC), A", 1, 7, 0, OpcodeType.None),
        new(0x12, 0, 0, "ld (DE), A", 1, 7, 0, OpcodeType.None),
        new(0x36, 0, 0, "ld (HL), {0}", 2, 10, 0, OpcodeType.Value1),
        new(0x70, 0, 0, "ld (HL), B", 1, 10, 0, OpcodeType.None),
        new(0x71, 0, 0, "ld (HL), C", 1, 10, 0, OpcodeType.None),
        new(0x72, 0, 0, "ld (HL), D", 1, 10, 0, OpcodeType.None),
        new(0x73, 0, 0, "ld (HL), E", 1, 10, 0, OpcodeType.None),
        new(0x74, 0, 0, "ld (HL), H", 1, 10, 0, OpcodeType.None),
        new(0x75, 0, 0, "ld (HL), L", 1, 10, 0, OpcodeType.None),
        new(0x77, 0, 0, "ld (HL), A", 1, 10, 0, OpcodeType.None),
        new(0xdd, 0x36, 0, "ld (IX+{0}), {1}", 4, 19, 0, OpcodeType.TwoOpcodesDisplacementN),
        new(0xdd, 0x70, 0, "ld (IX+{0}), B", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xdd, 0x71, 0, "ld (IX+{0}), C", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xdd, 0x72, 0, "ld (IX+{0}), D", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xdd, 0x73, 0, "ld (IX+{0}), E", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xdd, 0x74, 0, "ld (IX+{0}), H", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xdd, 0x75, 0, "ld (IX+{0}), L", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xdd, 0x77, 0, "ld (IX+{0}), A", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xfd, 0x36, 0, "ld (IY+{0}), {1}", 4, 19, 0, OpcodeType.TwoOpcodesDisplacementN),
        new(0xfd, 0x70, 0, "ld (IY+{0}), B", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xfd, 0x71, 0, "ld (IY+{0}), C", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xfd, 0x72, 0, "ld (IY+{0}), D", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xfd, 0x73, 0, "ld (IY+{0}), E", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xfd, 0x74, 0, "ld (IY+{0}), H", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xfd, 0x75, 0, "ld (IY+{0}), L", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xfd, 0x77, 0, "ld (IY+{0}), A", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0x32, 0, 0, "ld ({0}), A", 3, 13, 0, OpcodeType.Address),
        new(0xed, 0x43, 0, "ld ({0}), BC", 4, 20, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0x53, 0, "ld ({0}), DE", 4, 20, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0x63, 0, "ld ({0}), HL", 4, 20, 0, OpcodeType.TwoOpcodes),
        new(0xdd, 0x22, 0, "ld ({0}), IX", 4, 20, 0, OpcodeType.TwoOpcodes),
        new(0xfd, 0x22, 0, "ld ({0}), IY", 4, 20, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0x73, 0, "ld ({0}), SP", 4, 20, 0, OpcodeType.TwoOpcodes),
        new(0x22, 0, 0, "ld ({0}), HL", 3, 16, 0, OpcodeType.Address),
        new(0x0a, 0, 0, "ld A, (BC)", 1, 7, 0, OpcodeType.None),
        new(0x1a, 0, 0, "ld A, (DE)", 1, 7, 0, OpcodeType.None),
        new(0x7e, 0, 0, "ld A, (HL)", 1, 7, 0, OpcodeType.None),
        new(0xdd, 0x7e, 0, "ld A, (IX+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xfd, 0x7e, 0, "ld A, (IY+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0x3a, 0, 0, "ld A, ({0})", 3, 13, 0, OpcodeType.Address),
        new(0x3e, 0, 0, "ld A, {0}", 2, 7, 0, OpcodeType.Value1),
        new(0x78, 0, 0, "ld A, B", 1, 4, 0, OpcodeType.None),
        new(0x79, 0, 0, "ld A, C", 1, 4, 0, OpcodeType.None),
        new(0x7a, 0, 0, "ld A, D", 1, 4, 0, OpcodeType.None),
        new(0x7b, 0, 0, "ld A, E", 1, 4, 0, OpcodeType.None),
        new(0x7c, 0, 0, "ld A, H", 1, 4, 0, OpcodeType.None),
        new(0x7d, 0, 0, "ld A, L", 1, 4, 0, OpcodeType.None),
        new(0x7f, 0, 0, "ld A, A", 1, 4, 0, OpcodeType.None),
        new(0xed, 0x57, 0, "ld A, I", 2, 9, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0x5f, 0, "ld A, R", 2, 9, 0, OpcodeType.TwoOpcodes),
        new(0x46, 0, 0, "ld B, (HL)", 1, 7, 0, OpcodeType.None),
        new(0xdd, 0x46, 0, "ld B, (IX+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xfd, 0x46, 0, "ld B, (IY+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0x06, 0, 0, "ld B, {0}", 2, 7, 0, OpcodeType.Value1),
        new(0x40, 0, 0, "ld B, B", 1, 4, 0, OpcodeType.None),
        new(0x41, 0, 0, "ld B, C", 1, 4, 0, OpcodeType.None),
        new(0x42, 0, 0, "ld B, D", 1, 4, 0, OpcodeType.None),
        new(0x43, 0, 0, "ld B, E", 1, 4, 0, OpcodeType.None),
        new(0x44, 0, 0, "ld B, H", 1, 4, 0, OpcodeType.None),
        new(0x45, 0, 0, "ld B, L", 1, 4, 0, OpcodeType.None),
        new(0x47, 0, 0, "ld B, A", 1, 4, 0, OpcodeType.None),
        new(0xed, 0x4b, 0, "ld BC, ({0})", 4, 20, 0, OpcodeType.TwoOpcodes),
        new(0x01, 0, 0, "ld BC, {0}", 3, 10, 0, OpcodeType.Value2),
        new(0x4e, 0, 0, "ld C, (HL)", 1, 7, 0, OpcodeType.None),
        new(0xdd, 0x4e, 0, "ld C, (IX+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xfd, 0x4e, 0, "ld C, (IY+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0x0e, 0, 0, "ld C, {0}", 2, 7, 0, OpcodeType.Value1),
        new(0x48, 0, 0, "ld C, B", 1, 4, 0, OpcodeType.None),
        new(0x49, 0, 0, "ld C, C", 1, 4, 0, OpcodeType.None),
        new(0x4a, 0, 0, "ld C, D", 1, 4, 0, OpcodeType.None),
        new(0x4b, 0, 0, "ld C, E", 1, 4, 0, OpcodeType.None),
        new(0x4c, 0, 0, "ld C, H", 1, 4, 0, OpcodeType.None),
        new(0x4d, 0, 0, "ld C, L", 1, 4, 0, OpcodeType.None),
        new(0x4f, 0, 0, "ld C, A", 1, 4, 0, OpcodeType.None),
        new(0x56, 0, 0, "ld D, (HL)", 1, 7, 0, OpcodeType.None),
        new(0xdd, 0x56, 0, "ld D, (IX+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xfd, 0x56, 0, "ld D, (IY+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0x16, 0, 0, "ld D, {0}", 2, 7, 0, OpcodeType.Value1),
        new(0x50, 0, 0, "ld D, B", 1, 4, 0, OpcodeType.None),
        new(0x51, 0, 0, "ld D, C", 1, 4, 0, OpcodeType.None),
        new(0x52, 0, 0, "ld D, D", 1, 4, 0, OpcodeType.None),
        new(0x53, 0, 0, "ld D, E", 1, 4, 0, OpcodeType.None),
        new(0x54, 0, 0, "ld D, H", 1, 4, 0, OpcodeType.None),
        new(0x55, 0, 0, "ld D, L", 1, 4, 0, OpcodeType.None),
        new(0x57, 0, 0, "ld D, A", 1, 4, 0, OpcodeType.None),
        new(0xed, 0x5b, 0, "ld DE, ({0})", 4, 20, 0, OpcodeType.TwoOpcodes),
        new(0x11, 0, 0, "ld DE, {0}", 3, 10, 0, OpcodeType.Value2),
        new(0x5e, 0, 0, "ld E, (HL)", 1, 7, 0, OpcodeType.None),
        new(0xdd, 0x5e, 0, "ld E, (IX+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xfd, 0x5e, 0, "ld E, (IY+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0x1e, 0, 0, "ld E, {0}", 2, 7, 0, OpcodeType.Value1),
        new(0x58, 0, 0, "ld E, B", 1, 4, 0, OpcodeType.None),
        new(0x59, 0, 0, "ld E, C", 1, 4, 0, OpcodeType.None),
        new(0x5a, 0, 0, "ld E, D", 1, 4, 0, OpcodeType.None),
        new(0x5b, 0, 0, "ld E, E", 1, 4, 0, OpcodeType.None),
        new(0x5c, 0, 0, "ld E, H", 1, 4, 0, OpcodeType.None),
        new(0x5d, 0, 0, "ld E, L", 1, 4, 0, OpcodeType.None),
        new(0x5f, 0, 0, "ld E, A", 1, 4, 0, OpcodeType.None),
        new(0x66, 0, 0, "ld H, (HL)", 1, 7, 0, OpcodeType.None),
        new(0xdd, 0x66, 0, "ld H, (IX+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xfd, 0x66, 0, "ld H, (IY+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0x26, 0, 0, "ld H, {0}", 2, 7, 0, OpcodeType.Value1),
        new(0x60, 0, 0, "ld H, B", 1, 4, 0, OpcodeType.None),
        new(0x61, 0, 0, "ld H, C", 1, 4, 0, OpcodeType.None),
        new(0x62, 0, 0, "ld H, D", 1, 4, 0, OpcodeType.None),
        new(0x63, 0, 0, "ld H, E", 1, 4, 0, OpcodeType.None),
        new(0x64, 0, 0, "ld H, H", 1, 4, 0, OpcodeType.None),
        new(0x65, 0, 0, "ld H, L", 1, 4, 0, OpcodeType.None),
        new(0x67, 0, 0, "ld H, A", 1, 4, 0, OpcodeType.None),
        new(0x2a, 0, 0, "ld HL, ({0})", 3, 16, 0, OpcodeType.Address),
        new(0xed, 0x06, 0, "ld HL, ({0})", 4, 20, 0, OpcodeType.TwoOpcodes),
        new(0x21, 0, 0, "ld HL, {0}", 3, 10, 0, OpcodeType.Value2),
        new(0xed, 0x47, 0, "ld I, A", 2, 9, 0, OpcodeType.TwoOpcodes),
        new(0xdd, 0x2a, 0, "ld IX, ({0})", 4, 20, 0, OpcodeType.TwoOpcodes),
        new(0xdd, 0x21, 0, "ld IX, {0}", 4, 14, 0, OpcodeType.TwoOpcodes),
        new(0xfd, 0x2a, 0, "ld IY, ({0})", 4, 20, 0, OpcodeType.TwoOpcodes),
        new(0xfd, 0x21, 0, "ld IY, {0}", 4, 14, 0, OpcodeType.TwoOpcodes),
        new(0x6e, 0, 0, "ld L, (HL)", 1, 7, 0, OpcodeType.None),
        new(0xdd, 0x6e, 0, "ld L, (IX+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xfd, 0x6e, 0, "ld L, (IY+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0x2e, 0, 0, "ld L, {0}", 2, 7, 0, OpcodeType.Value1),
        new(0x68, 0, 0, "ld L, B", 1, 4, 0, OpcodeType.None),
        new(0x69, 0, 0, "ld L, C", 1, 4, 0, OpcodeType.None),
        new(0x6a, 0, 0, "ld L, D", 1, 4, 0, OpcodeType.None),
        new(0x6b, 0, 0, "ld L, E", 1, 4, 0, OpcodeType.None),
        new(0x6c, 0, 0, "ld L, H", 1, 4, 0, OpcodeType.None),
        new(0x6d, 0, 0, "ld L, L", 1, 4, 0, OpcodeType.None),
        new(0x6f, 0, 0, "ld L, A", 1, 4, 0, OpcodeType.None),
        new(0xed, 0x4f, 0, "ld R, A", 2, 9, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0x7b, 0, "ld SP, ({0})", 4, 20, 0, OpcodeType.TwoOpcodes),
        new(0xf9, 0, 0, "ld SP, HL", 1, 6, 0, OpcodeType.None),
        new(0xdd, 0xf9, 0, "ld SP, IX", 2, 10, 0, OpcodeType.TwoOpcodes),
        new(0xfd, 0xf9, 0, "ld SP, IY", 2, 10, 0, OpcodeType.TwoOpcodes),
        new(0x31, 0, 0, "ld SP, {0}", 3, 10, 0, OpcodeType.Value2),
        new(0xed, 0xa8, 0, "ldd", 2, 16, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0xb8, 0, "lddr", 2, 21, 16, OpcodeType.TwoOpcodes),
        new(0xed, 0xa0, 0, "lddi", 2, 16, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0xb0, 0, "lddir", 2, 21, 16, OpcodeType.TwoOpcodes),

        new(0xed, 0x44, 0, "neg", 2, 8, 0, OpcodeType.TwoOpcodes),
        new(0x00, 0, 0, "nop", 1, 4, 0, OpcodeType.None),

        new(0xb6, 0, 0, "or (HL)", 1, 7, 0, OpcodeType.None),
        new(0xdd, 0xb6, 0, "or (IX+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xfd, 0xb6, 0, "or (IY+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xf6, 0, 0, "or {0}", 2, 7, 0, OpcodeType.Value1),
        new(0xb0, 0, 0, "or B", 1, 4, 0, OpcodeType.None),
        new(0xb1, 0, 0, "or C", 1, 4, 0, OpcodeType.None),
        new(0xb2, 0, 0, "or D", 1, 4, 0, OpcodeType.None),
        new(0xb3, 0, 0, "or E", 1, 4, 0, OpcodeType.None),
        new(0xb4, 0, 0, "or H", 1, 4, 0, OpcodeType.None),
        new(0xb5, 0, 0, "or L", 1, 4, 0, OpcodeType.None),
        new(0xb7, 0, 0, "or A", 1, 4, 0, OpcodeType.None),

        new(0xed, 0xbb, 0, "otdr", 2, 21, 16, OpcodeType.TwoOpcodes),
        new(0xed, 0xb3, 0, "otir", 2, 21, 16, OpcodeType.TwoOpcodes),
        new(0xed, 0x79, 0, "out (C), A", 2, 12, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0x41, 0, "out (C), B", 2, 12, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0x49, 0, "out (C), C", 2, 12, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0x51, 0, "out (C), D", 2, 12, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0x59, 0, "out (C), E", 2, 12, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0x61, 0, "out (C), H", 2, 12, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0x69, 0, "out (C), L", 2, 12, 0, OpcodeType.TwoOpcodes),
        new(0xd3, 0, 0, "out ({0}), A", 2, 11, 0, OpcodeType.Value1),
        new(0xed, 0xab, 0, "outd", 2, 16, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0xa3, 0, "outi", 2, 16, 0, OpcodeType.TwoOpcodes),

        new(0xf1, 0, 0, "pop AF", 1, 10, 0, OpcodeType.None),
        new(0xc1, 0, 0, "pop BC", 1, 10, 0, OpcodeType.None),
        new(0xd1, 0, 0, "pop DE", 1, 10, 0, OpcodeType.None),
        new(0xe1, 0, 0, "pop HL", 1, 10, 0, OpcodeType.None),
        new(0xdd, 0xe1, 0, "pop IX", 2, 14, 0, OpcodeType.TwoOpcodes),
        new(0xfd, 0xe1, 0, "pop IY", 2, 14, 0, OpcodeType.TwoOpcodes),
        new(0xf5, 0, 0, "push AF", 1, 11, 0, OpcodeType.None),
        new(0xc5, 0, 0, "push BC", 1, 11, 0, OpcodeType.None),
        new(0xd5, 0, 0, "push DE", 1, 11, 0, OpcodeType.None),
        new(0xe5, 0, 0, "push HL", 1, 11, 0, OpcodeType.None),
        new(0xdd, 0xe5, 0, "push IX", 2, 15, 0, OpcodeType.TwoOpcodes),
        new(0xfd, 0xe5, 0, "push IY", 2, 15, 0, OpcodeType.TwoOpcodes),

        new(0xcb, 0x86, 0, "res {0}, (HL)", 2, 15, 0, OpcodeType.Bit),
        new(0xdd, 0xcb, 0x86, "res {0}, (IX+{1})", 4, 23, 0, OpcodeType.TwoOpcodesDisplacementBit),
        new(0xfd, 0xcb, 0x86, "res {0}, (IY+{1})", 4, 23, 0, OpcodeType.TwoOpcodesDisplacementBit),
        new(0xcb, 0x80, 0, "res {0}, B", 2, 8, 0, OpcodeType.Bit),
        new(0xcb, 0x81, 0, "res {0}, C", 2, 8, 0, OpcodeType.Bit),
        new(0xcb, 0x82, 0, "res {0}, D", 2, 8, 0, OpcodeType.Bit),
        new(0xcb, 0x83, 0, "res {0}, E", 2, 8, 0, OpcodeType.Bit),
        new(0xcb, 0x84, 0, "res {0}, H", 2, 8, 0, OpcodeType.Bit),
        new(0xcb, 0x85, 0, "res {0}, L", 2, 8, 0, OpcodeType.Bit),
        new(0xcb, 0x87, 0, "res {0}, A", 2, 8, 0, OpcodeType.Bit),

        new(0xc9, 0, 0, "ret", 1, 10, 0, OpcodeType.None),
        new(0xd8, 0, 0, "ret C", 1, 11, 5, OpcodeType.None),
        new(0xf8, 0, 0, "ret M", 1, 11, 5, OpcodeType.None),
        new(0xd0, 0, 0, "ret NC", 1, 11, 5, OpcodeType.None),
        new(0xc0, 0, 0, "ret NZ", 1, 11, 5, OpcodeType.None),
        new(0xf0, 0, 0, "ret P", 1, 11, 5, OpcodeType.None),
        new(0xe8, 0, 0, "ret PE", 1, 11, 5, OpcodeType.None),
        new(0xe0, 0, 0, "ret PO", 1, 11, 5, OpcodeType.None),
        new(0xc8, 0, 0, "ret Z", 1, 11, 5, OpcodeType.None),
        new(0xed, 0x4d, 0, "reti", 2, 14, 0, OpcodeType.TwoOpcodes),
        new(0xed, 0x45, 0, "retn", 2, 14, 0, OpcodeType.TwoOpcodes),

        new(0xcb, 0x16, 0, "rl (HL)", 2, 15, 0, OpcodeType.TwoOpcodes),
        new(0xdd, 0xcb, 0x16, "rl (IX+{0})", 4, 23, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xfd, 0xcb, 0x16, "rl (IY+{0})", 4, 23, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xcb, 0x10, 0, "rl B", 2, 8, 0, OpcodeType.TwoOpcodes),
        new(0xcb, 0x11, 0, "rl C", 2, 8, 0, OpcodeType.TwoOpcodes),
        new(0xcb, 0x12, 0, "rl D", 2, 8, 0, OpcodeType.TwoOpcodes),
        new(0xcb, 0x13, 0, "rl E", 2, 8, 0, OpcodeType.TwoOpcodes),
        new(0xcb, 0x14, 0, "rl H", 2, 8, 0, OpcodeType.TwoOpcodes),
        new(0xcb, 0x15, 0, "rl L", 2, 8, 0, OpcodeType.TwoOpcodes),
        new(0xcb, 0x17, 0, "rl A", 2, 8, 0, OpcodeType.TwoOpcodes),
        new(0x17, 0, 0, "rla", 1, 4, 0, OpcodeType.None),
        
        new (0xcb, 0x06, 0, "rlc (HL)", 2, 15, 0, OpcodeType.TwoOpcodes),
        new (0xdd, 0xcb, 0x06, "rlc (IX+{0})", 4, 23, 0, OpcodeType.TwoOpcodesDisplacement),
        new (0xfd, 0xcb, 0x06, "rlc (IY+{0})", 4, 23, 0, OpcodeType.TwoOpcodesDisplacement),
        new(0xcb, 0x00, 0, "rlc B", 2, 8, 0, OpcodeType.TwoOpcodes),
        new(0xcb, 0x01, 0, "rlc C", 2, 8, 0, OpcodeType.TwoOpcodes),
        new(0xcb, 0x02, 0, "rlc D", 2, 8, 0, OpcodeType.TwoOpcodes),
        new(0xcb, 0x03, 0, "rlc E", 2, 8, 0, OpcodeType.TwoOpcodes),
        new(0xcb, 0x04, 0, "rlc H", 2, 8, 0, OpcodeType.TwoOpcodes),
        new(0xcb, 0x05, 0, "rlc L", 2, 8, 0, OpcodeType.TwoOpcodes),
        new(0xcb, 0x07, 0, "rlc A", 2, 8, 0, OpcodeType.TwoOpcodes),
        new(0x07, 0, 0, "rlca", 1, 4, 0, OpcodeType.None),

        new (0xed, 0x6f, 0, "rld", 2, 18, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x1e, 0, "rr (HL)", 2, 15, 0, OpcodeType.TwoOpcodes),
        new (0xdd, 0xcb, 0x1e, "rr (IX+{0})", 4, 23, 0, OpcodeType.TwoOpcodesDisplacement),
        new (0xfd, 0xcb, 0x1e, "rr (IY+{0})", 4, 23, 0, OpcodeType.TwoOpcodesDisplacement),
        new (0xcb, 0x18, 0, "rr B", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x19, 0, "rr C", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x1a, 0, "rr D", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x1b, 0, "rr E", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x1c, 0, "rr H", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x1d, 0, "rr L", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x1f, 0, "rr A", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0x1f, 0, 0, "rra", 1, 4, 0, OpcodeType.None),

        new (0xcb, 0x0e, 0, "rrc (HL)", 2, 15, 0, OpcodeType.TwoOpcodes),
        new (0xdd, 0xcb, 0x0e, "rrc (IX+{0})", 4, 23, 0, OpcodeType.TwoOpcodesDisplacement),
        new (0xfd, 0xcb, 0x0e, "rrc (IY+{0})", 4, 23, 0, OpcodeType.TwoOpcodesDisplacement),
        new (0xcb, 0x08, 0, "rrc B", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x09, 0, "rrc C", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x0a, 0, "rrc D", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x0b, 0, "rrc E", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x0c, 0, "rrc H", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x0d, 0, "rrc L", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x0f, 0, "rrc A", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0x0f, 0, 0, "rrca", 1, 4, 0, OpcodeType.None),

        new (0xed, 0x67, 0, "rrd", 2, 18, 0, OpcodeType.TwoOpcodes),
        new (0xc7, 0, 0, "rst 00h", 1, 11, 0, OpcodeType.None),
        new (0xcf, 0, 0, "rst 08h", 1, 11, 0, OpcodeType.None),
        new (0xd7, 0, 0, "rst 10h", 1, 11, 0, OpcodeType.None),
        new (0xdf, 0, 0, "rst 18h", 1, 11, 0, OpcodeType.None),
        new (0xe7, 0, 0, "rst 20h", 1, 11, 0, OpcodeType.None),
        new (0xef, 0, 0, "rst 28h", 1, 11, 0, OpcodeType.None),
        new (0xf7, 0, 0, "rst 30h", 1, 11, 0, OpcodeType.None),
        new (0xff, 0, 0, "rst 38h", 1, 11, 0, OpcodeType.None),

        new (0x9e, 0, 0, "sbc A, (HL)", 1, 7, 0, OpcodeType.None),
        new (0xdd, 0x9e, 0, "sbc A, (IX+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new (0xfd, 0x9e, 0, "sbc A, (IY+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new (0xde, 0, 0, "sbc A, {0}", 2, 7, 0, OpcodeType.Value1),
        new (0x98, 0, 0, "sbc A, B", 1, 4, 0, OpcodeType.None),
        new (0x99, 0, 0, "sbc A, C", 1, 4, 0, OpcodeType.None),
        new (0x9a, 0, 0, "sbc A, D", 1, 4, 0, OpcodeType.None),
        new (0x9b, 0, 0, "sbc A, E", 1, 4, 0, OpcodeType.None),
        new (0x9c, 0, 0, "sbc A, H", 1, 4, 0, OpcodeType.None),
        new (0x9d, 0, 0, "sbc A, L", 1, 4, 0, OpcodeType.None),
        new (0x9f, 0, 0, "sbc A, A", 1, 4, 0, OpcodeType.None),
        new (0xed, 0x42, 0, "sbc HL, BC", 2, 15, 0, OpcodeType.TwoOpcodes),
        new (0xed, 0x52, 0, "sbc HL, DE", 2, 15, 0, OpcodeType.TwoOpcodes),
        new (0xed, 0x62, 0, "sbc HL, HL", 2, 15, 0, OpcodeType.TwoOpcodes),
        new (0xed, 0x72, 0, "sbc HL, SP", 2, 15, 0, OpcodeType.TwoOpcodes),
        new (0x37, 0, 0, "scf", 1, 4, 0, OpcodeType.None),
        
        new (0xcb, 0xc6, 0, "set {0}, (HL)", 2, 15, 0, OpcodeType.Bit),
        new (0xdd, 0xcb, 0xc6, "set {0}, (IX+{1})", 4, 23, 0, OpcodeType.TwoOpcodesDisplacementBit),
        new (0xfd, 0xcb, 0xc6, "set {0}, (IY+{1})", 4, 23, 0, OpcodeType.TwoOpcodesDisplacementBit),
        new (0xcb, 0xc0, 0, "set {0}, B", 2, 8, 0, OpcodeType.Bit),
        new (0xcb, 0xc1, 0, "set {0}, C", 2, 8, 0, OpcodeType.Bit),
        new (0xcb, 0xc2, 0, "set {0}, D", 2, 8, 0, OpcodeType.Bit),
        new (0xcb, 0xc3, 0, "set {0}, E", 2, 8, 0, OpcodeType.Bit),
        new (0xcb, 0xc4, 0, "set {0}, H", 2, 8, 0, OpcodeType.Bit),
        new (0xcb, 0xc5, 0, "set {0}, L", 2, 8, 0, OpcodeType.Bit),
        new (0xcb, 0xc7, 0, "set {0}, A", 2, 8, 0, OpcodeType.Bit),

        new (0xcb, 0x26, 0, "sla (HL)", 2, 15, 0, OpcodeType.TwoOpcodes),
        new (0xdd, 0xcb, 0x26, "sla (IX+{0})", 4, 23, 0, OpcodeType.TwoOpcodesDisplacement),
        new (0xfd, 0xcb, 0x26, "sla (IY+{0})", 4, 23, 0, OpcodeType.TwoOpcodesDisplacement),
        new (0xcb, 0x20, 0, "sla B", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x21, 0, "sla C", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x22, 0, "sla D", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x23, 0, "sla E", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x24, 0, "sla H", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x25, 0, "sla L", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x27, 0, "sla A", 2, 8, 0, OpcodeType.TwoOpcodes),

        new (0xcb, 0x2e, 0, "sra (HL)", 2, 15, 0, OpcodeType.TwoOpcodes),
        new (0xdd, 0xcb, 0x2e, "sra (IX+{0})", 4, 23, 0, OpcodeType.TwoOpcodesDisplacement),
        new (0xfd, 0xcb, 0x2e, "sra (IY+{0})", 4, 23, 0, OpcodeType.TwoOpcodesDisplacement),
        new (0xcb, 0x28, 0, "sra B", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x29, 0, "sra C", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x2a, 0, "sra D", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x2b, 0, "sra E", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x2c, 0, "sra H", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x2d, 0, "sra L", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x2f, 0, "sra A", 2, 8, 0, OpcodeType.TwoOpcodes),

        new (0xcb, 0x3e, 0, "srl (HL)", 2, 15, 0, OpcodeType.TwoOpcodes),
        new (0xdd, 0xcb, 0x3e, "srl (IX+{0})", 4, 23, 0, OpcodeType.TwoOpcodesDisplacement),
        new (0xfd, 0xcb, 0x3e, "srl (IY+{0})", 4, 23, 0, OpcodeType.TwoOpcodesDisplacement),
        new (0xcb, 0x38, 0, "srl B", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x39, 0, "srl C", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x3a, 0, "srl D", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x3b, 0, "srl E", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x3c, 0, "srl H", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x3d, 0, "srl L", 2, 8, 0, OpcodeType.TwoOpcodes),
        new (0xcb, 0x3f, 0, "srl A", 2, 8, 0, OpcodeType.TwoOpcodes),

        new (0x96, 0, 0, "sub (HL)", 1, 7, 0, OpcodeType.None),
        new (0xdd, 0x96, 0, "sub A, (IX+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new (0xfd, 0x96, 0, "sub A, (IY+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new (0xd6, 0, 0, "sub {0}", 2, 7, 0, OpcodeType.Value1),
        new (0x90, 0, 0, "sub B", 1, 4, 0, OpcodeType.None),
        new (0x91, 0, 0, "sub C", 1, 4, 0, OpcodeType.None),
        new (0x92, 0, 0, "sub D", 1, 4, 0, OpcodeType.None),
        new (0x93, 0, 0, "sub E", 1, 4, 0, OpcodeType.None),
        new (0x94, 0, 0, "sub H", 1, 4, 0, OpcodeType.None),
        new (0x95, 0, 0, "sub L", 1, 4, 0, OpcodeType.None),
        new (0x97, 0, 0, "sub A", 1, 4, 0, OpcodeType.None),

        new (0xae, 0, 0, "xor (HL)", 1, 7, 0, OpcodeType.None),
        new (0xdd, 0xae, 0, "xor (IX+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new (0xfd, 0xae, 0, "xor (IY+{0})", 3, 19, 0, OpcodeType.TwoOpcodesDisplacement),
        new (0xee, 0, 0, "xor {0}", 2, 7, 0, OpcodeType.Value1),
        new (0xa8, 0, 0, "xor B", 1, 4, 0, OpcodeType.None),
        new (0xa9, 0, 0, "xor C", 1, 4, 0, OpcodeType.None),
        new (0xaa, 0, 0, "xor D", 1, 4, 0, OpcodeType.None),
        new (0xab, 0, 0, "xor E", 1, 4, 0, OpcodeType.None),
        new (0xac, 0, 0, "xor H", 1, 4, 0, OpcodeType.None),
        new (0xad, 0, 0, "xor L", 1, 4, 0, OpcodeType.None),
        new (0xaf, 0, 0, "xor A", 1, 4, 0, OpcodeType.None),
    };
    
    private int ConvertToSignedInteger(byte value)
    {
        // Se il byte ha il bit più significativo (MSB) impostato a 1, significa che il numero è negativo
        return (sbyte)value; // Cast del byte a sbyte, che è un tipo con segno (8 bit)
    }
    
    private ushort? lastPC;
    private Opcode? currentOpcode;
    private int curByte = 1;

    private byte? byte1;
    private byte? byte2;
    private byte? byte3;
    private byte? byte4;

    public void Restart()
    {
        lastPC = null;
        currentOpcode = null;
        curByte = 1;

        byte1 = null;
        byte2 = null;
        byte3 = null;
        byte4 = null;
    }

    public string Disassemble(ushort pc, byte b)
    {
        string retMnem = "";

        // Controlla se l'indirizzo corrente è lo stesso di quello precedente, se sì, non fare nulla
        if (pc == lastPC)
        {
            return "";
        }

        lastPC = pc;

        if (currentOpcode == null)
        {
            // Cerca l'opcode corrispondente
            var idx = opcodes.FindIndex(op => op.OpCode1 == b);
            if (idx != -1)
            {
                if (opcodes[idx].Bytes == 1)
                {
                    retMnem = opcodes[idx].Mnemonic;
                }
                else
                {
                    byte1 = b;
                    currentOpcode = opcodes[idx];
                    curByte++;
                }
            }
        }
        else
        {
            // Gestisce diversi tipi di opcode
            if (currentOpcode?.Type == OpcodeType.TwoOpcodes || currentOpcode?.Type == OpcodeType.TwoOpcodesDisplacementBit)
            {
                var idx = opcodes.FindIndex(op => op.OpCode1 == byte1 && op.OpCode2 == b);
                if (idx != -1)
                {
                    currentOpcode = opcodes[idx];
                }
            }
            else if (currentOpcode?.Type == OpcodeType.TwoOpcodesDisplacement || currentOpcode?.Type == OpcodeType.TwoOpcodesDisplacementN)
            {
                if (curByte == 2)
                {
                    var idx = opcodes.FindIndex(op => op.OpCode1 == byte1 && op.OpCode2 == b);
                    if (idx != -1)
                    {
                        currentOpcode = opcodes[idx];
                    }
                }
                else if (curByte == 4)
                {
                    var idx = opcodes.FindIndex(op => op.OpCode1 == byte1 && op.OpCode2 == byte2 && op.OpCode3 == b);
                    if (idx != -1)
                    {
                        currentOpcode = opcodes[idx];
                    }
                }
            }
            else if (currentOpcode?.Type == OpcodeType.Bit)
            {
                var idx = opcodes.FindIndex(op => op.OpCode1 == byte1 && (op.OpCode2 & 0xc0) == b);
                if (idx != -1)
                {
                    currentOpcode = opcodes[idx];
                }
            }

            // Gestisce i diversi tipi di byte
            if (currentOpcode?.Bytes == 2)
            {
                int byteValue = b;
                if (currentOpcode?.Type == OpcodeType.Address)
                {
                    byteValue = pc + ConvertToSignedInteger(b) + 1;
                    //retMnem = string.Format(currentOpcode?.Mnemonic, byteValue);
                    retMnem = string.Format(currentOpcode?.Mnemonic, byteValue.ToZ80Hex());
                }
                else if (currentOpcode?.Type == OpcodeType.Bit)
                {
                    //retMnem = string.Format(currentOpcode?.Mnemonic, (byteValue & 0x38) >> 3);
                    retMnem = string.Format(currentOpcode?.Mnemonic, ((byteValue & 0x38) >> 3).ToZ80Hex());
                }
                else
                {
                    //retMnem = string.Format(currentOpcode?.Mnemonic, byteValue);
                    retMnem = string.Format(currentOpcode?.Mnemonic, byteValue.ToZ80Hex());
                }

                currentOpcode = null;
                curByte = 1;
            }
            else if (currentOpcode?.Bytes == 3)
            {
                if (curByte == 2)
                {
                    byte2 = b;
                    curByte++;
                }
                else
                {
                    if (currentOpcode?.Type == OpcodeType.Address || currentOpcode?.Type == OpcodeType.Value2)
                    {
                        ushort address = (ushort)(byte2 | (b << 8));
                        //retMnem = string.Format(currentOpcode?.Mnemonic, address);
                        retMnem = string.Format(currentOpcode?.Mnemonic, address.ToZ80Hex());
                    }
                    else if (currentOpcode?.Type == OpcodeType.TwoOpcodesDisplacement)
                    {
                        //retMnem = string.Format(currentOpcode?.Mnemonic, b);
                        retMnem = string.Format(currentOpcode?.Mnemonic, b.ToZ80Hex());
                    }
                    else
                    {
                        retMnem = "ERROR 3!!!";
                    }

                    currentOpcode = null;
                    curByte = 1;
                }
            }
            else if (currentOpcode?.Bytes == 4)
            {
                if (curByte == 2)
                {
                    byte2 = b;
                    curByte++;
                }
                else if (curByte == 3)
                {
                    byte3 = b;
                    curByte++;
                }
                else
                {
                    if (currentOpcode?.Type == OpcodeType.TwoOpcodes)
                    {
                        ushort address = (ushort)(byte3 | (b << 8));
                        //retMnem = string.Format(currentOpcode?.Mnemonic, address);
                        retMnem = string.Format(currentOpcode?.Mnemonic, address.ToZ80Hex());
                    }
                    else if (currentOpcode?.Type == OpcodeType.TwoOpcodesDisplacement)
                    {
                        //retMnem = string.Format(currentOpcode?.Mnemonic, byte3);
                        retMnem = string.Format(currentOpcode?.Mnemonic, byte3?.ToZ80Hex());
                    }
                    else if (currentOpcode?.Type == OpcodeType.TwoOpcodesDisplacementN)
                    {
                        //retMnem = string.Format(currentOpcode?.Mnemonic, byte3, b);
                        retMnem = string.Format(currentOpcode?.Mnemonic, byte3, b.ToZ80Hex());
                    }
                    else if (currentOpcode?.Type == OpcodeType.TwoOpcodesDisplacementBit)
                    {
                        //retMnem = string.Format(currentOpcode?.Mnemonic, (b & 0x38) >> 3, byte3);
                        retMnem = string.Format(currentOpcode?.Mnemonic, (b & 0x38) >> 3, byte3?.ToZ80Hex());
                    }
                    else
                    {
                        retMnem = "ERROR 4!!!";
                    }

                    currentOpcode = null;
                    curByte = 1;
                }
            }
        }

        return retMnem;
    }
}
