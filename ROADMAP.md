# Roadmap delle funzionalità di ALEX80 IDE

Documento di riferimento per pianificare e tracciare l'introduzione delle nuove
funzionalità dell'IDE. Creato il 28 ottobre 2026.

Il primo percorso riguarda l'evoluzione didattica dell'IDE:

**sorgente C → assembler generato → codice macchina → attività del bus → diagramma temporale.**

Le attività elencate sono pianificate: la presenza nel documento non indica che
la funzionalità sia già implementata o verificata sull'hardware.

## Come aggiornare questo documento

- Assegnare a ogni funzionalità un identificativo stabile e aggiungerla al registro.
- Descrivere obiettivo, dipendenze, attività e criteri di completamento.
- Usare gli stati **Pianificata**, **In corso**, **Bloccata**, **Completata** o
  **Rinviata**. Per un blocco, indicare causa e condizione necessaria per ripartire.
- Spuntare le attività solo dopo averle implementate e verificate. Registrare
  separatamente ciò che è stato provato senza hardware e sulla scheda reale.
- Aggiornare stato, decisioni e verifiche quando cambia la funzionalità; annotare
  versione firmware, toolchain e piattaforma quando rilevanti.
- Collegare commit o PR pertinenti. Se il lavoro coinvolge più repository,
  mantenere riferimenti e commit separati.
- Aggiungere le nuove funzionalità in sezioni dedicate, conservando lo storico
  delle decisioni e dei traguardi completati.

Le note storiche sul lavoro già svolto rimangono in [NOTE-DI-LAVORO.md](NOTE-DI-LAVORO.md).

## Vincoli del progetto

- ALEX80 e ALEX80µ basati su Z80 sono la piattaforma principale: preservare
  assemblaggio, caricamento, comunicazione seriale e debug esistenti.
- L'IDE deve restare multipiattaforma: macOS, Windows e Linux.
- Il supporto eZ80 rimane sperimentale. Il supporto C in modalità ADL richiede
  una valutazione specifica; non è incluso nel primo rilascio C per Z80.
- La ZDI dell'implementazione ESP32 attuale non offre visibilità sui segnali
  del bus: non può alimentare un diagramma temporale del bus reale.
- Non modificare o riattivare `zdi_arduino`, il cui sviluppo è sospeso.
- Usare `ALEX80ArduinoLibrary` come riferimento. Qualunque modifica richiede
  un'esplicita richiesta dell'utente, come previsto dalle istruzioni del workspace.
- Prima di modificare un protocollo, analizzare IDE e firmware interessati;
  conservare il protocollo esistente quando possibile.
- Concludere ogni incremento che modifica file con un commit pertinente in
  ciascun repository coinvolto, senza includere modifiche preesistenti estranee.

## Registro delle funzionalità

| ID | Funzionalità | Stato | Dipendenze |
|---|---|---|---|
| DID-01 | Contratti comuni ed esempi didattici | Pianificata | — |
| C-01 | Compilazione C per Z80 con SDCC | Pianificata | DID-01 |
| C-02 | Runtime e profili delle schede Z80 | Pianificata | C-01 |
| C-03 | Espansione C → assembler nell'editor | Pianificata | C-01; C-02 per le prove sulla scheda |
| TIM-01 | Vista Timing dei log esistenti | Pianificata | DID-01 |
| TIM-02 | Acquisizione temporale estesa | Pianificata | TIM-01 e verifica hardware |
| DID-02 | Navigazione C → assembler → timing | Pianificata | C-02, C-03, TIM-01; TIM-02 per misure temporali |
| REL-01 | Validazione e distribuzione didattica | Pianificata | Progressiva, per ogni traguardo |

## Stato iniziale rilevato

Rilevazione del 28 ottobre 2026, basata sulla lettura del codice; le osservazioni
sui firmware non sostituiscono la verifica della versione caricata sulla scheda.

- L'IDE usa C#, Avalonia e AvaloniaEdit. Sono già disponibili editor, progetti,
  assemblatore, listing, caricamento sulla scheda e pannelli agganciabili.
- `Alex80_IDE/Services/ProjectBuilder.cs` costruisce attualmente programmi assembler.
- `Alex80_IDE/Models/Alex80Project.cs` descrive i progetti `.a80proj`.
- `Alex80_IDE/Models/LogElement.cs` contiene indirizzo, dato e livelli di sei
  segnali: `/RD`, `/WR`, `/IORQ`, `/MREQ`, `/RFSH`, `/M1`.
- `Alex80_IDE/SerialManager.cs` decodifica i messaggi `!LOG`: quattro byte,
  senza timestamp, contatore di ciclo o segnalazione di campioni mancanti.
- `Alex80_IDE/Controls/BusLogView.axaml` mostra i campioni nella tabella del bus.
- Nei firmware alcuni campioni sono soppressi durante attese o operazioni di
  debug: la posizione nella lista non garantisce una distanza temporale costante.
- Nell'esempio `ALEX80ArduinoLibrary/ALEX80u/examples/ALEX80u_manager_terminal/`,
  `captureBusLog` in `memory_commands.ino` acquisisce un campione per ciclo
  completo, immediatamente prima del fronte di discesa, riutilizzando lo snapshot
  del debugger e il dato pilotato. La semantica di acquisizione va conservata nei metadati.
- Nel sorgente consultato di `alex80_manager`, l'invio seriale di `!LOG` è
  commentato: verificare il firmware realmente utilizzato prima di intervenire.
- Il firmware `zdi_esp32` rifiuta il log del bus perché ZDI non ne espone i segnali.

## DID-01 — Contratti comuni ed esempi didattici

**Obiettivo:** separare i dati di compilazione e acquisizione dalle viste, con
informazioni sufficienti a collegare in seguito sorgente, codice e registrazioni.

### Attività

- [ ] Definire un risultato di compilazione con segmenti e immagine di memoria,
  diagnostica, simboli, indirizzo d'ingresso e associazioni sorgente/indirizzi.
- [ ] Conservare identificativo della compilazione, versione e opzioni della
  toolchain, copia dei sorgenti utilizzati e identificazione dell'immagine caricata.
- [ ] Definire una registrazione del bus con campioni, segnali disponibili,
  validità dei dati, modalità di acquisizione, riferimento temporale e discontinuità.
- [ ] Distinguere campioni osservati, valori pilotati, informazioni dedotte e
  tempi nominali o misurati, quando queste informazioni sono disponibili.
- [ ] Preparare esempi di assegnazione, somma, condizione, ciclo, funzione,
  accesso tramite puntatore e scrittura sui LED, con comportamento atteso.

**Criterio di completamento:** contratti documentati ed esempi di riferimento
definiti, senza alterare il comportamento dei progetti assembler esistenti.

## C-01 — Compilazione C per Z80

**Scelta iniziale proposta:** SDCC, usando la sua catena completa di compilazione,
assemblaggio e collegamento. La versione sarà fissata dopo la prova tecnica.

```text
C → compilatore SDCC → assembler/linker SDCC → immagine caricabile
```

L'assembler integrato continua a gestire i progetti Macro80/TASM esistenti.
Non è previsto rendere subito compatibile tutto il dialetto assembler di SDCC
con l'assembler dell'IDE.

### Attività

- [ ] Eseguire una prova tecnica su generazione di codice, artefatti di debug,
  listing rilocati e associazione sorgente/indirizzi dopo il collegamento.
- [ ] Scegliere e documentare una versione SDCC validata per Z80.
- [ ] Aggiungere percorso configurabile e verifica della toolchain esterna.
- [ ] Eseguire la compilazione in modo asincrono, con annullamento e raccolta
  degli errori navigabili verso file e riga.
- [ ] Compilare una copia coerente dei documenti aperti, includendo le modifiche
  non salvate e preservando la corrispondenza con i percorsi originali.
- [ ] Gestire sorgenti C multipli, header e cartelle di inclusione.
- [ ] Importare Intel HEX verificando segmenti, indirizzi e sovrapposizioni;
  riutilizzare il percorso esistente di caricamento dei byte.
- [ ] Aggiungere impostazioni C opzionali a `.a80proj`, mantenendo validi i
  progetti precedenti.
- [ ] Verificare disponibilità e funzionamento della toolchain su macOS,
  Windows e Linux, incluse le architetture effettivamente distribuite.
- [ ] Valutare successivamente la distribuzione insieme all'IDE, con verifica
  delle licenze e dei requisiti dei componenti inclusi.

**Criterio di completamento:** un programma C compila dall'IDE, produce
diagnostica navigabile e genera un'immagine utilizzabile dal caricamento esistente.

## C-02 — Runtime e profili delle schede Z80

**Obiettivo:** eseguire correttamente i programmi C su ALEX80 e ALEX80µ.

### Attività

- [ ] Verificare mappa di memoria, modalità di avvio e firmware realmente in uso
  su ciascuna scheda.
- [ ] Definire profili distinti con aree per codice, dati e stack e indirizzo d'ingresso.
- [ ] Predisporre il codice di avvio: stack, inizializzazione delle variabili
  globali, azzeramento delle aree previste e chiamata a `main()`.
- [ ] Definire il comportamento al ritorno da `main()`.
- [ ] Proteggere le aree riservate a monitor e debugger e rilevare conflitti.
- [ ] Fornire piccole funzioni di I/O, iniziando dalla scrittura sui LED.
- [ ] Documentare la convenzione di chiamata fra C e assembler.
- [ ] Supportare piccoli moduli assembler nel formato della toolchain C.
- [ ] Valutare separatamente il collegamento libero con moduli Macro80/TASM,
  verificando formati oggetto e convenzioni: non è un prerequisito del primo rilascio.

**Criterio di completamento:** gli esempi vengono caricati ed eseguiti sulle
due piattaforme Z80; il debug assembler esistente continua a funzionare.

## C-03 — Espansione C → assembler

**Obiettivo:** espandere una riga o un'istruzione C e osservare il codice
effettivamente generato dalla compilazione completa.

### Attività

- [ ] Aggiungere riconoscimento dei documenti C ed evidenziazione sintattica.
- [ ] Aggiungere un indicatore nel margine dell'editor e il comando contestuale
  **Mostra assembler generato**.
- [ ] Mostrare assembler, indirizzi e byte dopo il collegamento, con navigazione
  reciproca fra sorgente e codice generato.
- [ ] Ricavare le associazioni dagli artefatti di debug e dai listing rilocati,
  verificandone il formato nella versione SDCC scelta.
- [ ] Rappresentare associazioni multiple, intervalli non contigui, istruzioni
  eliminate e chiamate a routine di supporto.
- [ ] Segnalare il risultato come da ricompilare quando cambia il sorgente.
- [ ] Introdurre un profilo **Didattica** con opzioni validate per favorire la
  leggibilità, senza promettere la disattivazione di tutte le ottimizzazioni.
- [ ] Aggiungere successivamente i profili **Dimensione** e **Velocità** e il
  confronto del codice generato.

### Regole di rappresentazione

Non compilare una singola riga isolatamente: il suo codice dipende dal contesto
del programma. Non presumere una relazione uno a uno fra istruzione C e assembler.

Il primo traguardo è l'espansione per riga o intervallo. Se più istruzioni sulla
stessa riga non sono distinguibili nei metadati, mostrare il gruppo e indicarlo.
Un eventuale parser C può precisare la selezione sintattica, ma non ricostruire
associazioni che il compilatore non fornisce.

**Criterio di completamento:** assegnazioni, condizioni, cicli, funzioni e codice
ottimizzato mostrano associazioni corrette oppure limiti esplicitamente segnalati.

## TIM-01 — Timing dei log esistenti

**Obiettivo:** trasformare i log già disponibili in diagrammi consultabili,
senza richiedere un aggiornamento firmware.

### Attività

- [ ] Aggiungere un pannello Timing che condivida i dati con la tabella Bus log.
- [ ] Disegnare le sei tracce digitali disponibili e i bus indirizzi e dati in esadecimale.
- [ ] Usare inizialmente l'asse **campione**, senza attribuire durate costanti
  ai record del protocollo attuale.
- [ ] Conservare il livello elettrico dei segnali attivi bassi: `/RD` attivo è basso.
- [ ] Segnalare che il livello è noto al campionamento e che i fronti fra
  campioni non sono temporalmente risolti.
- [ ] Implementare zoom, scorrimento, selezione di intervalli e cursori A/B
  con distanza in campioni.
- [ ] Sincronizzare la selezione fra diagramma e tabella del bus.
- [ ] Salvare e riaprire registrazioni con formato versionato e metadati disponibili.
- [ ] Separare l'archivio dei campioni dai controlli grafici, raggruppare gli
  aggiornamenti e disegnare la sola porzione visibile.
- [ ] Definire un limite di memoria o una strategia di conservazione per
  registrazioni lunghe, segnalando ogni eventuale scarto di dati.

**Criterio di completamento:** i log esistenti sono visualizzabili come forme
d'onda, con navigazione fluida e senza precisione temporale artificiale.

## TIM-02 — Acquisizione temporale estesa

**Obiettivo:** aggiungere riferimenti temporali e informazioni di integrità,
nei limiti verificati delle singole schede.

### Attività

- [ ] Verificare segnali accessibili, costo delle letture MCP23S17, coerenza
  degli snapshot e interferenza con memoria, clock e debug.
- [ ] Misurare frequenza sostenibile, memoria disponibile e capacità del trasporto seriale.
- [ ] Definire un protocollo aggiuntivo negoziato tramite capacità dichiarate
  dal firmware, mantenendo invariato `!LOG`.
- [ ] Includere versione, identificativo della registrazione, progressivo,
  contatore di ciclo/fase o timestamp secondo le capacità effettive.
- [ ] Includere livelli e maschera di validità dei segnali, pause, overflow e
  campioni persi; documentare sorgente del clock e risoluzione temporale.
- [ ] Partire da acquisizioni brevi a clock controllato, bufferizzate quando
  possibile e trasferite successivamente.
- [ ] Implementare solo le modifiche necessarie nei firmware autorizzati,
  verificando contemporaneamente il lato IDE.
- [ ] Mostrare tempi nominali e tempi misurati come grandezze distinte.
- [ ] Confrontare l'acquisizione con un analizzatore logico per fetch, letture,
  scritture, I/O e WAIT.

L'arrivo di un messaggio seriale non misura il tempo del bus. Non promettere
acquisizione continua a qualsiasi frequenza né fronti o durate inferiori alla
risoluzione verificata. Per fronti più fini del campionamento disponibile,
valutare separatamente un'acquisizione hardware esterna.

Se il firmware effettivamente utilizzato è l'esempio dentro
`ALEX80ArduinoLibrary`, registrare qui la dipendenza: modificarlo richiede
un'esplicita richiesta dell'utente. Nel frattempo rimangono realizzabili TIM-01
e le attività indipendenti da quel repository.

**Criterio di completamento:** acquisizioni validate contro strumentazione
esterna, con risoluzione, frequenza sostenibile e limiti documentati per scheda.

## DID-02 — Navigazione integrata

**Obiettivo:** seguire un'operazione dal C fino alla sua attività osservata sul bus.

### Attività

- [ ] Selezionare un'istruzione C e trovare le sue esecuzioni nella registrazione.
- [ ] Selezionare un fetch e navigare all'assembler e al sorgente associati.
- [ ] Distinguere fetch delle istruzioni e accessi ai dati, gestendo prefissi
  e casi in cui la decodifica non è certa.
- [ ] Annotare cicli macchina e T-state soltanto quando i dati permettono di riconoscerli.
- [ ] Separare conteggi osservati e stime teoriche delle istruzioni, tenendo
  conto di salti condizionati, attese e interruzioni.
- [ ] Associare registrazione, compilazione e immagine caricata; segnalare
  origine sconosciuta, sorgente modificato o immagine non verificata.

**Criterio di completamento:** selezionando una scrittura C sui LED si osserva
l'assembler prodotto e si ritrova il ciclo di I/O nella registrazione corretta.

## REL-01 — Validazione e distribuzione

La validazione accompagna ogni incremento, senza essere rimandata alla fine.

- [ ] Eseguire le regressioni assembler e seriali pertinenti a ogni modifica.
- [ ] Verificare runtime, stack, variabili inizializzate e azzerate sulle schede reali.
- [ ] Verificare associazioni sorgente/indirizzi con ottimizzazioni, più file,
  header, istruzioni su più righe e più istruzioni sulla stessa riga.
- [ ] Provare frame incompleti o malformati, disconnessioni, campioni persi,
  pause e overflow senza perdere la sincronizzazione della registrazione.
- [ ] Verificare prestazioni con registrazioni lunghe e interfaccia a diversi
  scaling, dimensioni della finestra e temi.
- [ ] Verificare compilazione e interazioni principali su macOS, Windows e Linux.
- [ ] Fornire esempi e registrazioni utilizzabili in classe anche senza hardware collegato.
- [ ] Documentare installazione della toolchain, profili delle schede e limiti dell'acquisizione.

### Ordine dei traguardi

| Traguardo | Risultato | Stato |
|---|---|---|
| 1 | Compilazione ed esecuzione C su Z80 | Pianificata |
| 2 | Espansione C → assembler | Pianificata |
| 3 | Timing dei log attuali per campioni | Pianificata |
| 4 | Acquisizione temporale estesa sulle schede validate | Pianificata |
| 5 | Navigazione integrata C → assembler → timing | Pianificata |

TIM-01 può essere anticipata dopo DID-01: non dipende dal compilatore C.
Il traguardo 5 può iniziare con TIM-01; le misure temporali richiedono TIM-02.

## Organizzazione del lavoro

**Minimo necessario: un agente principale, senza sottoagenti.**

Procedere per incrementi circoscritti mantenendo nello stesso contesto i
contratti di compilazione e comunicazione. Non è necessario lavorare in parallelo
su IDE e firmware: le modifiche di protocollo richiedono invece una verifica
coordinata dei due lati.

Le prime due verifiche decisive sono la prova tecnica del mapping SDCC e
l'identificazione dei log e del firmware realmente usati sulla scheda.

## Questioni da risolvere durante l'implementazione

| Questione | Quando risolverla | Conseguenza |
|---|---|---|
| Versione SDCC e qualità del mapping Z80 | C-01, prova tecnica | Formati da importare e precisione dell'espansione |
| Mappa di memoria e avvio per scheda | C-02 | Runtime e compatibilità con monitor/debugger |
| Firmware effettivamente caricato | Avvio di TIM-02 | Repository da modificare e disponibilità dei log |
| Segnali e risoluzione acquisibili | TIM-02, verifica hardware | Tracce disponibili e significato dell'asse temporale |
| Frequenza e durata sostenibili | TIM-02, misure | Limiti delle registrazioni e strategia di trasferimento |
| Necessità di modificare la libreria protetta | Prima di qualunque modifica in quel repository | Richiesta esplicita dell'utente necessaria |
| Distribuzione della toolchain con l'IDE | Dopo C-01 | Pacchetti per piattaforma e obblighi di distribuzione |

## Riferimenti tecnici

- [SDCC — sito ufficiale](https://sdcc.sourceforge.net/).
- [SDCC Compiler User Guide](https://sdcc.sourceforge.net/doc/sdccman.pdf):
  compilazione, output `.asm`, `.lst`, `.rst`, `.map`, `.ihx`, debug `.adb`/`.cdb`,
  opzioni di generazione e collegamento. Verificare sempre contro la versione scelta.
- [z88dk — debugging](https://github.com/z88dk/z88dk/wiki/debugging): riferimento
  alternativo per informazioni di debug e associazioni sorgente/indirizzi;
  non è una seconda toolchain prevista nel primo rilascio.

## Storico degli aggiornamenti

| Data | Aggiornamento | Verifiche / riferimenti |
|---|---|---|
| 2026-10-28 | Creazione della roadmap didattica e del registro per le funzionalità future | Analisi del codice IDE e firmware; nessuna implementazione o prova hardware delle nuove funzionalità |

## Modello per le funzionalità future

Per una nuova funzionalità, aggiungere una riga al registro e una sezione con:

- **ID e nome**.
- **Stato** e obiettivo osservabile dall'utente.
- **Ambito**: repository e componenti interessati.
- **Dipendenze** e questioni aperte.
- **Attività** con checklist.
- **Criteri di completamento** e verifiche richieste.
- **Decisioni**, eventuali blocchi e riferimenti a commit o PR.
