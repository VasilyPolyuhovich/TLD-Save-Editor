namespace The_Long_Dark_Save_Editor_2.Helpers
{
    // Plain data holder used by enum-backed combo boxes. The XAML markup extension that
    // produces these (EnumerationExtension) lives in the UI project; this POCO stays in Core
    // so that Util / save loading can return display entries without a UI dependency.
    public class EnumerationMember
    {
        public string Description { get; set; }
        public object Value { get; set; }
    }
}
