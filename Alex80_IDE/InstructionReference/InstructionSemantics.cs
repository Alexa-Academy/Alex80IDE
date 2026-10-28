using System;

namespace Alex80_IDE.InstructionReference;

public static class InstructionSemantics
{
    public sealed record Entry(string Operation, string Description, string Flags);
    public static Entry For(string mnemonic, string fallback = "")
    {
        var split = mnemonic.Split(' ', 2); string name = split[0].Split('.')[0];
        string operands = split.Length > 1 ? split[1] : "";
        var args = operands.Split(','); string dest = args[0], source = args.Length > 1 ? args[1] : "";
        const string unchanged = "I flag S, Z, H, P/V, N e C restano invariati.";
        const string arithmetic = "S: segno; Z: risultato zero; H: riporto intermedio; P/V: overflow; C: riporto/prestito.";
        switch (name)
        {
            case "INC": case "DEC":
                bool inc = name == "INC";
                bool pair = dest is "BC" or "DE" or "HL" or "SP" or "IX" or "IY";
                return new($"{dest} ← {dest} {(inc ? "+" : "−")} 1", $"{(inc ? "Incrementa" : "Decrementa")} {dest} di uno. Il risultato resta nella larghezza dell'operando.", pair ? unchanged :
                    $"S: segno; Z: zero; H: {(inc ? "riporto" : "prestito")} dal bit 3; P/V: overflow {(inc ? "7Fh → 80h" : "80h → 7Fh")}; N={(inc ? 0 : 1)}; C invariato.");
            case "LD": return new($"{dest} ← {source}", "Copia il valore sorgente nella destinazione. Le parentesi indicano un accesso alla memoria.",
                source is "I" or "R" && dest == "A" ? "S e Z dal risultato; H=0; P/V=IFF2; N=0; C invariato." : unchanged);
            case "ADD": case "ADC": case "SUB": case "SBC": case "CP":
                string target = args.Length > 1 ? dest : "A", value = args.Length > 1 ? source : dest;
                bool subtract = name is "SUB" or "SBC" or "CP";
                string carry = name is "ADC" or "SBC" ? " − C" : "";
                if (name == "ADC") carry = " + C";
                return new(name == "CP" ? $"Flag ← A − {value}" : $"{target} ← {target} {(subtract ? "−" : "+")} {value}{carry}",
                    name == "CP" ? "Confronta con l'accumulatore aggiornando i flag; A conserva il suo valore." : "Esegue l'operazione aritmetica nella larghezza della destinazione.",
                    name == "ADD" && target is "HL" or "IX" or "IY" ? "H e C dal risultato; N=0; S, Z e P/V invariati." : arithmetic + (subtract ? " N=1." : " N=0."));
            case "AND": case "OR": case "XOR": case "TST":
                string op = name == "TST" ? "AND" : name;
                return new(name == "TST" ? $"Flag ← A AND {(dest == "A" && source != "" ? source : operands)}" : $"A ← A {op} {operands}",
                    name == "TST" ? "Verifica i bit senza modificare l'accumulatore." : "Esegue l'operazione logica bit per bit sull'accumulatore.",
                    $"S: segno; Z: zero; P/V: parità; H={(name is "AND" or "TST" ? 1 : 0)}; N=0; C=0.");
            case "JP": case "JR": case "DJNZ":
                if (name == "JP" && dest is "(HL)" or "(IX)" or "(IY)")
                    return new($"PC ← {dest.Trim('(', ')')}", "Salta all'indirizzo contenuto nel registro, senza leggere la memoria a quell'indirizzo.", unchanged);
                return new(name == "DJNZ" ? $"B ← B − 1; se B ≠ 0: PC ← {operands}" : args.Length > 1 ? $"Se {dest}: PC ← {source}" : $"PC ← {dest}",
                    name == "JR" ? "Salto relativo: l'indirizzo mostrato è calcolato rispetto alla fine dell'istruzione." : name == "DJNZ" ? "Decrementa B e ripete il salto finché B è diverso da zero." : "Trasferisce il controllo alla destinazione, eventualmente sotto condizione.", unchanged);
            case "CALL": return new($"{(args.Length > 1 ? "Se " + dest + ": " : "")}Stack ← PC successivo; PC ← {args[^1]}", "Chiama una sottoprocedura, salvando l'indirizzo di ritorno sullo stack. Se presente, la condizione deve risultare vera.", unchanged);
            case "RET": case "RETI": case "RETN": return new((operands == "" ? "" : "Se " + operands + ": ") + "PC ← Stack; SP avanza", "Riprende dall'indirizzo salvato sullo stack, eventualmente sotto condizione. RETI/RETN hanno anche gli effetti previsti sullo stato di interrupt.", unchanged);
            case "RST": return new($"Stack ← PC successivo; PC ← {operands}", "Chiama il vettore fisso indicato dall'opcode.", unchanged);
            case "PUSH": return new($"Stack ← {dest}; SP diminuisce", "Salva la coppia sullo stack: due byte nello Z80, secondo la modalità nello eZ80.", unchanged);
            case "POP": return new($"{dest} ← Stack; SP aumenta", "Recupera dallo stack il valore salvato.", dest == "AF" ? "F è ripristinato dallo stack insieme ad A." : unchanged);
            case "BIT": return new($"Z ← NOT bit {dest} di {source}", "Verifica un bit senza modificare l'operando.", "Z: bit non impostato; H=1; N=0; C invariato. S e P/V dipendono dal bit verificato e dal risultato.");
            case "SET": case "RES": return new($"Bit {dest} di {source} ← {(name == "SET" ? 1 : 0)}", "Modifica il solo bit selezionato.", unchanged);
            case "RLC": case "RRC": case "RL": case "RR": case "SLA": case "SRA": case "SRL": case "SLL":
                return new($"{dest} ← {name}({dest})", "Ruota o sposta i bit dell'operando; C riceve il bit espulso. RL/RR usano anche il riporto precedente.", "S: segno; Z: zero; P/V: parità; H=0; N=0; C: bit espulso.");
            case "RLCA": case "RRCA": case "RLA": case "RRA": return new($"A ← {name}(A)", "Ruota i bit dell'accumulatore.", "C: bit espulso; H=0; N=0; S, Z e P/V invariati.");
            case "EX": case "EXX": return new("Scambio dei registri indicati", name == "EXX" ? "Scambia BC, DE e HL con i rispettivi registri alternativi." : "Scambia i due operandi senza perdere il loro valore precedente.", operands.Contains("AF") ? "F è scambiato con il registro dei flag alternativo." : unchanged);
            case "NOP": return new("Nessuna modifica ai registri generali", "Avanza alla prossima istruzione; restano gli effetti del fetch sul PC e sul refresh.", unchanged);
            case "HALT": return new("CPU in attesa di interrupt", "Il processore entra in HALT. Non equivale a fermare il segnale di clock.", unchanged);
            case "SLP": return new("CPU in modalità sleep", "Entra nella modalità a basso consumo prevista dal processore.", unchanged);
            case "EI": case "DI": return new(name == "EI" ? "Abilita gli interrupt mascherabili" : "Disabilita gli interrupt mascherabili", name == "EI" ? "Gli interrupt diventano accettabili dopo l'istruzione successiva." : "Gli interrupt non mascherabili restano indipendenti da DI.", unchanged);
            case "IM": return new($"Modo interrupt ← {operands}", "Seleziona la modalità di risposta agli interrupt mascherabili.", unchanged);
            case "IN": case "IN0": return new($"{dest} ← Porta {source}", "Legge una porta di ingresso. IN0 usa l'indirizzamento immediato delle porte eZ80.", name == "IN" && source != "(C)" ? unchanged : "S, Z e P/V dal dato letto; H=0; N=0; C invariato.");
            case "OUT": case "OUT0": return new($"Porta {dest} ← {source}", "Scrive il dato sulla porta di uscita indicata.", unchanged);
            case "LDI": case "LDD": case "LDIR": case "LDDR":
                string direction = name.StartsWith("LDD") ? "−" : "+";
                return new($"(DE) ← (HL); HL, DE {direction} 1; BC ← BC − 1", "Trasferisce un byte. Le forme con R ripetono fino a BC=0.", "H=0; N=0; P/V: BC diverso da zero; S, Z e C invariati.");
            case "CPI": case "CPD": case "CPIR": case "CPDR":
                return new($"Confronta A con (HL); HL {(name.StartsWith("CPD") ? "−" : "+")} 1; BC ← BC − 1", "Le forme con R ripetono finché BC è diverso da zero e il confronto non trova uguaglianza.", "S, Z e H dal confronto; P/V: BC diverso da zero; N=1; C invariato.");
            case "TSTIO": return new($"Flag ← Porta C AND {operands}", "Verifica i bit del dato letto dalla porta I/O applicando la maschera immediata.", "S: segno; Z: zero; H=1; P/V: parità; N=0; C=0.");
            case "MLT": return new($"{dest} ← byte alto × byte basso", "Moltiplicazione senza segno di due operandi a 8 bit con risultato a 16 bit.", unchanged);
            case "LEA": return new($"{dest} ← {source}", "Calcola un indirizzo effettivo sommando lo scostamento al registro indice.", unchanged);
            case "PEA": return new($"Stack ← indirizzo {dest}", "Calcola un indirizzo effettivo e lo salva sullo stack.", unchanged);
            case "NEG": return new("A ← 0 − A", "Calcola il complemento a due dell'accumulatore.", arithmetic + " N=1.");
            case "CPL": return new("A ← NOT A", "Inverte tutti i bit dell'accumulatore.", "H=1; N=1; S, Z, P/V e C invariati.");
            case "SCF": return new("C ← 1", "Imposta il riporto.", "C=1; H=0; N=0; S, Z e P/V invariati.");
            case "CCF": return new("C ← NOT C", "Inverte il riporto.", "H: precedente C; N=0; S, Z e P/V invariati.");
            case "DAA": return new("A ← correzione decimale BCD", "Corregge l'accumulatore dopo un'operazione su cifre BCD.", "S, Z, H, P/V e C dipendono dalla correzione; N invariato.");
            case "RLD": case "RRD": return new("Rotazione di cifre BCD fra A e (HL)", "Scambia le semicifre di memoria con la semicifra bassa di A.", "S, Z e P/V dal nuovo A; H=0; N=0; C invariato.");
            case "STMIX": case "RSMIX": return new(name == "STMIX" ? "MADL ← 1" : "MADL ← 0", "Configura il comportamento eZ80 per interrupt fra modalità di memoria.", unchanged);
            case "DB": return new("Opcode non riconosciuto", "Non vengono inventati funzione o tempi per opcode non supportati.", "Non disponibile.");
        }
        if (name.StartsWith("LD") || name.StartsWith("CP") || name.StartsWith("IN") || name.StartsWith("OT") || name.StartsWith("OUT"))
            return new("Operazione a blocchi sui registri e sulla memoria/porte", "Trasferisce, confronta o scambia dati aggiornando puntatori e contatori. Le forme ripetute proseguono fino alla condizione di terminazione indicata dal manuale.", "Effetti sui flag specifici della variante: consultare il riferimento del processore.");
        return new(fallback == "" ? mnemonic : fallback, "Consultare il manuale del processore per gli effetti dettagliati di questa variante.", "Consultare il riferimento.");
    }
}
