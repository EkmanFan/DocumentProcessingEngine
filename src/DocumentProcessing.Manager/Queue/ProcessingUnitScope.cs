namespace DocumentProcessing.Manager.Queue;

/// <summary>
/// Defines the immutable source scope of one atomic processing unit.
/// </summary>
public abstract record ProcessingUnitScope
{
    private ProcessingUnitScope()
    {
    }

    /// <summary>
    /// Represents processing of the complete submitted document.
    /// </summary>
    public sealed record WholeDocument
        : ProcessingUnitScope;

    /// <summary>
    /// Represents an approved inclusive range of original physical pages.
    /// </summary>
    public sealed record PageRange
        : ProcessingUnitScope
    {
        #region Properties

        /// <summary>
        /// Gets the inclusive first physical page number.
        /// </summary>
        public int StartPhysicalPageNumber { get; }

        /// <summary>
        /// Gets the inclusive last physical page number.
        /// </summary>
        public int EndPhysicalPageNumber { get; }

        /// <summary>
        /// Gets the user-visible segment title.
        /// </summary>
        public string Title { get; }

        #endregion

        #region ctor

        /// <summary>
        /// Creates one approved physical-page range.
        /// </summary>
        public PageRange(
            int startPhysicalPageNumber,
            int endPhysicalPageNumber,
            string title)
        {
            if (startPhysicalPageNumber <=
                0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startPhysicalPageNumber),
                    startPhysicalPageNumber,
                    "Start physical page number must be positive.");
            }

            if (endPhysicalPageNumber <
                startPhysicalPageNumber)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(endPhysicalPageNumber),
                    endPhysicalPageNumber,
                    "End physical page number cannot precede the start page.");
            }

            if (string.IsNullOrWhiteSpace(
                    title))
            {
                throw new ArgumentException(
                    "Processing-unit range title cannot be empty.",
                    nameof(title));
            }

            StartPhysicalPageNumber =
                startPhysicalPageNumber;

            EndPhysicalPageNumber =
                endPhysicalPageNumber;

            Title =
                title.Trim();
        }

        #endregion
    }

    /// <summary>
    /// Represents an approved inclusive range of stable ordered native content
    /// units, such as EPUB spine resources.
    /// </summary>
    public sealed record ContentUnitRange
        : ProcessingUnitScope
    {
        #region Properties

        /// <summary>Gets the zero-based inclusive first content-unit index.</summary>
        public int StartContentUnitIndex { get; }

        /// <summary>Gets the stable identifier of the first content unit.</summary>
        public string StartContentUnitId { get; }

        /// <summary>Gets the zero-based inclusive last content-unit index.</summary>
        public int EndContentUnitIndex { get; }

        /// <summary>Gets the stable identifier of the last content unit.</summary>
        public string EndContentUnitId { get; }

        /// <summary>Gets the user-visible segment title.</summary>
        public string Title { get; }

        #endregion

        #region ctor

        /// <summary>Creates one approved native content-unit range.</summary>
        public ContentUnitRange(
            int startContentUnitIndex,
            string startContentUnitId,
            int endContentUnitIndex,
            string endContentUnitId,
            string title)
        {
            if (startContentUnitIndex <
                0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startContentUnitIndex),
                    startContentUnitIndex,
                    "Start content-unit index cannot be negative.");
            }

            if (endContentUnitIndex <
                startContentUnitIndex)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(endContentUnitIndex),
                    endContentUnitIndex,
                    "End content-unit index cannot precede the start unit.");
            }

            StartContentUnitId =
                NormalizeRequired(
                    startContentUnitId,
                    nameof(startContentUnitId),
                    "Content-unit identifier cannot be empty.");

            EndContentUnitId =
                NormalizeRequired(
                    endContentUnitId,
                    nameof(endContentUnitId),
                    "Content-unit identifier cannot be empty.");

            if (startContentUnitIndex ==
                    endContentUnitIndex &&
                !string.Equals(
                    StartContentUnitId,
                    EndContentUnitId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Equal content-unit indexes must identify the same unit.",
                    nameof(endContentUnitId));
            }

            Title =
                NormalizeRequired(
                    title,
                    nameof(title),
                    "Processing-unit range title cannot be empty.");

            StartContentUnitIndex =
                startContentUnitIndex;

            EndContentUnitIndex =
                endContentUnitIndex;
        }

        #endregion

        #region Methods Validation

        private static string NormalizeRequired(
            string value,
            string parameterName,
            string message)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                throw new ArgumentException(
                    message,
                    parameterName);
            }

            return value.Trim();
        }

        #endregion
    }
}
