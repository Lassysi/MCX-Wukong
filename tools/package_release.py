"""Package explicit source/payload allowlists. Third-party mods stay as pinned downloads."""
from pathlib import Path
import argparse,hashlib,json,shutil,zipfile
from verify_public import verify
from release_info import NAME,VERSION,TAG,STEM,MOD_JAR,SETUP_EXE,PACK_FILE
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def write(p,text):p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(text.replace('\r\n','\n').encode('utf-8'))
def zip_rows(path,rows):
    with zipfile.ZipFile(path,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
        for source,name in rows:z.write(source,name)
    with zipfile.ZipFile(path) as z:
        if z.testzip() is not None:raise RuntimeError('Archive verification failed')
        if sorted(z.namelist())!=sorted(name for _,name in rows):raise RuntimeError('Archive allowlist mismatch')
def main():
    p=argparse.ArgumentParser();p.add_argument('--host',required=True);p.add_argument('--addon',required=True);p.add_argument('--reshade',required=True);p.add_argument('--setup',required=True);p.add_argument('--output',required=True);p.add_argument('--staging',required=True);a=p.parse_args()
    source=verify(ROOT);out=Path(a.output).resolve();out.mkdir(parents=True,exist_ok=True)
    if source['release']!=TAG:raise ValueError('Source manifest version differs from public version')
    if ('version='+VERSION) not in (ROOT/'bridge/minecraft-1.21.4/gradle.properties').read_text():raise ValueError('Gradle version differs')
    player=Path(a.staging).resolve()/STEM
    if player.exists():raise RuntimeError('Do not merge into an existing player package')
    payload=player/'payload';payload.mkdir(parents=True)
    files=[];install=[]
    def add(src,name,target=None):
        dest=payload/name;shutil.copy2(src,dest)
        if target:install.append({'source':'payload/'+name,'target':target})
    add(a.host,'DqjCombat.dll','CSharpLoader/Mods/DqjCombat/DqjCombat.dll')
    add(a.addon,'DqjMCCompositor.addon64','DqjMCCompositor.addon64')
    add(a.reshade,'dxgi.dll','dxgi.dll')
    add(ROOT/'bridge/compositor/shaders/MCPassthrough.fx','MCPassthrough.fx','reshade-shaders/Shaders/MCPassthrough.fx')
    add(ROOT/'bridge/compositor/shaders/ReShade.fxh','ReShade.fxh','reshade-shaders/Shaders/ReShade.fxh')
    add(ROOT/'bridge/minecraft-1.21.4/src/main/resources/assets/dqj/textures/entity/dasheng.png','skin.png')
    add(ROOT/'config/cg-skin-scale.txt','cg-skin-scale.txt','CSharpLoader/Mods/DqjCombat/cg-skin-scale.txt')
    jar=ROOT/'bridge/minecraft-1.21.4/build/libs'/MOD_JAR
    with zipfile.ZipFile(jar) as z:
        if json.loads(z.read('fabric.mod.json'))['version']!=VERSION:raise ValueError('Built JAR version differs')
    add(jar,MOD_JAR)
    write(payload/'ReShade.ini','[GENERAL]\nEffectSearchPaths=.\\reshade-shaders\\Shaders\nTextureSearchPaths=.\\reshade-shaders\\Textures\nPresetPath=.\\DqjMCBridge.ini\nPerformanceMode=0\nSkipLoadingDisabledEffects=0\n\n[OVERLAY]\nTutorialProgress=4\nShowClock=0\nShowFPS=0\n\n[SCREENSHOT]\nSavePath=.\\ReShadeScreenshots\nFileFormat=1\nSaveBeforeShot=0\nSaveOverlayShot=0\n\n[DEPTH]\nDepthCopyBeforeClears=1\nUseAspectRatioHeuristics=2\nDrawStatsHeuristic=1\n')
    write(payload/'DqjMCBridge.ini','Techniques=MCPassthrough@MCPassthrough.fx\nTechniqueSorting=MCPassthrough@MCPassthrough.fx\n\n[MCPassthrough.fx]\nReproject=0\nLightMatch=0.6\nGradeMatch=0.55\nHazeStrength=0.85\nBloomStrength=0\nContactShadow=0.5\nEdgeSoftness=0\nDepthBias=0.02\nSlopeBias=0\nMaxBias=0.03\nHostReversedZ=1\nDebugView=0\n')
    write(payload/'mc-bridge.enabled','mc121\n')
    for name,target in [('ReShade.ini','ReShade.ini'),('DqjMCBridge.ini','DqjMCBridge.ini'),('mc-bridge.enabled','CSharpLoader/Mods/DqjCombat/mc-bridge.enabled')]:install.append({'source':'payload/'+name,'target':target})
    shutil.copy2(a.setup,player/SETUP_EXE)
    for name in ('config/compatibility.json','pack/dependencies.lock.json'):shutil.copy2(ROOT/name,player/Path(name).name)
    for row in source['files']:
        rel=row['path']
        if rel.startswith('LICENSES/'):
            dest=player/rel;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(ROOT/rel,dest)
    doc_paths=['LICENSE','README.md','README.zh-CN.md','CHANGELOG.md','CHANGELOG.zh-CN.md','THIRD-PARTY-NOTICES.md','THIRD-PARTY-NOTICES.zh-CN.md']
    doc_paths += ['docs/'+name+lang+'.md' for name in ('INSTALL','FAQ','KNOWN-ISSUES','SKINS') for lang in ('','.zh-CN')]
    for name in doc_paths:
        target=player/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(ROOT/name,target)
    # Source and player guides share the same bilingual layout. Build guides stay in source.
    for name,line in [('README.md','- [Build instructions](docs/BUILD.md)\n'),('README.zh-CN.md','- [编译源码](docs/BUILD.zh-CN.md)\n')]:
        write(player/name,(player/name).read_text(encoding='utf-8').replace(line,''))
    commit=json.loads((ROOT/'source-lock.json').read_text(encoding='utf-8'))['upstream']['commit']
    for name in ('THIRD-PARTY-NOTICES.md','THIRD-PARTY-NOTICES.zh-CN.md'):
        value=(player/name).read_text(encoding='utf-8').replace('pack/dependencies.lock.json','dependencies.lock.json')
        value=value.replace('commit pinned in source-lock.json','commit `'+commit+'`').replace('锁定提交见 source-lock.json','锁定提交：`'+commit+'`')
        write(player/name,value)
    index={'formatVersion':1,'game':'minecraft','versionId':VERSION,'name':'MCX Wukong','summary':'Minecraft Java 1.21.4 bridge for Black Myth: Wukong','dependencies':{'minecraft':'1.21.4','fabric-loader':'0.19.5'},'files':[]}
    for row in json.loads((ROOT/'pack/dependencies.lock.json').read_text(encoding='utf-8')):index['files'].append({k:row[k] for k in ('path','hashes','fileSize','downloads','env')})
    pack=player/PACK_FILE
    with zipfile.ZipFile(pack,'w',zipfile.ZIP_DEFLATED) as z:
        z.writestr('modrinth.index.json',json.dumps(index,indent=2))
        z.write(payload/MOD_JAR,'overrides/mods/'+MOD_JAR)
        z.write(ROOT/'pack/overrides/config/dqj-balance.json','overrides/config/dqj-balance.json')
        z.writestr('overrides/options.txt','pauseOnLostFocus:false\nenableVsync:false\nmaxFps:120\nrenderDistance:6\nsimulationDistance:5\nfullscreen:false\noverrideWidth:1280\noverrideHeight:720\nguiScale:3\n')
    all_files=sorted(p for p in player.rglob('*') if p.is_file())
    manifest={'version':1,'release':TAG,'files':[{'path':p.relative_to(player).as_posix(),'sha256':sha(p),'bytes':p.stat().st_size} for p in all_files],'install':install,'claims':'Offline/package checks only; live public-profile acceptance pending'}
    write(player/'bundle.json',json.dumps(manifest,indent=2,ensure_ascii=False))
    source_rows=[(ROOT/row['path'],row['path']) for row in source['files']]+[(ROOT/'PUBLIC-FILES.json','PUBLIC-FILES.json')]
    zip_rows(out/(STEM+'-Source.zip'),source_rows)
    player_rows=[(p,STEM+'/'+p.relative_to(player).as_posix()) for p in all_files]+[(player/'bundle.json',STEM+'/bundle.json')]
    zip_rows(out/(STEM+'-Players.zip'),player_rows)
    archives=[out/(STEM+'-Source.zip'),out/(STEM+'-Players.zip')]
    write(out/'SHA256SUMS.txt',''.join(sha(p)+'  '+p.name+'\n' for p in archives))
    print(json.dumps({'sourceFiles':len(source_rows),'playerFiles':len(player_rows),'archives':[{ 'name':p.name,'bytes':p.stat().st_size,'sha256':sha(p)} for p in archives]},indent=2))
if __name__=='__main__':main()
