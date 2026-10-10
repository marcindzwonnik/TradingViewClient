using System.Text.Json;

namespace TradingView.Client;

/// <summary>Result of <c>tv indicator add</c>. <see cref="TvResult.Success"/> is false when TradingView did not create a study.</summary>
public sealed record IndicatorAddResult : TvResult
{
    public string? Action { get; init; }

    /// <summary>The name as requested.</summary>
    public string? Indicator { get; init; }

    /// <summary>Entity id of the new study, or null when nothing was added.</summary>
    public string? EntityId { get; init; }

    public int NewStudyCount { get; init; }

    /// <summary>Outcome of applying input overrides; null when none were requested.</summary>
    public IndicatorAddInputs? Inputs { get; init; }
}

public sealed record IndicatorAddInputs : TvObject
{
    /// <summary>Overrides that took effect (confirmed by reading the study back).</summary>
    public Dictionary<string, JsonElement>? Applied { get; init; }

    /// <summary>Requested keys the study does not have; they were ignored.</summary>
    public IReadOnlyList<string>? UnknownInputs { get; init; }

    public string? Error { get; init; }
}

/// <summary>Result of <c>tv indicator remove</c>.</summary>
public sealed record IndicatorRemoveResult : TvResult
{
    public string? Action { get; init; }
    public string EntityId { get; init; } = string.Empty;
}

/// <summary>Result of <c>tv indicator toggle</c>.</summary>
public sealed record IndicatorVisibilityResult : TvResult
{
    public string EntityId { get; init; } = string.Empty;
    public bool Visible { get; init; }
}

/// <summary>Result of <c>tv indicator set</c>. Unknown input keys are silently ignored by the CLI.</summary>
public sealed record IndicatorSetInputsResult : TvResult
{
    public string EntityId { get; init; } = string.Empty;
    public Dictionary<string, JsonElement> UpdatedInputs { get; init; } = [];
}
