# Note di lavoro — revisione dell'interfaccia e allineamento ai firmware

Diario del lavoro sull'IDE e di quello che ne è emerso sui firmware delle tre
schede. Serve da documentazione: raccoglie le decisioni prese e il perché, quello
che è stato verificato e come, e quello che resta da fare.

Ramo di lavoro: `ide-ui-improvements` (24 commit, non ancora uniti a `main`).

---

## 1. Come è cambiata l'interfaccia

### Tool window agganciate ai bordi

I pannelli fissi sono diventati dock a scomparsa nello stile JetBrains: una strip
di icone sempre visibile per bordo, un solo pannello aperto per volta, e ogni tool
window conserva la propria misura fra una sessione e l'altra.

| Dock | Tool window |
|---|---|
| sinistro | Progetto, Assemblatore |
| destro | Registri, Scheda |
| inferiore | Memoria, Bus log, Seriale |

Aggiungere uno strumento significa dichiararlo in `MainViewModel.Layout.cs` e
mettere la vista nel pannello del dock che lo ospita; spostarlo da un bordo
all'altro è un cambio di dock e basta.

### I comandi stanno dove sta la cosa su cui agiscono

È il criterio applicato più volte, ogni volta togliendo qualcosa dalla barra in
alto:

- **Carica bin** è passato dalla toolbar dei sorgenti al pannello Memoria, accanto
  a lettura e scrittura: i byte finiscono lì.
- **Clock avvia/ferma** è passato dalla barra al pannello Scheda, sotto la
  frequenza che governa.
- **Rileggi registri** (era «Dump registri») è passato dalla barra al pannello
  Registri, dove il risultato compare.
- **Apertura, salvataggio, progetto** sono passati al menu File.

Nella barra in alto restano i comandi di uso continuo: assembla, reset, run,
pausa/riprendi, abilita debug, step.

### Menu File

Su macOS finisce nella barra di sistema, su Windows e Linux nella `NativeMenuBar`
in cima alla finestra. È costruito nel codice e non in XAML perché le scorciatoie
devono usare il modificatore della piattaforma (Cmd o Ctrl).

Nota tecnica: `ViewModelCommandProxy` deve propagare `CanExecuteChanged` davvero.
Una voce di menu legge lo stato una volta sola, quando le viene assegnato il
comando, e a quel punto il view model non esiste ancora: con l'evento no-op le
voci restavano spente per sempre.

### Pannello del progetto

Le azioni sui file sono passate a un menu contestuale e i sorgenti si aggiungono
anche trascinandoli sull'elenco. Le azioni sono comandi della riga e non del view
model principale, perché un menu contestuale vive in un popup fuori dall'albero
visuale, dove un binding a un antenato non arriva.

### Registri e flag

I flag del registro F hanno una riga dedicata, un riquadro ciascuno con sigla,
stato e un suggerimento che ne spiega il significato. Prima erano una stringa sola
in una cella, e veniva troncata. Il registro F è lo stesso su Z80 ed eZ80, quindi
la riga serve entrambe le viste.

### Icone

Tutte disegnate su griglia 24×24 e verificate a 15 px, che è la dimensione reale
nella barra.

| Comando | Icona | Nota |
|---|---|---|
| Assembla | martello | il segno di «costruisci» negli IDE |
| Reset | arco di ¾ con punta | prima era una barra con triangolo, cioè «torna all'inizio» dei lettori multimediali |
| Run | triangolo play | invariato |
| Pausa / Riprendi | doppia barra / barra+triangolo | prima erano le parole «Break» e «Continua» |
| Step | freccia che scende nel punto | prima era una freccia orizzontale, cioè il segno dello **step over** |
| Clock | orologio ad anello | il quadrante pieno a 15 px diventa un disco |
| Leggi / Scrivi memoria | frecce speculari col chip | |
| Carica bin | file con freccia | per non confonderlo con la lettura |

Attenzione a un tranello: se una figura sporge dal riquadro di 24 unità,
`PathIcon` riscala tutto per farla stare e l'icona risulta più piccola e
decentrata rispetto alle altre. Va calcolato l'ingombro, non stimato a occhio.

### Tema

Terza scelta «Come il sistema» accanto a chiaro e scuro. Non viene risolta
dall'IDE: diventa `ThemeVariant.Default`, che è il modo con cui Avalonia lascia
decidere alla piattaforma e tiene la variante aggiornata quando l'impostazione di
sistema cambia. Il valore predefinito resta Scuro.

### Impostazioni di assemblaggio

Hanno un pannello loro nel dock sinistro. Restano lì e non in un menu Impostazioni
perché sono impostazioni **del progetto**: finiscono nel file `.a80` e cambiano il
binario che esce.

Senza un progetto aperto non sono «valori predefiniti», come diceva il testo
vecchio: sono le impostazioni con cui *Assembla* lavora davvero sul sorgente
selezionato, e insieme quelle da cui parte un progetto nuovo. Per questo non è
stato aggiunto un dialogo dei predefiniti: avrebbe creato due posti da cui
modificare gli stessi valori.

---

## 2. Cosa fa davvero ogni scheda

Tabella ricavata leggendo i tre firmware. È la base di tutte le abilitazioni
dell'interfaccia.

| Comando IDE | Stringa seriale | ALEX80 | ALEX80µ | eZ80 (ZDI) |
|---|---|---|---|---|
| Assembla | *(locale)* | ok | ok | ok |
| Avvia/ferma clock | `sclk n`, `clk start/stop` | ok, solo con clock da Arduino | ok, solo con clock da Arduino | **rifiutato**: «the eZ80 clocks itself» |
| Reset | `reset` | ok | ok | ok |
| Run | `nmi` / `go addr` | ok | ok | ok |
| Pausa / Riprendi | `break` / `cont` | — | — | ok |
| Abilita debug | `debug on/off` | ok | ok | **accettato ma non fa nulla** |
| Step | `s` | ok | ok | ok |
| Rileggi registri | `d` | **richiede modo monitor** | ok, con memoria Arduino + debug | ok |
| Leggi / Scrivi memoria | `rb` / `wb` | ok | ok | ok |
| Log del bus | `log on/off` | ok | ok | **rifiutato**: «no bus visibility over ZDI» |

### Le tre scoperte che contano

**`monitor on` sull'ALEX80 non è un flag passivo.** Forza il clock a 500 Hz, lo
commuta su Arduino, lo avvia, e **scrive in RAM un programma demo a 0x8000 più una
routine monitor a 0xF010** con il vettore a 0xF000. Il comando `d` lo richiede,
quindi l'IDE non può accenderlo per leggere i registri: cancellerebbe il programma
dell'utente. Nel sorgente dell'ALEX80 esiste anche la via dell'iniezione — quella
che il micro usa — ma è **commentata**.

**`go` e `cont` non sono lo stesso comando.** `go <addr>` fa break, **scrive il
PC** e riparte; `cont` riprende dal PC dov'è. Per questo il pulsante Run e il
pulsante pausa/riprendi restano due comandi distinti: uno solo ne perderebbe uno.

**`alex_dump` si arrangia da solo.** Chiama `zdi_break_and_wait()`, legge, e in
fondo fa `if (!(status & 0x80)) zdi_continue();` con lo `status` catturato prima:
se la CPU stava girando la ferma, legge e la rimette in moto. Quindi la lettura
dei registri funziona anche a programma in corsa, e non va vincolata al break.

### Lo step è uno step *into* su tutte e tre

- **eZ80**: `zdi_debug_step` alza il bit SINGLE_STEP di `BRK_CTL` (0x81 → 0x80).
- **ALEX80 e ALEX80µ**: `s` rilascia WAIT, e in `debug_mode` il manager lo rialza
  al fronte di M1 successivo, cioè all'inizio dell'istruzione dopo.

Una istruzione sola in tutti i casi: dentro le `CALL` ci si entra.

---

## 3. Regole di abilitazione

Il principio: un comando è attivo **quando la scheda collegata sa davvero
eseguirlo**, e quando non lo è c'è scritto accanto il perché.

### La scheda deve essersi presentata

`IsBoardConnected` non è «porta aperta» ma «è arrivato `!INFO`»: finché non
arriva non si sa se dall'altra parte c'è una scheda ALEX80 o un dispositivo
qualunque. Chiudendo la porta se ne va anche l'identità della scheda, non solo il
nome, altrimenti si continuerebbe a decidere in base a un hardware che non c'è più.

Poiché ora l'interfaccia dipende da `!INFO`, `OpenCloseSerialPortAsync` ripete la
richiesta fino a due volte prima di rinunciare: una risposta persa non è più solo
un badge vuoto ma un IDE inutilizzabile.

### Con una scheda collegata decide la scheda

`IsEz80Context` con la porta aperta guarda la scheda, non la CPU di progetto. Senza
questa precedenza, una ALEX80 collegata con progetto impostato su eZ80 mandava a
«play» un comando ZDI invece dell'NMI, faceva comparire il pulsante di break e
mostrava gli indirizzi a 24 bit su una macchina che ne ha 16. A porta chiusa decide
la CPU di progetto, perché assemblare non richiede una scheda.

### CPU effettiva

`EffectiveCpuName` è la CPU per cui si assembla davvero: con una ALEX80 o una
ALEX80µ collegata è Z80 comunque. La usano sia la status bar sia le richieste di
assemblaggio — un indicatore che dicesse Z80 mentre l'assemblatore produce codice
eZ80 sarebbe peggio che non averlo.

Il file di progetto **non viene toccato**: la scelta resta scritta dentro e torna a
valere appena la scheda viene scollegata. Il disaccordo si vede in tre punti: nel
pannello Assemblatore, in coda all'esito dell'assemblata, e come intestazione a
commento in cima al listato — che finisce anche su disco come `.lst`, così chi lo
ritrova mesi dopo capisce perché contiene codice Z80.

**Conseguenza da tenere presente**: lo stesso progetto produce un binario diverso a
seconda di cosa è collegato.

### Riepilogo delle condizioni

| Comando | Attivo quando |
|---|---|
| Clock avvia/ferma, frequenza | scheda ALEX80 o ALEX80µ **e** clock generato dall'Arduino |
| Modo Z80 (ADL) | scheda eZ80 |
| Scelta CPU | nessuna ALEX80 collegata (con ALEX80 è bloccata su Z80) |
| Leggi / Scrivi memoria | scheda presentata |
| Run | scheda presentata (anche ALEX80µ: il suo firmware implementa `nmi`) |
| Pausa / Riprendi | scheda eZ80 |
| Rileggi registri | eZ80 sempre; ALEX80µ con memoria Arduino + debug + clock in moto; ALEX80 mai |

---

## 4. Modifiche al firmware ALEX80µ

Fatte in `alex80micro_manager_terminal/v_1_5/alex80micro_manager_terminal`, per
avere il **dump automatico dei registri dopo ogni step**.

Non si poteva fare dall'IDE mandando `d` dopo `s`: il firmware legge `z80_add`, il
PC visibile durante M1, e dopo `s` la CPU deve ancora eseguire l'istruzione. A
50 Hz un ciclo di clock dura 20 ms e un'istruzione ne prende almeno quattro,
mentre i due comandi seriali arrivano a millisecondi di distanza — il `d`
arriverebbe sempre troppo presto e inietterebbe all'indirizzo sbagliato.

Il momento giusto è il fronte di M1 che chiude lo step, e lo vede solo
`clockTrigger`, che è un handler di interrupt e non può preparare l'iniezione. Da
qui i tre passaggi:

1. `s` imposta `step_dump_requested` (solo se il dump è possibile);
2. l'interrupt riconosce la fine dello step e alza `step_dump_ready`;
3. `loop()` ricontrolla le condizioni e chiama `dumpRegistersByInjection()`.

Modifiche, in ordine:

- `alex80micro_manager_terminal.ino`: due flag `volatile` dopo `debug_mode`;
  `dumpByInjectionPossible()` e `dumpRegistersByInjection()` messe **prima di
  `clockTrigger()`**, cioè dopo le dichiarazioni delle globali che usano; il
  passaggio di consegne dentro il blocco M1 di `clockTriggerMemArduino`; il consumo
  del flag in `loop()`.
- `serial.ino`, **solo in `myCommandHandler`** — `serialLoop` non è più chiamato
  dal loop ed è codice morto: `s` richiede il dump, `reset` azzera la richiesta
  pendente, e il ramo di iniezione di `d` chiama la funzione condivisa.

Verificato che compila su entrambi i target dichiarati:

```
arduino:avr:uno                 27880 byte (86% flash), 1480 byte (72% RAM)
arduino:renesas_uno:unor4wifi   88116 byte (33% flash), 14436 byte (44% RAM)
```

Sull'Uno il margine è poco e non è migliorato.

---

## 5. Da fare e da verificare

### Da provare su hardware — ALEX80µ

Il dump automatico dopo lo step **non è mai stato provato su scheda**. Da
verificare:

- che il dump arrivi con il PC giusto, cioè quello dell'istruzione *dopo* quella
  eseguita;
- che non ci siano effetti sul timing del WAIT quando dump automatico e step si
  susseguono in fretta;
- che uno step con clock fermo, o con memoria esterna, non lasci flag pendenti che
  facciano partire un dump inatteso più tardi.

Se stepping e dump si pestano i piedi, il sospetto numero uno è che `loop()` arrivi
troppo presto dopo il fronte di M1.

### Da decidere — firmware ALEX80

**Riabilitare la via dell'iniezione per il dump dei registri.** Oggi `d` richiede
il modo monitor, che sovrascrive la RAM: di fatto la lettura dei registri non è
utilizzabile mentre si lavora su un proprio programma, ed è per questo che nell'IDE
il pulsante è spento su questa scheda. Nel sorgente il ramo di iniezione esiste ma
è commentato — va capito **perché** è stato disattivato prima di riattivarlo.

Se venisse riabilitato, seguirebbero: il dump automatico dopo lo step come sul
micro, e il pulsante «Rileggi dalla scheda» attivo anche su ALEX80.

### Da decidere — firmware eZ80 e IDE

- **`debug on/off` è un no-op** (`{ alex_ok(); return true; }`): il pulsante
  «Abilita» cambia etichetta e mette `DebugEnabled = true`, ma sull'eZ80 non
  cambia niente. Sull'eZ80 il controllo dell'esecuzione è già pausa/riprendi.
  Da nascondere il pulsante su questa scheda, oppure da implementare.
- **Il log del bus è rifiutato** («no bus visibility over ZDI») ma la tool window
  «Bus log» e il suo pulsante Abilita restano attivi. Da spegnere su eZ80.

### Da decidere — IDE

**Run sull'eZ80 usa il campo Indirizzo del pannello Memoria**, e dalla barra non si
vede da dove partirà: se quel pannello è chiuso non lo si sa proprio. Tre strade:
lasciare com'è, mostrarlo nel suggerimento di Run, oppure spostare l'avvio da
indirizzo accanto al campo che usa.

---

## 6. Note di metodo

**Non esiste una suite di test automatici.** `AssemblerTests` è un progetto console
con un `Program.cs`, quindi `dotnet test` non esegue nulla. Le verifiche di questo
lavoro sono state fatte facendo stampare all'applicazione lo stato calcolato per
ogni combinazione di scheda e configurazione, e leggendo l'esito.

**Le impostazioni utente vanno protette durante le verifiche.** Il file
`~/Library/Application Support/Alex80_IDE/settings.json` viene riscritto da diverse
proprietà del view model (`CpuName`, tema, stato dei dock): una prova che imposta
la CPU la salva su disco. Va fatto uno snapshot prima e ripristinato dopo.
