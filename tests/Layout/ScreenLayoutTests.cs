using Schipper.Io.Xterm.Layout;
using Schipper.Io.Xterm.Primitives;
using Schipper.Io.Xterm.Screen;

namespace Schipper.Io.Xterm.Tests.Layout;

[TestClass]
public sealed class ScreenLayoutTests
{
    [TestMethod]
    public void Header_fill_footer_partition_the_height()
    {
        var layout = new ScreenLayout(80, 24);
        Region header = layout.AddTop(1, name: "header");
        Region footer = layout.AddBottom(2, name: "footer");
        Region body = layout.AddFill(name: "body");

        Assert.AreEqual(new Rect(0, 0, 80, 1), header.Bounds);
        Assert.AreEqual(new Rect(0, 22, 80, 2), footer.Bounds);
        Assert.AreEqual(new Rect(0, 1, 80, 21), body.Bounds);
    }

    [TestMethod]
    public void Fill_absorbs_remaining_space_and_reports_content_size()
    {
        var layout = new ScreenLayout(40, 10);
        layout.AddTop(1);
        Region body = layout.AddFill();
        Assert.AreEqual(9, body.ContentHeight);
        Assert.AreEqual(40, body.ContentWidth);
        Assert.AreEqual(body.ContentWidth, body.Surface.Width);
        Assert.AreEqual(body.ContentHeight, body.Surface.Height);
    }

    [TestMethod]
    public void Border_shrinks_content_by_one_per_bordered_side()
    {
        var layout = new ScreenLayout(20, 10);
        Region body = layout.AddFill(BorderStyle.Single, BorderSides.All);
        Assert.AreEqual(new Rect(0, 0, 20, 10), body.Bounds);
        Assert.AreEqual(new Rect(1, 1, 18, 8), body.Content);
    }

    [TestMethod]
    public void Top_border_only_shrinks_content_height_by_one()
    {
        var layout = new ScreenLayout(20, 10);
        Region footer = layout.AddBottom(3, BorderStyle.Single, BorderSides.Top);
        Assert.AreEqual(3, footer.Bounds.Height);
        Assert.AreEqual(2, footer.ContentHeight); // one row consumed by the top border
        Assert.AreEqual(20, footer.ContentWidth); // no left/right border
    }

    [TestMethod]
    public void RequestHeight_grows_the_region_and_returns_granted_size()
    {
        var layout = new ScreenLayout(40, 10);
        Region footer = layout.AddBottom(1);
        Region body = layout.AddFill();
        Assert.AreEqual(1, footer.ContentHeight);

        int granted = footer.RequestHeight(3);
        Assert.AreEqual(3, granted);
        Assert.AreEqual(new Rect(0, 7, 40, 3), footer.Bounds);
        Assert.AreEqual(7, body.ContentHeight); // body gave up the rows
    }

    [TestMethod]
    public void RequestHeight_is_clamped_to_available_space()
    {
        var layout = new ScreenLayout(40, 5);
        Region footer = layout.AddBottom(1);
        int granted = footer.RequestHeight(99);
        Assert.AreEqual(5, granted); // cannot exceed the screen
    }

    [TestMethod]
    public void Resize_reflows_all_regions()
    {
        var layout = new ScreenLayout(80, 24);
        layout.AddTop(1);
        Region body = layout.AddFill();
        Assert.AreEqual(23, body.ContentHeight);

        layout.Resize(100, 40);
        Assert.AreEqual(39, body.ContentHeight);
        Assert.AreEqual(100, body.ContentWidth);
    }

    [TestMethod]
    public void Fullscreen_is_a_single_fill_region()
    {
        var layout = ScreenLayout.Fullscreen(80, 24);
        Assert.AreEqual(1, layout.Regions.Count);
        Region content = layout["content"];
        Assert.AreEqual(new Rect(0, 0, 80, 24), content.Bounds);
    }

    [TestMethod]
    public void Compose_blits_region_surfaces_and_borders()
    {
        var layout = new ScreenLayout(10, 4);
        Region header = layout.AddTop(1, name: "h");
        Region body = layout.AddFill(BorderStyle.Single, BorderSides.All, name: "b");

        header.Surface.DrawText(0, 0, "HEAD", Color.Default, Color.Default);
        body.Surface.DrawText(0, 0, "x", Color.Default, Color.Default);

        var target = new ScreenBuffer(10, 4);
        layout.Compose(target);

        Assert.AreEqual('H', target[0, 0].Glyph);          // header surface
        Assert.AreEqual('┌', target[0, 1].Glyph);          // body border top-left (row 1)
        Assert.AreEqual('x', target[1, 2].Glyph);          // body content offset by border
    }

    [TestMethod]
    public void Modal_push_pop_and_active()
    {
        var layout = new ScreenLayout(20, 10);
        layout.AddFill();
        Assert.IsNull(layout.ActiveModal);

        Region dialog = layout.PushModal(8, 4, name: "dlg");
        Assert.AreSame(dialog, layout.ActiveModal);
        Assert.AreEqual(new Rect(6, 3, 8, 4), dialog.Bounds); // centered

        Assert.IsTrue(layout.PopModal());
        Assert.IsNull(layout.ActiveModal);
        Assert.IsFalse(layout.PopModal());
    }

    [TestMethod]
    public void Compose_dims_backdrop_under_a_modal()
    {
        var layout = new ScreenLayout(20, 10);
        Region body = layout.AddFill();
        body.Surface.Fill(new Cell('.', Color.Default, Color.Default));
        layout.PushModal(6, 4, BorderStyle.Single);

        var target = new ScreenBuffer(20, 10);
        layout.Compose(target);

        // a backdrop cell (outside the modal) is dimmed
        Assert.IsTrue((target[0, 0].Attributes & CellAttributes.Faint) != 0);
        // the modal border is drawn on top
        Assert.AreEqual('┌', target[7, 3].Glyph);
    }

    [TestMethod]
    public void Unknown_region_name_throws()
    {
        var layout = new ScreenLayout(10, 10);
        layout.AddFill(name: "a");
        Assert.IsNull(layout.Find("missing"));
        Assert.ThrowsExactly<KeyNotFoundException>(() => _ = layout["missing"]);
    }

    [TestMethod]
    public void Empty_content_surface_matches_content_size()
    {
        var layout = new ScreenLayout(20, 10);
        Region body = layout.AddFill();
        Region footer = layout.AddBottom(1, BorderStyle.Single, BorderSides.All);
        Assert.AreEqual(0, footer.ContentHeight);
        Assert.AreEqual(0, footer.Surface.Height);
        Assert.AreEqual(footer.ContentWidth, footer.Surface.Width);
        Assert.IsFalse(footer.HasContent);

        var before = new ScreenBuffer(20, 10);
        before.Fill(new Cell('.', Color.Default, Color.Default));
        layout.Compose(before);

        footer.Surface.Fill(new Cell('#', Color.Default, Color.Default));
        var after = new ScreenBuffer(20, 10);
        after.Fill(new Cell('.', Color.Default, Color.Default));
        layout.Compose(after);

        for (int y = 0; y < 10; y++)
        {
            for (int x = 0; x < 20; x++)
            {
                Assert.AreEqual(before[x, y], after[x, y]);
            }
        }

        Assert.AreEqual(body.ContentWidth, body.Surface.Width);
        Assert.AreEqual(body.ContentHeight, body.Surface.Height);
    }
}
