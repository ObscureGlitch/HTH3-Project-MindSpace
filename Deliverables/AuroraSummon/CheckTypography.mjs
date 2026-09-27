import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
const {createCanvas,GlobalFonts}=require('C:/Users/obscu/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas');
if(!GlobalFonts.registerFromPath('D:/Unity/HTH3 Project/Assets/TherapyGame/UI/QuietGlass/Fonts/Carlito-Regular.ttf','MindSpaceSans'))throw Error('Font unavailable');
const ctx=createCanvas(8,8).getContext('2d');
const cases=[
 ['Summon northern lights',22,284],
 ['Active · 120s remaining',17,418],
 ['One gentle, two-minute display',17,418],
 ['Sets a clear night and enables aurora; fades out by dawn.',17,755],
 ['Future nights stay random. Choose Auto to resume the weather cycle.',17,755]
];
for(const [text,size,width] of cases){ctx.font=`${size}px MindSpaceSans`;if(ctx.measureText(text).width>width)throw Error(`Text clips: ${text}`);}
console.log('PASS: five summon-control text-fit cases using actual Carlito font (headless, not a Unity screenshot).');
