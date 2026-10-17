from pathlib import Path
import subprocess,sys,tempfile

root=Path(__file__).resolve().parents[1]
projects=['appearance-compat','cutscene-skin-scope','ground-prepare','host-projection','native-input-compat','native-input-drag','native-lifetime','native-manual-skip','native-ray','native-travel-compat','prologue-skip','story-presentation','world-coordinates']
for folder in projects:
    project=next((root/'bridge/tests'/folder).glob('*.csproj'))
    command=['dotnet','run','--project',str(project),'-c','Release']
    if folder=='world-coordinates':
        with tempfile.TemporaryDirectory(prefix='coordinates-fixture-',dir=root.parent) as temp:
            subprocess.run(command+['--',temp],cwd=root,check=True)
    else:subprocess.run(command,cwd=root,check=True)
subprocess.run([sys.executable,'-m','unittest','discover','-s','tests','-p','test_*.py'],cwd=root,check=True)
if (root/'PUBLIC-FILES.json').exists():subprocess.run([sys.executable,'tools/verify_public.py'],cwd=root,check=True)
print('Offline checks passed. Live chapter/CG acceptance is separate.')
