namespace DocumentProcessing.Core.Results;

/// <summary>
/// How the engine came to hold a concluded metadata value.
/// </summary>
/// <remarks>
/// Absence is never an origin. A metadata fact the engine could not conclude is
/// represented by no value at all, so a consumer never has to distinguish
/// "absent" from "present but meaning absent".
/// </remarks>
public enum DocumentMetadataOrigin
{
    /// <summary>
    /// Read from a metadata field of the source representation, such as the PDF
    /// information dictionary or the EPUB package document.
    /// </summary>
    Native = 0,

    /// <summary>
    /// Derived from the document's own structure or content rather than from a
    /// metadata field.
    /// </summary>
    Structural = 1,

    /// <summary>
    /// Concluded by the engine from more than one piece of evidence.
    /// </summary>
    Reconciled = 2
}

/// <summary>
/// What a source states a date to be.
/// </summary>
/// <remarks>
/// Creation, modification and publication are not the same fact, and collapsing
/// them into one date would destroy the only thing that makes a date usable.
/// A source that does not say which it means yields
/// <see cref="Unspecified"/> rather than a guess.
/// </remarks>
public enum DocumentMetadataDateKind
{
    /// <summary>The source states no specific kind.</summary>
    Unspecified = 0,

    /// <summary>The source states when the document was created.</summary>
    Created = 1,

    /// <summary>The source states when the document was last modified.</summary>
    Modified = 2,

    /// <summary>The source states when the document was published.</summary>
    Published = 3
}

/// <summary>
/// One concluded metadata value with its provenance.
/// </summary>
public sealed record DocumentMetadataValue
{
    #region Properties

    /// <summary>
    /// Gets the concluded value, as the evidence supplied it.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets how the engine came to hold this value.
    /// </summary>
    public DocumentMetadataOrigin Origin { get; }

    /// <summary>
    /// Gets the concrete source of the value when one identifies it, such as
    /// <c>pdf.info.title</c> or <c>epub.opf.dc:title</c>.
    /// </summary>
    public string? SourceHint { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates a concluded metadata value.
    /// </summary>
    /// <param name="value">Concluded value.</param>
    /// <param name="origin">How the value was obtained.</param>
    /// <param name="sourceHint">Concrete source of the value.</param>
    public DocumentMetadataValue(
        string value,
        DocumentMetadataOrigin origin,
        string? sourceHint = null)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                "Document metadata value cannot be empty.",
                nameof(value));
        }

        Value =
            value;

        Origin =
            origin;

        SourceHint =
            string.IsNullOrWhiteSpace(
                sourceHint)
                ? null
                : sourceHint.Trim();
    }

    #endregion
}

/// <summary>
/// One contributor statement exactly as the source expresses it.
/// </summary>
/// <remarks>
/// A contributor statement is not a person. Sources routinely list a producing
/// toolchain beside the real authors, and the same human appears in different
/// forms across formats. Deciding which statements denote people, and which
/// denote the same person, is a consumer's domain problem; the engine carries
/// the statements without asserting either.
/// </remarks>
public sealed record DocumentMetadataContributor
{
    #region Properties

    /// <summary>
    /// Gets the contributor statement as the source expresses it.
    /// </summary>
    public string Statement { get; }

    /// <summary>
    /// Gets how the engine came to hold this statement.
    /// </summary>
    public DocumentMetadataOrigin Origin { get; }

    /// <summary>
    /// Gets the concrete source of the statement.
    /// </summary>
    public string? SourceHint { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates a contributor statement.
    /// </summary>
    /// <param name="statement">Contributor statement.</param>
    /// <param name="origin">How the statement was obtained.</param>
    /// <param name="sourceHint">Concrete source of the statement.</param>
    public DocumentMetadataContributor(
        string statement,
        DocumentMetadataOrigin origin,
        string? sourceHint = null)
    {
        if (string.IsNullOrWhiteSpace(
                statement))
        {
            throw new ArgumentException(
                "Document contributor statement cannot be empty.",
                nameof(statement));
        }

        Statement =
            statement;

        Origin =
            origin;

        SourceHint =
            string.IsNullOrWhiteSpace(
                sourceHint)
                ? null
                : sourceHint.Trim();
    }

    #endregion
}

/// <summary>
/// One dated statement, kept in the source's own textual form.
/// </summary>
/// <remarks>
/// The value is not parsed. Sources express dates in incompatible forms — a PDF
/// uses <c>D:20260723165741+00'00'</c>, an EPUB uses ISO-8601 — and some carry
/// placeholders such as <c>0101-01-01</c> that a parser would silently turn into
/// a plausible instant. Keeping the text preserves the evidence a consumer needs
/// in order to reject it.
/// </remarks>
public sealed record DocumentMetadataDate
{
    #region Properties

    /// <summary>
    /// Gets the date as the source expresses it, unparsed.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets what the source states this date to be.
    /// </summary>
    public DocumentMetadataDateKind Kind { get; }

    /// <summary>
    /// Gets how the engine came to hold this date.
    /// </summary>
    public DocumentMetadataOrigin Origin { get; }

    /// <summary>
    /// Gets the concrete source of the date.
    /// </summary>
    public string? SourceHint { get; }

    #endregion

    #region ctor

    /// <summary>
    /// Creates a dated statement.
    /// </summary>
    /// <param name="value">Date as the source expresses it.</param>
    /// <param name="kind">What the source states the date to be.</param>
    /// <param name="origin">How the date was obtained.</param>
    /// <param name="sourceHint">Concrete source of the date.</param>
    public DocumentMetadataDate(
        string value,
        DocumentMetadataDateKind kind,
        DocumentMetadataOrigin origin,
        string? sourceHint = null)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                "Document metadata date cannot be empty.",
                nameof(value));
        }

        Value =
            value;

        Kind =
            kind;

        Origin =
            origin;

        SourceHint =
            string.IsNullOrWhiteSpace(
                sourceHint)
                ? null
                : sourceHint.Trim();
    }

    #endregion
}

/// <summary>
/// Neutral document metadata concluded by the engine.
/// </summary>
/// <remarks>
/// This describes the intellectual document, not the file that carried it.
/// <c>Source.FileName</c> is source identity and never appears here: a filename
/// that happens to read like a title is still a filename, and a consumer must be
/// able to tell the difference.
///
/// Every field is optional. A document whose source states nothing yields an
/// empty instance rather than fabricated values, and a consumer is never forced
/// to invent metadata to satisfy the contract.
/// </remarks>
public sealed record DocumentMetadata
{
    #region Variables and Constants

    /// <summary>
    /// Gets metadata carrying no concluded value.
    /// </summary>
    public static DocumentMetadata Empty { get; } =
        new();

    #endregion

    #region Properties

    /// <summary>
    /// Gets the concluded document title.
    /// </summary>
    public DocumentMetadataValue? Title { get; }

    /// <summary>
    /// Gets the concluded document subtitle.
    /// </summary>
    /// <remarks>
    /// No supported format populates this yet. PDF has no subtitle field, and
    /// EPUB expresses one through a <c>meta refines</c> refinement whose
    /// resolution is an interpretation step this version does not perform. The
    /// field exists so that adding it later needs no contract change.
    /// </remarks>
    public DocumentMetadataValue? Subtitle { get; }

    /// <summary>
    /// Gets the concluded document description or abstract.
    /// </summary>
    public DocumentMetadataValue? Description { get; }

    /// <summary>
    /// Gets contributor statements in the source's own order.
    /// </summary>
    public IReadOnlyList<DocumentMetadataContributor> Contributors { get; }

    /// <summary>
    /// Gets the concluded publisher statement.
    /// </summary>
    public DocumentMetadataValue? Publisher { get; }

    /// <summary>
    /// Gets the concluded document language.
    /// </summary>
    public DocumentMetadataValue? Language { get; }

    /// <summary>
    /// Gets dated statements, each retaining what the source said it was.
    /// </summary>
    public IReadOnlyList<DocumentMetadataDate> Dates { get; }

    /// <summary>
    /// Gets whether no metadata value was concluded.
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
    /// Creates neutral document metadata.
    /// </summary>
    /// <param name="title">Concluded title.</param>
    /// <param name="subtitle">Concluded subtitle.</param>
    /// <param name="description">Concluded description or abstract.</param>
    /// <param name="contributors">Contributor statements.</param>
    /// <param name="publisher">Concluded publisher statement.</param>
    /// <param name="language">Concluded language.</param>
    /// <param name="dates">Dated statements.</param>
    public DocumentMetadata(
        DocumentMetadataValue? title = null,
        DocumentMetadataValue? subtitle = null,
        DocumentMetadataValue? description = null,
        IReadOnlyList<DocumentMetadataContributor>? contributors = null,
        DocumentMetadataValue? publisher = null,
        DocumentMetadataValue? language = null,
        IReadOnlyList<DocumentMetadataDate>? dates = null)
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

    private static IReadOnlyList<T> Freeze<T>(
        IReadOnlyList<T>? values,
        string parameterName)
        where T : class
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
                "Document metadata cannot contain null values.",
                parameterName);
        }

        return Array.AsReadOnly(
            frozen);
    }

    #endregion
}
