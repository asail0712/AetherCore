namespace AetherCore.Utility.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AppSettingsAttribute : Attribute
    {
        public string SectionName { get; }

        public AppSettingsAttribute()
            : this(null!)
        {
        }

        public AppSettingsAttribute(string? sectionName)
        {
            SectionName = sectionName ?? string.Empty;
        }

        public string ResolveSectionName(Type type)
            => string.IsNullOrWhiteSpace(SectionName)
                ? type.Name
                : SectionName;
    }
}
