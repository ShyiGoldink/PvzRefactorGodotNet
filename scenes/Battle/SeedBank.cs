using System.Collections.Generic;
using Godot;

/// <summary>
/// 种子包：屏幕上方那一排卡片。
///
/// 它是**表现层 + 输入**：卡片长什么样、点到哪张，都在这儿；
/// 但"阳光够不够、这张卡在不在冷却里"全是问 <see cref="GameManager"/>——
/// 冷却和花费是玩法，不是界面的事。
///
/// 卡片上的小图直接从植物的图集里抠第一帧，不用另外画图标。
/// </summary>
public partial class SeedBank : Control
{
    private const float CardWidth = 92f;
    private const float CardHeight = 124f;
    private const float CardGap = 8f;
    private const float MarginLeft = 28f;
    private const float MarginTop = 18f;

    /// <summary>现在选中的是哪张卡（植物编号）；0 表示没选。</summary>
    public int SelectedPlantId { get; private set; }

    /// <summary>换了选的卡。表现层拿它去换鼠标上的预览。</summary>
    [Signal]
    public delegate void SelectionChangedEventHandler();

    private sealed class Icon
    {
        public Texture2D Atlas;
        public Rect2 Source;
    }

    private readonly Dictionary<int, Icon> _icons = new Dictionary<int, Icon>();

    /// <summary>换一张卡。点同一张就取消。</summary>
    public void Select(int plantId)
    {
        SelectedPlantId = SelectedPlantId == plantId ? 0 : plantId;
        QueueRedraw();
        EmitSignal(SignalName.SelectionChanged);
    }

    /// <summary>清掉选择（种下去之后调）。</summary>
    public void ClearSelection()
    {
        if (SelectedPlantId == 0)
        {
            return;
        }

        SelectedPlantId = 0;
        QueueRedraw();
        EmitSignal(SignalName.SelectionChanged);
    }

    public override void _Process(double delta)
    {
        // 阳光在涨、冷却在走，所以每帧重画
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mouse
            || !mouse.Pressed
            || mouse.ButtonIndex != MouseButton.Left)
        {
            return;
        }

        int index = IndexAt(mouse.Position);
        if (index < 0)
        {
            return;
        }

        Select(GameManager.Seeds[index]);
    }

    public override void _Draw()
    {
        if (!GameManager.InBattle)
        {
            return;
        }

        IReadOnlyList<int> seeds = GameManager.Seeds;

        DrawSunCounter();

        for (int i = 0; i < seeds.Count; i++)
        {
            DrawCard(i, seeds[i]);
        }
    }

    /// <summary>左上角那个阳光数字。</summary>
    private void DrawSunCounter()
    {
        Font font = GetThemeDefaultFont();
        int size = GetThemeDefaultFontSize();

        var box = new Rect2(MarginLeft, MarginTop + CardHeight + 12f, 120f, 36f);
        DrawRect(box, new Color(0.12f, 0.12f, 0.12f, 0.85f));
        DrawRect(box, new Color(1f, 0.85f, 0.15f), false, 2f);

        DrawString(font, box.Position + new Vector2(12f, 25f), $"阳光 {GameManager.Sun}",
            HorizontalAlignment.Left, -1f, size, new Color(1f, 0.95f, 0.7f));
    }

    private void DrawCard(int index, int plantId)
    {
        PlantData data = PlantLibrary.Get(plantId);
        if (data == null)
        {
            return;
        }

        Rect2 rect = CardRect(index);
        bool ready = GameManager.IsSeedReady(data);
        bool selected = SelectedPlantId == plantId;

        // 底：能点就是亮的，不能点（阳光不够或冷却中）压暗
        DrawRect(rect, ready ? new Color(0.18f, 0.2f, 0.16f, 0.95f) : new Color(0.1f, 0.1f, 0.1f, 0.9f));

        Icon icon = GetIcon(plantId, data);
        if (icon != null)
        {
            // 卡片上面的方格里放植物的小图
            var iconBox = new Rect2(rect.Position + new Vector2(8f, 8f), new Vector2(rect.Size.X - 16f, rect.Size.Y - 40f));
            Vector2 iconSize = icon.Source.Size;
            float fit = Mathf.Min(iconBox.Size.X / iconSize.X, iconBox.Size.Y / iconSize.Y);
            Vector2 drawSize = iconSize * fit;
            var target = new Rect2(iconBox.Position + (iconBox.Size - drawSize) * 0.5f, drawSize);

            DrawTextureRectRegion(icon.Atlas, target, icon.Source, ready ? Colors.White : new Color(0.45f, 0.45f, 0.45f));
        }

        // 花费
        Font font = GetThemeDefaultFont();
        int size = GetThemeDefaultFontSize();
        DrawString(font, rect.Position + new Vector2(8f, rect.Size.Y - 10f), data.Cost.ToString(),
            HorizontalAlignment.Left, -1f, size, ready ? new Color(1f, 0.95f, 0.7f) : new Color(0.7f, 0.4f, 0.4f));

        // 冷却：从下往上盖一层黑
        float cooldown = GameManager.CooldownLeft(plantId);
        if (cooldown > 0f && data.Cooldown > 0f)
        {
            float ratio = Mathf.Clamp(cooldown / data.Cooldown, 0f, 1f);
            var cover = new Rect2(rect.Position, new Vector2(rect.Size.X, rect.Size.Y * ratio));
            DrawRect(cover, new Color(0f, 0f, 0f, 0.6f));
        }

        // 边框：选中的画粗一点、亮一点
        DrawRect(rect, selected ? new Color(1f, 0.9f, 0.3f) : new Color(0.35f, 0.35f, 0.35f), false, selected ? 4f : 2f);
    }

    private static Rect2 CardRect(int index)
    {
        return new Rect2(
            MarginLeft + index * (CardWidth + CardGap),
            MarginTop,
            CardWidth,
            CardHeight);
    }

    private int IndexAt(Vector2 point)
    {
        for (int i = 0; i < GameManager.Seeds.Count; i++)
        {
            if (CardRect(i).HasPoint(point))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 卡片上的小图：临时装一株出来，问它图集和当前该播的那一行，抠第一帧。
    /// 装一次就够了，之后缓存着——这东西不会变。
    /// </summary>
    private Icon GetIcon(int plantId, PlantData data)
    {
        if (_icons.TryGetValue(plantId, out Icon cached))
        {
            return cached;
        }

        Plant dummy = EntityAssembler.BuildPlant(data);
        AnimationGroupComponent animation = dummy?.GetComponent<AnimationGroupComponent>();
        if (animation == null || !animation.CanDraw)
        {
            _icons[plantId] = null;
            return null;
        }

        var icon = new Icon
        {
            Atlas = GD.Load<Texture2D>(animation.Atlas),
            Source = new Rect2(0f, animation.CurrentRow * animation.Cell.Y, animation.Cell.X, animation.Cell.Y),
        };

        _icons[plantId] = icon;
        return icon;
    }
}
