namespace AetherCore.DTO
{
    public class ModerationInput
    {
        public string MessageId { get; set; }
        public string Content { get; set; }
    }

    public class ModerationCheckResult
    {
        public bool Flagged { get; set; }
        public Dictionary<string, bool> Categories { get; set; }        = new();
        public Dictionary<string, double> CategoryScores { get; set; }  = new();
    }

    public class ModerationResultItem
    {
        public string MessageId { get; set; }               = "";
        public ModerationCheckResult Result { get; set; }   = new();
    }

    public class ModerationResponse
    {
        public List<ModerationResultItem> Results { get; set; } = new();
    }
}
