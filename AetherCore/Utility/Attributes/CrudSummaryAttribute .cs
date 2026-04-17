using System;

namespace AetherCore.Utility.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class CrudSummaryAttribute : Attribute
    {
        public string SummaryTitle { get; }

        public CrudSummaryAttribute(string summaryTitle)
        {
            SummaryTitle = summaryTitle;
        }
    }
}