using System.Text.Json;
using TradingView.Client.Tests.Support;

namespace TradingView.Client.Tests;

public class Stage4ModelTests
{
    private static T Fixture<T>(string name) where T : TvResult => (T)Fixtures.Deserialize(name);

    [Fact]
    public void Drawing_shape_results_carry_the_entity_id()
    {
        var horizontal = Fixture<DrawShapeResult>("drawings/shape-horizontal");
        var text = Fixture<DrawShapeResult>("drawings/shape-text");

        Assert.Equal("horizontal_line", horizontal.Shape);
        Assert.False(string.IsNullOrEmpty(horizontal.EntityId));
        Assert.Equal("text", text.Shape);
    }

    [Fact]
    public void Drawing_list_get_remove_clear_map()
    {
        var list = Fixture<DrawingListResult>("drawings/list");
        var empty = Fixture<DrawingListResult>("drawings/list-empty");
        var details = Fixture<DrawingDetailsResult>("drawings/get");
        var removed = Fixture<DrawingRemoveResult>("drawings/remove");
        var cleared = Fixture<DrawingClearResult>("drawings/clear");

        Assert.Equal(5, list.Count);
        Assert.Equal(["horizontal_line", "trend_line", "rectangle", "text", "vertical_line"], list.Shapes.Select(s => s.Name));
        Assert.Empty(empty.Shapes);
        Assert.Equal("horizontal_line", details.Name);
        Assert.Equal(7866, Assert.Single(details.Points!).Price);
        Assert.Equal(2, details.Properties!["linewidth"].GetInt32());
        Assert.True(details.Properties["visible"].GetBoolean());
        Assert.True(details.Selectable);
        Assert.True(removed.Removed);
        Assert.Equal(4, removed.RemainingShapes);
        Assert.Equal("all_shapes_removed", cleared.Action);
    }

    [Fact]
    public void Alert_fixtures_map()
    {
        var list = Fixture<AlertListResult>("alerts/list");
        var created = Fixture<AlertCreateResult>("alerts/create");
        var deleted = Fixture<AlertDeleteResult>("alerts/delete");

        Assert.Equal(list.AlertCount, list.Alerts.Count);
        var first = list.Alerts[0];
        Assert.Equal("price", first.Type);
        Assert.False(first.Active);
        Assert.Equal("moving_up_percents", first.Condition!.Value.GetProperty("type").GetString());
        Assert.Equal(new DateTimeOffset(2025, 7, 9, 7, 8, 56, TimeSpan.Zero), first.CreatedAt);
        Assert.Null(first.LastFiredAt);
        Assert.Equal("greater", created.Condition);
        Assert.NotNull(created.AlertId);
        Assert.Equal([created.AlertId!.Value], deleted.AlertIds);
        Assert.Equal(1, deleted.DeletedCount);
    }

    [Fact]
    public void Alert_dates_tolerate_numbers_and_garbage()
    {
        var alert = JsonSerializer.Deserialize(
            """{"success":true,"alert_count":1,"alerts":[{"alert_id":1,"created":1700000000,"last_fired":"not a date","expiration":"2026-11-09T20:34:16Z"}]}""",
            TvJsonContext.Default.AlertListResult)!.Alerts[0];

        Assert.Equal("1700000000", alert.Created);
        Assert.Null(alert.LastFiredAt);
        Assert.Equal(new DateTimeOffset(2026, 11, 9, 20, 34, 16, TimeSpan.Zero), alert.ExpiresAt);
    }

    [Fact]
    public void Alert_failure_shapes_from_the_contract_parse()
    {
        var failed = JsonSerializer.Deserialize("""{"success":false,"source":"internal_api","error":"bad","response":"{}"}""", TvJsonContext.Default.AlertCreateResult)!;

        Assert.False(failed.Success);
        Assert.Equal("{}", failed.Response);
        Assert.Null(failed.AlertId);
    }

    [Fact]
    public void Watchlist_fixtures_map_display_strings()
    {
        var get = Fixture<WatchlistResult>("watchlist/get");
        var add = Fixture<WatchlistAddResult>("watchlist/add");
        var bulk = Fixture<WatchlistAddBulkResult>("watchlist/add-bulk");
        var remove = Fixture<WatchlistRemoveResult>("watchlist/remove");

        Assert.Equal(get.Count, get.Symbols.Count);
        Assert.Equal("dom_rows", get.Source);
        var spx = get.Symbols[0];
        Assert.Equal("TVC:SPX", spx.Symbol);
        Assert.Equal(7811.54, spx.LastValue);
        Assert.Equal(0.59, spx.ChangePercentValue);
        Assert.Null(spx.Volume);
        Assert.Equal("added", add.Action);
        Assert.Equal("NASDAQ:MSFT", add.AddedAs);
        Assert.Equal((2, 0), (bulk.Added, bulk.Failed));
        Assert.All(bulk.Results, r => Assert.True(r.Success));
        Assert.Equal(["NASDAQ:MSFT", "NASDAQ:NVDA", "NASDAQ:AMD"], remove.Removed);
        Assert.Equal(["NASDAQ:ZZZNOTINLIST"], remove.Skipped);
        Assert.True(remove.Verified);
    }

    [Fact]
    public void Layout_fixtures_map_ids_and_dates_as_text()
    {
        var list = Fixture<LayoutListResult>("layouts/list");
        var switched = Fixture<LayoutSwitchResult>("layouts/switch");

        Assert.Equal(list.LayoutCount, list.Layouts.Count);
        Assert.Equal("1001", list.Layouts[0].Id);
        Assert.Equal("2026-10-09", list.Layouts[0].Modified);
        Assert.Equal("switched", switched.Action);
        Assert.True(switched.UnsavedDialogDismissed);
        Assert.Equal("1001", switched.LayoutId);
    }

    [Fact]
    public void Pane_fixtures_expose_visible_panes_only()
    {
        var single = Fixture<PaneListResult>("panes/list");
        var twoH = Fixture<PaneListResult>("panes/list-2h");
        var shrunk = Fixture<PaneLayoutResult>("panes/set-layout-single");

        Assert.Equal("s", single.Layout);
        Assert.Equal(0, single.ActiveIndex);
        Assert.Equal(2, twoH.ChartCount);
        Assert.Equal(4, twoH.Panes.Count);   // TradingView keeps reporting stale panes
        Assert.Equal(2, twoH.VisiblePanes.Count);
        Assert.Equal(1, shrunk.ChartCount);
        Assert.Single(shrunk.VisiblePanes);
        Assert.Equal("1 chart", shrunk.LayoutName);
    }

    [Fact]
    public void Pane_set_symbol_and_focus_map()
    {
        var symbol = Fixture<PaneSymbolResult>("panes/set-symbol");
        var focus = Fixture<PaneFocusResult>("panes/focus");

        Assert.Equal((1, "NASDAQ:AAPL"), (symbol.Index, symbol.Symbol));
        Assert.Equal(1, focus.FocusedIndex);
        Assert.True(focus.TotalPanes >= 2); // counts panes TradingView still holds, including stale ones
    }

    [Fact]
    public void Tab_fixtures_map()
    {
        var list = Fixture<TabListResult>("tabs/list");
        var two = Fixture<TabListResult>("tabs/list-two");
        var created = Fixture<TabNewResult>("tabs/new");
        var closed = Fixture<TabCloseResult>("tabs/close");
        var switched = Fixture<TabSwitchResult>("tabs/switch");

        Assert.True(Assert.Single(list.Tabs).IsChart);
        Assert.Equal(2, two.TabCount);
        Assert.Contains(two.Tabs, t => !t.IsChart && t.ChartId is null);
        Assert.Equal("new_tab_opened", created.Action);
        Assert.Contains(created.Tabs, t => t.Title == "New tab");
        Assert.True(closed.Success);
        Assert.True(closed.TabsAfter < closed.TabsBefore);
        Assert.True(switched.VisuallySwitched);
    }

    [Fact]
    public void Screenshot_fixture_maps()
    {
        var shot = Fixture<ScreenshotResult>("screenshot/chart");

        Assert.Equal("chart", shot.Region);
        Assert.EndsWith("tvclient_fixture.png", shot.FilePath);
        Assert.True(shot.SizeBytes > 0);
        Assert.False(shot.WaitedForRender);
    }

    [Fact]
    public void Ui_click_hover_key_scroll_type_mouse_map()
    {
        var click = Fixture<UiClickResult>("ui/click");
        var hover = Fixture<UiHoverResult>("ui/hover");
        var key = Fixture<UiKeyResult>("ui/keyboard-modifiers");
        var scroll = Fixture<UiScrollResult>("ui/scroll");
        var type = Fixture<UiTypeResult>("ui/type");
        var mouse = Fixture<UiMouseResult>("ui/mouse");

        Assert.True(click.Clicked.Found);
        Assert.Equal("open-indicators-dialog", click.Clicked.DataName);
        Assert.Equal("aria-label", hover.Hovered.By);
        Assert.True(hover.Hovered.X > 0);
        Assert.Equal(["shift", "alt"], key.Modifiers);
        Assert.Equal(("right", 50d), (scroll.Direction, scroll.Amount));
        Assert.Equal(("relative", 8), (type.Typed, type.Length));
        Assert.Equal((700d, 700d, "left", false), (mouse.X, mouse.Y, mouse.Button, mouse.DoubleClick));
    }

    [Fact]
    public void Ui_find_panel_and_fullscreen_map()
    {
        var find = Fixture<UiFindResult>("ui/find");
        var panel = Fixture<UiPanelResult>("ui/panel");
        var fullscreen = Fixture<UiFullscreenResult>("ui/fullscreen");

        Assert.Equal("text", find.Strategy);
        Assert.Equal(find.Count, find.Elements.Count == 20 ? find.Count : find.Elements.Count);
        Assert.All(find.Elements, e => Assert.True(e.Width >= 0));
        Assert.Equal("watchlist", panel.Panel);
        Assert.Contains(panel.Performed, new[] { "opened", "closed" });
        Assert.Equal("fullscreen_toggled", fullscreen.Action);
    }

    [Fact]
    public void Ui_eval_results_keep_json_shape()
    {
        var number = Fixture<UiEvalResult>("ui/eval-number");
        var obj = Fixture<UiEvalResult>("ui/eval-object");
        var undefined = Fixture<UiEvalResult>("ui/eval-undefined");
        var text = Fixture<UiEvalResult>("ui/eval-string");

        Assert.Equal(2, number.Result!.Value.GetInt32());
        Assert.Equal(JsonValueKind.Object, obj.Result!.Value.ValueKind);
        Assert.Equal(JsonValueKind.Null, obj.Result.Value.GetProperty("c").ValueKind);
        Assert.Null(undefined.Result);
        Assert.False(string.IsNullOrEmpty(text.Result!.Value.GetString()));
    }
}
