using System.Text.Json.Nodes;

namespace TradingView.Client;

internal sealed class IndicatorApi(TvExecutor executor) : IIndicatorApi
{
    public Task<IndicatorAddResult> AddAsync(string name, IReadOnlyDictionary<string, object?>? inputs = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var json = inputs is { Count: > 0 } ? IndicatorInputs.ToJson(inputs) : null;
        return Add(name, json, cancellationToken);
    }

    public Task<IndicatorAddResult> AddAsync(string name, JsonObject inputs, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var json = inputs is { Count: > 0 } ? IndicatorInputs.ToJson(inputs) : null;
        return Add(name, json, cancellationToken);
    }

    public Task<IndicatorRemoveResult> RemoveAsync(string entityId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        return executor.RunAsync(TvCommand.Create("indicator", "remove", entityId), TvJsonContext.Default.IndicatorRemoveResult, cancellationToken);
    }

    public Task<IndicatorVisibilityResult> SetVisibilityAsync(string entityId, bool visible, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        var command = TvCommand.Create("indicator", "toggle", entityId, visible ? "--visible" : "--hidden");
        return executor.RunAsync(command, TvJsonContext.Default.IndicatorVisibilityResult, cancellationToken);
    }

    public Task<IndicatorSetInputsResult> SetInputsAsync(string entityId, IReadOnlyDictionary<string, object?> inputs, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentNullException.ThrowIfNull(inputs);
        return Set(entityId, inputs.Count == 0 ? null : IndicatorInputs.ToJson(inputs), cancellationToken);
    }

    public Task<IndicatorSetInputsResult> SetInputsAsync(string entityId, JsonObject inputs, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentNullException.ThrowIfNull(inputs);
        return Set(entityId, inputs.Count == 0 ? null : IndicatorInputs.ToJson(inputs), cancellationToken);
    }

    public Task<IndicatorInfoResult> GetAsync(string entityId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        return executor.RunAsync(TvCommand.Create("indicator", "get", entityId), TvJsonContext.Default.IndicatorInfoResult, cancellationToken);
    }

    private Task<IndicatorAddResult> Add(string name, string? inputsJson, CancellationToken cancellationToken)
    {
        var args = new List<string> { "indicator", "add", name };
        if (inputsJson is not null)
        {
            args.Add("-i");
            args.Add(inputsJson);
        }

        return executor.RunAsync(new TvCommand(args), TvJsonContext.Default.IndicatorAddResult, cancellationToken);
    }

    private Task<IndicatorSetInputsResult> Set(string entityId, string? inputsJson, CancellationToken cancellationToken)
    {
        if (inputsJson is null)
        {
            throw new ArgumentException("At least one input is required.", "inputs");
        }

        var command = TvCommand.Create("indicator", "set", entityId, "-i", inputsJson);
        return executor.RunAsync(command, TvJsonContext.Default.IndicatorSetInputsResult, cancellationToken);
    }
}
