// S1.4：Steve 身体接入 + 天命人隐藏
//
// 照抄上游 wukong-steve 的 SteveMesh：
//   - 从 steve.mesh（STEVEM01 格式，12 个部件）读取几何
//   - 每个部件一个 UProceduralMeshComponent，挂到原生 Pawn 的根组件上
//   - 皮肤颜色是烘在顶点色里的（UV 全指向 SolidMaskUv，靠顶点色显色）
//   - HideNativeComponents() 隐藏原生角色的所有图元组件 —— 这就是"天命人隐藏"
//   - 第一人称只保留右臂部件，其余隐藏（MC 第一人称只看得到手）
//
// 注意（见 模组开发雷区.md）：本文件不得出现 const 字段。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using b1;
using UnrealEngine.Engine;
using UnrealEngine.Plugins.ProceduralMeshComponent;
using UnrealEngine.Runtime;

namespace DqjCombat
{
    // ---------------------------------------------------------------- 网格数据

    internal sealed class SteveMeshData
    {
        internal float[][] Positions = new float[0][];
        internal float[][] Normals = new float[0][];
        internal float[][] Uv = new float[0][];
        internal float[][] Colors = new float[0][];
        internal int[] Triangles = new int[0];
    }

    internal sealed class SteveMeshPart
    {
        internal string Name = "";
        internal float[] Pivot = new float[0];
        internal SteveMeshData Mesh = new SteveMeshData();
    }

    internal sealed class SteveMeshBundle
    {
        internal SteveMeshPart[] Parts = new SteveMeshPart[0];

        internal static SteveMeshBundle Read(string path, int expectedParts)
        {
            using (FileStream stream = File.OpenRead(path))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != "STEVEM01" || reader.ReadInt32() != expectedParts)
                    throw new InvalidDataException("steve.mesh 格式不符");

                var bundle = new SteveMeshBundle { Parts = new SteveMeshPart[expectedParts] };
                for (int i = 0; i < expectedParts; i++)
                {
                    int size = reader.ReadInt32();
                    if (size < 1 || size > 64) throw new InvalidDataException("部件名长度非法");
                    var part = new SteveMeshPart
                    {
                        Name = Encoding.ASCII.GetString(reader.ReadBytes(size)),
                        Pivot = Floats(reader, 3)
                    };
                    int vertices = reader.ReadInt32(), indices = reader.ReadInt32();
                    if (vertices < 0 || vertices > 100000 || indices < 0 || indices > 600000 || indices % 3 != 0
                        || (vertices == 0) != (indices == 0) || (vertices > 0 && (vertices < 3 || indices < 3)))
                        throw new InvalidDataException("网格数量非法");

                    SteveMeshData m = part.Mesh;
                    m.Positions = new float[vertices][];
                    m.Normals = new float[vertices][];
                    m.Uv = new float[vertices][];
                    m.Colors = new float[vertices][];
                    for (int v = 0; v < vertices; v++)
                    {
                        m.Positions[v] = Floats(reader, 3);
                        m.Normals[v] = Floats(reader, 3);
                        m.Uv[v] = Floats(reader, 2);
                        m.Colors[v] = Floats(reader, 4);
                    }
                    m.Triangles = new int[indices];
                    for (int t = 0; t < indices; t++)
                    {
                        int index = reader.ReadInt32();
                        if (index < 0 || index >= vertices) throw new InvalidDataException("网格索引越界");
                        m.Triangles[t] = index;
                    }
                    bundle.Parts[i] = part;
                }
                if (stream.Position != stream.Length) throw new InvalidDataException("steve.mesh 尾部多余数据");
                return bundle;
            }
        }

        private static float[] Floats(BinaryReader reader, int count)
        {
            var values = new float[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = reader.ReadSingle();
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i])) throw new InvalidDataException("网格含非有限值");
            }
            return values;
        }
    }

    // ---------------------------------------------------------------- 身体

    internal sealed class SteveBody : IDisposable
    {
        // 1 格 = 100cm；MC 像素 = 1/16 格；模型整体乘 0.9375（上游取值）
        private static readonly double Scale = 100.0 * .9375 / 16;
        private static readonly int ExpectedParts = 12;
        // 引擎自带材质靠这个 texel 做遮罩显色
        private static readonly FVector2D SolidMaskUv = new FVector2D(433.5 / 512, 139.5 / 256);

        private readonly BGUPlayerCharacterCS player;
        private readonly List<PartEntry> parts = new List<PartEntry>();
        private readonly List<HiddenEntry> hiddenComponents = new List<HiddenEntry>();
        private readonly double footOffset;
        private double age, stride;
        private bool active;
        private bool gameplay = true;
        private string lastError;

        private sealed class PartEntry
        {
            internal UProceduralMeshComponent Component;
            internal SteveMeshPart Part;
            internal bool Hidden;
        }

        private sealed class HiddenEntry
        {
            internal USceneComponent Component;
            internal bool WasHidden;
        }

        internal bool Active { get { return active; } }
        internal string LastError { get { return lastError; } }
        internal int PartCount { get { return parts.Count; } }

        internal SteveBody(BGUPlayerCharacterCS player, UMaterialInterface material, string meshPath)
        {
            this.player = player;
            footOffset = -player.CapsuleComponent.GetScaledCapsuleHalfHeight();

            SteveMeshBundle bundle = SteveMeshBundle.Read(meshPath, ExpectedParts);
            try
            {
                foreach (SteveMeshPart part in bundle.Parts)
                {
                    if (part.Mesh.Positions.Length == 0) continue;

                    UProceduralMeshComponent component = UObject.NewObject<UProceduralMeshComponent>(player);
                    if (component == null) throw new InvalidOperationException("创建网格组件失败");

                    var entry = new PartEntry { Component = component, Part = part };
                    parts.Add(entry);

                    component.AttachToComponent(player.GetRootComponent(), default(FName),
                        EAttachmentRule.KeepRelative, EAttachmentRule.KeepRelative, EAttachmentRule.KeepRelative, false);
                    component.SetCollisionEnabled(ECollisionEnabled.NoCollision);
                    component.SetCastShadow(true);

                    SteveMeshData mesh = part.Mesh;
                    component.CreateMeshSection_LinearColor(0,
                        mesh.Positions.Select(p => Convert(p, Scale)).ToList(),
                        mesh.Triangles.ToList(),
                        mesh.Normals.Select(p => Convert(p, 1)).ToList(),
                        mesh.Uv.Select(p => SolidMaskUv).ToList(),
                        new List<FVector2D>(), new List<FVector2D>(), new List<FVector2D>(),
                        mesh.Colors.Select(p => new FLinearColor(Linear(p[0]), Linear(p[1]), Linear(p[2]), p[3])).ToList(),
                        new List<FProcMeshTangent>(), false);
                    component.SetMaterial(0, material);
                    UGSE_EngineFuncLib.RegisterComponent(component);
                    Pose(entry, 0, 0, 0, 0);
                }

                HideNativeComponents();
                active = true;
                Log.Write("body", "Steve 身体已接入，部件数=" + parts.Count);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private static FVector Convert(float[] p, double scale)
        {
            return new FVector((float)(-p[2] * scale), (float)(-p[0] * scale), (float)(-p[1] * scale));
        }

        // sRGB -> 线性，顶点色必须线性化，否则整体偏亮
        private static float Linear(float c)
        {
            return c <= .04045f ? c / 12.92f : (float)Math.Pow((c + .055) / 1.055, 2.4);
        }

        // ------------------------------------------------------------ 天命人隐藏

        /// <summary>隐藏原生角色的所有图元组件（不含胶囊、不含我们自己加的网格）。</summary>
        internal void HideNativeComponents()
        {
            try
            {
                USceneComponent root = player.GetRootComponent();
                if (root == null || root.IsDestroyed || root.IsPendingKill) return;

                List<USceneComponent> children;
                root.GetChildrenComponents(true, out children);
                if (children == null) return;

                foreach (USceneComponent child in children)
                {
                    var component = child as UPrimitiveComponent;
                    if (component == null) continue;
                    if (component.IsDestroyed || component.IsPendingKill) continue;
                    if (component is UCapsuleComponent) continue;
                    if (parts.Any(p => (USceneComponent)p.Component == component)) continue;
                    if (hiddenComponents.Any(h => h.Component == component)) continue;

                    hiddenComponents.Add(new HiddenEntry { Component = component, WasHidden = component.HiddenInGame });
                    component.SetHiddenInGame(true);
                }
            }
            catch (Exception e)
            {
                Log.Write("body-error", "隐藏原生组件失败 " + e.GetType().Name + ": " + e.Message);
            }
        }

        // ------------------------------------------------------------ 每帧

        internal void SetGameplay(bool value)
        {
            if (gameplay == value) return;
            gameplay = value;
            foreach (HiddenEntry entry in hiddenComponents)
            {
                if (entry.Component != null && !entry.Component.IsDestroyed && !entry.Component.IsPendingKill)
                    entry.Component.SetHiddenInGame(value || entry.WasHidden);
            }
            if (!value)
            {
                foreach (PartEntry entry in parts)
                {
                    if (entry.Component.IsDestroyed || entry.Component.IsPendingKill) continue;
                    entry.Component.SetHiddenInGame(true);
                    entry.Hidden = true;
                }
            }
        }

        internal void Update(double dt, bool firstPerson, McCameraRig cameraRig, double swing)
        {
            if (!active) return;
            if (player.IsDestroyed || player.IsPendingKill) return;

            try
            {
                age += dt * 20;

                FVector velocity = player.GetVelocity();
                double speed = Math.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y) / 100;
                double amplitude = Math.Min(1, speed / 4.317);
                stride += speed * dt * 4;

                double headYaw = 0, headPitch = 0;
                AController controller = player.GetController();
                if (controller != null)
                {
                    FRotator look = controller.GetControlRotation();
                    headYaw = Math.Max(-75, Math.Min(75, (look.Yaw - player.GetActorRotation().Yaw + 540) % 360 - 180)) * Math.PI / 180;
                    headPitch = -Math.Max(-60, Math.Min(60, (look.Pitch + 540) % 360 - 180)) * Math.PI / 180;
                }

                foreach (PartEntry entry in parts)
                {
                    string name = entry.Part.Name;
                    bool arm = name.EndsWith("arm") || name.EndsWith("sleeve");
                    // 第一人称只保留右臂（MC 第一人称只看得到持物的手）
                    bool hide = firstPerson && (!arm || !name.StartsWith("right"));

                    if (entry.Hidden != hide)
                    {
                        entry.Component.SetHiddenInGame(hide);
                        entry.Hidden = hide;
                    }
                    if (hide) continue;

                    double x = 0, y = 0, z = 0;
                    if (name == "head" || name == "hat") { y = headYaw; x = headPitch; }

                    bool right = name.StartsWith("right_");
                    if (name.EndsWith("leg") || name.EndsWith("pants"))
                        x = Math.Cos(stride * .6662 + (right ? 0 : Math.PI)) * 1.4 * amplitude;
                    if (name.EndsWith("arm") || name.EndsWith("sleeve"))
                    {
                        x = Math.Cos(stride * .6662 + (right ? Math.PI : 0)) * amplitude;
                        if (right) x = x * .5 - Math.PI / 10;
                        x += (right ? 1 : -1) * Math.Sin(age * .067) * .05;
                        z = (right ? 1 : -1) * (Math.Cos(age * .09) * .05 + .05);
                    }

                    Pose(entry, x, y, z, 0);
                    if (firstPerson && cameraRig != null && cameraRig.Active)
                    {
                        // MC 视图手臂：位于镜头右下方，跟随完整俯仰，不再留在身体侧面。
                        double lift = Math.Sin(swing * Math.PI);
                        FRotator view = cameraRig.Rotation;
                        FVector offset = new FVector(12 - 18 * lift, 32, -24 + 10 * lift);
                        FRotator armRotation = view + new FRotator(65 + 45 * lift, -8, 0);
                        entry.Component.SetWorldLocationAndRotation(cameraRig.Position + view.RotateVector(offset),
                            armRotation, false, out _, false);
                    }
                }
            }
            catch (Exception e)
            {
                if (lastError == null)
                {
                    lastError = e.GetType().Name + ": " + e.Message;
                    Log.Write("body-error", "更新失败 " + lastError);
                }
            }
        }

        private void Pose(PartEntry entry, double x, double y, double z, double unused)
        {
            FVector location = Convert(entry.Part.Pivot, Scale);
            location.Z += (float)(24 * Scale + footOffset);
            var rotation = new FRotator(
                (float)(-x * 180 / Math.PI), (float)(y * 180 / Math.PI), (float)(-z * 180 / Math.PI));
            entry.Component.SetRelativeLocationAndRotation(location, rotation, false, out _, true);
        }

        // ------------------------------------------------------------ 卸载

        public void Dispose()
        {
            active = false;
            foreach (HiddenEntry entry in hiddenComponents)
            {
                try
                {
                    if (entry.Component != null && !entry.Component.IsDestroyed && !entry.Component.IsPendingKill)
                        entry.Component.SetHiddenInGame(entry.WasHidden);
                }
                catch { }
            }
            hiddenComponents.Clear();

            foreach (PartEntry entry in parts)
            {
                try
                {
                    if (entry.Component != null && !entry.Component.IsDestroyed && !entry.Component.IsPendingKill
                        && !entry.Component.IsBeingDestroyed())
                        entry.Component.DestroyComponent(player);
                }
                catch { }
            }
            parts.Clear();
            Log.Write("body", "Steve 身体已移除，原生角色可见性已恢复");
        }

        internal string Inspect()
        {
            return "active=" + active + " parts=" + parts.Count + " hiddenNative=" + hiddenComponents.Count
                + (lastError == null ? "" : " error=" + lastError);
        }
    }
}
