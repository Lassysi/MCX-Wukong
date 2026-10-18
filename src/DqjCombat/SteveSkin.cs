// Preview73 (user, option B): Steve performs every native Wukong animation.
//
// The 12 steve.mesh parts (vertices relative to each part's MC joint pivot) are attached to the
// matching bones of the hidden Wukong skeleton: head/hat and body/jacket to the neck, arms and
// sleeves to the upper arms, legs and pants to the thighs. Position follows the bone through the
// attachment; rotation is recomputed each frame from bone directions (shoulder->hand, hip->foot,
// pelvis->neck), so the unknown bone axis conventions do not matter. The Wukong mesh stays hidden
// but keeps ticking its pose. Bone names are matched by pattern and logged ("steve-skin").
// Note (MOD-DEVELOPMENT-PITFALLS): no const fields in this file.
using System;
using System.Collections.Generic;
using System.Linq;
using b1;
using UnrealEngine.Engine;
using UnrealEngine.Plugins.ProceduralMeshComponent;
using UnrealEngine.Runtime;

namespace DqjCombat
{
    internal sealed class SteveSkin : IDisposable
    {
        private static readonly double Scale = 100.0 * .9375 / 16;
        private static readonly int ExpectedParts = 12;
        private static readonly FVector2D SolidMaskUv = new FVector2D(433.5 / 512, 139.5 / 256);

        private sealed class Part
        {
            internal UProceduralMeshComponent Component;
            internal string Name;
            internal FName Joint, Tip;
            internal FVector BaseScale;
            internal int Kind; // 0 limb hanging from joint to tip, 1 torso (pelvis->neck up), 2 head (neck->head up)
        }
        private sealed class Hidden { internal UPrimitiveComponent Component; internal bool Body; internal readonly AppearanceVisibilityLease Lease=new AppearanceVisibilityLease(); }

        private readonly AActor owner;
        private readonly USkeletalMeshComponent mesh;
        private FName shoulderL, shoulderR;
        private readonly List<Part> parts = new List<Part>();
        private readonly List<Hidden> hidden = new List<Hidden>();
        private FName pelvis, neck, head;
        private double nextScan;
        private bool loggedError;
        private float bodyScale=1f;
        private bool cinematic,tickLeased,nativeView,catalogueDue=true,loggedCatalogue;
        private EVisibilityBasedAnimTickOption originalAnimTick;
        internal bool Active { get; private set; }

        internal USkeletalMeshComponent Mesh { get { return mesh; } }
        internal AActor Owner { get { return owner; } }
        internal bool ManagesNativeVisibility(UPrimitiveComponent component)
        {return Live&&hidden.Any(h=>h.Component==component);}
        internal bool IsReplacementComponent(UPrimitiveComponent component)
        {return parts.Any(p=>p.Component==component);}
        // Called every frame; only a change of state does any work (Preview99: was a per-frame reveal).
        internal void SetNativeView(bool value)
        {
            if(nativeView==value)return;
            nativeView=value;
            if(Live){if(value){catalogueDue=true;HideNative();}Reveal(value||cinematic);ApplyNativeVisibility();}
        }
        internal bool Live { get { return Active&&NativeObjectLifetime.Live(owner)&&NativeObjectLifetime.Live(mesh); } }
        internal SteveSkin(BGUPlayerCharacterCS player, string meshPath) : this(player, player.Mesh, meshPath, true) { }
        // Preview75: any skeletal mesh on the Wukong skeleton (cutscene stand-ins too). hideOwner hides the
        // owner's other primitives and attached actors (player: staff, gourd); otherwise only `mesh`.
        internal SteveSkin(AActor owner, USkeletalMeshComponent mesh, string meshPath, bool hideOwner) : this(owner,mesh,meshPath,hideOwner,false) { }
        internal SteveSkin(AActor owner, USkeletalMeshComponent mesh, string meshPath, bool hideOwner,bool cinematic)
        {
            this.owner = owner; this.mesh = mesh; this.hideOwner = hideOwner;this.cinematic=cinematic;
            if (!NativeObjectLifetime.Live(owner)
                || !CutsceneSkinScope.IsLiveActor(owner.GetName(), NativeObjectLifetime.ActorWorld(owner) != null))
                throw new InvalidOperationException("skin requires a live world actor, never a class default");
            if (!NativeObjectLifetime.Live(mesh) || mesh.GetOwner() != owner)
                throw new InvalidOperationException("no owned live skeletal mesh");
            var material = UObject.LoadObject<UMaterialInterface>(null, "/Engine/EngineMaterials/DefaultTextMaterialOpaque.DefaultTextMaterialOpaque");
            if (material == null) throw new InvalidOperationException("engine vertex-colour material unavailable");
            material.AddToRoot();

            var bones = new List<string>();
            int count = mesh.GetNumBones();
            for (int i = 0; i < count; i++) bones.Add(mesh.GetBoneName(i).ToString());
            Log.Write("steve-skin", "Wukong bones=" + count + ": " + string.Join(",", bones.Take(160)));
            HumanoidSkinRig rig;
            if(!HumanoidSkinRig.TryResolve(bones,out rig))throw new InvalidOperationException("required humanoid bones not found");
            string sPelvis=rig.Pelvis,sNeck=rig.Neck,sHead=rig.Head;
            string armL=rig.ArmL,armR=rig.ArmR,handL=rig.HandL,handR=rig.HandR;
            string legL=rig.LegL,legR=rig.LegR,footL=rig.FootL,footR=rig.FootR;
            Log.Write("steve-skin", "mapping pelvis=" + sPelvis + " neck=" + sNeck + " head=" + sHead + " armL=" + armL + " handL=" + handL + " armR=" + armR + " handR=" + handR
                + " legL=" + legL + " footL=" + footL + " legR=" + legR + " footR=" + footR);
            if (sPelvis == null || sNeck == null || armL == null || armR == null || handL == null || handR == null || legL == null || legR == null || footL == null || footR == null)
                throw new InvalidOperationException("required bones not found");
            pelvis = new FName(sPelvis); neck = new FName(sNeck); head = new FName(sHead);
            shoulderL = new FName(armL); shoulderR = new FName(armR);
            // Preview102: transformation forms (Erlang phase-2 giant monkey) have a much larger skeleton. Steve's leg is
            // 12 px = 70 cm; only a body clearly larger than the normal Wukong (> 1.6x) scales the parts up with it.
            try{var leg=mesh.GetSocketLocation(new FName(legL))-mesh.GetSocketLocation(new FName(footL));
                double ratio=Math.Sqrt(leg.X*leg.X+leg.Y*leg.Y+leg.Z*leg.Z)/(12*Scale);
                if(ratio>1.6&&ratio<8){bodyScale=(float)ratio;Log.Write("steve-skin","large body: leg ratio="+ratio.ToString("0.00")+"; Steve parts scaled with it");}}
            catch{}

            var bundle = SteveMeshBundle.Read(meshPath, ExpectedParts);
            try
            {
                foreach (var source in bundle.Parts)
                {
                    if (source.Mesh.Positions.Length == 0) continue;
                    string n = source.Name;
                    var part = new Part { Name = n };
                    if (n == "head" || n == "hat") { part.Kind = 2; part.Joint = neck; part.Tip = head; }
                    else if (n == "body" || n == "jacket") { part.Kind = 1; part.Joint = neck; part.Tip = pelvis; }
                    else if (n.StartsWith("left_") && (n.EndsWith("arm") || n.EndsWith("sleeve"))) { part.Joint = new FName(armL); part.Tip = new FName(handL); }
                    else if (n.StartsWith("right_") && (n.EndsWith("arm") || n.EndsWith("sleeve"))) { part.Joint = new FName(armR); part.Tip = new FName(handR); }
                    else if (n.StartsWith("left_")) { part.Joint = new FName(legL); part.Tip = new FName(footL); }
                    else if (n.StartsWith("right_")) { part.Joint = new FName(legR); part.Tip = new FName(footR); }
                    else continue;
                    var component = UObject.NewObject<UProceduralMeshComponent>(owner);
                    if (component == null) throw new InvalidOperationException("procedural mesh creation failed");
                    part.Component = component;
                    var data = source.Mesh;
                    component.CreateMeshSection_LinearColor(0,
                        data.Positions.Select(p => Convert(p, Scale)).ToList(), data.Triangles.ToList(),
                        data.Normals.Select(p => Convert(p, 1)).ToList(), data.Uv.Select(p => SolidMaskUv).ToList(),
                        new List<FVector2D>(), new List<FVector2D>(), new List<FVector2D>(),
                        data.Colors.Select(p => new FLinearColor(Linear(p[0]), Linear(p[1]), Linear(p[2]), p[3])).ToList(),
                        new List<FProcMeshTangent>(), false);
                    component.SetMaterial(0, material);
                    component.SetCollisionEnabled(ECollisionEnabled.NoCollision);
                    component.SetCastShadow(true);
                    UGSE_EngineFuncLib.RegisterComponent(component);
                    component.AttachToComponent(mesh, part.Joint, EAttachmentRule.SnapToTarget, EAttachmentRule.KeepWorld, EAttachmentRule.KeepWorld, false);
                    component.SetRelativeLocation(new FVector(0, 0, 0), false, out _, true);
                    part.BaseScale=component.RelativeScale3D;
                    parts.Add(part);
                }
                originalAnimTick=mesh.VisibilityBasedAnimTickOption;tickLeased=true;
                mesh.VisibilityBasedAnimTickOption = EVisibilityBasedAnimTickOption.AlwaysTickPoseAndRefreshBones;
                ApplyScale();
                HideNative();
                Active = true;
                Update();
                Log.Write("steve-skin", "Steve attached to Wukong bones; parts=" + parts.Count + " hiddenNative=" + hidden.Count);
            }
            catch { Dispose(); throw; }
        }

        internal static bool SupportsMesh(USkeletalMeshComponent component)
        {
            if(!NativeObjectLifetime.Live(component))return false;
            var bones=new List<string>();
            for(int i=0;i<component.GetNumBones();i++)bones.Add(component.GetBoneName(i).ToString());
            HumanoidSkinRig rig;return HumanoidSkinRig.TryResolve(bones,out rig);
        }

        private readonly bool hideOwner;
        private bool CinematicBody(UPrimitiveComponent primitive)
        {
            if(primitive==mesh)return true;
            var actor=primitive.GetOwner();
            string actorClass=actor==null?"":actor.GetClass().GetName();
            string socket=NativeAppearanceApi.Socket(primitive,"GetAttachSocketName");
            if(actor!=null&&actor!=owner)socket+=" "+NativeAppearanceApi.Socket(actor,"GetAttachParentSocketName");
            if(CinematicSkinProfile.IsProp(primitive.GetName(),socket,actorClass))return false;
            if(actor==owner&&(primitive is USkinnedMeshComponent||primitive is UStaticMeshComponent))return true;
            var skeletal=primitive as USkeletalMeshComponent;
            if(skeletal!=null&&skeletal.SkeletalMesh!=null&&mesh.SkeletalMesh!=null
                &&skeletal.SkeletalMesh.Skeleton==mesh.SkeletalMesh.Skeleton)return true;
            return primitive is UStaticMeshComponent&&CinematicSkinProfile.IsArmor(primitive.GetName()+" "+actorClass);
        }
        private void Track(UPrimitiveComponent primitive)
        {
            if(!NativeObjectLifetime.Live(primitive)||primitive is UCapsuleComponent)return;
            if(parts.Any(p=>p.Component==primitive)||hidden.Any(h=>h.Component==primitive))return;
            var actor=primitive.GetOwner();
            if(actor is BGUCharacterCS&&actor!=owner)return; // a grabbed/carried character is never cosmetic ownership
            hidden.Add(new Hidden{Component=primitive,Body=CinematicBody(primitive)});
        }
        private void ApplyNativeVisibility()
        {
            foreach(var h in hidden){
                if(!NativeObjectLifetime.Live(h.Component))continue;
                bool? value=h.Lease.Update(!(cinematic||nativeView)||h.Body,h.Component.HiddenInGame);
                if(value.HasValue)h.Component.SetHiddenInGame(value.Value,false);
            }
        }
        private void HideNative()
        {
            if (!hideOwner)
            {
                Track(mesh);ApplyNativeVisibility();
                return;
            }
            var root = owner.GetRootComponent(); if (root == null || root.IsDestroyed) return;
            // Live97: GetComponentsByClass(TSubclassOf<>) is absent in the installed engine assembly.
            // The generic UObject catalogue is already used successfully by the bridge on this game build.
            // Preview99: the catalogue walks every primitive in the world, so only on attach and state
            // changes (CG / native view); the periodic rescans use the attachment tree below.
            if(catalogueDue){
                catalogueDue=false;int seen=0;var watch=System.Diagnostics.Stopwatch.StartNew();
                foreach(UPrimitiveComponent component in UObject.GetObjects<UPrimitiveComponent>()){
                    seen++;
                    if(NativeObjectLifetime.Live(component)&&component.GetOwner()==owner)Track(component);
                }
                if(!loggedCatalogue){loggedCatalogue=true;Log.Write("steve-skin","component catalogue scanned="+seen+" in "+watch.ElapsedMilliseconds+" ms; owner="+owner.GetName());}
            }
            List<USceneComponent> children; root.GetChildrenComponents(true, out children); if (children == null) children=new List<USceneComponent>();
            children.Add(root);Track(mesh);
            // Live73: the staff is a separate actor attached to the player, not a player component.
            var attached=NativeAppearanceApi.AttachedActors(owner);
            if (attached != null)
                foreach (var item in attached)
                {
                    var actor=item as AActor;
                    if (!NativeObjectLifetime.Live(actor)||NativeObjectLifetime.ActorWorld(actor)!=NativeObjectLifetime.ActorWorld(owner)||actor is BGUCharacterCS) continue;
                    var actorRoot = actor.GetRootComponent(); if (actorRoot == null || actorRoot.IsDestroyed) continue;
                    children.Add(actorRoot);
                    List<USceneComponent> more; actorRoot.GetChildrenComponents(true, out more); if (more != null) children.AddRange(more);
                }
            foreach (var child in children)
            {
                Track(child as UPrimitiveComponent);
            }
            ApplyNativeVisibility();
        }

        private void ApplyScale()
        {
            var profile=new CinematicSkinProfile();
            if(cinematic)try{profile=CinematicSkinProfile.Load(System.IO.Path.Combine(Log.Dir,"cg-skin-scale.txt"));}
                catch(Exception e){Log.Write("cg-skin","scale config ignored: "+e.Message);}
            foreach(var part in parts){var s=part.BaseScale;float width=(cinematic?profile.Width:1f)*bodyScale,height=(cinematic?profile.Height:1f)*bodyScale;
                part.Component.SetRelativeScale3D(new FVector(s.X*width,s.Y*width,s.Z*height));}
            if(cinematic)Log.Write("cg-skin","visual scale width="+profile.Width+" height="+profile.Height+" actor="+owner.GetName());
        }
        internal void SetCinematic(bool value)
        {
            if(!Live||cinematic==value)return;
            cinematic=value;ApplyScale();catalogueDue=true;HideNative();nextScan=0;
            Reveal(value||nativeView);
            Log.Write("cg-skin",value?"CG appearance entered: "+owner.GetName():"CG ended; original MC part scales restored: "+owner.GetName());
        }

        /// Every frame: hide newly attached native parts (weapons, effects) and aim each Steve part.
        /// Grab view (Preview75): show the Steve parts to the owner's own camera.
        internal void Reveal(bool visible)
        {
            foreach (var part in parts) { try { part.Component.SetHiddenInGame(!visible, false); part.Component.SetOwnerNoSee(false); } catch { } }
        }
        internal void Update()
        {
            if (!Live) return;
            try
            {
                double now = Environment.TickCount / 1000.0;
                if (now >= nextScan) { nextScan = now + (cinematic?.25:2); HideNative(); }
                ApplyNativeVisibility();
                if(cinematic||nativeView)Reveal(true);
                // Preview75: facing from the skeleton itself (left->right shoulder x up), so cutscene
                // stand-ins with other actor/mesh orientation conventions face correctly too.
                FVector rightAxis = mesh.GetSocketLocation(shoulderR) - mesh.GetSocketLocation(shoulderL);
                // Live73: Wukong is hunched and its neck->head bone points forward, so the head lay
                // on its side and the torso leaned. Torso: halfway between pelvis->neck and vertical;
                // head: same up as the torso.
                FVector spine = Unit(mesh.GetSocketLocation(neck) - mesh.GetSocketLocation(pelvis), new FVector(0, 0, 1));
                FVector up = cinematic?spine:Unit(new FVector(spine.X, spine.Y, spine.Z + 1f), new FVector(0, 0, 1));
                FVector forward = Unit(Cross(rightAxis, up), owner.GetActorForwardVector());
                foreach (var part in parts)
                {
                    FVector z;
                    if (part.Kind == 1) z = up;
                    else if (part.Kind == 2) z = up;
                    else z = Unit(mesh.GetSocketLocation(part.Joint) - mesh.GetSocketLocation(part.Tip), up);
                    part.Component.SetWorldRotation(FromZX(z, forward), false, out _, true);
                }
            }
            catch (Exception e)
            {
                if (!loggedError) { loggedError = true; Log.Write("steve-skin-error", e.GetType().Name + ": " + e.Message); }
            }
        }

        private static FVector Unit(FVector v, FVector fallback)
        {
            double length = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
            return length < 1e-3 ? fallback : new FVector((float)(v.X / length), (float)(v.Y / length), (float)(v.Z / length));
        }
        private static FVector Cross(FVector a, FVector b)
        {
            return new FVector(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        }
        // UE FRotationMatrix::MakeFromZX, then FMatrix::Rotator.
        private static FRotator FromZX(FVector z, FVector xHint)
        {
            FVector y = Unit(Cross(z, xHint), Unit(Cross(z, new FVector(1, 0, 0)), new FVector(0, 1, 0)));
            FVector x = Cross(y, z);
            double pitch = Math.Atan2(x.Z, Math.Sqrt(x.X * x.X + x.Y * x.Y));
            double yaw = Math.Atan2(x.Y, x.X);
            var sy = new FVector((float)-Math.Sin(yaw), (float)Math.Cos(yaw), 0);
            double roll = Math.Atan2(z.X * sy.X + z.Y * sy.Y + z.Z * sy.Z, y.X * sy.X + y.Y * sy.Y + y.Z * sy.Z);
            double d = 180 / Math.PI;
            return new FRotator((float)(pitch * d), (float)(yaw * d), (float)(roll * d));
        }
        private static FVector Convert(float[] p, double scale)
        {
            return new FVector((float)(-p[2] * scale), (float)(-p[0] * scale), (float)(-p[1] * scale));
        }
        private static float Linear(float c)
        {
            return c <= .04045f ? c / 12.92f : (float)Math.Pow((c + .055) / 1.055, 2.4);
        }

        public void Dispose()
        {
            Active = false;
            foreach (var h in hidden) { try { bool? value=h.Lease.Release();if(value.HasValue&&NativeObjectLifetime.Live(h.Component)) h.Component.SetHiddenInGame(value.Value, false); } catch { } }
            hidden.Clear();
            if(tickLeased)try{if(NativeObjectLifetime.Live(mesh))mesh.VisibilityBasedAnimTickOption=originalAnimTick;}catch{}
            tickLeased=false;
            foreach (var part in parts) { try { if (NativeObjectLifetime.Live(owner) && NativeObjectLifetime.Live(part.Component) && !part.Component.IsBeingDestroyed()) part.Component.DestroyComponent(owner); } catch { } }
            parts.Clear();
            Log.Write("steve-skin", "Steve skin removed; Wukong visibility restored");
        }
    }
}
