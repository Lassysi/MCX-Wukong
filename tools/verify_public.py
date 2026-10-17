"""Check an explicit source manifest; generated build caches are never source-archive inputs."""
from pathlib import Path,PurePosixPath
import hashlib,json,re,subprocess,sys

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def verify(root,check_git=True):
    root=Path(root).resolve();manifest=json.loads((root/'PUBLIC-FILES.json').read_text(encoding='utf-8'))
    paths=set()
    blocked={'references','_ref_src','_archive','.work','runtime-1.21.4','saves','logs','crashes','node_modules','.tools','bin','obj','build','.gradle','__pycache__','.git'}
    for row in manifest['files']:
        path=row['path'];p=(root/path).resolve()
        if path in paths or PurePosixPath(path).is_absolute() or '..' in PurePosixPath(path).parts or '\\' in path or ':' in path or not p.is_relative_to(root):raise ValueError('Invalid source manifest entry')
        paths.add(path)
        if blocked.intersection(Path(path).parts):raise ValueError('Private content in source: '+path)
        if p.suffix.lower() in {'.dll','.exe','.dmp','.pdb','.mesh','.bin','.log','.zip','.addon64'}:raise ValueError('Runtime/private binary in source: '+path)
        if p.suffix.lower()=='.jar' and path!='bridge/minecraft-1.21.4/gradle/wrapper/gradle-wrapper.jar':raise ValueError('Unexpected JAR in source')
        if not p.is_file() or p.stat().st_size!=row['bytes'] or sha(p)!=row['sha256']:raise ValueError('Source content differs: '+path)
        if p.suffix.lower() not in {'.png','.jar'}:
            text=p.read_text(encoding='utf-8-sig')
            if re.search(r'(?i)(?:[A-Z]:[/\\](?:Users[/\\]|Steam[/\\]|MCXGames[/\\]|PCL2[/\\]|Codex_Projects[/\\]))',text):raise ValueError('Personal machine path in source: '+path)
            if re.search(r'gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|-----BEGIN (?:RSA |OPENSSH )?PRIVATE KEY-----',text):raise ValueError('Credential-shaped data in source: '+path)
    if check_git and (root/'.git').is_dir():
        result=subprocess.run(['git','ls-files','-z'],cwd=root,capture_output=True,check=True)
        tracked=set(result.stdout.decode('utf-8').rstrip('\0').split('\0')) if result.stdout else set()
        if tracked!=paths|{'PUBLIC-FILES.json'}:raise ValueError('Git index differs from public source allowlist')
    return manifest

if __name__=='__main__':
    r=Path(sys.argv[1]) if len(sys.argv)>1 else Path(__file__).resolve().parents[1]
    m=verify(r);print('Public source manifest verified:',len(m['files']),'files')
