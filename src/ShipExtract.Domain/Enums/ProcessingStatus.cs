namespace ShipExtract.Domain.Enums;

/// <summary>Represents the processing status of a single document extraction job.</summary>
public enum ProcessingStatus
{
    /// <summary>Job has been queued but not yet started.</summary>
    Pending,
    /// <summary>Job is currently being processed.</summary>
    Running,
    /// <summary>Job completed successfully with all data extracted.</summary>
    Succeeded,
    /// <summary>Job completed but some fields could not be extracted.</summary>
    PartialSuccess,
    /// <summary>Job failed and no usable data was extracted.</summary>
    Failed
}
