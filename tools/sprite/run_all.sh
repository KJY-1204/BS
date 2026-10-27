set -e
SC=$(pwd -W); A="E:/SteamLibrary/steamapps/common/Aseprite/Aseprite.exe"; R=E:/Unity/BS/ArtSource
one(){ cfg=$1; spec=$2; out=$3; name=$4
  python -I charpipe.py $cfg.json >/dev/null 2>&1
  python -I assemble_char.py $spec.json | tail -2 | tr '\n' ' '
  mkdir -p $out/Exports
  "$A" -b --script-param manifest=$SC/manifest.lua --script-param out=$out/${name}_anim.aseprite --script build.lua >/dev/null 2>&1
  "$A" -b $out/${name}_anim.aseprite --sheet $out/Exports/${name}_sheet.png --data $out/Exports/${name}_sheet.json --format json-array --list-tags >/dev/null 2>&1
  python -I -c "
from PIL import Image
import json
m=json.load(open('manifest.json'));W,H=m['w'],m['h'];s=Image.open('sheet_char.png');n=len(m['frames'])
fr=[s.crop(((i%10)*W,(i//10)*H,(i%10)*W+W,(i//10)*H+H)) for i in range(n)]
fr[0].save(r'$out/Exports/${name}_preview.gif',save_all=True,append_images=fr[1:],duration=100,loop=0,disposal=2)"
  echo "$name done"; }
one reimu2 reimu_spec $R/Survivors/Reimu reimu
one marisa marisa_spec $R/Survivors/Marisa marisa
one sakuya sakuya_spec $R/Survivors/Sakuya sakuya
one sanae sanae_spec $R/Survivors/Sanae sanae
one sniper sniper_spec $R/Bosses/Sniper sniper
one giant giant_spec $R/Bosses/Giant giant
one std std_spec $R/Bosses/Standard standard
one dev dev_spec $R/Devices/SummonDevice device
