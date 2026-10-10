using System.Text.Json.Nodes;

namespace TradingView.Client;

/// <summary>Indicators on the chart (<c>tv indicator *</c>). Entity ids come from <see cref="ChartStateResult.Studies"/>.</summary>
public interface IIndicatorApi
{
    /// <summary>
    /// Adds an indicator by its full TradingView name (e.g. "Relative Strength Index", not "RSI").
    /// Input overrides are applied after creation; keys the study does not have are reported in
    /// <see cref="IndicatorAddInputs.UnknownInputs"/>.
    /// </summary>
    /// <exception cref="TvOperationFailedException">TradingView did not create a study (unknown name, plan limit).</exception>
    Task<IndicatorAddResult> AddAsync(string name, IReadOnlyDictionary<string, object?>? inputs = null, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="AddAsync(string, IReadOnlyDictionary{string, object}, CancellationToken)"/>
    Task<IndicatorAddResult> AddAsync(string name, JsonObject inputs, CancellationToken cancellationToken = default);

    Task<IndicatorRemoveResult> RemoveAsync(string entityId, CancellationToken cancellationToken = default);

    Task<IndicatorVisibilityResult> SetVisibilityAsync(string entityId, bool visible, CancellationToken cancellationToken = default);

    /// <summary>Changes input values by input id (see <see cref="GetAsync"/>). Unknown ids are silently ignored by the CLI.</summary>
    Task<IndicatorSetInputsResult> SetInputsAsync(string entityId, IReadOnlyDictionary<string, object?> inputs, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="SetInputsAsync(string, IReadOnlyDictionary{string, object}, CancellationToken)"/>
    Task<IndicatorSetInputsResult> SetInputsAsync(string entityId, JsonObject inputs, CancellationToken cancellationToken = default);

    /// <summary>Visibility and input values of a study.</summary>
    /// <exception cref="TvCommandException">The study was not found.</exception>
    Task<IndicatorInfoResult> GetAsync(string entityId, CancellationToken cancellationToken = default);
}
