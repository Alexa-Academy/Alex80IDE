// Banco di prova dell'assembler: assembla sorgenti e confronta i byte prodotti
// con quelli attesi.
//
// Serve a due scopi, in ordine di importanza:
//
//  1. REGRESSIONE Z80. La compilazione Z80 e' il cuore del progetto. Ogni
//     modifica all'assembler (in particolare l'allargamento degli indirizzi a
//     24 bit per l'eZ80) deve lasciare invariato ogni byte prodotto per lo Z80.
//     I casi "z80 *" sono la rete che lo garantisce.
//
//  2. VERIFICA DELLE CODIFICHE eZ80. Le tabelle di opcode sono inserite a mano,
//     e un errore li' produce byte plausibili ma sbagliati. Ogni codifica
//     aggiunta dovrebbe entrare con il suo caso qui.
//     Dove esiste un riscontro esterno e' indicato: "OUT0 (n),A = ED 39 n" e'
//     confermato da un binario prodotto da Zilog ZDS II.
//
// Si esegue con:  dotnet run --project AssemblerTests
// Il codice di uscita e' il numero di test falliti, quindi e' utilizzabile in
// una pipeline di build.

using System.Text;
using Konamiman.Nestor80.Assembler;

static (byte[] bytes, string[] errors, int firstAddress) AssembleFull(string source, string cpu)
{
    var config = new AssemblyConfiguration { CpuName = cpu, BuildType = BuildType.Absolute };
    using var input = new MemoryStream(Encoding.UTF8.GetBytes(source));
    var result = AssemblySourceProcessor.Assemble(input, Encoding.UTF8, config);
    var errs = result.Errors.Where(e => !e.IsWarning).Select(e => $"line {e.LineNumber}: {e.Message}").ToArray();
    if (errs.Length > 0) return (Array.Empty<byte>(), errs, 0);
    using var output = new MemoryStream();
    OutputGenerator.GenerateAbsolute(result, output);
    return (output.ToArray(), errs, result.FirstAddress);
}

static (byte[] bytes, string[] errors) Assemble(string source, string cpu)
{
    var config = new AssemblyConfiguration { CpuName = cpu, BuildType = BuildType.Absolute };
    using var input = new MemoryStream(Encoding.UTF8.GetBytes(source));
    var result = AssemblySourceProcessor.Assemble(input, Encoding.UTF8, config);
    var errors = result.Errors.Where(e => !e.IsWarning).Select(e => $"line {e.LineNumber}: {e.Message}").ToArray();
    if (errors.Length > 0) return (Array.Empty<byte>(), errors);
    using var output = new MemoryStream();
    OutputGenerator.GenerateAbsolute(result, output);
    return (output.ToArray(), Array.Empty<string>());
}

static string Hex(byte[] b) => string.Join(" ", b.Select(x => x.ToString("x2")));

var blinkSourceForBuilder =
    "\tassume adl = 1\n" +
    "\torg 0FFF000h\n" +
    "PB_DR   equ 09Ah\n" +
    "PB_DDR  equ 09Bh\n" +
    "PB_ALT1 equ 09Ch\n" +
    "PB_ALT2 equ 09Dh\n" +
    "start:\n" +
    "\tdi\n" +
    "\txor a\n" +
    "\tout0 (PB_ALT1), a\n" +
    "\tout0 (PB_ALT2), a\n" +
    "\tld a, 0FEh\n" +
    "\tout0 (PB_DDR), a\n" +
    "blink:\n" +
    "\tld a, 1\n" +
    "\tout0 (PB_DR), a\n" +
    "\tcall delay\n" +
    "\txor a\n" +
    "\tout0 (PB_DR), a\n" +
    "\tcall delay\n" +
    "\tjr blink\n" +
    "delay:\n" +
    "\tld d, 30\n" +
    "delay_outer:\n" +
    "\tld b, 0\n" +
    "delay_middle:\n" +
    "\tld c, 0\n" +
    "delay_inner:\n" +
    "\tdec c\n" +
    "\tjr nz, delay_inner\n" +
    "\tdjnz delay_middle\n" +
    "\tdec d\n" +
    "\tjr nz, delay_outer\n" +
    "\tret\n";

int failures = 0;
void Check(string name, string source, string cpu, string expected)
{
    var (bytes, errors) = Assemble(source, cpu);
    if (errors.Length > 0)
    {
        Console.WriteLine($"FAIL {name}: {string.Join(" | ", errors)}");
        failures++;
        return;
    }
    var got = Hex(bytes);
    if (got != expected)
    {
        Console.WriteLine($"FAIL {name}\n     atteso: {expected}\n      botten: {got}");
        failures++;
        return;
    }
    Console.WriteLine($"ok   {name}  [{got}]");
}

// ---- regressione Z80: deve restare identica ----
Check("z80 base", "\torg 100h\n\tld a,5\n\tcall 1234h\n\tld hl,0abcdh\n\tpush hl\n\tret\n", "Z80",
      "3e 05 cd 34 12 21 cd ab e5 c9");
Check("z80 in/out", "\torg 0\n\tin a,(10h)\n\tout (20h),a\n", "Z80", "db 10 d3 20");

// ---- eZ80: istruzioni nuove ----
Check("ez80 out0 a", "\t.cpu ez80\n\torg 0\n\tout0 (9ch),a\n", "Z80", "ed 39 9c");
Check("ez80 out0 b", "\t.cpu ez80\n\torg 0\n\tout0 (12h),b\n", "Z80", "ed 01 12");
Check("ez80 in0", "\t.cpu ez80\n\torg 0\n\tin0 a,(34h)\n", "Z80", "ed 38 34");
Check("ez80 mlt/slp", "\t.cpu ez80\n\torg 0\n\tmlt hl\n\tslp\n", "Z80", "ed 6c ed 76");
Check("ez80 tst", "\t.cpu ez80\n\torg 0\n\ttst b\n\ttstio 55h\n", "Z80", "ed 04 ed 74 55");
Check("ez80 mix", "\t.cpu ez80\n\torg 0\n\tstmix\n\trsmix\n\totimr\n", "Z80", "ed 7d ed 7e ed 93");

// ---- ASSUME ADL riconosciuta ----
Check("ez80 assume", "\t.cpu ez80\n\tassume adl = 1\n\torg 0\n\txor a\n", "Z80", "af");

// ---- lo Z80 non deve conoscere le istruzioni eZ80 ----
var (_, e) = Assemble("\torg 0\n\tout0 (9ch),a\n", "Z80");
Console.WriteLine(e.Length > 0 ? $"ok   z80 rifiuta out0  [{e[0]}]" : "FAIL: lo Z80 ha accettato out0");
if (e.Length == 0) failures++;

// ---- il pezzo di blink che oggi fallisce ----
var blink = "\t.cpu ez80\n\tassume adl = 1\n\torg 0\nPB_ALT1 equ 9ch\n\tdi\n\txor a\n\tout0 (PB_ALT1),a\n\tld a,0feh\n";
Check("blink (frammento)", blink, "Z80", "f3 af ed 39 9c 3e fe");

// ================= regressione Z80 estesa =================
// Copre i modi di indirizzamento, gli operandi a 16 bit, i salti relativi,
// le espressioni e le direttive dati: tutto cio' che l'allargamento degli
// indirizzi a 24 bit potrebbe rompere senza accorgersene.
Check("z80 8 bit", "\torg 0\n\tld a,b\n\tld b,(hl)\n\tadd a,7\n\tcp 0ffh\n\tinc c\n", "Z80",
      "78 46 c6 07 fe ff 0c");
Check("z80 16 bit", "\torg 0\n\tld bc,1234h\n\tld de,0\n\tld sp,0ffffh\n\tld (5678h),hl\n\tld hl,(9abch)\n", "Z80",
      "01 34 12 11 00 00 31 ff ff 22 78 56 2a bc 9a");
Check("z80 indicizzati", "\torg 0\n\tld a,(ix+5)\n\tld (iy-3),b\n\tbit 7,(ix+0)\n", "Z80",
      "dd 7e 05 fd 70 fd dd cb 00 7e");
Check("z80 salti", "\torg 100h\nl1: jr l1\n\tdjnz l1\n\tjp l1\n\tjr nz,l1\n", "Z80",
      "18 fe 10 fc c3 00 01 20 f7");
Check("z80 espressioni", "\torg 0\nv equ 10h\n\tld a,v+5\n\tld hl,v*100h\n\tld b,v and 0fh\n", "Z80",
      "3e 15 21 00 10 06 00");
Check("z80 dati", "\torg 0\n\tdb 1,2,3\n\tdw 1234h\n\tdb 'AB'\n\tds 3,0ffh\n", "Z80",
      "01 02 03 34 12 41 42 ff ff ff");
Check("z80 org alto", "\torg 0fff0h\n\tld hl,0fff0h\n\tjp 0fff0h\n", "Z80",
      "21 f0 ff c3 f0 ff");
Check("z80 etichette fwd", "\torg 200h\n\tjp fine\n\tnop\nfine: ret\n", "Z80",
      "c3 04 02 00 c9");
Check("z80 ed/cb", "\torg 0\n\tldir\n\tneg\n\tim 1\n\trlc b\n\tsrl (hl)\n", "Z80",
      "ed b0 ed 44 ed 56 cb 00 cb 3e");
Check("z80 rst/call cond", "\torg 0\n\trst 38h\n\tcall nz,1234h\n\tret po\n", "Z80",
      "ff c4 34 12 e0");

// ---- indirizzi a 24 bit: il punto della fase 2 ----
void CheckAddr(string name, string source, string cpu, int expectedFirst)
{
    var (_, errs, first) = AssembleFull(source, cpu);
    if (errs.Length > 0) { Console.WriteLine($"FAIL {name}: {errs[0]}"); failures++; return; }
    if (first != expectedFirst) { Console.WriteLine($"FAIL {name}: atteso {expectedFirst:X6}, ottenuto {first:X6}"); failures++; return; }
    Console.WriteLine($"ok   {name}  [first={first:X6}]");
}
CheckAddr("ez80 org 24 bit", "\t.cpu ez80\n\torg 0FFF000h\n\txor a\n", "Z80", 0xFFF000);
CheckAddr("ez80 org basso", "\t.cpu ez80\n\torg 100h\n\txor a\n", "Z80", 0x100);
CheckAddr("z80 org invariato", "\torg 8000h\n\txor a\n", "Z80", 0x8000);

// etichetta risolta nello spazio alto
var (eb, ee, _) = AssembleFull("\t.cpu ez80\n\torg 0FFF000h\nl2:\n\tnop\n\tdw 0\n\tdb l2 and 0ffh\n", "Z80");
Console.WriteLine(ee.Length > 0 ? $"FAIL etichetta alta: {ee[0]}" : $"ok   etichetta alta  [{Hex(eb)}]  (db l2 and 0ffh deve dare 00)");
if (ee.Length > 0) failures++;

// ---- operandi a 24 bit in ADL: un byte in piu' rispetto allo Z80 ----
Check("adl ld rr,nn", "\t.cpu ez80\n\tassume adl=1\n\torg 0\n\tld hl,1234h\n\tld bc,0ABCDEFh\n\tld sp,0\n", "Z80",
      "21 34 12 00 01 ef cd ab 31 00 00 00");
Check("adl jp/call", "\t.cpu ez80\n\tassume adl=1\n\torg 0\n\tjp 0FFF000h\n\tcall nz,1234h\n", "Z80",
      "c3 00 f0 ff c4 34 12 00");
Check("adl ld (nn),a", "\t.cpu ez80\n\tassume adl=1\n\torg 0\n\tld (0FFF010h),a\n\tld a,(1234h)\n", "Z80",
      "32 10 f0 ff 3a 34 12 00");
Check("adl indipendenti", "\t.cpu ez80\n\tassume adl=1\n\torg 0\n\tld a,5\n\trst 38h\nl3: jr l3\n\tpush hl\n", "Z80",
      "3e 05 ff 18 fe e5");
// con ADL a 0 l'eZ80 deve tornare alle dimensioni Z80
Check("adl=0 come z80", "\t.cpu ez80\n\tassume adl=0\n\torg 0\n\tld hl,1234h\n\tcall 5678h\n", "Z80",
      "21 34 12 cd 78 56");

// ================= riscontro esterno =================
// blink_ez80.asm assemblato da Zilog ZDS II. I byte attesi sono quelli di
// blink_ez80.bin, non una deduzione: e' l'unica verifica indipendente che
// abbiamo sulle codifiche eZ80 e sulla dimensione degli operandi in ADL.
{
    // Qui la CPU si sceglie nel sorgente; nel test di ProjectBuilder
    // piu' sotto arriva invece dall'impostazione di progetto.
    var blinkSource = "\t.cpu ez80\n" + blinkSourceForBuilder;

    var expectedBlink =
        "f3 af ed 39 9c ed 39 9d 3e fe ed 39 9b 3e 01 ed " +
        "39 9a cd 20 f0 ff af ed 39 9a cd 20 f0 ff 18 ed " +
        "16 1e 06 00 0e 00 0d 20 fd 10 f9 15 20 f4 c9";
    var (bb, be, bfirst) = AssembleFull(blinkSource, "Z80");
    if (be.Length > 0)
    {
        Console.WriteLine($"FAIL blink completo: {be[0]}");
        failures++;
    }
    else if (Hex(bb) != expectedBlink || bfirst != 0xFFF000)
    {
        Console.WriteLine($"FAIL blink completo\n     atteso: {expectedBlink} @FFF000\n    ottenuto: {Hex(bb)} @{bfirst:X6}");
        failures++;
    }
    else
    {
        Console.WriteLine($"ok   blink completo vs ZDS II  [{bb.Length} byte @{bfirst:X6}]");
    }
}

// ---- stato attuale di org a 24 bit (fase 2) ----
var (b2, e2) = Assemble("\t.cpu ez80\n\tassume adl=1\n\torg 0FFF000h\n\txor a\n", "Z80");
Console.WriteLine(e2.Length > 0 ? $"org 24 bit -> {e2[0]}" : $"org 24 bit -> assemblato: {Hex(b2)}");
var (b3, e3) = Assemble("\t.cpu ez80\n\tassume adl=1\n\torg 0\nloop: call loop\n", "Z80");
Console.WriteLine(e3.Length > 0 ? $"call in ADL -> {e3[0]}" : $"call in ADL -> {Hex(b3)}  (in ADL deve essere 4 byte)");

// ================= percorso completo dell'IDE =================
// ProjectBuilder e' cio' che gira quando si preme "Assembla": mette il
// risultato in una finestra di memoria da 64 KB. Con l'eZ80 il codice sta a
// FFF000h, ben oltre quella finestra, e senza un ancoraggio il blocco viene
// rifiutato pur essendo assemblato correttamente.
{
    var request = new Alex80_IDE.Services.BuildRequest
    {
        Sources = new[] { new Alex80_IDE.Services.BuildSource("blink_ez80.asm", null, blinkSourceForBuilder) },
        CpuName = "EZ80",
        TasmCompatibility = false
    };
    var built = Alex80_IDE.Services.ProjectBuilder.Build(request);
    var errs = built.Messages.Where(m => !m.IsWarning).Select(m => m.Text).ToArray();
    if (errs.Length > 0)
    {
        Console.WriteLine($"FAIL ProjectBuilder eZ80: {string.Join(" | ", errs)}");
        failures++;
    }
    else if (built.FirstAddress != 0xFFF000 || built.Bytes.Length != 47)
    {
        Console.WriteLine($"FAIL ProjectBuilder eZ80: {built.Bytes.Length} byte @{built.FirstAddress:X6}");
        failures++;
    }
    else
    {
        Console.WriteLine($"ok   ProjectBuilder eZ80  [{built.Bytes.Length} byte @{built.FirstAddress:X6}]");
    }
}

// e lo Z80 deve continuare a passare dallo stesso percorso senza cambiamenti
{
    var request = new Alex80_IDE.Services.BuildRequest
    {
        Sources = new[] { new Alex80_IDE.Services.BuildSource("z80.asm", null, "\torg 8000h\n\tld a,5\n\tret\n") },
        CpuName = "Z80",
        TasmCompatibility = false
    };
    var built = Alex80_IDE.Services.ProjectBuilder.Build(request);
    var errs = built.Messages.Where(m => !m.IsWarning).Select(m => m.Text).ToArray();
    if (errs.Length > 0 || built.FirstAddress != 0x8000 || Hex(built.Bytes) != "3e 05 c9")
    {
        Console.WriteLine($"FAIL ProjectBuilder z80: {string.Join(" | ", errs)} [{Hex(built.Bytes)} @{built.FirstAddress:X4}]");
        failures++;
    }
    else
    {
        Console.WriteLine($"ok   ProjectBuilder z80   [{Hex(built.Bytes)} @{built.FirstAddress:X4}]");
    }
}

// ---- larghezza dell'indirizzo nella griglia memoria ----
{
    var narrow = new Alex80_IDE.Models.RowElement(0x8000, new List<byte>(), "", false);
    var wide = new Alex80_IDE.Models.RowElement(0xFFF010, new List<byte>(), "", true);
    var ok1 = narrow.AddressText == "8000";
    var ok2 = wide.AddressText == "FFF010";
    Console.WriteLine(ok1 && ok2
        ? $"ok   indirizzo di riga   [{narrow.AddressText} / {wide.AddressText}]"
        : $"FAIL indirizzo di riga: {narrow.AddressText} / {wide.AddressText}");
    if (!(ok1 && ok2)) failures++;
}

// Il firmware Z80 interpreta rb/wb in base 16; quello eZ80 in base 10,
// anche quando il processore eZ80 lavora in modalita' Z80.
foreach (var (address, ez80, expected) in new (uint, bool, string)[]
{
    (0, false, "0"), (0x8000, false, "8000"),
    (0xFFFF, false, "FFFF"), (0x8000, true, "32768"),
    (0xFFF000, true, "16773120")
})
{
    var actual = Alex80_IDE.SerialManager.FormatMemoryAddress(address, ez80);
    Console.WriteLine(actual == expected
        ? $"ok   indirizzo seriale {address:X} eZ80={ez80}: {actual}"
        : $"FAIL indirizzo seriale: atteso {expected}, ricevuto {actual}");
    if (actual != expected) failures++;
}

failures += BoardConnectionTests.Run();
failures += InstructionReferenceTests.Run();
failures += ListingExecutionTests.Run();
failures += RegisterRefreshTests.Run();
failures += ClockTraceTrackerTests.Run();

Console.WriteLine(failures == 0 ? "\nTUTTI I TEST PASSATI" : $"\n{failures} TEST FALLITI");
return failures;
