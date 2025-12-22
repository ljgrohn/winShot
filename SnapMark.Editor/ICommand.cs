namespace SnapMark.Editor;

public interface ICommand
{
    void Execute();
    void Undo();
}


