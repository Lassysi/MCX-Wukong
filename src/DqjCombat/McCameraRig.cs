// S1.1：MC 相机装置
//
// 做法照抄上游 wukong-steve 的 SteveMovement.ToggleCamera / UpdateCamera（已验证的写法）：
//   在原生 Pawn 旁边生成一个空 Actor，挂一个 UCameraComponent，用
//   controller.SetViewTargetWithBlend 接管视点；每帧把相机放到角色眼点。
//
// 注意（见 模组开发雷区.md）：本文件不得出现 const 字段。
using System;
using System.Collections.Generic;
using System.Reflection;
using b1;
using BtlShare;
using UnrealEngine.Engine;
using UnrealEngine.Runtime;

namespace DqjCombat
{
    internal sealed class McCameraRig : IDisposable
    {
        internal enum ViewMode
        {
            FirstPerson = 0,
            ThirdBack = 1,
            ThirdFront = 2
        }

        // 眼高固定采用 MC 标准（脚底往上量）：
        //   站立 1.62 格 = 162cm，潜行 1.27 格 = 127cm
        // 将来接上 Steve 模型时，这套数值正好与模型比例一致，不需要再调。
        // （原生角色的眼位更高，但本项目最终要以 MC 视角为准，故不采用。）

        // Java 版 MC 眼高：站立 1.62 格、潜行 1.27 格（1 格 = 100cm）
        private static readonly float EyeHeightStanding = 162f;
        private static readonly float EyeHeightCrouch = 127f;
        // 以 16:9 的 103 度水平 FOV 为基准，再按实际视口宽高比换算
        private static readonly float BaseHorizontalFov = 103f;
        private static readonly float BaseAspect = 16f / 9f;
        // 第三人称距离、防穿墙探针半径、以及贴着墙时的最小距离
        private static readonly float ThirdPersonDistanceCm = 220f;
        private static readonly float ThirdPersonProbeRadiusCm = 14f;
        private static readonly float MinThirdPersonDistanceCm = 60f;
        // 每多少帧检查一次视点是否被游戏夺回
        private static readonly int ViewTargetCheckInterval = 5;

        private readonly BGUPlayerCharacterCS player;
        private readonly APlayerController controller;

        private AActor cameraActor;
        private UCameraComponent camera;
        private ViewMode mode = ViewMode.FirstPerson;
        private float capsuleHalfHeight = 88f;
        private bool meshHidden;
        private bool ownsViewTarget;
        private string lastError;
        private float lastDistanceCm;
        private int viewTargetChecks;

        // 独立持有偏航/俯仰；CameraModifier 另由 NativeCameraEffects 管理。
        private float camYaw;
        private float camPitch;
        // 视角灵敏度（单位：每 1.0 轴值变化对应的度数）。
        // 冷启动日志证实原始幅值可达 35~61，20 倍会持续触发 60 度限幅。
        // 使用 0.15 度/轴单位作为初始标定，实机仍需检查鼠标方向与手感。
        private static readonly float LookSensitivity = 0.15f;
        private int seenLook;
        private bool crouching;
        private bool suspended;

        internal bool Active { get { return cameraActor != null && !cameraActor.IsDestroyed; } }
        internal ViewMode Mode { get { return mode; } }
        internal string LastError { get { return lastError; } }
        internal FVector Position { get { return cameraActor.GetActorLocation(); } }
        internal FRotator Rotation { get { return cameraActor.GetActorRotation(); } }
        internal void SetCrouching(bool value) { crouching = value; }

        internal void SetGameplay(bool gameplay)
        {
            if (!Active || suspended == !gameplay) return;
            suspended = !gameplay;
            if (suspended)
            {
                ownsViewTarget = false;
                controller.SetViewTargetWithBlend(player, 0);
                ApplyFirstPersonVisibility(false);
            }
            else
            {
                controller.SetViewTargetWithBlend(cameraActor, 0);
                ownsViewTarget = true;
            }
        }

        internal McCameraRig(BGUPlayerCharacterCS player, APlayerController controller)
        {
            this.player = player;
            this.controller = controller;
        }

        // ---------------------------------------------------------------- 启用 / 还原

        internal bool Enable()
        {
            if (Active) return true;
            lastError = null;
            try
            {
                cameraActor = BGUFunctionLibraryCS.BGUSpawnActor(
                    player.World, UClass.GetClass<AActor>(), player.GetActorLocation(), default(FRotator));
                if (cameraActor == null)
                {
                    lastError = "相机载体 Actor 创建失败";
                    return false;
                }

                camera = UObject.NewObject<UCameraComponent>(cameraActor);
                cameraActor.RootComponent = camera;
                camera.FieldOfView = BaseHorizontalFov;
                camera.ConstrainAspectRatio = false;
                camera.UsePawnControlRotation = false;

                UGSE_EngineFuncLib.RegisterComponent(camera);
                ReadNativeMetrics();
                // 从当前 ControlRotation 接管偏航/俯仰，保证切换时镜头不跳变
                FRotator init = controller.GetControlRotation();
                camYaw = init.Yaw;
                camPitch = init.Pitch;
                Log.Write("camera", EyeInspect());
                Update();
                controller.SetViewTargetWithBlend(cameraActor, 0);
                ownsViewTarget = true;
                Log.Write("camera", "已接管视点，模式=" + mode + " | " + EyeInspect());
                return true;
            }
            catch (Exception e)
            {
                lastError = e.GetType().Name + ": " + e.Message;
                Log.Write("camera-error", "启用失败 " + lastError);
                Restore();
                return false;
            }
        }

        internal void Cycle()
        {
            if (!Active)
            {
                Enable();
                return;
            }
            mode = (ViewMode)(((int)mode + 1) % 3);
            Update();
            Log.Write("camera", "视角切换 -> " + mode + " | " + Inspect());
            Log.Echo("camera mode = " + mode);
        }

        // ---------------------------------------------------------------- 每帧更新

        internal void Update()
        {
            if (!Active || suspended || camera == null || camera.IsDestroyed) return;
            if (player.IsDestroyed || player.IsPendingKill) return;

            try
            {
                int width, height;
                controller.GetViewportSize(out width, out height);
                if (width > 0 && height > 0)
                {
                    // 按实际宽高比换算，避免超宽屏把第一人称手持物切掉
                    double aspect = width / (double)height;
                    double rad = BaseHorizontalFov * Math.PI / 180.0;
                    double halfTan = Math.Tan(rad / 2.0) / BaseAspect;
                    camera.FieldOfView = (float)(2.0 * Math.Atan(halfTan * aspect) * 180.0 / Math.PI);
                }

                FRotator rotation = new FRotator(camPitch, camYaw, 0f);
                FVector eye = EyePosition();
                FVector forward = rotation.RotateVector(new FVector(1f, 0f, 0f));
                FVector position;
                FRotator cameraRotation = rotation;

                if (mode == ViewMode.FirstPerson)
                {
                    position = eye;
                    // 第一人称：身体朝向跟随镜头偏航，移动方向才和视角一致
                    player.SetActorRotation(new FRotator(0f, rotation.Yaw, 0f), false);
                }
                else
                {
                    // 后视：镜头在眼点后方；前视：镜头在眼点前方
                    FVector offset = mode == ViewMode.ThirdBack
                        ? forward * -ThirdPersonDistanceCm
                        : forward * ThirdPersonDistanceCm;
                    position = ClipToLineOfSight(eye, eye + offset);

                    // 最小距离：避免防穿墙把镜头推到角色身体内部
                    FVector delta = position - eye;
                    float distance = delta.Size();
                    if (distance < MinThirdPersonDistanceCm)
                    {
                        FVector direction = distance > 0.5f
                            ? delta * (1f / distance)
                            : forward * (mode == ViewMode.ThirdBack ? -1f : 1f);
                        position = eye + direction * MinThirdPersonDistanceCm;
                    }

                    // 前视必须回头看角色（MC 的 F5 前视就是看角色正脸）：
                    // 偏航翻转 180°，俯仰取镜像，否则会变成"背对角色往前看"
                    if (mode == ViewMode.ThirdFront)
                        cameraRotation = new FRotator(-rotation.Pitch, rotation.Yaw + 180f, rotation.Roll);
                }

                cameraActor.SetActorLocationAndRotation(position, cameraRotation, false, out _, false);
                lastDistanceCm = (position - eye).Size();

                // 把我们持有的偏航/俯仰写回 ControlRotation，
                // 让黑猴的原版移动（未接管时）仍与我们的视角方向一致。
                try { controller.SetControlRotation(rotation); } catch { }

                // 游戏在开菜单 / 过场 / 切形态后会自己夺回视点（实测：ESC 进菜单再回来就变回黑猴镜头）。
                // 这里定期检查并重新夺回，保证 MC 视角不会被悄悄换掉。
                if (ownsViewTarget && ++viewTargetChecks >= ViewTargetCheckInterval)
                {
                    viewTargetChecks = 0;
                    try
                    {
                        AActor current = controller.GetViewTarget();
                        if (current != cameraActor)
                        {
                            controller.SetViewTargetWithBlend(cameraActor, 0);
                            Log.Write("camera", "游戏夺回视点，已重新接管（当前目标="
                                + (current == null ? "null" : current.GetClass().GetName()) + "）");
                        }
                    }
                    catch { }
                }

                // 第一人称必须隐藏角色自己的模型：镜头高度按 MC 标准（1.62 格）定，
                // 而原生角色远高于此，不隐藏就会看到模型内部的衣料。
                // （接入 Steve 模型后同样需要，否则会看到 Steve 自己的后脑勺。）
                ApplyFirstPersonVisibility(mode == ViewMode.FirstPerson);
            }
            catch (Exception e)
            {
                Log.Write("camera-error", "更新失败 " + e.GetType().Name + ": " + e.Message);
            }
        }

        internal void ReadNativeMetrics()
        {
            try { capsuleHalfHeight = player.CapsuleComponent.GetScaledCapsuleHalfHeight(); } catch { capsuleHalfHeight = 88f; }
        }

        /// <summary>视角输入入口：由输入路由在消费到鼠标视角动作时调用。</summary>
        internal void AddLook(string action, float x, float y)
        {
            float k = LookSensitivity;
            // 单帧钳制，避免轴值异常时镜头瞬间转飞
            const float maxStep = 60f;
            if (action == "IA_B1TurnRight")
            {
                // 一维轴可能把值放在 X 或 Y，取主导分量
                float d = Clamp(Dominant(x, y) * k, -maxStep, maxStep);
                // 本机复测确认原来的减法使左右反转。
                camYaw += d;
            }
            else if (action == "IA_B1LookUp")
            {
                float d = Clamp(Dominant(y, x) * k, -maxStep, maxStep);
                // 鼠标上滑 → 抬头（pitch 增大）
                camPitch += d;
            }
            if (camPitch > 89f) camPitch = 89f;
            if (camPitch < -89f) camPitch = -89f;

            if (seenLook < 32)
            {
                seenLook++;
                Log.Write("camera", "视角输入 " + action
                    + " x=" + x.ToString("0.0000") + " y=" + y.ToString("0.0000")
                    + " -> yaw=" + camYaw.ToString("0.0") + " pitch=" + camPitch.ToString("0.0"));
            }
        }

        private static float Dominant(float a, float b)
        {
            return Math.Abs(a) >= Math.Abs(b) ? a : b;
        }

        private static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }

        internal float Yaw { get { return camYaw; } }
        internal float Pitch { get { return camPitch; } }

        /// <summary>眼点相对角色原点（胶囊中心）的 Z 偏移。</summary>
        private float EyeOffsetFromActor()
        {
            if (capsuleHalfHeight <= 1f) ReadNativeMetrics();
            return (crouching ? EyeHeightCrouch : EyeHeightStanding) - capsuleHalfHeight;
        }

        private FVector EyePosition()
        {
            return player.GetActorLocation() + new FVector(0f, 0f, EyeOffsetFromActor());
        }

        internal string EyeInspect()
        {
            FVector eye = EyePosition();
            return "眼高=MC标准 " + EyeHeightStanding.ToString("0") + "cm(脚底起)"
                + " capsuleHalf=" + capsuleHalfHeight.ToString("0.0")
                + " offsetFromActor=" + EyeOffsetFromActor().ToString("0.0")
                + " eyeZ=" + eye.Z.ToString("0.0")
                + " actorZ=" + player.GetActorLocation().Z.ToString("0.0");
        }

        /// <summary>
        /// 第一人称下把角色自己的所有网格设为"拥有者不可见"（SetOwnerNoSee）。
        /// 只影响本玩家看到的画面，不影响怪物/联机别人看到的模型。
        /// </summary>
        private void ApplyFirstPersonVisibility(bool firstPerson)
        {
            if (firstPerson == meshHidden) return;
            try
            {
                int count = 0;
                foreach (USkeletalMeshComponent mesh in UObject.GetObjects<USkeletalMeshComponent>())
                {
                    if (mesh == null || mesh.IsDestroyed || mesh.IsPendingKill) continue;
                    AActor owner = null;
                    try { owner = mesh.GetOwner(); } catch { }
                    if (owner != player) continue;
                    mesh.SetOwnerNoSee(firstPerson);
                    count++;
                }
                meshHidden = firstPerson;
                Log.Write("camera", "第一人称隐藏自身模型 -> ownerNoSee=" + firstPerson + " 影响网格数=" + count);
            }
            catch (Exception e)
            {
                Log.Write("camera-error", "切换模型可见性失败 " + e.GetType().Name + ": " + e.Message);
            }
        }

        /// <summary>从眼点向期望位置做球形扫掠，命中就把相机放到命中点之前，避免穿墙。</summary>
        private FVector ClipToLineOfSight(FVector eye, FVector desired)
        {
            try
            {
                FVector delta = desired - eye;
                float length = delta.Size();
                if (length <= 1f) return desired;

                FHitResult hit;
                bool blocked = USystemLibrary.SphereTraceSingle(
                    player, eye, desired, ThirdPersonProbeRadiusCm,
                    ETraceTypeQuery.TraceTypeQuery1, false,
                    new List<AActor> { player }, EDrawDebugTrace.None,
                    out hit, true, default(FLinearColor), default(FLinearColor), 0f);

                if (blocked)
                {
                    // ImpactPoint 是 FVector_NetQuantize，先转成普通 FVector 再算
                    FVector impact = new FVector(hit.ImpactPoint.X, hit.ImpactPoint.Y, hit.ImpactPoint.Z);
                    FVector forward = delta * (1f / length);
                    return impact - forward * (ThirdPersonProbeRadiusCm + 2f);
                }
            }
            catch (Exception e)
            {
                Log.Write("camera-error", "防穿墙探针失败 " + e.GetType().Name);
            }
            return desired;
        }

        // ---------------------------------------------------------------- 还原

        internal void Restore()
        {
            // 先把模型可见性还原，再交还视点
            ApplyFirstPersonVisibility(false);
            try
            {
                if (ownsViewTarget && controller != null && !controller.IsDestroyed && !controller.IsPendingKill)
                {
                    AActor target = controller.GetControlledPawn();
                    if (target != null && !target.IsDestroyed && !target.IsPendingKill)
                        controller.SetViewTargetWithBlend(target, 0);
                }
            }
            catch { }

            try
            {
                if (cameraActor != null && !cameraActor.IsDestroyed && !cameraActor.IsPendingKill)
                    cameraActor.DestroyActor();
            }
            catch { }

            cameraActor = null;
            camera = null;
            ownsViewTarget = false;
        }

        public void Dispose()
        {
            Restore();
            Log.Write("camera", "已还原原版视点");
        }

        internal string Inspect()
        {
            return "active=" + Active + " mode=" + mode
                + " dist=" + lastDistanceCm.ToString("0") + "cm"
                + " fov=" + (camera == null ? 0f : camera.FieldOfView)
                + (lastError == null ? "" : " error=" + lastError);
        }
    }
}
