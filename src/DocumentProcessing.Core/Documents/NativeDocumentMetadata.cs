namespace DocumentProcessing.Core.Documents;

/// <summary>
/// One descriptive metadata fact read verbatim from a native representation.
/// </summary>
/// <remarks>
/// The value is preserved exactly as the representation supplies it. A format
/// adapter never cleans, normalizes or reinterprets it: a PDF
/// <c>/Title</c> reading "Microsoft Word - final3.docx" is acquired as such, and
/// deciding what it is worth belongs to a later reconciliation step.
///
/// Origin is not modelled as a property in this contract because the containing
/// <see cref="NativeDocumentMetadata"/> already establishes it: every value here
/// is native by construction. <see cref="SourceHint"/> records which native
/// field it came from, which is the finer fact.
/// </remarks>
public sealed record NativeDocumentMetadataValue
{
    #region Properties

    /// <summary>
    /// Gets the verbatim value supplied by the native representation.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the stable identifier of the native field the value came from, such
    /// as <c>pdf.info.title</c> or <c>epub.opf.dc:title</c>.
    /// </summary>
    public string SourceHint { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates a native metadata value.
    /// </summary>
    /// <param name="value">Verbatim native value.</param>
    /// <param name="sourceHint">Native field the value came from.</param>
    public NativeDocumentMetadataValue(
        string value,
        string sourceHint)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                "Native metadata value cannot be empty.",
                nameof(value));
        }

        if (string.IsNullOrWhiteSpace(
                sourceHint))
        {
            throw new ArgumentException(
                "Native metadata source hint cannot be empty.",
                nameof(sourceHint));
        }

        Value =
            value;

        SourceHint =
            sourceHint.Trim();
    }

    #endregion

    #region Methods Factory

    /// <summary>
    /// Creates a value when the native field carries usable text, and returns
    /// <see langword="null"/> otherwise.
    /// </summary>
    /// <remarks>
    /// Absence is represented by no value rather than by an empty one, so a
    /// consumer never has to distinguish "absent" from "present but blank".
    /// </remarks>
    /// <param name="value">Candidate native value.</param>
    /// <param name="sourceHint">Native field the value came from.</param>
    /// <returns>The value, or <see langword="null"/> when unusable.</returns>
    public static NativeDocumentMetadataValue? TryCreate(
        string? value,
        string sourceHint) =>
        string.IsNullOrWhiteSpace(
            value)
            ? null
            : new NativeDocumentMetadataValue(
                value,
                sourceHint);

    #endregion
}

/// <summary>
/// Descriptive metadata facts acquired from a document's native representation.
/// </summary>
/// <remarks>
/// This is acquisition evidence, not concluded document metadata. Presence of a
/// native title does not make it the document title: reconciling native and
/// structural evidence into one portable answer is a later Engine
/// responsibility, and this contract deliberately keeps no place for a decision.
///
/// The shape is intentionally limited to fields at least one supported format
/// exposes today. Nothing is declared for formats that are not implemented.
/// </remarks>
public sealed record NativeDocumentMetadata
{
    #region Variables and Constants

    /// <summary>
    /// Gets metadata carrying no acquired fact.
    /// </summary>
    public static NativeDocumentMetadata Empty { get; } =
        new();

    #endregion

    #region Properties

    /// <summary>
    /// Gets the native document title when the representation supplies one.
    /// </summary>
    public NativeDocumentMetadataValue? Title { get; }

    /// <summary>
    /// Gets the native document subtitle when the representation supplies one.
    /// </summary>
    public NativeDocumentMetadataValue? Subtitle { get; }

    /// <summary>
    /// Gets the native description, abstract or subject when supplied.
    /// </summary>
    public NativeDocumentMetadataValue? Description { get; }

    /// <summary>
    /// Gets native contributor statements in the representation's own order.
    /// </summary>
    public IReadOnlyList<NativeDocumentMetadataValue> Contributors { get; }

    /// <summary>
    /// Gets the native publisher statement when supplied.
    /// </summary>
    public NativeDocumentMetadataValue? Publisher { get; }

    /// <summary>
    /// Gets the native language statement when supplied.
    /// </summary>
    public NativeDocumentMetadataValue? Language { get; }

    /// <summary>
    /// Gets native date statements, verbatim and unparsed.
    /// </summary>
    /// <remarks>
    /// PDF dates arrive in the representation's own textual form. Parsing them
    /// would be interpretation, which this slice does not perform.
    /// </remarks>
    public IReadOnlyList<NativeDocumentMetadataValue> Dates { get; }

    /// <summary>
    /// Gets whether no native metadata fact was acquired.
    /// </summary>
    public bool IsEmpty =>
        Title is null &&
        Subtitle is null &&
        Description is null &&
        Publisher is null &&
        Language is null &&
        Contributors.Count == 0 &&
        Dates.Count == 0;

    #endregion

    #region ctor

    /// <summary>
    /// Creates native document metadata.
    /// </summary>
    /// <param name="title">Native title.</param>
    /// <param name="subtitle">Native subtitle.</param>
    /// <param name="description">Native description, abstract or subject.</param>
    /// <param name="contributors">Native contributor statements.</param>
    /// <param name="publisher">Native publisher statement.</param>
    /// <param name="language">Native language statement.</param>
    /// <param name="dates">Native date statements, verbatim.</param>
    public NativeDocumentMetadata(
        NativeDocumentMetadataValue? title = null,
        NativeDocumentMetadataValue? subtitle = null,
        NativeDocumentMetadataValue? description = null,
        IReadOnlyList<NativeDocumentMetadataValue>? contributors = null,
        NativeDocumentMetadataValue? publisher = null,
        NativeDocumentMetadataValue? language = null,
        IReadOnlyList<NativeDocumentMetadataValue>? dates = null)
    {
        Title =
            title;

        Subtitle =
            subtitle;

        Description =
            description;

        Publisher =
            publisher;

        Language =
            language;

        Contributors =
            Freeze(
                contributors,
                nameof(contributors));

        Dates =
            Freeze(
                dates,
                nameof(dates));
    }

    #endregion

    #region Methods Validation

    private static IReadOnlyList<NativeDocumentMetadataValue> Freeze(
        IReadOnlyList<NativeDocumentMetadataValue>? values,
        string parameterName)
    {
        if (values is null)
        {
            return [];
        }

        var frozen =
            values.ToArray();

        if (frozen.Any(
                value =>
                    value is null))
        {
            throw new ArgumentException(
                "Native document metadata cannot contain null values.",
                parameterName);
        }

        return Array.AsReadOnly(
            frozen);
    }

    #endregion
}
