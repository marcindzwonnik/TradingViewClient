using TradingView.Client.Tests.Support;

namespace TradingView.Client.Tests;

public class FixtureContractTests
{
    [Fact]
    public void Every_fixture_file_is_registered_with_a_model()
    {
        var onDisk = Directory.EnumerateFiles(Fixtures.Root, "*.json", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Fixtures.Root, f).Replace('\\', '/'))
            .Select(f => f[..^".json".Length])
            .Order();

        Assert.Equal(Fixtures.Models.Keys.Order(), onDisk);
    }

    [Theory]
    [MemberData(nameof(JsonFixtures))]
    public void Fixture_deserializes_successfully(string fixture)
    {
        var result = Fixtures.Deserialize(fixture);

        Assert.Equal(!Fixtures.SoftFailures.Contains(fixture), result.Success);
        if (!result.Success)
        {
            Assert.False(string.IsNullOrEmpty(result.Error));
        }
    }

    /// <summary>
    /// Contract drift detector: a field present in real CLI output but missing from the model would land in
    /// <see cref="TvObject.AdditionalData"/>. After deserializing a captured response nothing may be left there.
    /// </summary>
    [Theory]
    [MemberData(nameof(JsonFixtures))]
    public void Fixture_leaves_no_unmapped_fields(string fixture)
    {
        var unmapped = Fixtures.FindUnmappedFields(Fixtures.Deserialize(fixture));

        Assert.Empty(unmapped);
    }

    [Fact]
    public void Status_fixture_maps_known_values()
    {
        var status = (StatusResult)Fixtures.Deserialize("health/status");

        Assert.True(status.CdpConnected);
        Assert.True(status.ApiAvailable);
        Assert.Equal("CME_MINI:ES1!", status.ChartSymbol);
        Assert.Equal(19, status.ChartTypeId); // outside the documented 0-9 range
        Assert.NotNull(status.Update);
    }

    [Fact]
    public void Discover_fixture_maps_camel_case_method_count()
    {
        var discover = (DiscoverResult)Fixtures.Deserialize("health/discover");

        Assert.Equal(6, discover.ApisTotal);
        Assert.NotNull(discover.Apis?.ChartApi);
        Assert.True(discover.Apis.ChartApi.Available);
        Assert.Equal(3, discover.Apis.ChartApi.MethodCount);
        Assert.False(discover.Apis.AlertService?.Available);
        Assert.Null(discover.Apis.AlertService?.MethodCount);
    }

    [Fact]
    public void UiState_fixture_maps_panels_buttons_and_chart()
    {
        var ui = (UiStateResult)Fixtures.Deserialize("health/ui-state");

        Assert.NotNull(ui.BottomPanel);
        Assert.NotNull(ui.Buttons);
        Assert.NotEmpty(ui.Buttons["top_bar"]);
        Assert.NotNull(ui.Chart);
        Assert.NotNull(ui.Chart.Symbol);
        Assert.NotNull(ui.Chart.ChartTypeId);
        Assert.NotNull(ui.Replay);
        Assert.Null(ui.Replay.Error);
    }

    public static TheoryData<string> JsonFixtures() => Fixtures.AllJsonFixtures();
}
