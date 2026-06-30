using Godot;

public partial class SelectionOverlay : Control
{
    public bool IsDragging { get; private set; }
    public Vector2 DragStartScreen { get; private set; }
    public Vector2 DragCurrentScreen { get; private set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        if (!IsDragging)
        {
            return;
        }

        Rect2 selectionRect = new Rect2(DragStartScreen, DragCurrentScreen - DragStartScreen).Abs();
        Color fillColor = new Color(0.35f, 0.72f, 1.0f, 0.16f);
        Color lineColor = new Color(0.42f, 0.82f, 1.0f, 0.9f);

        DrawRect(selectionRect, fillColor);
        DrawRect(selectionRect, lineColor, false, 2.0f);
    }

    public void ShowSelectionBox(Vector2 startScreen, Vector2 currentScreen)
    {
        IsDragging = true;
        DragStartScreen = startScreen;
        DragCurrentScreen = currentScreen;
        QueueRedraw();
    }

    public void UpdateSelectionBox(Vector2 currentScreen)
    {
        if (!IsDragging)
        {
            return;
        }

        DragCurrentScreen = currentScreen;
        QueueRedraw();
    }

    public void HideSelectionBox()
    {
        IsDragging = false;
        QueueRedraw();
    }
}
