using System.Reflection;
using Alex80_IDE;
using Alex80_IDE.Services;
using Avalonia.Threading;

internal static class ListingExecutionTests
{
    public static int Run()
    {
        var failures = 0;
        void Check(string name, bool passed)
        {
            Console.WriteLine($"{(passed ? "ok  " : "FAIL")} listing: {name}");
            if (!passed) failures++;
        }

        BuildResult Build(string source, string cpu = "Z80") => ProjectBuilder.Build(new BuildRequest
        {
            Sources = new[] { new BuildSource("test.asm", null, source) },
            CpuName = cpu,
            TasmCompatibility = false
        });

        const string source = "magic equ 0ABCDh\n\torg 2000h\nstart:\n\tld a,42h\n\tld ix,1234h\n\tld (9000h),a\n\tjp start\n";
        var built = Build(source);
        var map = ListingAddressMap.Create(built.Listing);
        Check("indirizzi e prefissi sulla riga che emette i byte", built.Success &&
            map[0x2000] == map[0x2001] && map[0x2002] == map[0x2005] &&
            map[0x2000] != map[0x2002] &&
            built.Listing.Split('\n')[map[0x2000] - 1].Contains("ld a,42h"));
        Check("EQU e simboli non sono codice", !map.ContainsKey(0xABCD));

        var data = Build("\torg 2100h\n\tdefb 1,2,3,4,5,6,7,8\n\tdefs 10h\n\tret\n");
        var dataMap = ListingAddressMap.Create(data.Listing);
        Check("righe di continuazione e spazi riservati", data.Success &&
            dataMap[0x2100] != dataMap[0x2104] && dataMap[0x2104] == dataMap[0x2107] &&
            !dataMap.ContainsKey(0x2108) && dataMap.ContainsKey(0x2118));
        var wide = Build("\tassume adl=1\n\torg 0FFF000h\n\tld hl,123456h\n", "EZ80");
        var wideMap = ListingAddressMap.Create(wide.Listing);
        Check("indirizzi eZ80 non troncati", wide.Success && wideMap.ContainsKey(0xFFF000) &&
            !wideMap.ContainsKey(0xF000));

        foreach (var mode in new[] { Alex80_IDE.Models.ProjectLinkMode.SeparateUnits, Alex80_IDE.Models.ProjectLinkMode.SingleUnit })
        {
            var project = ProjectBuilder.Build(new BuildRequest
            {
                Sources = new[] { new BuildSource("first.asm", null, "\torg 3000h\n\tnop\n"),
                    new BuildSource("second.asm", null, "\tret\n") },
                LinkMode = mode, TasmCompatibility = false
            });
            var projectMap = ListingAddressMap.Create(project.Listing);
            Check($"progetto {mode}, secondo file senza ORG", project.Success &&
                projectMap.ContainsKey(0x3000) && projectMap.ContainsKey(0x3001) &&
                project.Listing.Split('\n')[projectMap[0x3001] - 1].Contains("ret"));
        }

        var temp = Directory.CreateTempSubdirectory("alex80-listing-");
        try
        {
            File.WriteAllText(Path.Combine(temp.FullName, "part.inc"), "\tinc a\n");
            var included = ProjectBuilder.Build(new BuildRequest
            {
                Sources = new[] { new BuildSource("main.asm", Path.Combine(temp.FullName, "main.asm"),
                    "twice macro\n\tnop\n\tnop\n\tendm\n\torg 4000h\n\tinclude \"part.inc\"\n\ttwice\n") },
                TasmCompatibility = false
            });
            var includedMap = ListingAddressMap.Create(included.Listing);
            Check("INCLUDE e macro espanse", included.Success &&
                includedMap.ContainsKey(0x4000) && includedMap.ContainsKey(0x4001) && includedMap.ContainsKey(0x4002));
        }
        finally { temp.Delete(true); }

        var vm = new MainViewModel(new NoDialogs());
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var manager = (SerialManager)typeof(MainViewModel).GetField("_serialManager", flags)!.GetValue(vm)!;
        var receive = typeof(SerialManager).GetMethod("HandleSerialMessage", flags)!;
        void Log(ushort address, byte value, byte control = 0x1A)
        {
            receive.Invoke(manager, new object[] { "!LOG" + Convert.ToBase64String(new[] {
                (byte)(address >> 8), (byte)address, value, control }) + ";" });
            Dispatcher.UIThread.RunJobs();
        }
        try
        {
            Check("senza documenti mostra le azioni iniziali e disabilita il seguito", vm.IsWorkspaceEmpty && !vm.CanFollowListingExecution);
            var availability = new List<bool>();
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.CanFollowListingExecution))
                    availability.Add(vm.CanFollowListingExecution);
            };
            var document = new DocumentViewModel { FileName = "test.asm", Text = source };
            vm.OpenDocuments.Add(document);
            vm.SelectedDocument = document;
            Check("un sorgente aperto nasconde le azioni ma non abilita il seguito", !vm.IsWorkspaceEmpty && !vm.CanFollowListingExecution);
            vm.AssembleCommand.Execute(null);
            var listing = vm.SelectedDocument;
            Check("listing assemblato abilita il seguito e notifica la barra", vm.CanFollowListingExecution &&
                listing.HasExecutionListing && availability.LastOrDefault());
            var listingMap = ListingAddressMap.Create(listing.Text);
            Log(0x2000, 0x3E);
            Check("segue il fetch prima del completamento del mnemonico",
                listing.ExecutionLine == listingMap[0x2000] && !listing.IsDirty);
            Log(0x2001, 0x42, 0x1B);
            Log(0x9000, 0x42, 0x2B); // scrittura in memoria
            Log(0x0000, 0x00, 0x18); // refresh: M1/MREQ bassi, RD alto, RFSH basso
            Log(0x2002, 0xC5, 0x5A); // istruzione iniettata
            Check("operandi, dati, refresh e registri non spostano la riga",
                listing.ExecutionLine == listingMap[0x2000]);
            Log(0x2002, 0xDD);
            Log(0x2003, 0x21);
            Log(0x2004, 0x34, 0x1B);
            Log(0x2005, 0x12, 0x1B);
            Check("prefisso resta sulla stessa riga e conserva il mnemonico",
                listing.ExecutionLine == listingMap[0x2002] && vm.LogTable[6].Mnemonic.Contains("IX"));

            vm.SelectedDocument = document;
            vm.SelectedLog = vm.LogTable[2]; // la scrittura dati appartiene a LD A,n
            Check("selezione dati risale al fetch e apre il listing", !vm.FollowListingExecution &&
                vm.SelectedDocument == listing && listing.ExecutionLine == listingMap[0x2000]);
            Log(0x2006, 0x32);
            Log(0x2007, 0x00, 0x1B);
            Log(0x2008, 0x90, 0x1B);
            Check("ispezione storica ferma mentre arrivano nuovi log", listing.ExecutionLine == listingMap[0x2000]);
            vm.FollowListingExecution = true;
            Check("ripresa torna all'ultimo fetch", vm.SelectedLog is null && listing.ExecutionLine == listingMap[0x2006]);
            Log(0x9000, 0x00);
            Check("uscita dal programma rimuove il vecchio indicatore", listing.ExecutionLine == 0);
            Log(0x2000, 0x00);
            Log(0xFFFF, 0x00, 0xBF);
            Check("reset rimuove l'indicatore", listing.ExecutionLine == 0);
            Log(0x2002, 0x00);
            vm.ResetCommand.Execute(null);
            Check("comando reset azzera anche senza log completo", listing.ExecutionLine == 0);

            // Il clear non deve usare indici seriali ormai fuori dalla tabella.
            vm.ClearLogsCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Log(0x2000, 0x00);
            Check("cancella log e ricezione successiva", vm.LogTable.Count == 1 &&
                vm.LogTable[0].Mnemonic.Length > 0 && listing.ExecutionLine == listingMap[0x2000]);
            manager.RestartBoard();
            Log(0x2002, 0x00);
            Check("riavvio disassemblatore con storico preservato", vm.LogTable.Count == 2 &&
                vm.LogTable[1].Mnemonic.Length > 0 && listing.ExecutionLine == listingMap[0x2002]);

            vm.SelectedDocument = document;
            vm.AssembleCommand.Execute(null);
            Check("nuova assemblata azzera l'indicatore", listing.ExecutionLine == 0);
            vm.SelectedLog = vm.LogTable[0];
            Check("log di assemblate precedenti esclusi", listing.ExecutionLine == 0);
            vm.FollowListingExecution = true;
            Log(0x2000, 0x00);
            listing.EditorDocument.Insert(0, "; modifica\n");
            Log(0x2002, 0x00);
            Check("listing modificato invalida la mappa e disabilita la barra", listing.ExecutionLine == 0 &&
                listing.IsDirty && !listing.HasExecutionListing && !vm.CanFollowListingExecution && !availability.Last());

            vm.SelectedDocument = document;
            vm.AssembleCommand.Execute(null);
            Check("riassemblare ripristina disponibilita' e notifica", vm.CanFollowListingExecution && availability.Last());
            Log(0x2000, 0x00);
            vm.IsSerialOpened = true;
            Check("nuova sessione seriale azzera l'indicatore", listing.ExecutionLine == 0);
            Log(0x2000, 0x00);
            vm.IsSerialOpened = false;
            Check("disconnessione azzera l'indicatore", listing.ExecutionLine == 0);
            vm.CloseTabCommand.Execute(listing);
            Log(0x2002, 0x00);
            Check("listing chiuso non viene piu' aggiornato e disabilita la barra", listing.ExecutionLine == 0 &&
                !vm.CanFollowListingExecution && !availability.Last());
            vm.CloseTabCommand.Execute(document);
            Check("chiudere l'ultima scheda ripristina le azioni iniziali", vm.IsWorkspaceEmpty);
            vm.NewTabCommand.Execute(null);
            Check("nuovo file dalle azioni iniziali apre un sorgente", !vm.IsWorkspaceEmpty &&
                vm.SelectedDocument.FileName == "Nuovo.asm" && !vm.CanFollowListingExecution);
            vm.CloseTabCommand.Execute(vm.SelectedDocument);
            Check("azioni iniziali disponibili anche dopo chiusure ripetute", vm.IsWorkspaceEmpty);
        }
        finally
        {
            ((System.Timers.Timer)typeof(MainViewModel).GetField("_serialPortCheckTimer", flags)!.GetValue(vm)!).Dispose();
            manager.SerialPort.Dispose();
        }
        return failures;
    }

    private sealed class NoDialogs : IDialogService
    {
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
    }
}
