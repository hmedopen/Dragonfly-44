namespace Dragonfly44.Application.Interfaces;

public interface IFocusService
{
    bool IsFocusInTextInput();
    void PushFocus();
    void PopFocus();
}
