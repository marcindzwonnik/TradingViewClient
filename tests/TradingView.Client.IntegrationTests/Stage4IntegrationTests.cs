using System.Text.Json;

namespace TradingView.Client.IntegrationTests;

public class Stage4IntegrationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<TradingViewClient> Client() => Live.CreateClientOrSkipAsync(Ct);

    // --- read-only ----------------------------------------------------------------------------------------

    [Fact]
    public async Task Alerts_list_is_well_formed()
    {
        using var tv = await Client();

        var list = await tv.Alerts.ListAsync(Ct);

        Assert.Equal(list.AlertCount, list.Alerts.Count);
        Assert.All(list.Alerts, a => Assert.True(a.AlertId > 0));
    }

    [Fact]
    public async Task Watchlist_get_returns_rows_with_symbols()
    {
        using var tv = await Client();

        var list = await tv.Watchlist.GetAsync(Ct);

        Assert.Equal(list.Count, list.Symbols.Count);
        Assert.All(list.Symbols, s => Assert.False(string.IsNullOrEmpty(s.Symbol)));
    }

    [Fact]
    public async Task Layouts_list_is_well_formed()
    {
        using var tv = await Client();

        var list = await tv.Layouts.ListAsync(Ct);

        Assert.Equal(list.LayoutCount, list.Layouts.Count);
        Assert.All(list.Layouts, l => Assert.False(string.IsNullOrEmpty(l.Name)));
    }

    [Fact]
    public async Task Panes_and_tabs_describe_the_window()
    {
        using var tv = await Client();

        var panes = await tv.Panes.ListAsync(Ct);
        var tabs = await tv.Tabs.ListAsync(Ct);

        Assert.True(panes.ChartCount >= 1);
        Assert.True(panes.VisiblePanes.Count >= 1);
        Assert.False(string.IsNullOrEmpty(panes.VisiblePanes[0].Symbol));
        Assert.Equal(tabs.TabCount, tabs.Tabs.Count);
        Assert.Contains(tabs.Tabs, t => t.IsChart);
    }

    [Fact]
    public async Task Drawings_list_is_well_formed()
    {
        using var tv = await Client();

        var list = await tv.Drawings.ListAsync(Ct);

        Assert.Equal(list.Count, list.Shapes.Count);
    }

    [Fact]
    public async Task Screenshot_writes_a_png()
    {
        using var tv = await Client();
        var name = "tvclient_it_" + Guid.NewGuid().ToString("N")[..8];

        var shot = await tv.Screenshots.CaptureAsync(ScreenshotRegion.Chart, name, Ct);
        try
        {
            Assert.Equal("chart", shot.Region);
            Assert.EndsWith(name + ".png", shot.FilePath);
            Assert.True(File.Exists(shot.FilePath));
            Assert.Equal(shot.SizeBytes, new FileInfo(shot.FilePath).Length);
        }
        finally
        {
            if (File.Exists(shot.FilePath))
            {
                File.Delete(shot.FilePath);
            }
        }
    }

    [Fact]
    public async Task Ui_evaluate_returns_json_values()
    {
        using var tv = await Client();

        var two = await tv.Ui.EvaluateAsync("1+1", Ct);
        var obj = await tv.Ui.EvaluateAsync("({a: 1, b: [1, 2], c: null})", Ct);
        var undefined = await tv.Ui.EvaluateAsync("undefined", Ct);
        var viewport = await tv.Ui.EvaluateAsync("innerWidth", Ct);

        Assert.Equal(2, two.GetInt32());
        Assert.Equal(JsonValueKind.Array, obj.GetProperty("b").ValueKind);
        Assert.Equal(JsonValueKind.Null, undefined.ValueKind);
        Assert.True(viewport.GetInt32() > 0);
    }

    [Fact]
    public async Task Ui_find_locates_toolbar_elements_and_unknown_click_fails()
    {
        using var tv = await Client();

        var find = await tv.Ui.FindAsync("Indicators", FindStrategy.Text, Ct);

        Assert.True(find.Count > 0);
        Assert.NotEmpty(find.Elements);
        await Assert.ThrowsAsync<TvCommandException>(() => tv.Ui.ClickAsync(UiSelector.DataName, "no-such-element-" + Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Ui_keyboard_escape_is_harmless()
    {
        using var tv = await Client();

        var key = await tv.Ui.PressKeyAsync("Escape", cancellationToken: Ct);

        Assert.Equal("Escape", key.Key);
        Assert.Empty(key.Modifiers);
    }

    // --- mutating (TV_INTEGRATION_MUTATING=1); each test restores what it changed ----------------------------

    [Fact]
    public async Task Drawing_lifecycle_draw_inspect_remove()
    {
        Live.SkipUnlessMutating();
        using var tv = await Client();
        var price = (await tv.Data.GetQuoteAsync(cancellationToken: Ct)).Last ?? 100;
        var now = DateTimeOffset.UtcNow;
        var mine = new List<string>();
        try
        {
            var line = await tv.Drawings.DrawHorizontalLineAsync(price, now, cancellationToken: Ct);
            var trend = await tv.Drawings.DrawTrendLineAsync(new ShapePoint(now.AddHours(-1), price), new ShapePoint(now, price + 5), cancellationToken: Ct);
            var text = await tv.Drawings.DrawTextAsync("tvclient", new ShapePoint(now, price), cancellationToken: Ct);
            mine.AddRange(new[] { line.EntityId, trend.EntityId, text.EntityId }.OfType<string>());
            Assert.Equal(3, mine.Count);

            var list = await tv.Drawings.ListAsync(Ct);
            Assert.All(mine, id => Assert.Contains(list.Shapes, s => s.Id == id));
            Assert.Contains(list.Shapes, s => s.Id == line.EntityId && s.Name == "horizontal_line");

            var details = await tv.Drawings.GetAsync(line.EntityId!, Ct);
            Assert.Equal("horizontal_line", details.Name);
            Assert.Equal(price, Assert.Single(details.Points!).Price);

            var removed = await tv.Drawings.RemoveAsync(line.EntityId!, Ct);
            Assert.True(removed.Removed);
            mine.Remove(line.EntityId!);
            Assert.DoesNotContain((await tv.Drawings.ListAsync(Ct)).Shapes, s => s.Id == line.EntityId);
        }
        finally
        {
            foreach (var id in mine)
            {
                await tv.Drawings.RemoveAsync(id, Ct);
            }
        }
    }

    [Fact]
    public async Task Alert_create_list_delete()
    {
        Live.SkipUnlessMutating();
        using var tv = await Client();
        var before = (await tv.Alerts.ListAsync(Ct)).AlertCount;
        var price = Math.Round(((await tv.Data.GetQuoteAsync(cancellationToken: Ct)).Last ?? 100) * 1.5);
        long? id = null;
        try
        {
            var created = await tv.Alerts.CreateAsync(price, AlertCondition.GreaterThan, "tvclient-integration", Ct);
            id = created.AlertId;
            Assert.NotNull(id);
            Assert.Equal("greater", created.Condition);

            var list = await tv.Alerts.ListAsync(Ct);
            var mine = Assert.Single(list.Alerts, a => a.AlertId == id);
            Assert.Equal("tvclient-integration", mine.Message);
            Assert.True(mine.Active);
        }
        finally
        {
            if (id is { } alertId)
            {
                await tv.Alerts.DeleteAsync(alertId, Ct);
            }
        }

        Assert.Equal(before, (await tv.Alerts.ListAsync(Ct)).AlertCount);
    }

    [Fact]
    public async Task Watchlist_add_and_remove_restores_the_list()
    {
        Live.SkipUnlessMutating();
        using var tv = await Client();
        var before = await tv.Watchlist.GetAsync(Ct);
        string[] test = ["NASDAQ:MSFT", "NASDAQ:NVDA"];
        if (before.Symbols.Any(s => test.Contains(s.Symbol)))
        {
            Assert.Skip("The watchlist already contains the test symbols.");
        }

        try
        {
            var added = await tv.Watchlist.AddAsync(test[0], Ct);
            Assert.Equal("NASDAQ:MSFT", added.AddedAs);
            await tv.Watchlist.AddRangeAsync([test[1]], Ct);
            var during = await tv.Watchlist.GetAsync(Ct);
            Assert.All(test, t => Assert.Contains(during.Symbols, s => s.Symbol == t));
        }
        finally
        {
            var removed = await tv.Watchlist.RemoveAsync(test);
            Assert.Equal(2, removed.Removed.Count);
        }

        var after = await tv.Watchlist.GetAsync(Ct);
        Assert.Equal(before.Symbols.Select(s => s.Symbol), after.Symbols.Select(s => s.Symbol));
    }

    [Fact]
    public async Task Pane_layout_round_trip_is_restored()
    {
        Live.SkipUnlessMutating();
        using var tv = await Client();
        var original = await tv.Panes.ListAsync(Ct);
        if (original.Layout != "s")
        {
            Assert.Skip("The test restores a single-chart layout only.");
        }

        try
        {
            var two = await tv.Panes.SetLayoutAsync(PaneLayout.TwoHorizontal, Ct);
            Assert.Equal(2, two.ChartCount);
            Assert.Equal(2, two.VisiblePanes.Count);

            var focus = await tv.Panes.FocusAsync(1, Ct);
            Assert.Equal(1, focus.FocusedIndex);
        }
        finally
        {
            await tv.Panes.FocusAsync(0, Ct);
            await tv.Panes.SetLayoutAsync(PaneLayout.Single, Ct);
        }

        var restored = await tv.Panes.ListAsync(Ct);
        Assert.Equal("s", restored.Layout);
        Assert.Equal(1, restored.ChartCount);
        Assert.Equal(original.VisiblePanes[0].Symbol, restored.VisiblePanes[0].Symbol);
    }

    [Fact]
    public async Task Invalid_pane_layout_is_rejected_by_the_client()
    {
        using var tv = await Client();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => tv.Panes.SetLayoutAsync((PaneLayout)99, Ct));
    }
}
