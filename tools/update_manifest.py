"""Refresh existing public-source entries. Adding a new path requires --add PATH."""
from pathlib import Path,PurePosixPath
import argparse,hashlib,json
root=Path(__file__).resolve().parents[1]
def main():
    p=argparse.ArgumentParser();p.add_argument('--add',action='append',default=[]);a=p.parse_args()
    path=root/'PUBLIC-FILES.json';data=json.loads(path.read_text(encoding='utf-8'))
    names={x['path'] for x in data['files']}
    for name in a.add:
        rel=PurePosixPath(name)
        if rel.is_absolute() or '..' in rel.parts or '\\' in name or ':' in name:raise ValueError('Invalid source path')
        names.add(name)
    rows=[]
    for name in sorted(names):
        file=root/name
        if not file.is_file():raise FileNotFoundError('Remove obsolete paths from PUBLIC-FILES.json explicitly: '+name)
        rows.append({'path':name,'bytes':file.stat().st_size,'sha256':hashlib.sha256(file.read_bytes()).hexdigest()})
    data['files']=rows;path.write_text(json.dumps(data,indent=2),encoding='utf-8',newline='\n')
    from verify_public import verify
    verify(root,check_git=False);print('Source manifest refreshed:',len(rows),'files; stage reviewed paths before release')
if __name__=='__main__':main()
