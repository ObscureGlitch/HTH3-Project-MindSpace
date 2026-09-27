import fs from 'node:fs';
import {createRequire} from 'node:module';
const sharp=createRequire(import.meta.url)('C:/Users/obscu/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
sharp.cache(false);sharp.concurrency(1);
const base='D:/Hack the Hill/Deliverables/ExteriorVerification';
const geometry=JSON.parse(fs.readFileSync(base+'/geometry.json','utf8').replace(/^\uFEFF/,''));
const garden=JSON.parse(fs.readFileSync('D:/Unity/HTH3 Project/Assets/TherapyGame/Exterior/Source/GardenModel.json','utf8'));
const colors={Backing:'5D4934',Cedar:'946F46',Trim:'4A4837',Oak:'B39A6D',Sage:'465D48',Soil:'473B2B',Leaves:'4E7047',Flowers:'E8C9AE',Lavender:'AC9FCA',Metal:'343F37',Lettering:'F0E8D2',LanternGlow:'E7CB97',AtticGlass:'789CA6'};
for(const m of garden.meshes.filter(m=>/Cabin roof|Cabin trim__rail|Front window frame|Porch/.test(m.name))){
 geometry[m.name]=Array.from({length:m.positions.length/3},(_,i)=>m.positions.slice(i*3,i*3+3));colors[m.name]=garden.materials.find(c=>c.name===m.material).hex;
}
const sub=(a,b)=>a.map((v,i)=>v-b[i]),dot=(a,b)=>a.reduce((v,x,i)=>v+x*b[i],0),cross=(a,b)=>[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
const norm=a=>{const l=Math.sqrt(dot(a,a));return a.map(v=>v/(l||1));};
const eye=[6.5,5,-17],target=[0,2,0],forward=norm(sub(target,eye)),right=norm(cross([0,1,0],forward)),up=norm(cross(forward,right));
const project=p=>{const d=sub(p,target);return [540+dot(d,right)*99,310-dot(d,up)*99];},light=norm([-.6,.85,-.6]);
const faces=[];
for(const [name,verts] of Object.entries(geometry))for(let i=0;i<verts.length;i+=3){
 const points=verts.slice(i,i+3),normal=norm(cross(sub(points[1],points[0]),sub(points[2],points[0])));
 if(dot(normal,forward)>-.0001)continue;
 const shade=name==='Lettering'?1:.53+.47*Math.max(0,dot(normal,light));
 const col=colors[name].replace('#','').match(/../g).map(v=>Math.round(parseInt(v,16)*shade));
 faces.push({depth:dot(sub(points[0],eye),forward),points:points.map(p=>[...project(p),dot(sub(p,eye),forward)]),col,alpha:name==='AtticGlass'?.45:1,text:'<polygon points="'+points.map(p=>project(p).map(x=>x.toFixed(2)).join(',')).join(' ')+'" fill="rgb('+col.join(',')+')"'+(name==='AtticGlass'?' opacity=".78"':'')+'/>'});
}
faces.sort((a,b)=>b.depth-a.depth);
const svg='<svg xmlns="http://www.w3.org/2000/svg" width="1080" height="620"><defs><linearGradient id="bg" x2="0" y2="1"><stop stop-color="#bed2d0"/><stop offset="1" stop-color="#e3e8d7"/></linearGradient></defs><rect width="1080" height="620" fill="url(#bg)"/><ellipse cx="530" cy="502" rx="437" ry="49" fill="#617256" opacity=".18"/>'+faces.map(f=>f.text).join('')+'<text x="24" y="590" fill="#4d605b" font-family="sans-serif" font-size="13">CPU geometry preview • existing room interiors, lighting and textures not rendered</text></svg>';
fs.writeFileSync(base+'/facade-preview.svg',svg);
const W=1080,H=620,rgba=Buffer.alloc(W*H*4),depth=new Float32Array(W*H);depth.fill(Infinity);
for(let y=0;y<H;y++)for(let x=0;x<W;x++){const n=(y*W+x)*4;rgba[n]=190+y*.06;rgba[n+1]=210+y*.035;rgba[n+2]=208-y*.008;rgba[n+3]=255;}
function raster(f){const [a,b,c]=f.points,den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1]);if(Math.abs(den)<.0001)return;
 const x0=Math.max(0,Math.floor(Math.min(a[0],b[0],c[0]))),x1=Math.min(W-1,Math.ceil(Math.max(a[0],b[0],c[0]))),y0=Math.max(0,Math.floor(Math.min(a[1],b[1],c[1]))),y1=Math.min(H-1,Math.ceil(Math.max(a[1],b[1],c[1])));
 for(let y=y0;y<=y1;y++)for(let x=x0;x<=x1;x++){const px=x+.5,py=y+.5,u=((b[1]-c[1])*(px-c[0])+(c[0]-b[0])*(py-c[1]))/den,v=((c[1]-a[1])*(px-c[0])+(a[0]-c[0])*(py-c[1]))/den,w=1-u-v;if(u<0||v<0||w<0)continue;
 const z=u*a[2]+v*b[2]+w*c[2],i=y*W+x;if(z>=depth[i])continue;if(f.alpha===1)depth[i]=z;
 for(let ch=0;ch<3;ch++)rgba[i*4+ch]=Math.round(f.col[ch]*f.alpha+rgba[i*4+ch]*(1-f.alpha));
 }}
for(const f of faces.filter(f=>f.alpha===1))raster(f);for(const f of faces.filter(f=>f.alpha<1))raster(f);
await sharp(rgba,{raw:{width:W,height:H,channels:4}}).png().toFile(base+'/facade-preview.png');
console.log('CPU facade preview written; '+faces.length+' visible triangles. No Unity/GPU render.');
