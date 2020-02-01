namespace DocFxForUnity
{
    /// <summary>
    /// Represents a xref map file of Unity.
    /// </summary>
    public sealed class XrefMap
    {
        public bool sorted { get; set; }

        public XrefMapReference[] references { get; set; }
    }
}