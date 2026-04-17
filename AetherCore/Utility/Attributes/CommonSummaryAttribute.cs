namespace AetherCore.Utility.Attributes
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class CommonSummaryAttribute : Attribute
    {
        public string SummaryContent { get; }

        public CommonSummaryAttribute(string summaryContent)
        {
            SummaryContent = summaryContent;
        }
    }
}