using System.Threading.Tasks;

public interface IDialogService
{
    Task ShowErrorAsync(string title, string message);
}