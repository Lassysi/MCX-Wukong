from pathlib import Path
import gzip,hashlib,importlib.util,json,struct,sys,tempfile,unittest,zipfile
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'tools'))
import player_setup as setup
from create_mesh import build as build_mesh
from new_world import world_bytes

def nbt(data):
    import io
    f=io.BytesIO(gzip.decompress(data))
    def word():return f.read(struct.unpack('>H',f.read(2))[0]).decode()
    def value(kind):
        if kind in (1,2,3,4,5,6):
            size={1:1,2:2,3:4,4:8,5:4,6:8}[kind];return struct.unpack({1:'>b',2:'>h',3:'>i',4:'>q',5:'>f',6:'>d'}[kind],f.read(size))[0]
        if kind==8:return word()
        if kind==9:
            t=f.read(1)[0];n=struct.unpack('>i',f.read(4))[0];return [value(t) for _ in range(n)]
        if kind==10:
            out={}
            while True:
                t=f.read(1)[0]
                if t==0:return out
                name=word();out[name]=value(t)
        raise ValueError(kind)
    assert f.read(1)==b'\x0a';word();out=value(10);assert f.read()==b'';return out

class InstallerTests(unittest.TestCase):
    def setUp(self):
        # Kept inside the current workspace; no Documents/default C-drive output.
        self.tmp=tempfile.TemporaryDirectory(prefix='installer-fixture-',dir=ROOT.parent)
        self.addCleanup(self.tmp.cleanup);self.root=Path(self.tmp.name)
        self.bundle=self.root/'bundle';self.game=self.root/'游戏 with spaces';self.mc=self.root/'MC 实例'
        native=self.game/'b1/Binaries/Win64';self.native=native
        for p in (self.bundle/'payload',native/'CSharpLoader',self.mc/'mods'):p.mkdir(parents=True)
        (native/'b1-Win64-Shipping.exe').write_bytes(b'fixture-game');(native/'version.dll').write_bytes(b'loader')
        (native/'CSharpLoader/CSharpModBase.dll').write_bytes(b'fixture-loader')
        (native/'CSharpLoader/b1cs.ini').write_text('[Settings]\nDevelop=0\nConsole=0\nEnableJit=0\nExtra=keep\n')
        saves=self.game/'b1/Saved/SaveGames';saves.mkdir(parents=True);(saves/'progress.sav').write_bytes(b'keep-native-progress')
        old=self.mc/'saves/平时的世界';old.mkdir(parents=True);(old/'user.data').write_bytes(b'keep-world')
        skin=ROOT/'bridge/minecraft-1.21.4/src/main/resources/assets/dqj/textures/entity/dasheng.png'
        (self.bundle/'payload/skin.png').write_bytes(skin.read_bytes())
        with zipfile.ZipFile(self.bundle/'payload'/setup.MOD_JAR,'w') as z:z.writestr('assets/dqj/textures/entity/dasheng.png',skin.read_bytes());z.writestr('test.class',b'keep-code')
        (self.bundle/'payload/host.dll').write_bytes(b'new-host')
        (self.bundle/'compatibility.json').write_text('{}')
        (self.bundle/'dependencies.lock.json').write_text('[]')
        files=[{'path':p.relative_to(self.bundle).as_posix(),'sha256':setup.digest(p)} for p in self.bundle.rglob('*') if p.is_file()]
        (self.bundle/'bundle.json').write_text(json.dumps({'files':files,'install':[{'source':'payload/host.dll','target':'CSharpLoader/Mods/DqjCombat/DqjCombat.dll'}]}))
        self.client=self.root/'client.jar'
        with zipfile.ZipFile(self.client,'w') as z:z.writestr('version.json','{"id":"1.21.4"}')
    def install(self):
        with patch.object(setup,'check_closed'),patch.object(setup,'extract_textures',side_effect=lambda src,dst:Path(dst).write_bytes(b'DQJBT1\0\0fixture')):
            return setup.install(self.bundle,self.game,self.mc,self.client,strict=False,log=lambda _:None)
    def test_install_uninstall_preserves_progress_and_unowned_files(self):
        previous=(self.native/'CSharpLoader/b1cs.ini').read_bytes()
        result=self.install();self.assertEqual(result['state'],'installed')
        self.assertTrue((self.mc/'saves/MCX-Wukong/mcx-world.json').exists())
        (self.native/'CSharpLoader/Mods/DqjCombat/dqj-combat.log').write_text('runtime log')
        with patch.object(setup,'check_closed'):setup.uninstall(self.bundle,self.game,self.mc,log=lambda _:None)
        self.assertEqual((self.native/'CSharpLoader/b1cs.ini').read_bytes(),previous)
        self.assertEqual((self.game/'b1/Saved/SaveGames/progress.sav').read_bytes(),b'keep-native-progress')
        self.assertEqual((self.mc/'saves/平时的世界/user.data').read_bytes(),b'keep-world')
        self.assertTrue((self.mc/'saves/MCX-Wukong/level.dat').exists())
        self.assertTrue((self.native/'CSharpLoader/Mods/DqjCombat/dqj-combat.log').exists())
    def test_modified_payload_rejects_before_install(self):
        (self.bundle/'payload/host.dll').write_bytes(b'tampered')
        with self.assertRaises(RuntimeError):self.install()
        self.assertFalse((self.native/setup.JOURNAL).exists())
    def test_modified_install_rejects_uninstall_before_any_restore(self):
        self.install();host=self.native/'CSharpLoader/Mods/DqjCombat/DqjCombat.dll';host.write_bytes(b'user-modified')
        before=(self.native/'CSharpLoader/b1cs.ini').read_bytes()
        with patch.object(setup,'check_closed'),self.assertRaises(RuntimeError):setup.uninstall(self.bundle,self.game,self.mc)
        self.assertEqual(host.read_bytes(),b'user-modified');self.assertEqual((self.native/'CSharpLoader/b1cs.ini').read_bytes(),before)
    def test_wrong_mc_instance_rejected(self):
        self.install();other=self.root/'other-mc';(other/'mods').mkdir(parents=True)
        with patch.object(setup,'check_closed'),self.assertRaises((RuntimeError,FileNotFoundError)):setup.uninstall(self.bundle,self.game,other)
        self.assertTrue((self.native/setup.JOURNAL).exists())
    def test_repeated_install_and_world_name_collision_reject(self):
        world=self.mc/'saves/MCX-Wukong';world.mkdir();(world/'user.data').write_bytes(b'keep')
        with self.assertRaises(RuntimeError):self.install()
        self.assertEqual((world/'user.data').read_bytes(),b'keep')
    def test_mid_install_failure_rolls_back(self):
        before=(self.native/'CSharpLoader/b1cs.ini').read_bytes();real=setup.atomic;raised=False
        def fail_once(path,data):
            nonlocal raised
            if Path(path).name=='b1cs.ini' and not raised:raised=True;raise OSError('fixture write failure')
            return real(path,data)
        with patch.object(setup,'atomic',side_effect=fail_once),self.assertRaises(OSError):self.install()
        self.assertEqual((self.native/'CSharpLoader/b1cs.ini').read_bytes(),before)
        self.assertFalse((self.native/'CSharpLoader/Mods/DqjCombat/DqjCombat.dll').exists())
    def test_path_traversal_and_link_escape_rejected(self):
        for path in ('../other','/outside','C:/outside','a\\..\\outside'):
            with self.assertRaises(ValueError):setup.safe(self.root,path)
    def test_new_world_has_no_player_identity_or_progress(self):
        data=nbt(world_bytes())['Data'];self.assertNotIn('Player',data);self.assertEqual(data['GameType'],3)
        self.assertEqual(data['DataVersion'],4189);self.assertEqual(data['WorldGenSettings']['seed'],0)
        self.assertTrue(data['GameRules']['keepInventory']=='true')
        dims=data['WorldGenSettings']['dimensions'];layers=dims['minecraft:overworld']['generator']['settings']['layers'];self.assertEqual(layers,[{'block':'minecraft:air','height':128}])
    def test_mesh_color_data_and_empty_parts_contract(self):
        skin=self.bundle/'payload/skin.png';out=self.root/'own.mesh';parts=build_mesh(skin,out)
        self.assertEqual(len(parts),12);self.assertEqual(out.read_bytes()[:8],b'STEVEM01')
        from PIL import Image
        image=Image.open(skin).convert('RGBA');colors={tuple(q/255 for q in image.getpixel((x,y))) for x in range(64) for y in range(64)};colors.add((0,0,0,1))
        for name,pivot,vertices,indices in parts:
            self.assertEqual(len(indices)%3,0)
            self.assertTrue(all(0<=i<len(vertices) for i in indices))
            self.assertTrue(all(tuple(v[8:12]) in colors for v in vertices))

if __name__=='__main__':unittest.main()
