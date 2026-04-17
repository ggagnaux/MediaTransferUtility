namespace MediaTransferUtility;

internal sealed class AppState
{
    public string? SourcePath { get; set; }
    public string? DestinationPath { get; set; }
    public string? DestinationFolderName { get; set; }
    public bool DarkTheme { get; set; }
    public bool RemoveSource { get; set; }
    public bool CreateEdits { get; set; }
    public bool CreateFinal { get; set; }
    public bool SaveLog { get; set; }
    public bool ShowDetailedLog { get; set; }
    public int WindowX { get; set; }
    public int WindowY { get; set; }
    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }
}
