// S1.5a：MC 移动手感
//
// 照抄上游 wukong-steve 的 SteveMovement 数值（只改数值，不动原版碰撞与落地）：
//   行走 4.317 m/s  = 431.7 cm/s
//   疾跑 5.612 m/s  = 561.2 cm/s
//   潜行 1.3 m/s    = 130   cm/s
//   重力缩放 3200/980（MC 的 32 m/s² 相对 UE 默认 9.8）
//
// 本阶段**只改速度与重力**，不改输入路由，也不接管跳跃/潜行 —— 那属于 S1.5b，
// 因为它会和原版动作（闪避/法术）冲突，必须与"只改数值"分开验证。
//
// 注意（见 模组开发雷区.md）：本文件不得出现 const 字段。
using System;
using System.Collections.Generic;
using b1;
using UnrealEngine.Engine;
using UnrealEngine.InputCore;
using UnrealEngine.Runtime;

namespace DqjCombat
{
    internal sealed class McMovement : IDisposable
    {
        private static readonly float WalkSpeed = 431.7f;
        private static readonly float SprintSpeed = 561.2f;
        private static readonly float SneakSpeed = 130f;
        private static readonly float GravityScale = 3200f / 980f;
        // MC 跳跃初速：0.42 格/tick × 20 tick/s = 8.4 m/s = 840 cm/s
        private static readonly float JumpVelocityCm = 840f;

        private static readonly Dictionary<string, FKey> keyCache = new Dictionary<string, FKey>();

        private readonly BGUPlayerCharacterCS player;
        private readonly APlayerController controller;
        private readonly McCameraRig cameraRig;
        private float originalMaxWalkSpeed;
        private float originalGravityScale;
        private bool active;
        private bool logged;

        // 本帧的移动输入（由输入路由在消费到移动动作时写入，Update 消费后清零）
        private float moveForward;
        private float moveRight;
        private bool sprintHeld;
        private bool jumpHeld;
        // 驱动后实测到的速度，用来确认 CMC 真的在动（验证接管是否生效）
        private float lastSpeed;

        internal McMovement(BGUPlayerCharacterCS player, APlayerController controller, McCameraRig cameraRig)
        {
            this.player = player;
            this.controller = controller;
            this.cameraRig = cameraRig;
        }

        internal bool Active { get { return active; } }

        internal bool Enable()
        {
            if (active) return true;
            try
            {
                UCharacterMovementComponent movement = player.CharacterMovement;
                if (movement == null) { Log.Write("move-error", "拿不到 CharacterMovement"); return false; }
                originalMaxWalkSpeed = movement.MaxWalkSpeed;
                originalGravityScale = movement.GravityScale;
                movement.GravityScale = GravityScale;
                active = true;
                Log.Write("move", "MC 移动已启用 原速度=" + originalMaxWalkSpeed.ToString("0.0")
                    + " 原重力=" + originalGravityScale.ToString("0.00")
                    + " -> 重力=" + GravityScale.ToString("0.00"));
                return true;
            }
            catch (Exception e)
            {
                Log.Write("move-error", "启用失败 " + e.GetType().Name + ": " + e.Message);
                return false;
            }
        }

        internal void Update(bool gameplay)
        {
            if (!active) return;
            if (player.IsDestroyed || player.IsPendingKill) return;
            if (!gameplay)
            {
                moveForward = moveRight = 0f;
                jumpHeld = Down("SpaceBar");
                return;
            }
            try
            {
                UCharacterMovementComponent movement = player.CharacterMovement;
                if (movement == null) return;

                bool sprintKey = Down("LeftControl");
                bool sneakKey = Down("LeftShift");
                bool sprint = sprintKey;
                float target = sneakKey ? SneakSpeed : (sprint ? SprintSpeed : WalkSpeed);
                if (movement.MaxWalkSpeed != target) movement.MaxWalkSpeed = target;
                movement.GravityScale = GravityScale;
                if (cameraRig != null) cameraRig.SetCrouching(sneakKey);
                bool jump = Down("SpaceBar");
                if (jump && !jumpHeld) TryJump();
                jumpHeld = jump;

                // 用我们自己的输入 + 镜头偏航驱动 CMC（视角已冻结，方向以镜头为准）
                float f = moveForward;
                float r = moveRight;
                moveForward = 0f;
                moveRight = 0f;
                float keyboardF = (Down("W") ? 1f : 0f) - (Down("S") ? 1f : 0f);
                float keyboardR = (Down("D") ? 1f : 0f) - (Down("A") ? 1f : 0f);
                if (keyboardF != 0 || keyboardR != 0) { f = keyboardF; r = keyboardR; }
                float length = (float)Math.Sqrt(f * f + r * r);
                if (length > 1f) { f /= length; r /= length; }
                if (f != 0f || r != 0f)
                {
                    float yaw = (cameraRig != null) ? cameraRig.Yaw : controller.GetControlRotation().Yaw;
                    double yr = yaw * Math.PI / 180.0;
                    FVector fwd = new FVector((float)Math.Cos(yr), (float)Math.Sin(yr), 0f);
                    FVector right = new FVector(-(float)Math.Sin(yr), (float)Math.Cos(yr), 0f);
                    FVector dir = fwd * f + right * r;
                    try { player.AddMovementInput(dir, 1f); } catch { }
                }

                if (!logged)
                {
                    logged = true;
                    Log.Write("move", "首次速度判定 目标=" + target.ToString("0.0")
                        + " (sprintKey=" + sprintKey + " sneakKey=" + sneakKey + " cameraYawAvailable=" + (cameraRig != null) + ")");
                }
                else if (tickLog-- <= 0)
                {
                    tickLog = 120;
                    float v = 0f;
                    try { v = player.GetVelocity().Size(); } catch { }
                    lastSpeed = v;
                    Log.Write("move", "速度自检 vel=" + v.ToString("0.0") + " maxWalk=" + movement.MaxWalkSpeed.ToString("0.0")
                        + " input(f=" + f.ToString("0.00") + ",r=" + r.ToString("0.00") + ")");
                }
            }
            catch (Exception e)
            {
                Log.Write("move-error", e.GetType().Name + ": " + e.Message);
            }
        }

        private int tickLog = 120;

        /// <summary>MC 跳跃：只在贴地时起跳，初速 8.4 m/s，保留原版碰撞与落地事件。</summary>
        internal void TryJump()
        {
            if (!active) return;
            try
            {
                if (player.IsDestroyed || player.IsPendingKill) return;
                UCharacterMovementComponent movement = player.CharacterMovement;
                if (movement == null) return;
                EMovementMode mode = movement.MovementMode;
                if (mode != EMovementMode.MOVE_Walking && mode != EMovementMode.MOVE_NavWalking) return;
                player.LaunchCharacter(new FVector(0f, 0f, JumpVelocityCm), false, true);
            }
            catch (Exception e)
            {
                Log.Write("move-error", "跳跃失败 " + e.GetType().Name + ": " + e.Message);
            }
        }

        /// <summary>由输入路由写入移动轴值。动作可能是 1D（只用 X）或 2D（X/Y 都有效），自动取主导分量。</summary>
        internal void SetMoveInput(string action, float x, float y)
        {
            if (action == "IA_B1MoveForward")
                moveForward = (Math.Abs(y) >= Math.Abs(x)) ? y : x;
            else if (action == "IA_B1MoveRight")
                moveRight = (Math.Abs(x) >= Math.Abs(y)) ? x : y;
        }

        internal void SetSprint(bool on) { sprintHeld = on; }

        private bool Down(string name)
        {
            try
            {
                FKey key;
                if (!keyCache.TryGetValue(name, out key))
                {
                    key = UGSE_UMGFuncLib.GetKeyWithName(new FName(name));
                    keyCache[name] = key;
                }
                return controller.IsInputKeyDown(key);
            }
            catch { return false; }
        }

        public void Dispose()
        {
            if (!active) return;
            active = false;
            try
            {
                if (player.IsDestroyed || player.IsPendingKill) return;
                UCharacterMovementComponent movement = player.CharacterMovement;
                if (movement == null) return;
                movement.MaxWalkSpeed = originalMaxWalkSpeed;
                movement.GravityScale = originalGravityScale;
                Log.Write("move", "已还原原版移动参数");
            }
            catch { }
        }
    }
}
