from pathlib import Path
import sys, tempfile, unittest
from unittest.mock import patch
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
import player_setup as setup

class Release030Tests(unittest.TestCase):
    def test_blank_client_uses_prism_discovery(self):
        with tempfile.TemporaryDirectory(dir=ROOT.parent) as tmp:
            client=Path(tmp)/'client.jar';client.write_bytes(b'fixture')
            with patch.object(setup,'find_client',return_value=str(client)):
                self.assertEqual(setup.resolve_client(Path(tmp),'  '),client)

    def test_blank_or_directory_client_has_actionable_error(self):
        with tempfile.TemporaryDirectory(dir=ROOT.parent) as tmp:
            with patch.object(setup,'find_client',return_value=''):
                for value in ('',tmp):
                    with self.subTest(value=value),self.assertRaisesRegex(RuntimeError,'field 3'):
                        setup.resolve_client(tmp,value)

    def test_reshade_ui_rewrite_accepts_only_bridge_contract(self):
        text='[GENERAL]\nEffectSearchPaths=.\\reshade-shaders\\Shaders\nTextureSearchPaths=.\\reshade-shaders\\Textures\nPresetPath=.\\DqjMCBridge.ini\nPerformanceMode=0\nSkipLoadingDisabledEffects=0\n[DEPTH]\nDepthCopyBeforeClears=1\nUseAspectRatioHeuristics=2\nDrawStatsHeuristic=1\n[OVERLAY]\nFontScale=2\n'
        with tempfile.TemporaryDirectory(dir=ROOT.parent) as tmp:
            path=Path(tmp)/'ReShade.ini';path.write_text(text,encoding='utf-8')
            self.assertTrue(setup.bridge_reshade_unchanged(path))
            path.write_text(text.replace('DqjMCBridge.ini','different.ini'),encoding='utf-8')
            self.assertFalse(setup.bridge_reshade_unchanged(path))
            path.write_text(text.replace('DrawStatsHeuristic=1','DrawStatsHeuristic=0'),encoding='utf-8')
            self.assertFalse(setup.bridge_reshade_unchanged(path))

if __name__=='__main__':unittest.main()
