"""Create a new, empty 1.21.4 bridge world. Never reads or edits an existing save."""
from pathlib import Path
import gzip,struct,time

def tag(kind,value):return (kind,value)
def text(value):return tag(8,value)
def integer(value):return tag(3,value)
def byte(value):return tag(1,value)
def long(value):return tag(4,value)
def compound(value):return tag(10,value)
def listing(kind,values):return tag(9,(kind,values))
def utf(value):
    b=value.encode('utf-8');return struct.pack('>H',len(b))+b
def payload(kind,value):
    if kind in (1,2,3,4,5,6):return struct.pack({1:'>b',2:'>h',3:'>i',4:'>q',5:'>f',6:'>d'}[kind],value)
    if kind==8:return utf(value)
    if kind==9:
        t,values=value;return bytes([t])+struct.pack('>i',len(values))+b''.join(payload(t,v) for v in values)
    if kind==10:return b''.join(bytes([t])+utf(n)+payload(t,v) for n,(t,v) in value.items())+b'\0'
    raise ValueError(kind)

def world_bytes():
    def noise(dimension,settings,source):
        return compound({'type':text(dimension),'generator':compound({'type':text('minecraft:noise'),'settings':text(settings),'biome_source':compound(source)})})
    dimensions={
      'minecraft:overworld':compound({'type':text('minecraft:overworld'),'generator':compound({'type':text('minecraft:flat'),'settings':compound({'biome':text('minecraft:plains'),'features':byte(0),'lakes':byte(0),'layers':listing(10,[{'block':text('minecraft:air'),'height':integer(128)}]),'structure_overrides':listing(8,[])})})}),
      'minecraft:the_nether':noise('minecraft:the_nether','minecraft:nether',{'type':text('minecraft:multi_noise'),'preset':text('minecraft:nether')}),
      'minecraft:the_end':noise('minecraft:the_end','minecraft:end',{'type':text('minecraft:the_end')})}
    rules={'keepInventory':'true','doMobSpawning':'false','doWeatherCycle':'false','doDaylightCycle':'false','sendCommandFeedback':'false'}
    data={'DataVersion':integer(4189),'version':integer(19133),'Version':compound({'Id':integer(4189),'Name':text('1.21.4'),'Series':text('main'),'Snapshot':byte(0)}),
      'LevelName':text('MCX-Wukong'),'GameType':integer(3),'allowCommands':byte(1),'hardcore':byte(0),'initialized':byte(1),'Difficulty':byte(2),'DifficultyLocked':byte(0),
      'SpawnX':integer(0),'SpawnY':integer(64),'SpawnZ':integer(0),'SpawnAngle':tag(5,0),'Time':long(0),'DayTime':long(6000),'LastPlayed':long(int(time.time()*1000)),
      'WasModded':byte(1),'ServerBrands':listing(8,['fabric']),'enabled_features':listing(8,['minecraft:vanilla']),'removed_features':listing(8,[]),
      'GameRules':compound({k:text(v) for k,v in rules.items()}),
      'DataPacks':compound({'Enabled':listing(8,['vanilla','fabric','file/mcx-starter']),'Disabled':listing(8,[])}),
      'WorldGenSettings':compound({'seed':long(0),'generate_features':byte(0),'bonus_chest':byte(0),'dimensions':compound(dimensions)})}
    return gzip.compress(b'\x0a\x00\x00'+payload(10,{'Data':compound(data)}),mtime=0)

def create(target):
    target=Path(target)
    if target.exists():return False
    target.mkdir(parents=True,exist_ok=False)
    (target/'level.dat').write_bytes(world_bytes())
    (target/'mcx-world.json').write_text('{"version":1,"purpose":"MCX-Wukong empty single-player bridge world"}',encoding='utf-8')
    pack=target/'datapacks/mcx-starter';functions=pack/'data/mcx/function';functions.mkdir(parents=True)
    (pack/'pack.mcmeta').write_text('{"pack":{"pack_format":61,"description":"MCX optional starter items"}}',encoding='utf-8')
    (functions/'starter.mcfunction').write_text('execute unless entity @s[tag=mcx_starter_received] run function mcx:starter_items\n',encoding='utf-8')
    commands=['give @s minecraft:diamond_sword','give @s minecraft:shield','give @s minecraft:diamond_pickaxe','give @s minecraft:bow','give @s minecraft:arrow 128','give @s minecraft:oak_log 64','give @s minecraft:cobblestone 64','give @s minecraft:cooked_beef 64','give @s minecraft:tnt 64','give @s minecraft:flint_and_steel','give @s minecraft:elytra','give @s minecraft:firework_rocket 64','tag @s add mcx_starter_received']
    (functions/'starter_items.mcfunction').write_text('\n'.join(commands)+'\n',encoding='utf-8')
    return True
