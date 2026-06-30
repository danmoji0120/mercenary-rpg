using Godot;

public partial class BuildHud : Control
{
    private const float FeedbackDurationSeconds = 2.0f;

    private Label? _buildLabel;
    private string _statusText = "Build: Off";
    private string _feedbackMessage = "";
    private float _feedbackTimer;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;

        _buildLabel = new Label
        {
            Name = "BuildLabel",
            Text = "Build: Off",
            Position = new Vector2(12.0f, 68.0f),
            Size = new Vector2(620.0f, 104.0f),
            MouseFilter = MouseFilterEnum.Ignore
        };

        AddChild(_buildLabel);
        RefreshLabel();
    }

    public override void _Process(double delta)
    {
        if (_feedbackTimer <= 0.0f)
        {
            return;
        }

        _feedbackTimer = Mathf.Max(0.0f, _feedbackTimer - (float)delta);

        if (_feedbackTimer <= 0.0f)
        {
            _feedbackMessage = "";
        }

        RefreshLabel();
    }

    public void UpdateBuildMode(TileBuildType buildType)
    {
        UpdateBuildStatus(buildType == TileBuildType.None ? "Build: Off" : $"Build: {buildType}");
    }

    public void UpdateBuildStatus(string statusText)
    {
        _statusText = statusText;
        RefreshLabel();
    }

    public void ShowFeedback(string message)
    {
        _feedbackMessage = message;
        _feedbackTimer = FeedbackDurationSeconds;
        RefreshLabel();
    }

    public void ClearFeedback()
    {
        _feedbackMessage = "";
        _feedbackTimer = 0.0f;
        RefreshLabel();
    }

    private void RefreshLabel()
    {
        if (_buildLabel == null)
        {
            return;
        }

        _buildLabel.Text = string.IsNullOrEmpty(_feedbackMessage)
            ? _statusText
            : $"{_statusText}\nFeedback: {_feedbackMessage}";
    }
}
