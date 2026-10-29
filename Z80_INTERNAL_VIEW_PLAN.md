# Piano: vista didattica interna dello Z80

Data: 29 ottobre 2026. Stato: proposta pianificata, nessuna implementazione.

## Obiettivo e soluzione proposta

Mostrare come un'istruzione coinvolge registri, ALU, controllo, bus, memoria
e periferiche mentre la scheda avanza per T-state. Ambito esclusivamente Z80;
eZ80 e ZDI restano esclusi.

Si propone una finestra Avalonia non modale **Dentro lo Z80**, aperta dal menu
Visualizza, utilizzabile accanto all'IDE o su un secondo monitor. La finestra
condivide sessione e comandi con l'IDE: nessuna seconda connessione seriale.
Lo schema sarà un controllo riutilizzabile, così da poterlo ospitare in futuro
anche in un pannello, senza introdurre ora un sistema generale di docking.

Lo schema sarà disegnato appositamente con geometrie e testo Avalonia,
ridimensionabile e coerente con tema e scaling. Il manuale Zilog fornisce il
riferimento architetturale e i timing; i collegamenti al manuale rimangono
disponibili. Una pagina raster del manuale è poco adatta a selezione,
evidenziazione, traduzione e ridimensionamento.

## Distinzione fondamentale: osservazione e spiegazione

Il protocollo espone il bus esterno, non i trasferimenti interni della CPU.
Il prodotto deve distinguere, anche con etichette oltre al colore:

| Informazione | Presentazione |
|---|---|
| Indirizzo, dato e segnali nel campione | **Osservato**, con fase di campionamento |
| Dato fornito dal servizio di memoria del manager, se identificabile | **Pilotato dal manager**, senza attribuirgli una misura indipendente |
| Registri restituiti da un dump | **Letto dalla scheda**, con punto della traccia e validità |
| Ciclo macchina riconosciuto da opcode e sequenza dei campioni | **Ricostruito**, con stato di sincronizzazione |
| Registri coinvolti, percorso funzionale e attività ALU | **Modello didattico** |
| Risultato calcolabile da operandi noti | **Previsto**, separato dal valore letto |
| Informazione non disponibile o ricostruzione non verificata | **Non disponibile** o **Non sincronizzato** |

Il manuale descrive architettura, risultati e timing esterni; questi elementi
non bastano a certificare ogni trasferimento interno al singolo T-state.
Le frecce interne illustrano il ruolo funzionale dei blocchi nell'intervallo
compatibile con il modello, senza promettere il comportamento dei latch fisici.
Non si assegna un fronte preciso a un aggiornamento interno senza una fonte
specifica verificata. Nessun valore sconosciuto viene sostituito con zero.

La lettura dei registri Z80 richiede lavoro aggiuntivo della CPU: la nuova
vista non deve richiedere dump a ogni T-state. Un dump esplicito usa il percorso
esistente e conserva separati clock del programma, WAIT e clock del debugger.
Il modello non sovrascrive mai i registri letti mostrati nell'IDE.

## Base disponibile nel codice

Rilevazione da sorgenti locali, non da una prova del firmware caricato:

| Componente | Riutilizzo / limite |
|---|---|
| `Models/LogElement.cs` | Indirizzo, dato, sei segnali attivi bassi, contatore opzionale, iniezione/reset, metadati WAIT e HALT |
| `Models/ClockTraceTracker.cs` | T relativi all'istruzione, WAIT, discontinuità e separazione dei clock debugger; non è un decodificatore completo dei cicli macchina |
| `Models/InstructionClockStepper.cs` | Avanzamento per impulsi confermati; nessuna duplicazione nella nuova finestra |
| `InstructionReference/InstructionDecoder.cs` e `InstructionInfo.cs` | Opcode, durata nominale e varianti; servono descrizioni strutturate aggiuntive per i percorsi interni |
| `InstructionReference/InstructionSemantics.cs` | Descrizioni funzionali già disponibili; evitare di usare il testo tradotto come chiave del modello |
| `ViewModels/MainViewModel.Instruction.cs` | Ricezione impulsi, identificazione istruzione e lettura dei byte dalla scheda |
| `ViewModels/MainViewModel.RegisterRefresh.cs` | Validità snapshot e richieste di rilettura |
| `ViewModels/MainViewModel.Listing.cs` | Selezione del log e navigazione verso il listing |
| `Views/MainWindow.axaml.cs`, `EditorWindow` | Precedente per finestra secondaria non modale con stato condiviso |
| `AssemblerTests` | Infrastruttura esistente per regressioni seriali, decoder e stepping |

La roadmap del 28 ottobre fotografa una versione precedente del protocollo.
Il codice attuale accetta anche record estesi a nove byte, con contatore fisico
e metadati. Le attività TIM-01/TIM-02 vanno verificate contro questo stato prima
di implementarle; non occorre progettare un nuovo protocollo per il primo rilascio.

### Supporto iniziale delle schede

| Configurazione | Comportamento proposto |
|---|---|
| ALEX80µ con manager aggiornato nell'esempio della libreria, clock Arduino fermo e capacità `CLKPULSE` | Primo target per avanzamento reale di un T-state e vista sincronizzata |
| Tracce estese consecutive, anche da esecuzione continua | Consultazione dei campioni; aggiornamento grafico raggruppato senza perdere i dati |
| Firmware legacy con record a quattro byte | Segnali e bus disponibili, nessun indice T inventato e spiegazione interna solo dove supportata |
| ALEX80 modulare | Schema disponibile; vista live subordinata alle capacità effettive. Nel sorgente `alex80_manager` consultato l'invio computer `!LOG` è commentato |
| Clock esterno | Nessuna promessa di arresto o impulso singolo; usare esclusivamente capacità dichiarate e verificate |
| eZ80 | Vista non disponibile per la sessione live |

La capacità si determina dalla sessione e dal firmware, non dal solo nome della
scheda. Il primo incremento modifica soltanto `Alex80_IDE`. La libreria e i
firmware vengono consultati come riferimento; eventuali estensioni per ALEX80
modulare costituiscono un'attività separata. Nessuna modifica a
`ALEX80ArduinoLibrary` o `zdi_arduino` è prevista.

## Contenuto della finestra

- Barra superiore: istruzione, indirizzo iniziale, stato Live/Consultazione,
  posizione T nell'istruzione, eventuale ciclo macchina e T locale, WAIT.
- Comandi: un T-state, un'istruzione, ferma avanzamento; riuso degli stessi
  comandi e condizioni di disponibilità dell'IDE. In consultazione i pulsanti
  precedente/successivo spostano soltanto il cursore; per riprendere i comandi
  hardware si torna esplicitamente al campione live.
- Schema centrale: A/F e ALU, B/C/D/E/H/L e coppie, PC/SP/IX/IY,
  registro istruzione e decodifica, controllo, bus dati e indirizzi.
- Registri alternativi e I/R presenti in un livello di dettaglio espandibile;
  il dump corrente non li fornisce e i loro valori rimangono non disponibili.
  Non confondere il registro I con il registro istruzione, indicato come IR.
- All'esterno: memoria e periferiche I/O, con indirizzo, direzione del dato e
  segnali disponibili `/M1`, `/MREQ`, `/IORQ`, `/RD`, `/WR`, `/RFSH`.
- Dettaglio inferiore: breve frase in italiano sul campione/intervallo,
  operandi conosciuti, provenienza dei valori e collegamento al manuale.
- Striscia dei T-state con selezione sincronizzata al Bus log. La futura vista
  Timing condividerà lo stesso cursore; un diagramma completo con zoom e
  cursori di misura rimane in TIM-01.

Si mostrano `T5 dell'istruzione` e, solo quando riconosciuto, `M2 · T1` come
coordinate distinte. I campioni non misurano fronti intermedi: oggi viene
campionato immediatamente prima del fronte di discesa, una volta per clock
completo. Un eventuale CLK idealizzato va etichettato come riferimento nominale.
Il bit WAIT descrive l'attesa del debugger, non una misura generale di ogni
sorgente elettrica di `/WAIT`. Segnali assenti, come INT/NMI/BUSACK, non vengono
visualizzati come se fossero acquisiti.

La memoria viene chiamata genericamente **Memoria** finché un profilo o una
configurazione verificata non ne distingue ROM, RAM e servizio Arduino.
Le uscite LED possono essere evidenziate per ALEX80µ, dove qualunque OUT le
interessa; non si applica automaticamente questa regola ad ALEX80 modulare.

## Modello tecnico proposto

Flusso: eventi seriali esistenti → registrazione condivisa → interpretazione
Z80 → stato della vista → schema e testo. I nomi seguenti sono proposte:

1. `ExecutionTraceSession`: identificativo sessione, campioni immutabili,
   capacità, discontinuità e cursore live/storico. Punto comune con DID-01 e
   TIM-01; adattamento circoscritto, senza rifare la gestione seriale.
2. `Z80CycleInterpreter`: associa campioni e opcode a fetch, letture/scritture,
   I/O, refresh e intervalli interni. Mantiene distinti T globale, ciclo M,
   T locale, WAIT e qualità della ricostruzione.
3. `Z80TeachingCatalog`: descrizioni strutturate per famiglie opcode/operandi,
   blocchi coinvolti, intervalli e riferimenti. Non ricava il comportamento
   analizzando le stringhe descrittive.
4. `Z80TeachingFrame`: riferimenti ai campioni, blocchi/frecce attivi,
   spiegazione e valori con origine e validità; riproducibile senza hardware.
5. `Z80TeachingViewModel` e controllo/finestra Avalonia: presentazione e
   navigazione. L'apertura della finestra non produce clock o letture aggiuntive.

Occorre conservare il contesto storico dell'istruzione e degli snapshot:
selezionare un vecchio campione non deve mostrare l'ultima istruzione o gli
ultimi registri come se fossero validi allora. Byte e operandi osservati sul bus
hanno precedenza sulla lettura preventiva della memoria. Se codice modificato,
accessi incoerenti o campioni mancanti invalidano il modello, si mantiene la
vista osservata e si sospende la spiegazione interna fino a risincronizzazione.

Valori sui bus si interpretano soltanto negli intervalli validi: un byte durante
refresh o bus inattivo non diventa un opcode o un operando. Anche l'identificazione
di un ciclo M può restare incerta dove il singolo campionamento non risolve il
cambio di segnali. Prefissi, ripetizioni e interrupt richiedono regole dedicate;
la durata nominale totale da sola non individua il percorso interno.

La registrazione avrà una conservazione limitata e dichiarata; lo scarto dei
campioni invalida il contesto interessato. Il disegno aggiorna solo quanto
visibile e può saltare fotogrammi intermedi a clock continuo, conservando i
campioni entro il limite. La frequenza di arrivo seriale non misura il tempo CPU.
Salvataggio/riapertura di tracce utente resta nell'attività comune TIM-01;
fixture dimostrative in memoria bastano alla prima versione.

## Incrementi, complessità e stima

Stime preliminari in giornate di lavoro di uno sviluppatore, incluse verifiche
ordinarie. Non sono scadenze; accesso alle schede e differenze firmware possono
allungare il calendario.

| Incremento | Risultato verificabile | Stima |
|---|---|---|
| 1. Contratto e prova tecnica | Fixture NOP, LD e ADD, allineamento campione/T, regole di provenienza e degradazione; verifica manager realmente caricato | 1–2 giorni |
| 2. Finestra e schema | Schema ridimensionabile, cursore e spiegazioni su fixture, riuso comandi esistenti; prova di apertura/chiusura e temi | 2–3 giorni |
| 3. Collegamento al bus reale | Live e consultazione sincronizzati, cicli comuni, WAIT/reset/iniezione e discontinuità, supporto legacy limitato | 3–5 giorni |
| 4. Catalogo didattico iniziale | Percorsi per le famiglie elencate sotto, operandi noti e previsioni separate dagli snapshot | 4–6 giorni |
| 5. Validazione e confezionamento | Esempi, regressioni, prove hardware e verifica delle tre piattaforme, documentazione dei limiti | 2–3 giorni |

**Prima versione utile: 12–19 giornate**, indicativamente 3–4 settimane a tempo
pieno con hardware disponibile. Dopo gli incrementi 1–3 è già valutabile una
prima vista concreta del bus e dei blocchi, con NOP/LD/ADD come casi pilota.
La parte grafica ha complessità media; interpretazione temporale e validità
dei valori sono la parte più impegnativa.

Catalogo iniziale: NOP; LD fra registri, immediato e `(HL)`; ADD/SUB fra
registri a 8 bit; INC/DEC di registri a 8 bit; JP assoluto; OUT immediato.
Si parte da NOP, LD A,n e ADD A,B, poi si estendono queste famiglie. Per ogni
variante si indica la copertura. Gli altri opcode continuano a mostrare i dati
osservati e la scheda istruzione, con spiegazione interna non disponibile.

Incremento successivo: salti condizionati e DJNZ, stack/CALL/RET, IN e altre
forme di I/O, CB/ED/DD/FD e indicizzazione, istruzioni a blocchi, HALT e
interrupt. Un supporto ampio e verificato può richiedere ulteriori 4–8 settimane,
da ristimare dopo i casi pilota. La fedeltà alla microarchitettura fisica per
ogni T-state richiederebbe ricerca e un modello molto più dettagliato: non è
un impegno incluso in questa proposta.

Un emulatore per lezioni completamente offline è un'estensione separata.
La consultazione di una traccia e le dimostrazioni preregistrate non richiedono
un emulatore completo e non eseguono il programma sulla scheda.

## Verifiche e criteri di completamento

- Sequenze note NOP, LD A,n, LD A,B, ADD A,B, accessi `(HL)` e OUT: fase
  osservata, blocco coinvolto e provenienza di ogni valore corretti.
- Un click hardware produce esattamente l'impulso richiesto; nessun dump
  automatico aggiunto dalla vista. Conservare il comportamento già configurato
  nell'IDE per l'avanzamento d'istruzione e le letture esplicite.
- T dell'istruzione distinti da T locali al ciclo, WAIT e clock di injection;
  nessun T certo con log legacy o dopo un buco non risolto.
- Prove di reset, disconnessione, errore/timeout, clock continuo, cambio
  sessione, contatore con wrap, opcode non coperto e prefissi: nessuno stato
  interno inventato. ACK interrupt e HALT riconosciuti come eventi speciali
  anche prima di implementarne la spiegazione dettagliata.
- Consultazione storica senza comandi hardware, senza usare snapshot futuri
  e senza alterare l'esecuzione. Selezione coerente tra finestra, log e listing.
- Finestra riapribile senza duplicare eventi; chiusura senza fermare la scheda.
  Stato e dimensioni ripristinati entro un monitor disponibile.
- Registrazioni lunghe con memoria limitata, nessun blocco della UI, scarti e
  perdita di contesto visibili. Temi chiaro/scuro, zoom e scaling leggibili;
  segnalazioni comprensibili anche senza distinguere i colori.
- Regressioni `dotnet build Alex80_IDE.sln` e
  `dotnet run --project AssemblerTests`; prove di interazione su macOS,
  Windows e Linux. Verifica reale di fetch, lettura/scrittura e I/O su ALEX80µ;
  confronto con analizzatore logico per confermare l'allineamento dei campioni,
  senza presentarlo come verifica dei trasferimenti interni.

Per ogni incremento registrare separatamente verifiche software e hardware,
versioni effettive e limiti rimasti. Commit pertinenti nel solo repository
modificato. Questa attività di pianificazione non esegue build o test hardware,
perché non introduce codice eseguibile.

## Riferimenti

- [Zilog Z80 CPU User Manual UM008011-0816](https://www.zilog.com/docs/z80/um0080.pdf):
  figura 1 e architettura (pagina PDF 15), registri e ALU; sezione Timing e
  diagrammi fetch/memoria/I/O; tabelle dei cicli delle singole istruzioni.
- [Roadmap](ROADMAP.md): DID-01, TIM-01, TIM-02, DID-02 e REL-01.
- [Guida istruzioni e trace](Alex80_IDE/InstructionReference/README.md): utile
  per il contratto; alcuni passaggi sul refresh automatico vanno confrontati
  con il codice e il README principale attuali.
- `../ALEX80ArduinoLibrary/ALEX80u/examples/ALEX80u_manager_terminal/clock.ino`
  e `memory_commands.ino`: impulso, contatore e semantica dei campioni;
  sorgenti consultati esclusivamente in lettura.
- `../alex80_manager/alex80_manager.ino` e i manager legacy ALEX80µ:
  riferimenti per identificare i limiti del supporto iniziale.
