# Descrizione del progetto

ALEX80 IDE è l'IDE di riferimento per l'ecosistema ALEX80.

È un'applicazione multipiattaforma sviluppata in C# utilizzando Avalonia. Lo sviluppo principale viene effettuato su macOS, con target:

- macOS
- Windows
- Linux

L'applicazione deve rimanere multipiattaforma. Le modifiche non devono introdurre dipendenze o comportamenti specifici di macOS che possano compromettere il funzionamento su Windows o Linux, salvo quando esplicitamente necessari e opportunamente isolati.

# Interfaccia utente

L'interfaccia deve rispettare, nei limiti consentiti da Avalonia e dalla natura multipiattaforma dell'applicazione, le convenzioni dei sistemi operativi target.

In particolare:

- preferire controlli e comportamenti standard rispetto a implementazioni personalizzate;
- evitare di riprodurre esplicitamente l'aspetto di macOS su Windows o Linux;
- utilizzare, quando disponibili, convenzioni specifiche della piattaforma per menu, scorciatoie da tastiera, finestre di dialogo e interazioni con il sistema operativo;
- mantenere coerenza funzionale tra le piattaforme anche quando la presentazione o alcune modalità di interazione differiscono;
- evitare dimensioni, font o layout basati su assunzioni valide per una sola piattaforma;
- verificare che modifiche al layout continuino a funzionare con dimensioni della finestra e scaling differenti.

# Compatibilità multipiattaforma

Il codice condiviso deve essere indipendente dalla piattaforma quando possibile.

Quando è necessario implementare un comportamento specifico per macOS, Windows o Linux:

1. mantenere la parte specifica della piattaforma separata dal codice condiviso;
2. non alterare il comportamento delle altre piattaforme;
3. utilizzare preferibilmente le API e i meccanismi messi a disposizione da Avalonia;
4. ricorrere direttamente alle API native del sistema operativo solo quando necessario.

# Principi per le modifiche

- Preservare il comportamento esistente salvo quando la modifica richiesta prevede esplicitamente un cambiamento.
- Preferire modifiche circoscritte a refactoring estesi non necessari.
- Non introdurre nuove dipendenze senza una reale necessità.
- Prima di implementare una funzionalità già potenzialmente fornita da Avalonia, verificare se esiste una soluzione standard nel framework.
- Non sacrificare la compatibilità multipiattaforma per semplificare l'implementazione su macOS.

# Commit al termine delle attività

Al termine di ogni attività che modifica file, creare un commit nel repository interessato con un messaggio che sintetizzi quanto fatto.

Se l'attività coinvolge più repository, creare un commit separato in ciascuno. Includere solo le modifiche pertinenti all'attività, senza inglobare modifiche preesistenti non correlate.
