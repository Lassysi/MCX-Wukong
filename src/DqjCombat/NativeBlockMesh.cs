using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using b1;
using UnrealEngine.Engine;
using UnrealEngine.Runtime;
using UnrealEngine.Plugins.ProceduralMeshComponent;

namespace DqjCombat
{
    // Preview84 (VIS-01, user: blocks drift and look pasted on; wukong-steve draws blocks natively):
    // a cube-like MC block is drawn by Wukong itself, in the host frame, with the engine's lit
    // vertex-colour material and real shadows, so it cannot drift against the scene. Geometry is the
    // MC collision boxes of the cell (already moved by the anchor dy); each visible texel of the MC
    // face texture becomes part of a coloured quad (equal texels merged). Textures come from
    // mc-blocktex.bin (tools/extract_block_textures.py, local MC client jar). Blocks not in that
    // table stay in the MC layer.
    internal static class NativeBlockMesh
    {
        private sealed class Entry {internal int Kind;internal int[] Faces;internal float[] Quads;internal FLinearColor[] QuadColors;}
        private static Dictionary<string,Entry> blocks;
        private static List<FLinearColor[]> textures;
        private static UMaterialInterface material;
        private static bool failed;
        private static readonly FVector2D SolidUv=new FVector2D(433.5/512,139.5/256);
        // Live check switch: an empty file native-blocks.flip next to the DLL reverses triangle winding.
        private static bool flip;
        // Live check switch (Preview85, drift diagnosis): an empty file native-blocks.tint tints native blocks red,
        // so a copy that still drifts can be told apart (red = Wukong-drawn, normal colours = MC layer).
        private static bool tint;

        internal static bool Ready(){
            if(blocks!=null)return true;if(failed)return false;
            try{
                string path=Path.Combine(Log.Dir,"mc-blocktex.bin");
                var data=File.ReadAllBytes(path);
                if(data.Length<16||Encoding.ASCII.GetString(data,0,6)!="DQJBT1")throw new InvalidDataException("mc-blocktex.bin header");
                int o=8;int count=BitConverter.ToInt32(data,o);o+=4;
                if(count<1||count>65535||o+count*1024>data.Length)throw new InvalidDataException("texture count");
                var list=new List<FLinearColor[]>(count);
                for(int t=0;t<count;t++){
                    var px=new FLinearColor[256];bool red=File.Exists(Path.Combine(Log.Dir,"native-blocks.tint"));
                    for(int i=0;i<256;i++,o+=4)px[i]=red?new FLinearColor(Math.Min(1f,Linear(data[o]/255f)+.5f),Linear(data[o+1]/255f)*.25f,Linear(data[o+2]/255f)*.25f,data[o+3]/255f)
                        :new FLinearColor(Linear(data[o]/255f),Linear(data[o+1]/255f),Linear(data[o+2]/255f),data[o+3]/255f);
                    list.Add(px);
                }
                int n=BitConverter.ToInt32(data,o);o+=4;
                var map=new Dictionary<string,Entry>(n);
                for(int b=0;b<n;b++){
                    int length=data[o++];string id=Encoding.UTF8.GetString(data,o,length);o+=length;
                    var e=new Entry{Kind=data[o++]};
                    if(e.Kind==4){
                        // Preview85: small prop baked from its MC model (texel quads, block space).
                        int quads=BitConverter.ToUInt16(data,o);o+=2;
                        if(quads<1||o+quads*64>data.Length)throw new InvalidDataException("prop quads");
                        e.Quads=new float[quads*15];e.QuadColors=new FLinearColor[quads];
                        for(int q=0;q<quads;q++){
                            for(int k=0;k<15;k++,o+=4)e.Quads[q*15+k]=BitConverter.ToSingle(data,o);
                            e.QuadColors[q]=new FLinearColor(Linear(data[o]/255f),Linear(data[o+1]/255f),Linear(data[o+2]/255f),1);o+=4;
                        }
                    }else{
                        e.Faces=new int[6];
                        for(int f=0;f<6;f++,o+=2){e.Faces[f]=BitConverter.ToUInt16(data,o);if(e.Faces[f]>=count)throw new InvalidDataException("face index");}
                    }
                    map[id]=e;
                }
                if(o!=data.Length)throw new InvalidDataException("trailing data");
                material=UObject.LoadObject<UMaterialInterface>(null,"/Engine/EngineMaterials/DefaultTextMaterialOpaque.DefaultTextMaterialOpaque");
                if(material==null)throw new InvalidOperationException("engine vertex-colour material unavailable");
                material.AddToRoot();
                flip=File.Exists(Path.Combine(Log.Dir,"native-blocks.flip"));
                tint=File.Exists(Path.Combine(Log.Dir,"native-blocks.tint"));
                textures=list;blocks=map;
                Log.Write("native-block","table loaded: "+map.Count+" blocks, "+count+" textures, flip="+flip+" tint="+tint);
                return true;
            }catch(Exception e){failed=true;Log.Write("native-block-error","table: "+e.GetType().Name+": "+e.Message+"; MC keeps drawing every block");return false;}
        }
        internal static bool Known(string id){return Ready()&&blocks.ContainsKey(id);}
        internal static bool Prop(string id){Entry e;return Ready()&&blocks.TryGetValue(id,out e)&&e.Kind==4;}

        // boxes: MC block-local [lx,ly,lz,hx,hy,hz] already including dy; convert: MC world -> host local.
        internal static UProceduralMeshComponent Build(AActor owner,FVector origin,string id,string axis,double ox,double dy,double oz,int x,int y,int z,
            List<double[]> boxes,Func<double,double,double,FVector> convert)
        {
            Entry entry;if(!Ready()||!blocks.TryGetValue(id,out entry))return null;
            var verts=new List<FVector>();var tris=new List<int>();var normals=new List<FVector>();var colors=new List<FLinearColor>();var uvs=new List<FVector2D>();
            if(entry.Kind==4)Props(entry,ox,dy,oz,x,y,z,origin,convert,verts,tris,normals,colors,uvs);
            else foreach(var b in boxes){
                // Texture coordinates use the unshifted block space; the anchor offset moves the vertices.
                double x0=b[0]-ox,y0=b[1]-dy,z0=b[2]-oz,x1=b[3]-ox,y1=b[4]-dy,z1=b[5]-oz;
                for(int face=0;face<6;face++)
                    Face(face,Texture(entry,face,axis),x0,y0,z0,x1,y1,z1,ox,dy,oz,x,y,z,origin,convert,verts,tris,normals,colors,uvs);
            }
            if(verts.Count==0)return null;
            var component=UObject.NewObject<UProceduralMeshComponent>(owner);
            if(component==null)return null;
            component.CreateMeshSection_LinearColor(0,verts,tris,normals,uvs,new List<FVector2D>(),new List<FVector2D>(),new List<FVector2D>(),colors,new List<FProcMeshTangent>(),false);
            component.SetMaterial(0,material);
            component.SetCollisionEnabled(ECollisionEnabled.NoCollision);
            component.SetCastShadow(true);
            UGSE_EngineFuncLib.RegisterComponent(component);
            component.AttachToComponent(owner.RootComponent,default(FName),EAttachmentRule.KeepRelative,EAttachmentRule.KeepRelative,EAttachmentRule.KeepRelative,false);
            component.SetRelativeLocation(new FVector(0,0,0),false,out _,true);
            return component;
        }
        // Face order: down, up, north, south, west, east. Columns (logs) turn their end texture to the axis.
        private static FLinearColor[] Texture(Entry e,int face,string axis)
        {
            if(e.Kind==1&&(axis=="x"||axis=="z")){
                bool end=axis=="x"?face>=4:face==2||face==3;
                return textures[end?e.Faces[1]:e.Faces[2]];
            }
            return textures[e.Faces[face]];
        }
        // MC default element UVs (BlockElement face uvs); texel (u,v) in 1/16 units, v=0 at the top row.
        private static void Face(int face,FLinearColor[] tex,double x0,double y0,double z0,double x1,double y1,double z1,double ox,double dy,double oz,
            int bx,int by,int bz,FVector origin,Func<double,double,double,FVector> convert,
            List<FVector> verts,List<int> tris,List<FVector> normals,List<FLinearColor> colors,List<FVector2D> uvs)
        {
            double u0,u1,v0,v1;double[] n;
            switch(face){
                case 0:u0=x0;u1=x1;v0=1-z1;v1=1-z0;n=new double[]{0,-1,0};break;
                case 1:u0=x0;u1=x1;v0=z0;v1=z1;n=new double[]{0,1,0};break;
                case 2:u0=1-x1;u1=1-x0;v0=1-y1;v1=1-y0;n=new double[]{0,0,-1};break;
                case 3:u0=x0;u1=x1;v0=1-y1;v1=1-y0;n=new double[]{0,0,1};break;
                case 4:u0=z0;u1=z1;v0=1-y1;v1=1-y0;n=new double[]{-1,0,0};break;
                default:u0=1-z1;u1=1-z0;v0=1-y1;v1=1-y0;n=new double[]{1,0,0};break;
            }
            int pu0=Clamp((int)Math.Floor(u0*16+1e-6)),pu1=Clamp((int)Math.Ceiling(u1*16-1e-6)),pv0=Clamp((int)Math.Floor(v0*16+1e-6)),pv1=Clamp((int)Math.Ceiling(v1*16-1e-6));
            if(pu1<=pu0||pv1<=pv0)return;
            // Greedy rectangles of equal opaque texels (cutout: alpha < .5 is a hole).
            var used=new bool[256];
            for(int pv=pv0;pv<pv1;pv++)for(int pu=pu0;pu<pu1;pu++){
                int i=pv*16+pu;if(used[i]||tex[i].A<.5f)continue;
                var c=tex[i];int w=1;while(pu+w<pu1&&!used[i+w]&&Same(tex[i+w],c))w++;
                int h=1;
                while(pv+h<pv1){bool row=true;for(int k=0;k<w;k++){int j=(pv+h)*16+pu+k;if(used[j]||!Same(tex[j],c)){row=false;break;}}if(!row)break;h++;}
                for(int a=0;a<h;a++)for(int k=0;k<w;k++)used[(pv+a)*16+pu+k]=true;
                double qu0=Math.Max(u0,pu/16.0),qu1=Math.Min(u1,(pu+w)/16.0),qv0=Math.Max(v0,pv/16.0),qv1=Math.Min(v1,(pv+h)/16.0);
                Quad(face,qu0,qu1,qv0,qv1,x0,y0,z0,x1,y1,z1,ox,dy,oz,bx,by,bz,n,c,origin,convert,verts,tris,normals,colors,uvs);
            }
        }
        private static void Quad(int face,double u0,double u1,double v0,double v1,double x0,double y0,double z0,double x1,double y1,double z1,double ox,double dy,double oz,
            int bx,int by,int bz,double[] n,FLinearColor c,FVector origin,Func<double,double,double,FVector> convert,
            List<FVector> verts,List<int> tris,List<FVector> normals,List<FLinearColor> colors,List<FVector2D> uvs)
        {
            var p=new double[4][];double[] us={u0,u1,u1,u0},vs={v0,v0,v1,v1};
            for(int k=0;k<4;k++){
                double u=us[k],v=vs[k];double px,py,pz;
                switch(face){
                    case 0:px=u;py=y0;pz=1-v;break;
                    case 1:px=u;py=y1;pz=v;break;
                    case 2:px=1-u;py=1-v;pz=z0;break;
                    case 3:px=u;py=1-v;pz=z1;break;
                    case 4:px=x0;py=1-v;pz=u;break;
                    default:px=x1;py=1-v;pz=1-u;break;
                }
                p[k]=new[]{px+ox,py+dy,pz+oz};
            }
            // Counter-clockwise seen from outside in MC's right-handed space; the MC->host axis map is a
            // mirror, which turns it into Unreal's front-face order (same convention as steve.mesh).
            double ax=p[1][0]-p[0][0],ay=p[1][1]-p[0][1],az=p[1][2]-p[0][2],bxv=p[2][0]-p[0][0],byv=p[2][1]-p[0][1],bzv=p[2][2]-p[0][2];
            double cx=ay*bzv-az*byv,cy=az*bxv-ax*bzv,cz=ax*byv-ay*bxv;
            bool ccw=cx*n[0]+cy*n[1]+cz*n[2]>0;
            if(flip)ccw=!ccw;
            int baseIndex=verts.Count;
            var hostNormal=Delta(convert,n[0],n[1],n[2]);
            for(int k=0;k<4;k++){
                var w=convert(bx+p[k][0],by+p[k][1],bz+p[k][2]);
                verts.Add(new FVector(w.X-origin.X,w.Y-origin.Y,w.Z-origin.Z));normals.Add(hostNormal);colors.Add(c);uvs.Add(SolidUv);
            }
            if(ccw){tris.Add(baseIndex);tris.Add(baseIndex+1);tris.Add(baseIndex+2);tris.Add(baseIndex);tris.Add(baseIndex+2);tris.Add(baseIndex+3);}
            else{tris.Add(baseIndex);tris.Add(baseIndex+2);tris.Add(baseIndex+1);tris.Add(baseIndex);tris.Add(baseIndex+3);tris.Add(baseIndex+2);}
        }
        // Preview86: 12 thin dark edge bars (2 mm outside the box, 1.2 cm thick) around an MC-world box.
        internal static void OutlineMesh(AActor owner,FVector origin,double[] b,Func<double,double,double,FVector> convert)
        {
            if(!Ready())return;
            var verts=new List<FVector>();var tris=new List<int>();var normals=new List<FVector>();var colors=new List<FLinearColor>();var uvs=new List<FVector2D>();
            double g=.002,w=.012;double x0=b[0]-g,y0=b[1]-g,z0=b[2]-g,x1=b[3]+g,y1=b[4]+g,z1=b[5]+g;
            var dark=new FLinearColor(.01f,.01f,.01f,1);
            double[][] edges={
                new[]{x0,y0,z0,x1,y0+w,z0+w},new[]{x0,y1-w,z0,x1,y1,z0+w},new[]{x0,y0,z1-w,x1,y0+w,z1},new[]{x0,y1-w,z1-w,x1,y1,z1},
                new[]{x0,y0,z0,x0+w,y1,z0+w},new[]{x1-w,y0,z0,x1,y1,z0+w},new[]{x0,y0,z1-w,x0+w,y1,z1},new[]{x1-w,y0,z1-w,x1,y1,z1},
                new[]{x0,y0,z0,x0+w,y0+w,z1},new[]{x1-w,y0,z0,x1,y0+w,z1},new[]{x0,y1-w,z0,x0+w,y1,z1},new[]{x1-w,y1-w,z0,x1,y1,z1}};
            foreach(var e in edges)
                for(int face=0;face<6;face++){
                    double[] n=face==0?new double[]{0,-1,0}:face==1?new double[]{0,1,0}:face==2?new double[]{0,0,-1}:face==3?new double[]{0,0,1}:face==4?new double[]{-1,0,0}:new double[]{1,0,0};
                    var p=new double[4][];
                    for(int k=0;k<4;k++){
                        int s=k==1||k==2?1:0,t=k>=2?1:0;
                        switch(face){
                            case 0:p[k]=new[]{s==0?e[0]:e[3],e[1],t==0?e[2]:e[5]};break;
                            case 1:p[k]=new[]{s==0?e[0]:e[3],e[4],t==0?e[2]:e[5]};break;
                            case 2:p[k]=new[]{s==0?e[0]:e[3],t==0?e[1]:e[4],e[2]};break;
                            case 3:p[k]=new[]{s==0?e[0]:e[3],t==0?e[1]:e[4],e[5]};break;
                            case 4:p[k]=new[]{e[0],s==0?e[1]:e[4],t==0?e[2]:e[5]};break;
                            default:p[k]=new[]{e[3],s==0?e[1]:e[4],t==0?e[2]:e[5]};break;
                        }
                    }
                    double ax=p[1][0]-p[0][0],ay=p[1][1]-p[0][1],az=p[1][2]-p[0][2],bx=p[2][0]-p[0][0],by=p[2][1]-p[0][1],bz=p[2][2]-p[0][2];
                    bool ccw=(ay*bz-az*by)*n[0]+(az*bx-ax*bz)*n[1]+(ax*by-ay*bx)*n[2]>0;if(flip)ccw=!ccw;
                    int start=verts.Count;var hn=Delta(convert,n[0],n[1],n[2]);
                    for(int k=0;k<4;k++){var v=convert(p[k][0],p[k][1],p[k][2]);verts.Add(new FVector(v.X-origin.X,v.Y-origin.Y,v.Z-origin.Z));normals.Add(hn);colors.Add(dark);uvs.Add(SolidUv);}
                    if(ccw){tris.Add(start);tris.Add(start+1);tris.Add(start+2);tris.Add(start);tris.Add(start+2);tris.Add(start+3);}
                    else{tris.Add(start);tris.Add(start+2);tris.Add(start+1);tris.Add(start);tris.Add(start+3);tris.Add(start+2);}
                }
            var component=UObject.NewObject<UProceduralMeshComponent>(owner);if(component==null)return;
            component.CreateMeshSection_LinearColor(0,verts,tris,normals,uvs,new List<FVector2D>(),new List<FVector2D>(),new List<FVector2D>(),colors,new List<FProcMeshTangent>(),false);
            component.SetMaterial(0,material);component.SetCollisionEnabled(ECollisionEnabled.NoCollision);component.SetCastShadow(false);
            UGSE_EngineFuncLib.RegisterComponent(component);
            component.AttachToComponent(owner.RootComponent,default(FName),EAttachmentRule.KeepRelative,EAttachmentRule.KeepRelative,EAttachmentRule.KeepRelative,false);
            component.SetRelativeLocation(new FVector(0,0,0),false,out _,true);
        }
        // Quads are counter-clockwise from the front in MC space, like the cube faces below.
        private static void Props(Entry e,double ox,double dy,double oz,int bx,int by,int bz,FVector origin,Func<double,double,double,FVector> convert,
            List<FVector> verts,List<int> tris,List<FVector> normals,List<FLinearColor> colors,List<FVector2D> uvs)
        {
            var q=e.Quads;
            for(int i=0;i<e.QuadColors.Length;i++){
                int b=i*15,start=verts.Count;
                var normal=Delta(convert,q[b+12],q[b+13],q[b+14]);
                var c=e.QuadColors[i];
                if(tint)c=new FLinearColor(Math.Min(1f,c.R+.5f),c.G*.25f,c.B*.25f,1);
                for(int k=0;k<4;k++){
                    var w=convert(bx+q[b+k*3]+ox,by+q[b+k*3+1]+dy,bz+q[b+k*3+2]+oz);
                    verts.Add(new FVector(w.X-origin.X,w.Y-origin.Y,w.Z-origin.Z));normals.Add(normal);colors.Add(c);uvs.Add(SolidUv);
                }
                if(!flip){tris.Add(start);tris.Add(start+1);tris.Add(start+2);tris.Add(start);tris.Add(start+2);tris.Add(start+3);}
                else{tris.Add(start);tris.Add(start+2);tris.Add(start+1);tris.Add(start);tris.Add(start+3);tris.Add(start+2);}
            }
        }
        private static FVector Delta(Func<double,double,double,FVector> convert,double dx,double dy,double dz)
        {
            var a=convert(0,64,0);var b=convert(dx,64+dy,dz);
            var d=new FVector(b.X-a.X,b.Y-a.Y,b.Z-a.Z);float len=(float)Math.Sqrt(d.X*d.X+d.Y*d.Y+d.Z*d.Z);
            return len>0?new FVector(d.X/len,d.Y/len,d.Z/len):d;
        }
        private static bool Same(FLinearColor a,FLinearColor b){return a.R==b.R&&a.G==b.G&&a.B==b.B&&b.A>=.5f;}
        private static int Clamp(int v){return v<0?0:v>16?16:v;}
        private static float Linear(float c){return c<=.04045f?c/12.92f:(float)Math.Pow((c+.055)/1.055,2.4);}
    }
}
