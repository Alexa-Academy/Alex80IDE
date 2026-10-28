using System.Reflection;
using Alex80_IDE;
using Alex80_IDE.InstructionReference;
using Alex80_IDE.Models;
using Avalonia.Threading;

internal static class InstructionReferenceTests
{
    public static int Run()
    {
        int failures = 0;
        void Check(string name, bool ok)
        { Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {name}"); if (!ok) failures++; }
        InstructionInfo D(string hex, bool ez = false, bool adl = false, uint pc = 0x2000) =>
            InstructionDecoder.Decode(pc, Convert.FromHexString(hex.Replace(" ", "")), ez, adl);
        foreach (var (hex, name, t) in new[] {
            ("3C", "INC A", 4), ("34", "INC (HL)", 11), ("DD 34 FE", "INC (IX-02h)", 23),
            ("DD 36 05 AA", "LD (IX+05h),AAh", 19), ("FD CB FE 46", "BIT 0,(IY-02h)", 20),
            ("ED 4B 34 12", "LD BC,(1234h)", 20), ("C3 34 12", "JP 1234h", 10),
            ("DD FD 21 34 12", "LD IY,1234h", 18), ("CD 34 12", "CALL 1234h", 17), ("C9", "RET", 10)
        }) {
            var i = D(hex); Check("scheda Z80 " + name, i.Complete && i.Mnemonic == name && i.NominalTStates == t);
        }
        Check("tempi condizionali distinti", D("20 FE").Timing.Contains("12 / 7") && D("20 FE").NominalTStates is null);
        Check("ripetizioni distinte dall'ultima iterazione", D("ED B0").Timing.Contains("21 / 16"));
        Check("cicli macchina degli accessi indicizzati", D("DD 34 FE").MachineCycles.StartsWith("6 cicli") && D("DD 36 05 AA").MachineCycles.StartsWith("5 cicli") && D("DD 7E 05").MachineCycles.StartsWith("5 cicli") && D("DD 86 05").MachineCycles.StartsWith("5 cicli"));
        Check("byte incompleti non inventano istruzioni", !D("DD 21 34").Complete && !D("DD FD DD FD DD FD DD FD").Complete);
        Check("operazione e flag INC", D("3C").Operation == "A ← A + 1" && D("3C").Flags.Contains("C invariato"));
        Check("tempo teorico senza WAIT", D("3C").IdealTime(500).Contains("8 ms"));
        Check("JR oltre FFFF resta a 16 bit", D("18 FE", pc:0xFFFF).Mnemonic == "JR FFFFh");
        var ezinc = D("3C", true, true);
        Check("eZ80 INC: un ciclo bus, nessun T-state Z80", ezinc.Timing.StartsWith("1 ciclo bus") && ezinc.NominalTStates is null);
        Check("eZ80 modo Z80 conserva tempi eZ80", D("3C", true).Timing.StartsWith("1 ciclo bus"));
        Check("eZ80 LD a 24 bit", D("21 56 34 12", true, true).Mnemonic == "LD HL,123456h" && D("21 56 34 12", true, true).Timing.StartsWith("4 cicli bus"));
        Check("eZ80 suffisso SIS cambia lunghezza", D("40 21 34 12", true, true).Mnemonic == "LD.SIS HL,1234h");
        Check("eZ80 MLT", D("ED 4C", true).Mnemonic == "MLT BC" && D("ED 4C", true).Timing.StartsWith("6 cicli bus"));
        Check("eZ80 IN0", D("ED 38 9C", true).Mnemonic == "IN0 A,(9Ch)" && D("ED 38 9C", true).Timing.StartsWith("4 cicli bus"));
        Check("eZ80 LEA", D("ED 32 FE", true).Mnemonic == "LEA IX,IX-02h" && D("ED 32 FE", true).Timing.StartsWith("3 cicli bus"));
        Check("eZ80 TST", D("ED 04", true).Timing.StartsWith("2 cicli bus"));
        Check("eZ80 TST immediato", D("ED 64 80", true).Mnemonic == "TST 80h" && D("ED 64 80", true).Timing.StartsWith("3 cicli bus"));
        Check("eZ80 TSTIO immediato", D("ED 74 80", true).Mnemonic == "TSTIO 80h" && D("ED 74 80", true).Timing.StartsWith("4 cicli bus"));
        Check("eZ80 NEG non confuso con TST", D("ED 44", true).Mnemonic == "NEG");
        Check("eZ80 riferimento diverso da Z80", ezinc.ManualUrl.Contains("UM0077.pdf"));

        // Destinations are actual one-based PDF pages from official bookmarks;
        // JP nn is printed page 262 but PDF page 276 (not 278).
        foreach (var (hex, page) in new[] {
            ("C3 01 00",276), ("C2 01 00",277), ("E9",289), ("DD E9",290), ("FD E9",291),
            ("3C",179), ("34",181), ("DD 34 FE",182), ("FD 34 FE",183), ("03",212), ("DD 23",213), ("3D",184), ("0B",215),
            ("3E 01",86), ("78",85), ("21 00 10",113), ("DD 21 00 10",114), ("2A 00 10",116), ("ED 4B 00 10",117), ("22 00 10",121),
            ("CB 46",259), ("DD CB FE 46",261), ("FD CB FE 46",263), ("CB 00",227), ("FD CB FE 06",233), ("CB 1E",241),
            ("ED 52",206), ("DB 03",309), ("ED 40",310), ("D3 03",320), ("ED 41",321), ("FF",306), ("ED B0",146)
        }) Check("manuale Z80: " + D(hex).Mnemonic + " → " + page, D(hex).ManualUrl == InstructionDecoder.Z80Manual + "#page=" + page);
        foreach (var (hex, adl, page) in new[] {
            ("3C",false,168), ("03",false,170), ("33",false,171), ("DD 23",false,166), ("DD 24",false,165),
            ("C3 01 00",false,194), ("C2 01 00",false,189), ("DD E9",false,192),
            ("21 56 34 12",true,239), ("40 21 34 12",true,239), ("31 56 34 12",true,245), ("DD 21 56 34 12",true,217),
            ("ED 38 03",false,162), ("ED 4C",false,255), ("ED 7C",false,256), ("A0",false,113), ("FE 03",false,130),
            ("ED 64 03",false,373), ("ED 04",false,374), ("ED 34",false,372), ("00",false,258), ("ED 44",false,257), ("ED B0",false,250)
        }) Check("manuale eZ80: " + D(hex,true,adl).Mnemonic + " → " + page, D(hex,true,adl).ManualUrl == Ez80Timing.Manual + "#page=" + page);
        Check("pulsante indica la pagina PDF", D("C3 01 00").ManualButtonText == "Apri manuale · pagina PDF 276");
        Check("opcode non documentati e byte incompleti non inventano una pagina", D("CB 30").ManualUrl == InstructionDecoder.Z80Manual && D("DD").ManualUrl == InstructionDecoder.Z80Manual);

        var trace = new ClockTraceTracker();
        LogElement L(uint clock, byte control, byte flags, ushort pc = 0x2000) => new(pc, 0x3C,
            (control & 32)!=0,(control &16)!=0,(control&8)!=0,(control&4)!=0,(control&2)!=0,(control&1)!=0,"",
            (control&64)!=0,(control&128)!=0, clock, flags);
        trace.Observe(L(0,0x3F,2)); // an idle sample establishes the preceding M1-high level
        var rows = new[] { L(1,0x3E,6), L(2,0x1A,7), L(3,0x1A,7), L(4,0x1A,7), L(5,0x1A,6), L(6,0x3D,2), L(7,0x39,2), L(8,0x3E,6,0x2001) };
        foreach(var row in rows) trace.Observe(row);
        Check("T-state esclusi i WAIT", rows[1].TStateText == "T2" && rows[2].TStateText == "Tw1" && rows[5].TStateText == "T3" && trace.Summary.Contains("4 T programma · 3 WAIT"));
        trace.Observe(L(9,0x5A,2,0x2001));
        Check("clock debugger separati", trace.Summary.Contains("1 clock debugger"));
        var gap = L(20,0x1A,7); trace.Observe(gap);
        Check("campioni omessi invalidano il T-state", gap.TStateText == "Tw?");
        var unknown=L(21,0x1A,7); trace.Observe(unknown); Check("attesa dopo un buco non dichiarata T misurato", unknown.TStateText == "Tw?");
        var halt=L(22,0x1A,11); trace.Observe(halt);
        Check("HALT non diventa una nuova istruzione", halt.TStateText == "—" && halt.TracePhase == "HALT");
        var old=new LogElement(0,0,false,true,true,false,true,false,""); trace.Observe(old);
        Check("log legacy non numerato come clock", old.ClockText == "—" && old.TStateText == "—");
        TestReadPath(Check);
        return failures;
    }
    private static void TestReadPath(Action<string,bool> check)
    {
        var vm = new MainViewModel(new NoDialogs());
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var manager = (SerialManager)typeof(MainViewModel).GetField("_serialManager",flags)!.GetValue(vm)!;
        var receive=typeof(SerialManager).GetMethod("HandleSerialMessage",flags)!;
        void Rx(string s) { receive.Invoke(manager,new object[]{s}); Dispatcher.UIThread.RunJobs(); }
        void Set(string name,object value) => typeof(SerialManager).GetField(name,flags)!.SetValue(manager,value);
        try {
            typeof(MainViewModel).GetField("_autoRefreshRegisters",flags)!.SetValue(vm,true);
            vm.IsSerialOpened=true;
            Rx("!INFO:CARD:ALEX80µ,SCLK:A,FCLK:500,ECLK:1,SMEM:A,LOG:OFF,DEBUG:ON,MONITOR:OFF;");
            typeof(MainViewModel).GetField("_instructionPc",flags)!.SetValue(vm,(uint?)0xFFFFu);
            typeof(MainViewModel).GetField("_instructionNeedsRead",flags)!.SetValue(vm,false);
            var purpose=typeof(SerialManager).GetField("readPurpose",flags)!;
            purpose.SetValue(manager,Enum.Parse(purpose.FieldType,"Instruction"));
            Set("instructionAddress",0xFFFFu); Set("instructionChunkAddress",0xFFFFu); Set("instructionWrapMask",0xFFFFu); Set("instructionChunkLength",1);
            check("Step attende la lettura dell'istruzione", !vm.StepCommand.CanExecute(null));
            int memoryEvents=0; manager.DataReceived += _=>memoryEvents++;
            Rx("!STATUS1;"); Rx("!DATAww==;"); Rx("!STATUS2;"); // JP opcode at FFFF, rest wraps to 0000
            check("peek memoria: secondo segmento avvolto a 0000", (uint)typeof(SerialManager).GetField("instructionChunkAddress",flags)!.GetValue(manager)! == 0);
            var tail=new byte[15]; tail[0]=0x34;tail[1]=0x12;
            Rx("!STATUS1;");Rx("!DATA"+Convert.ToBase64String(tail)+";");Rx("!STATUS2;");
            check("scheda PC alimentata dai byte ricevuti", vm.CurrentInstruction?.Mnemonic == "JP 1234h" && vm.CurrentInstruction.Address == 0xFFFF);
            check("peek non sovrascrive la vista memoria", memoryEvents==0 && vm.DataTable.Count==0 && !manager.IsReadPending);
            vm.CurrentInstruction = InstructionDecoder.Decode(0x2000,new byte[]{0x3C}); vm.AReg=0x28;
            typeof(MainViewModel).GetField("_hasRegisterSnapshot",flags)!.SetValue(vm,true);
            typeof(MainViewModel).GetField("_registersAreCurrent",flags)!.SetValue(vm,true);
            check("previsione INC dai registri della scheda", vm.InstructionPrediction.Contains("28h → 29h"));
            vm.StepCommand.Execute(null);
            check("Step invalida la scheda precedente", vm.CurrentInstruction is null);
            // Both the legacy and extended !LOG forms remain accepted.
            Rx("!LOGID A8Gg==;".Replace(" ",""));
            byte[] extended={0x20,0,0x3C,0x1A,7,0,0,0,42};
            Rx("!LOG"+Convert.ToBase64String(extended)+";");
            check("log esteso: clock/WAIT reali letti", vm.LogTable[^1].ClockNumber==42 && vm.LogTable[^1].Waiting==true);
            Rx("!INFO:CARD:eZ80Test,SCLK:E,FCLK:0,ECLK:1,SMEM:E;");
            var registers = new byte[30]; registers[2]=0x34; registers[3]=0x80;
            registers[24]=0; registers[25]=0x12; registers[26]=0x34;
            Rx("!DUMP24"+Convert.ToBase64String(registers)+";");
            check("eZ80 modo Z80 usa MBASE:PC", (uint?)typeof(MainViewModel).GetField("_instructionPc",flags)!.GetValue(vm)==0x341234u);
            registers[3]=0x90; registers[24]=0xFF; registers[25]=0xF0; registers[26]=0;
            Rx("!DUMP24"+Convert.ToBase64String(registers)+";");
            check("eZ80 ADL conserva PC a 24 bit", (uint?)typeof(MainViewModel).GetField("_instructionPc",flags)!.GetValue(vm)==0xFFF000u);
            purpose.SetValue(manager,Enum.Parse(purpose.FieldType,"Memory"));
            Rx("!ERROR:READ FAILED;"); check("errore memoria sblocca i comandi", !manager.IsReadPending);
            purpose.SetValue(manager,Enum.Parse(purpose.FieldType,"Instruction"));
            typeof(SerialManager).GetMethod("FailInstructionRead",flags)!.Invoke(manager,new object[]{"Timeout test"});
            Rx("!STATUS2;"); check("risposta peek scaduta non finisce nella memoria", memoryEvents==0);
            vm.CurrentInstruction = InstructionDecoder.Decode(0x2000,new byte[]{0x3C});
            purpose.SetValue(manager,Enum.Parse(purpose.FieldType,"Instruction"));
            vm.ResetCommand.Execute(null); Dispatcher.UIThread.RunJobs();
            check("reset cancella lettura pendente e scheda precedente", !manager.IsReadPending && vm.CurrentInstruction is null && vm.ClockSummary == "Nessun intervallo completo misurato.");
            Rx("!STATUS2;"); check("risposta peek dopo reset ignorata", memoryEvents==0);
            Rx("!STATUS1;"); Rx("!DATAqg==;"); Rx("!STATUS2;");
            check("lettura seriale legacy conserva la destinazione memoria", memoryEvents==1);
            Rx("!INFO:CARD:ALEX80µ,SCLK:A,FCLK:500,ECLK:0,SMEM:A,LOG:OFF,DEBUG:OFF;");
            check("clock manuale disabilitato con firmware precedente", !vm.PulseClockCommand.CanExecute(null));
            Rx("!INFO:CARD:ALEX80µ,SCLK:A,FCLK:500,ECLK:0,SMEM:A,LOG:OFF,DEBUG:OFF,CLKPULSE:ON;");
            check("clock manuale disponibile a clock fermo e Debug off", vm.PulseClockCommand.CanExecute(null));
            vm.IsClockActive=true; check("clock manuale escluso con clock automatico", !vm.PulseClockCommand.CanExecute(null));
            vm.IsClockActive=false; vm.DebugEnabled=true;
            check("clock manuale escluso con WAIT debugger", !vm.PulseClockCommand.CanExecute(null));
            vm.DebugEnabled=false;
            Set("clockPulsePending",true);
            check("impulso pendente blocca duplicati, Step e lettura memoria", !vm.PulseClockCommand.CanExecute(null) && !vm.StepCommand.CanExecute(null) && !vm.ReadMemoryCommand.CanExecute(null));
            int beforePulses=vm.LogTable.Count;
            void Pulse(ushort address,byte control,byte trace,uint clock)
            {
                byte[] sample={(byte)(address>>8),(byte)address,0x3C,control,trace,(byte)(clock>>24),(byte)(clock>>16),(byte)(clock>>8),(byte)clock};
                Rx("!PULSE"+Convert.ToBase64String(sample)+";");
            }
            Pulse(0x2000,0x3E,4,100); // T1, before MREQ/RD; explicit first M1 marker
            check("impulso confermato alimenta il log anche con LOG off", !manager.IsClockPulsePending && !vm.LogEnabled && vm.LogTable.Count==beforePulses+1);
            check("fetch manuale identifica istruzione senza WAIT", (uint?)typeof(MainViewModel).GetField("_instructionPc",flags)!.GetValue(vm)==0x2000u && vm.InstructionTitle=="ISTRUZIONE SUL BUS");
            // Feed the dedicated rb response, just as a real stopped board does.
            purpose.SetValue(manager,Enum.Parse(purpose.FieldType,"Instruction"));
            Set("instructionAddress",0x2000u); Set("instructionChunkAddress",0x2000u); Set("instructionWrapMask",0xFFFFu); Set("instructionChunkLength",16);
            ((List<byte>)typeof(SerialManager).GetField("instructionBytes",flags)!.GetValue(manager)!).Clear();
            typeof(MainViewModel).GetField("_instructionNeedsRead",flags)!.SetValue(vm,false);
            byte[] instruction=new byte[16]; instruction[0]=0x3C;
            Rx("!STATUS1;"); Rx("!DATA"+Convert.ToBase64String(instruction)+";"); Rx("!STATUS2;");
            check("scheda manuale descrive INC e non predice con registri obsoleti", vm.CurrentInstruction?.Mnemonic=="INC A" && vm.InstructionPrediction=="" && vm.InstructionStatus.Contains("registri non riletti") && memoryEvents==1);
            Pulse(0x2000,0x1A,4,101); Pulse(0x0043,0x3D,0,102);
            check("cicli dello stesso fetch e refresh mantengono la scheda", vm.CurrentInstruction?.Address==0x2000u && !(bool)typeof(MainViewModel).GetField("_instructionNeedsRead",flags)!.GetValue(vm)!);
            Pulse(0x2001,0x3E,4,104);
            check("nuovo fetch manuale aggiorna l'indirizzo e invalida la scheda", (uint?)typeof(MainViewModel).GetField("_instructionPc",flags)!.GetValue(vm)==0x2001u && vm.CurrentInstruction is null);
            Set("clockPulsePending",true); Rx("!ERROR:STOP CLOCK AND DISABLE DEBUG FIRST;");
            check("errore impulso sblocca il comando e cancella lo stato incerto", !manager.IsClockPulsePending && vm.CurrentInstruction is null && vm.InstructionStatus.Contains("STOP CLOCK"));
            Set("clockPulsePending",true); Rx("!PULSEbad!;");
            check("impulso malformato sblocca il comando", !manager.IsClockPulsePending);
            Rx("!INFO:CARD:eZ80Test,SCLK:E,FCLK:0,ECLK:1,SMEM:E,CLKPULSE:ON;");
            check("nessun clock manuale sulla scheda eZ80", !vm.PulseClockCommand.CanExecute(null));
        } finally {
            ((System.Timers.Timer)typeof(MainViewModel).GetField("_serialPortCheckTimer",flags)!.GetValue(vm)!).Dispose();
            manager.SerialPort.Dispose();
        }
    }
    private sealed class NoDialogs : IDialogService { public Task ShowErrorAsync(string title,string message)=>Task.CompletedTask; }
}
