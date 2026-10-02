from pathlib import Path
import subprocess,json
p=Path(r'D:\Hack the Hill\Deliverables\Trailer'); ff=str(p/'tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe')
src=r'C:\Users\obscu\Videos\Captures\HTH3 Project - TherapyRoom - Windows, Mac, Linux - Unity 6.6 (6000.6.3f1) _DX11_ 2026-09-27 16-55-54.mp4'
shots=[(174,5.3,'Outdoor exploration'),(98,5.3,'Therapy room'),(125.2,5.3,'AI companion'),(281,3.3,'Second companion'),(181,4.3,'Garden wildlife'),(223.3,8,'Aurora')]
(p/'edit-list.json').write_text(json.dumps({'shots':shots,'transition_seconds':.3,'audio_source_start':223.3,'duration':30},indent=2))
def render(item):
 i,(start,dur,name)=item
 print('Rendering',i+1,name,flush=True)
 args=[ff,'-y','-hide_banner','-loglevel','error','-threads','4','-ss',str(start),'-i',src,'-t',str(dur),'-an','-vf','crop=1380:776:260:152,scale=1280:720:flags=lanczos,minterpolate=fps=30:mi_mode=mci:mc_mode=aobmc:me_mode=bidir:me=hexbs:search_param=16:vsbmc=1,tpad=stop_mode=clone:stop_duration=0.2,setsar=1','-c:v','libx264','-preset','fast','-crf','18','-threads','4',str(p/f'review/shot-{i}.mp4')]
 subprocess.run(args,check=True)
from concurrent.futures import ThreadPoolExecutor
with ThreadPoolExecutor(max_workers=3) as pool:
 list(pool.map(render,list(enumerate(shots))[1:]))
print('SHOTS_COMPLETE',flush=True)

