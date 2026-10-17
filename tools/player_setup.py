"""MCX player installer. Native assets are generated from the player's local client."""
from pathlib import Path,PurePosixPath
import argparse,csv,gzip,hashlib,io,json,os,re,shutil,subprocess,sys,tempfile,time,uuid,zipfile
from create_mesh import build as build_mesh
from new_world import create as create_world
from extract_block_textures import main as extract_textures

JOURNAL='MCX-Wukong-install.json'
from release_info import MOD_JAR,TAG,SETUP_EXE
def digest(path):
    h=hashlib.sha256()
    with Path(path).open('rb') as f:
        for chunk in iter(lambda:f.read(1024*1024),b''):h.update(chunk)
    return h.hexdigest()
def read(path):return json.loads(Path(path).read_text(encoding='utf-8-sig'))
def safe(root,relative):
    root=Path(root).resolve();rel=PurePosixPath(relative)
    if rel.is_absolute() or '..' in rel.parts or '\\' in relative or ':' in relative:raise ValueError('Invalid relative installation path')
    p=(root/relative).resolve()
    if not p.is_relative_to(root):raise ValueError('Installation path exits selected folder')
    return p
def atomic(path,data):
    path=Path(path);path.parent.mkdir(parents=True,exist_ok=True)
    tmp=path.with_name(path.name+'.mcx-'+uuid.uuid4().hex+'.tmp')
    try:tmp.write_bytes(data);os.replace(tmp,path)
    finally:
        if tmp.exists():tmp.unlink()
def check_closed():
    if os.name!='nt':return
    result=subprocess.run(['tasklist','/FO','CSV','/NH'],capture_output=True,creationflags=0x08000000)
    names={row[0].lower() for row in csv.reader(io.StringIO(result.stdout.decode('mbcs',errors='replace'))) if row}
    if names.intersection({'b1.exe','b1-win64-shipping.exe','java.exe','javaw.exe'}):raise RuntimeError('Exit Black Myth and Minecraft normally before installing or uninstalling. / 请先正常退出黑神话和 Minecraft，再安装或卸载。')
def verify_bundle(bundle):
    bundle=Path(bundle);manifest=read(bundle/'bundle.json')
    for row in manifest['files']:
        p=safe(bundle,row['path'])
        if not p.is_file() or digest(p)!=row['sha256']:raise RuntimeError('Missing or damaged package file: / 安装包缺失或损坏：'+row['path'])
    return manifest
def roots(game,mc):
    game=Path(game).resolve();mc=Path(mc).resolve()
    if (game/'b1/Binaries/Win64').is_dir():native=game/'b1/Binaries/Win64'
    elif (game/'b1-Win64-Shipping.exe').is_file():native=game;game=game.parents[2]
    else:raise RuntimeError('Select the BlackMythWukong installation folder. / 请选择 BlackMythWukong 安装文件夹。')
    if (mc/'.minecraft/mods').is_dir():mc=mc/'.minecraft'
    if not (mc/'mods').is_dir():raise RuntimeError('Select the imported MCX instance game folder; it must contain mods. / 请选择已导入 MCX 整合包的 MC 游戏文件夹，里面应有 mods 文件夹。')
    return game,native,mc
def preflight(bundle,game,mc,client,strict=True):
    manifest=verify_bundle(bundle);game,native,mc=roots(game,mc)
    if (native/JOURNAL).exists():raise RuntimeError('MCX is already installed. Uninstall it with this tool before updating; saves are kept. / 已安装 MCX。更新前请先用本安装工具卸载；存档会保留。')
    if (mc/'config/mcx-install-id.json').exists():raise RuntimeError('This Minecraft instance already has an MCX record. Uninstall using its original game folder first. / 这个 MC 实例已有 MCX 安装记录，请先使用原安装位置卸载。')
    if (mc/'saves/MCX-Wukong').exists() and not (mc/'saves/MCX-Wukong/mcx-world.json').is_file():raise RuntimeError('An unrelated MCX-Wukong world already exists. Use a new instance; no world was changed. / 已有同名 MCX-Wukong 世界，未改动。请使用新的整合包实例。')
    compat=read(Path(bundle)/'compatibility.json')
    if strict and digest(native/'b1-Win64-Shipping.exe')!=compat['exeSha256']:raise RuntimeError('Unsupported game version. Required Steam build: / 黑神话版本尚未验证。本包对应 Steam build '+compat['steamBuild']+'. Install stopped. / ，请勿强行安装。')
    loader=native/'CSharpLoader'
    if not (loader/'CSharpModBase.dll').is_file() or not (native/'version.dll').is_file():raise RuntimeError('Install CSharpLoader 0.1.0 from its author first. See the installation guide. / 请先从作者原站安装 CSharpLoader 0.1.0。链接在使用说明中。')
    if strict and digest(loader/'CSharpModBase.dll')!=compat['loaderBaseSha256']:raise RuntimeError('Unsupported CSharpLoader version; use the pinned 0.1.0 release. / CSharpLoader 与已验证版本不一致，请检查加载器版本。')
    if not (loader/'b1cs.ini').is_file():raise RuntimeError('CSharpLoader/b1cs.ini is missing. Check the loader installation. / 缺少 CSharpLoader/b1cs.ini，请重新检查加载器安装。')
    expected=read(Path(bundle)/'dependencies.lock.json')
    for row in expected:
        p=safe(mc,row['path'])
        if not p.is_file() or digest(p)!=row['hashes']['sha256']:raise RuntimeError('Missing or mismatched Minecraft mod: / MC 模组缺失或版本不符：'+Path(row['path']).name+'. Reimport the included .mrpack. / 。请重新导入随包 .mrpack。')
    allowed={Path(row['path']).name for row in expected}|{MOD_JAR}
    extras={p.name for p in (mc/'mods').glob('*.jar')}-allowed
    if extras:raise RuntimeError('Use a separate MCX instance. Extra mods found: / 请使用独立的 MCX 整合包实例。额外模组：'+', '.join(sorted(extras)))
    client=Path(client)
    if strict and digest(client)!=compat['clientSha256']:raise RuntimeError('Select the vanilla Minecraft Java 1.21.4 client JAR, not a mod or server JAR. / 请选择原版 Minecraft Java 1.21.4 客户端 JAR，不要选择 Fabric 模组或服务端 JAR。')
    with zipfile.ZipFile(client) as z:
        if json.loads(z.read('version.json'))['id']!='1.21.4':raise RuntimeError('Minecraft client version must be 1.21.4. / Minecraft 客户端版本须为 1.21.4。')
    return manifest,game,native,mc
def ini_loader(raw):
    text=raw.decode('utf-8-sig');lines=text.splitlines();section=False;seen=set();output=[]
    for line in lines:
        if line.strip().startswith('['):
            if section:
                for key in ('Develop','EnableJit'):
                    if key not in seen:output.append(key+'=1')
            section=line.strip().lower()=='[settings]'
        if section:
            key=line.split('=',1)[0].strip()
            if key in ('Develop','EnableJit'):line=key+'=1';seen.add(key)
        output.append(line)
    if section:
        for key in ('Develop','EnableJit'):
            if key not in seen:output.append(key+'=1')
    if not any(x.strip().lower()=='[settings]' for x in lines):output+=['[Settings]','Develop=1','EnableJit=1']
    return ('\r\n'.join(output)+'\r\n').encode('utf-8')
def reskin_jar(source,skin,target):
    with zipfile.ZipFile(source) as src,zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED) as dst:
        for entry in src.infolist():dst.writestr(entry,Path(skin).read_bytes() if entry.filename=='assets/dqj/textures/entity/dasheng.png' else src.read(entry.filename))
def install(bundle,game,mc,client,skin=None,skip_prologue=True,log=print,strict=True):
    check_closed();bundle=Path(bundle);manifest,game,native,mc=preflight(bundle,game,mc,client,strict)
    backup=native/'MCX-Wukong-backups'/uuid.uuid4().hex
    backup.mkdir(parents=True,exist_ok=False)
    journal={'version':1,'release':TAG,'state':'preparing','id':uuid.uuid4().hex,'backup':backup.relative_to(native).as_posix(),'files':[],'worldCreated':False}
    selected_skin=Path(skin) if skin else bundle/'payload/skin.png'
    with tempfile.TemporaryDirectory(prefix='mcx-stage-',dir=backup) as tmp:
        stage=Path(tmp);log('Generating local block textures and character mesh... / 生成本机材质和人物模型……')
        build_mesh(selected_skin,stage/'steve.mesh');extract_textures(str(client),str(stage/'mc-blocktex.bin'))
        reskin_jar(bundle/'payload'/MOD_JAR,selected_skin,stage/MOD_JAR)
        plan=[]
        for row in manifest['install']:
            plan.append(('game',row['target'],safe(bundle,row['source']).read_bytes()))
        for name in ('steve.mesh','mc-blocktex.bin'):plan.append(('game','CSharpLoader/Mods/DqjCombat/'+name,(stage/name).read_bytes()))
        plan += [('game','CSharpLoader/b1cs.ini',ini_loader((native/'CSharpLoader/b1cs.ini').read_bytes())),('mc','mods/'+MOD_JAR,(stage/MOD_JAR).read_bytes()),('mc','config/mcx-install-id.json',json.dumps({'id':journal['id']}).encode())]
        if skip_prologue:plan.append(('game','CSharpLoader/Mods/DqjCombat/skip-prologue.on',b''))
        elif (native/'CSharpLoader/Mods/DqjCombat/skip-prologue.on').exists():raise RuntimeError('An old prologue-skip setting exists. Remove the previous MCX configuration first. / 已有序章跳过设置，请先移走旧版 MCX 配置后安装。')
        # Back up native progression locally; rollback never restores it automatically.
        saves=game/'b1/Saved/SaveGames'
        if saves.is_dir():
            save_rows=[(p,p.relative_to(saves).as_posix(),digest(p)) for p in sorted(saves.rglob('*')) if p.is_file()]
            with zipfile.ZipFile(backup/'native-save-before-install.zip','w',zipfile.ZIP_DEFLATED) as z:
                for p,relative,expected in save_rows:z.write(p,relative)
            with zipfile.ZipFile(backup/'native-save-before-install.zip') as z:
                if z.testzip() is not None:raise RuntimeError('Save backup verification failed. Nothing was installed. / 存档备份校验失败，未安装。')
                for p,relative,expected in save_rows:
                    if digest(p)!=expected or hashlib.sha256(z.read(relative)).hexdigest()!=expected:raise RuntimeError('Save files changed during backup. Install stopped. / 存档在备份时发生变化，已停止安装。')
        try:
            for i,(scope,relative,data) in enumerate(plan):
                target=safe(native if scope=='game' else mc,relative)
                before=target.read_bytes() if target.is_file() else None
                previous=None
                if before is not None:
                    previous='previous-'+str(i)+'.bin';(backup/previous).write_bytes(before)
                row={'scope':scope,'target':relative,'sha256':hashlib.sha256(data).hexdigest(),'previous':previous,'previousSha256':hashlib.sha256(before).hexdigest() if before is not None else None}
                journal['files'].append(row)
                atomic(native/JOURNAL,json.dumps(journal,indent=2).encode())
                atomic(target,data)
            journal['worldCreated']=create_world(mc/'saves/MCX-Wukong')
            journal['state']='installed';atomic(native/JOURNAL,json.dumps(journal,indent=2).encode())
        except Exception:
            for row in reversed(journal['files']):
                target=safe(native if row['scope']=='game' else mc,row['target'])
                if row['previous']:atomic(target,(backup/row['previous']).read_bytes())
                elif target.exists():target.unlink()
            journal['state']='failed-rolled-back';atomic(native/JOURNAL,json.dumps(journal,indent=2).encode());raise
    log('Installed. Enter the MCX-Wukong world in Minecraft, then start Black Myth. / 安装完成。先开 MC，进入 MCX-Wukong 世界，再开黑神话。')
    return journal
def uninstall(bundle,game,mc,log=print):
    check_closed();game,native,mc=roots(game,mc);journal=read(native/JOURNAL)
    if journal.get('version')!=1 or journal.get('state')!='installed':raise RuntimeError('No valid install record was found. / 没有有效的安装记录。')
    if read(mc/'config/mcx-install-id.json').get('id')!=journal.get('id'):raise RuntimeError('This Minecraft instance does not match the install record. Nothing was changed. / 所选 MC 文件夹与安装记录不一致，未改动。')
    backup=safe(native,journal['backup'])
    for row in journal['files']:
        if row['scope'] not in ('game','mc'):raise ValueError('Unknown installation scope')
        target=safe(native if row['scope']=='game' else mc,row['target'])
        if target.exists() and digest(target)!=row['sha256']:raise RuntimeError('Installed file has changed. Uninstall stopped: / 文件安装后发生变化，已停止卸载：'+row['target'])
        if row['previous'] and digest(safe(backup,row['previous']))!=row['previousSha256']:raise RuntimeError('Original-file backup is damaged. Uninstall stopped. / 原文件备份损坏，已停止卸载。')
    for row in reversed(journal['files']):
        target=safe(native if row['scope']=='game' else mc,row['target'])
        if row['previous']:atomic(target,safe(backup,row['previous']).read_bytes())
        elif target.exists():target.unlink()
    journal['state']='uninstalled';atomic(backup/'uninstalled.json',json.dumps(journal,indent=2).encode());(native/JOURNAL).unlink()
    log('MCX removed. Game progress, Minecraft worlds and the third-party loader were kept. / 已卸载 MCX。游戏进度、MC 世界和第三方加载器均保留。')

def find_client(mc):
    root=Path(mc).resolve()
    for base in [root]+list(root.parents)[:3]:
        for pattern in ('versions/*/*.jar','libraries/com/mojang/minecraft/1.21.4/*.jar','libraries/com/mojang/minecraft/*1.21.4*.jar'):
            for p in base.glob(pattern):
                try:
                    with zipfile.ZipFile(p) as z:
                        if json.loads(z.read('version.json'))['id']=='1.21.4':return str(p)
                except (OSError,KeyError,ValueError,zipfile.BadZipFile):pass
    return ''
def gui(bundle):
    import tkinter as tk
    from tkinter import ttk,filedialog,messagebox
    from tkinter.scrolledtext import ScrolledText
    import threading
    root=tk.Tk();root.title('MCX Wukong '+TAG+' | Setup / 安装');root.geometry('1120x520');root.minsize(980,480)
    values=[tk.StringVar() for _ in range(4)];busy=False
    labels=['Black Myth installation folder\n黑神话安装文件夹','Minecraft instance game folder\nMC 整合包游戏文件夹','Vanilla Minecraft 1.21.4 client JAR\n原版 MC 1.21.4 客户端 JAR','Custom skin PNG (optional)\n自选皮肤 PNG（可留空）']
    def choose(index):
        p=filedialog.askdirectory() if index<2 else filedialog.askopenfilename(filetypes=[('JAR','*.jar')] if index==2 else [('PNG','*.png')])
        if p:values[index].set(p)
        if index==1 and p and not values[2].get():values[2].set(find_client(p))
    for i,label in enumerate(labels):
        ttk.Label(root,text=label).grid(row=i,column=0,padx=10,pady=8,sticky='w')
        ttk.Entry(root,textvariable=values[i],width=58).grid(row=i,column=1,sticky='ew')
        ttk.Button(root,text='Browse / 选择',command=lambda n=i:choose(n)).grid(row=i,column=2,padx=10)
    skip=tk.BooleanVar(value=True);ttk.Checkbutton(root,text='Skip the entire prologue in new saves (including combat)\n新存档跳过序章（含序章战斗）',variable=skip).grid(row=4,column=1,sticky='w',pady=5)
    output=ScrolledText(root,height=10);output.grid(row=6,column=0,columnspan=3,padx=10,pady=10,sticky='nsew')
    root.columnconfigure(1,weight=1);root.rowconfigure(6,weight=1)
    def log(s):root.after(0,lambda:output.insert('end',s+'\n'))
    def start(action):
        nonlocal busy
        if busy:return
        args=[x.get().strip() for x in values];selected=skip.get();busy=True
        def work():
            nonlocal busy
            try:
                if action=='install':install(bundle,args[0],args[1],args[2],args[3] or None,selected,log)
                else:uninstall(bundle,args[0],args[1],log)
                root.after(0,lambda:messagebox.showinfo('MCX','Done. See the log below. / 操作完成，详情见窗口下方。'))
            except Exception as e:
                log(str(e));root.after(0,lambda msg=str(e):messagebox.showerror('MCX',msg))
            finally:busy=False
        threading.Thread(target=work,daemon=True).start()
    buttons=ttk.Frame(root);buttons.grid(row=5,column=1,sticky='w')
    ttk.Button(buttons,text='Install / 安装',command=lambda:start('install')).pack(side='left',padx=5)
    ttk.Button(buttons,text='Uninstall / 卸载',command=lambda:start('uninstall')).pack(side='left',padx=5)
    def close():
        if busy:messagebox.showinfo('MCX','Files are being processed. Wait until the operation finishes. / 正在处理文件，请等待操作结束。')
        else:root.destroy()
    root.protocol('WM_DELETE_WINDOW',close);root.mainloop()
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--verify-bundle',action='store_true');parser.add_argument('--bundle');args=parser.parse_args()
    bundle=Path(args.bundle) if args.bundle else (Path(sys.executable).parent if getattr(sys,'frozen',False) else Path(__file__).resolve().parents[1]/'dist/player')
    if args.verify_bundle:
        manifest=verify_bundle(bundle);report={'verified':True,'files':len(manifest['files'])};(bundle/'verify-result.json').write_text(json.dumps(report),encoding='utf-8');return
    gui(bundle)
if __name__=='__main__':main()
