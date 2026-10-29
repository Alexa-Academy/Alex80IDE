using System.Collections.Generic;
using Alex80_IDE.ViewModels;

/// <summary>
/// Disposizione della shell: le tool window agganciate ai bordi della finestra.
/// Aggiungere uno strumento significa dichiarare qui la sua <see cref="ToolWindowViewModel"/>,
/// inserirla nel dock che la ospita e aggiungere la vista corrispondente al pannello di
/// quel dock, in MainWindow.axaml. Spostarla da un bordo all'altro e' solo un cambio di
/// dock in <see cref="InitializeDocks"/>.
/// </summary>
public partial class MainViewModel
{
    public ToolWindowViewModel ProjectTool { get; } = new("project", "Progetto", "ProjectIconData", 270);

    public ToolWindowViewModel AssemblerTool { get; } = new("assembler", "Assemblatore", "AssemblerIconData", 270);

    public ToolWindowViewModel RegistersTool { get; } = new("registers", "Registri", "RegistersIconData", 330);

    // Strumenti del dock inferiore: la misura e' un'altezza, non una larghezza.
    public ToolWindowViewModel MemoryTool { get; } = new("memory", "Memoria", "MemoryIconData", 300);

    public ToolWindowViewModel BusLogTool { get; } = new("buslog", "Bus log", "BusLogIconData", 300);

    public ToolWindowViewModel SerialTool { get; } = new("serial", "Seriale", "SerialIconData", 260);

    /// <summary>Dock sinistro: i file che compongono il programma e come va assemblato.</summary>
    public ToolDockViewModel LeftDock { get; private set; } = null!;

    /// <summary>Dock destro: lo stato dei registri della CPU.</summary>
    public ToolDockViewModel RightDock { get; private set; } = null!;

    /// <summary>Dock inferiore: gli strumenti di osservazione della scheda.</summary>
    public ToolDockViewModel BottomDock { get; private set; } = null!;

    /// <summary>
    /// La strip di sinistra raccoglie due gruppi, come negli IDE JetBrains: in alto gli
    /// strumenti che si aprono di fianco, in basso quelli che si aprono sotto.
    /// </summary>
    public bool HasLeftStripTools => LeftDock.HasTools || BottomDock.HasTools;

    /// <summary>I dock nell'ordine in cui vengono salvati e ripristinati.</summary>
    public IReadOnlyList<ToolDockViewModel> Docks { get; private set; } = [];

    private void InitializeDocks()
    {
        LeftDock = new ToolDockViewModel(ToolDockPlacement.Left, ProjectTool, AssemblerTool);
        RightDock = new ToolDockViewModel(ToolDockPlacement.Right, RegistersTool);
        BottomDock = new ToolDockViewModel(ToolDockPlacement.Bottom, MemoryTool, BusLogTool, SerialTool);

        Docks = [LeftDock, RightDock, BottomDock];
    }
}
