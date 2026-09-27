export async function resolve(specifier,context,next) {
 if(specifier==='@smartspectra/node-sdk')return {url:new URL('./PresageMockSdk.mjs',import.meta.url).href,shortCircuit:true};
 return next(specifier,context);
}
