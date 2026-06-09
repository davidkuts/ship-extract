namespace ShipExtract.Domain.Enums;

/// <summary>Represents the overall status of a batch processing job.</summary>
public enum BatchStatus
{
    /// <summary>Batch has been created but processing has not started.</summary>
    Pending,
    /// <summary>Batch is currently being processed.</summary>
    Running,
    /// <summary>All files in the batch have been processed.</summary>
    Completed,
    /// <summary>Batch processing was cancelled before completion.</summary>
    Cancelled
}
