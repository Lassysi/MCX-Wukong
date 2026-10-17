// S1.2：MC 风格 HUD（准星 + 红心 + 九格快捷栏）
//
// 用**真 MC 贴图**（来自上游 SteveMinecraftUI_P.pak，路径 /Game/Mods/SteveTransformation/UI/T_*），
// 按 MC 原生布局绘制：GUI 缩放 + 182x22 快捷栏居中贴底 + 其上一排红心 + 正中准星。
// 构建方式照抄上游 MinecraftHud（CreateUserWidgetWithClass → WidgetTree → UCanvasPanel → UImage）。
//
// 注意（见 模组开发雷区.md）：本文件不得出现 const 字段。
using System;
using System.Collections.Generic;
using System.Reflection;
using b1;
using BtlShare;
using UnrealEngine.Engine;
using UnrealEngine.Runtime;
using UnrealEngine.SlateCore;
using UnrealEngine.UMG;

namespace DqjCombat
{
    internal sealed class McHud : IDisposable
    {
        private static readonly int HotbarSlots = 9;
        private static readonly int HeartSlots = 10;
        private static readonly float HeartPoint = 2f;      // 1 颗心 = 2 点生命
        private static readonly string TextureRoot = "/Game/Mods/SteveTransformation/UI/";
        // 关掉即不画数值行（MC 原版没有这行，仅供核对 HP 映射时打开）
        private static readonly bool ShowDebugText = false;

        private readonly BGUPlayerCharacterCS player;
        private readonly APlayerController controller;

        private UUserWidget widget;
        private UCanvasPanel canvas;

        private UImage crosshair;
        private UImage hotbar;
        private UImage selectedFrame;
        private readonly List<UImage> hearts = new List<UImage>();
        private UTextBlock debugText;

        private readonly Dictionary<string, UTexture2D> textures = new Dictionary<string, UTexture2D>();
        private int selected;
        private bool active;
        private bool visible;
        private readonly bool[] slotKeys = new bool[9];
        private bool wheelUpHeld, wheelDownHeld;
        private static readonly string[] DigitKeys = { "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine" };
        private string lastError;

        internal bool Active { get { return active; } }
        internal string LastError { get { return lastError; } }

        internal McHud(BGUPlayerCharacterCS player, APlayerController controller)
        {
            this.player = player;
            this.controller = controller;
        }

        internal void SetSelected(int slot)
        {
            if (slot < 0) slot = 0;
            if (slot >= HotbarSlots) slot = HotbarSlots - 1;
            selected = slot;
        }

        internal int Selected { get { return selected; } }

        // ---------------------------------------------------------------- 构建

        internal bool Enable()
        {
            if (active) return true;
            lastError = null;
            try
            {
                widget = UGSE_UMGFuncLib.CreateUserWidgetWithClass(player, UClass.GetClass<UUserWidget>());
                if (widget == null) { lastError = "UUserWidget 创建失败"; return false; }
                widget.AddToRoot();

                object treeProperty = ObjectProperty(widget, "WidgetTree");
                UObject tree = GetObject(treeProperty, widget);
                if (tree == null)
                {
                    tree = UObject.NewObject<UObject>(widget, UObject.LoadClass<UObject>(null, "/Script/UMG.WidgetTree"));
                    SetObject(treeProperty, widget, tree);
                }

                canvas = UObject.NewObject<UCanvasPanel>(tree);
                object rootProperty = ObjectProperty(tree, "RootWidget");
                SetObject(rootProperty, tree, canvas);

                widget.SetVisibility(ESlateVisibility.HitTestInvisible);
                widget.AddToViewport(100);

                hotbar = MakeImage(10);
                selectedFrame = MakeImage(20);
                crosshair = MakeImage(100);
                for (int i = 0; i < HeartSlots; i++) hearts.Add(MakeImage(30 + i));
                if (ShowDebugText) debugText = MakeLabel(200);

                active = true;
                visible = true;
                Log.Write("hud", "MC HUD 已挂载（真 MC 贴图）");
                return true;
            }
            catch (Exception e)
            {
                lastError = e.GetType().Name + ": " + e.Message;
                Log.Write("hud-error", "挂载失败 " + lastError);
                Dispose();
                return false;
            }
        }

        private static object ObjectProperty(UObject owner, string name)
        {
            IntPtr address = owner.GetClass().FindPropertyByName(new FName(name));
            if (address == IntPtr.Zero) throw new InvalidOperationException("UMG 属性不可用: " + name);
            return new FObjectPropertyBase(address);
        }

        private static UObject GetObject(object property, UObject owner)
        {
            return ((FObjectPropertyBase)property).GetObjectPropertyValue_InContainer(owner.Address, 0);
        }

        private static void SetObject(object property, UObject owner, UObject value)
        {
            ((FObjectPropertyBase)property).SetObjectPropertyValue_InContainer(owner.Address, value, 0);
        }

        private UImage MakeImage(int zOrder)
        {
            UImage image = UObject.NewObject<UImage>(canvas);
            canvas.AddChildToCanvas(image);
            image.SetVisibility(ESlateVisibility.HitTestInvisible);
            var slot = (UCanvasPanelSlot)image.Slot;
            slot.SetAutoSize(false);
            slot.SetZOrder(zOrder);
            return image;
        }

        private UTextBlock MakeLabel(int zOrder)
        {
            UTextBlock label = UObject.NewObject<UTextBlock>(canvas);
            canvas.AddChildToCanvas(label);
            label.SetColorAndOpacity(new FSlateColor { SpecifiedColor = new FLinearColor(1f, 1f, 1f, 1f) });
            label.SetShadowOffset(new FVector2D(1f, 1f));
            label.SetShadowColorAndOpacity(new FLinearColor(0f, 0f, 0f, 1f));
            label.SetVisibility(ESlateVisibility.HitTestInvisible);
            var slot = (UCanvasPanelSlot)label.Slot;
            slot.SetAutoSize(true);
            slot.SetZOrder(zOrder);
            return label;
        }

        private UTexture2D Texture(string name)
        {
            UTexture2D cached;
            if (textures.TryGetValue(name, out cached)) return cached;
            UTexture2D texture = null;
            try
            {
                texture = UObject.LoadObject<UTexture2D>(null, TextureRoot + "T_" + name + ".T_" + name);
                if (texture != null) texture.AddToRoot();
            }
            catch { }
            textures[name] = texture;
            if (texture == null) Log.Write("hud-texture", "缺失 " + name);
            return texture;
        }

        private void DrawImage(UImage image, string textureName, float x, float y, float w, float h, FLinearColor tint)
        {
            if (image == null) return;
            UTexture2D texture = Texture(textureName);
            if (texture == null) { image.SetVisibility(ESlateVisibility.Collapsed); return; }
            if (image.GetVisibility() != ESlateVisibility.HitTestInvisible)
                image.SetVisibility(ESlateVisibility.HitTestInvisible);
            if (image.Brush.ResourceObject != texture)
            {
                image.SetBrushFromTexture(texture, false);
                image.InvalidateLayoutAndVolatility();
            }
            if (image.ColorAndOpacity != tint) image.SetColorAndOpacity(tint);
            var slot = (UCanvasPanelSlot)image.Slot;
            slot.SetPosition(new FVector2D(x, y));
            slot.SetSize(new FVector2D(w, h));
        }

        // ---------------------------------------------------------------- 显隐

        /// <summary>只有真正进入游戏（可操作角色）时才显示，加载界面/菜单一律隐藏。</summary>
        internal void SetVisible(bool value)
        {
            if (visible == value) return;
            visible = value;
            try
            {
                if (widget != null && !widget.IsDestroyed)
                    widget.SetVisibility(value ? ESlateVisibility.HitTestInvisible : ESlateVisibility.Collapsed);
            }
            catch { }
        }

        // ---------------------------------------------------------------- 每帧

        internal void Update()
        {
            if (!active || widget == null || widget.IsDestroyed) return;
            if (player.IsDestroyed || player.IsPendingKill) return;
            if (!visible) return;

            try
            {
                int previousSlot = selected;
                for (int i = 0; i < DigitKeys.Length; i++)
                {
                    bool down = McKeys.Down(controller, DigitKeys[i]);
                    if (down && !slotKeys[i]) SetSelected(i);
                    slotKeys[i] = down;
                }
                bool up = McKeys.Down(controller, "MouseScrollUp");
                bool downWheel = McKeys.Down(controller, "MouseScrollDown");
                float wheel = controller.GetInputAnalogKeyState(McKeys.Key("MouseWheelAxis"));
                if (wheel > 0 || (up && !wheelUpHeld)) SetSelected((selected + 8) % 9);
                else if (wheel < 0 || (downWheel && !wheelDownHeld)) SetSelected((selected + 1) % 9);
                wheelUpHeld = up; wheelDownHeld = downWheel;
                if (selected != previousSlot) Log.Write("hotbar", "空槽选择 " + (selected + 1));
                int width, height;
                controller.GetViewportSize(out width, out height);
                if (width <= 0 || height <= 0) return;

                // MC 自动 GUI 缩放：以 320x240 为基准，1~4 档
                float gui = Math.Min(width / 320f, height / 240f);
                if (gui < 1f) gui = 1f;
                if (gui > 4f) gui = 4f;

                float hotbarW = 182f * gui, hotbarH = 22f * gui;
                float hotbarX = width * 0.5f - hotbarW * 0.5f;
                float hotbarY = height - hotbarH;

                DrawImage(hotbar, "hotbar", hotbarX, hotbarY, hotbarW, hotbarH, White);

                // 选中框：MC 是 24x24，比格子大一圈
                float selSize = 24f * gui;
                DrawImage(selectedFrame, "selected",
                    hotbarX + 1f * gui + selected * 20f * gui - 1f * gui,
                    hotbarY - 1f * gui, selSize, selSize, White);

                // 红心：10 颗，每颗 9x9，间隔 8，位于快捷栏上方
                float hp = 0f, hpMax = 1f;
                try
                {
                    hp = BGUFunctionLibraryCS.BGUGetFloatAttr(player, EBGUAttrFloat.Hp);
                    hpMax = BGUFunctionLibraryCS.BGUGetFloatAttr(player, EBGUAttrFloat.HpMax);
                }
                catch { }
                if (hpMax <= 0f) hpMax = 1f;
                float display = 20f * hp / hpMax;
                if (display < 0f) display = 0f;
                if (display > 20f) display = 20f;

                for (int i = 0; i < HeartSlots; i++)
                {
                    float cell = display - i * HeartPoint;
                    bool full = cell >= HeartPoint;
                    float x = hotbarX + 1f * gui + i * 8f * gui;
                    float y = hotbarY - 10f * gui;
                    DrawImage(hearts[i], "heart_empty", x, y, 9f * gui, 9f * gui,
                        full ? HeartFull : HeartEmpty);
                }

                // 准星
                float cross = 15f * gui;
                DrawImage(crosshair, "crosshair",
                    width * 0.5f - cross * 0.5f, height * 0.5f - cross * 0.5f, cross, cross, White);

                if (ShowDebugText && debugText != null)
                {
                    string value = "HP " + hp.ToString("0") + "/" + hpMax.ToString("0");
                    if (debugText.GetText() != value) debugText.SetText(FText.FromString(value));
                    ((UCanvasPanelSlot)debugText.Slot).SetPosition(new FVector2D(hotbarX, hotbarY - 30f * gui));
                }
            }
            catch (Exception e)
            {
                if (lastError == null)
                {
                    lastError = e.GetType().Name + ": " + e.Message;
                    Log.Write("hud-error", "刷新失败 " + lastError);
                }
            }
        }

        private static FLinearColor White { get { return new FLinearColor(1f, 1f, 1f, 1f); } }
        // 满心用亮红（贴图是空心底，用色相染色得到实心观感）
        private static FLinearColor HeartFull { get { return new FLinearColor(1f, 0.12f, 0.12f, 1f); } }
        private static FLinearColor HeartEmpty { get { return new FLinearColor(0.25f, 0.25f, 0.25f, 0.55f); } }

        // ---------------------------------------------------------------- 卸载

        public void Dispose()
        {
            active = false;
            try
            {
                if (widget != null && !widget.IsDestroyed && !widget.IsPendingKill)
                {
                    widget.RemoveFromParent();
                    widget.RemoveFromRoot();
                }
            }
            catch { }
            widget = null;
            canvas = null;
            crosshair = null;
            hotbar = null;
            selectedFrame = null;
            hearts.Clear();
            debugText = null;
            textures.Clear();
            Log.Write("hud", "MC HUD 已移除");
        }
    }
}
