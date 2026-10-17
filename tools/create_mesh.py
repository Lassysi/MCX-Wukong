"""Build twelve named cuboid skin parts. No game mesh or base texture is required."""
from pathlib import Path
import struct
from PIL import Image

PARTS=[
 ('head',(-4,-8,-4),(8,8,8),(0,0),(0,0,0)),
 ('body',(-4,0,-2),(8,12,4),(16,16),(0,0,0)),
 ('right_arm',(-3,-2,-2),(4,12,4),(40,16),(-5,2,0)),
 ('left_arm',(-1,-2,-2),(4,12,4),(32,48),(5,2,0)),
 ('right_leg',(-2,0,-2),(4,12,4),(0,16),(-1.9,12,0)),
 ('left_leg',(-2,0,-2),(4,12,4),(16,48),(1.9,12,0)),
]
OVERLAYS=[('hat',0,(32,0),.5),('jacket',1,(16,32),.25),('right_sleeve',2,(40,32),.25),('left_sleeve',3,(48,48),.25),('right_pants',4,(0,32),.25),('left_pants',5,(0,48),.25)]

def build(skin_path,output):
    skin=Image.open(skin_path).convert('RGBA')
    if skin.size!=(64,64):raise ValueError('Use a 64x64 PNG with classic 4-pixel arms. / 皮肤必须为 64×64 PNG，使用经典四像素手臂。')
    rows=[]
    specs=[(n,lo,size,uv,pivot,0) for n,lo,size,uv,pivot in PARTS]
    specs += [(name,PARTS[i][1],PARTS[i][2],uv,PARTS[i][4],d) for name,i,uv,d in OVERLAYS]
    for name,lo,size,uv,pivot,dilate in specs:
        x,y,z=lo;w,h,d=size;u,v=uv
        # Face origin, horizontal edge, vertical edge, texture origin, outward normal.
        faces=[((x,y,z),(w,0,0),(0,h,0),(u+d,v+d),(0,0,-1)),
               ((x+w,y,z+d),(-w,0,0),(0,h,0),(u+2*d+w,v+d),(0,0,1)),
               ((x,y,z+d),(0,0,-d),(0,h,0),(u,v+d),(-1,0,0)),
               ((x+w,y,z),(0,0,d),(0,h,0),(u+d+w,v+d),(1,0,0)),
               ((x,y,z+d),(w,0,0),(0,0,-d),(u+d,v),(0,-1,0)),
               ((x,y+h,z),(w,0,0),(0,0,d),(u+d+w,v),(0,1,0))]
        vertices=[];indices=[];centre=[lo[k]+size[k]/2 for k in range(3)]
        for origin,a,b,tex,normal in faces:
            aw=int(sum(abs(q) for q in a));bh=int(sum(abs(q) for q in b))
            cross=(a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
            positive=sum(cross[k]*normal[k] for k in range(3))>0
            for ty in range(bh):
                for tx in range(aw):
                    color=skin.getpixel((tex[0]+tx,tex[1]+ty))
                    if color[3]<128 and name in {q[0] for q in OVERLAYS}:continue
                    if color[3]<128:color=(0,0,0,255)
                    first=len(vertices)
                    for dx,dy in ((0,0),(1,0),(1,1),(0,1)):
                        p=[origin[k]+a[k]*(tx+dx)/aw+b[k]*(ty+dy)/bh for k in range(3)]
                        p=[centre[k]+(p[k]-centre[k])*(size[k]+2*dilate)/size[k] for k in range(3)]
                        vertices.append(p+list(normal)+[(tex[0]+tx+.5)/64,(tex[1]+ty+.5)/64]+[q/255 for q in color])
                    indices += [first+i for i in ((0,1,2,0,2,3) if positive else (0,2,1,0,3,2))]
        rows.append((name,pivot,vertices,indices))
    raw=bytearray(b'STEVEM01')+struct.pack('<i',len(rows))
    for name,pivot,vertices,indices in rows:
        encoded=name.encode();raw+=struct.pack('<i',len(encoded))+encoded+struct.pack('<3f',*pivot)+struct.pack('<2i',len(vertices),len(indices))
        for vertex in vertices:raw+=struct.pack('<12f',*vertex)
        if indices:raw+=struct.pack('<%di'%len(indices),*indices)
    Path(output).write_bytes(raw)
    return rows

if __name__=='__main__':
    import argparse
    p=argparse.ArgumentParser();p.add_argument('skin');p.add_argument('output');a=p.parse_args();build(a.skin,a.output)
