/* Assembly / collapse poses derived from the accepted v05 meshes. */
window.BuildingMotion=(()=>{
 const T=THREE,clamp=x=>Math.max(0,Math.min(1,x)),smooth=x=>{x=clamp(x);return x*x*(3-2*x);};
 function pose(key,kind,t){
  if(kind==='collapse'&&t>=1)return BuildingMaster.build(key,true);
  const root=BuildingMaster.build(key),spec=BuildingMaster.specs[key];
  if(kind==='build'&&t>=1)return root;
  root.updateMatrixWorld(true);const parts=[];root.traverse(o=>{if(o.isMesh)parts.push(o);});
  let index=0;
  for(const o of parts){
   const bounds=new T.Box3().setFromObject(o),size=bounds.getSize(new T.Vector3()),center=bounds.getCenter(new T.Vector3());
   const seed=(index++*73%101)/101,foundation=bounds.max.y<.08;
   if(foundation)continue;
   if(kind==='build'){
    const structural=size.y>.28&&bounds.min.y<.19;
    const roof=size.y<.28&&(size.x>.5||size.z>.5)&&center.y>.5;
    const start=structural?.10+seed*.08:roof?.49+seed*.08:.52+clamp(center.y/2)*.20+seed*.09;
    const end=structural?.56:roof?.86:.98,progress=smooth((t-start)/(end-start));
    o.visible=progress>0;
    if(structural){o.scale.y*=Math.max(.001,progress);o.position.y=o.position.y*progress+.055*(1-progress);}
    else if(roof){o.position.y+=(1-progress)*.24;}
    else{o.scale.multiplyScalar(.2+.8*progress);}
   }else{
    const fall=smooth((t-.04-seed*.14)/.76);
    o.scale.y*=Math.max(.12,1-fall*.88);o.position.y=o.position.y*(1-fall*.88)+.055*fall*.88;
    o.position.x+=(seed-.5)*fall*.11;o.position.z+=(.5-seed)*fall*.10;
    o.rotation.z+=(seed-.5)*fall*.45;
    o.visible=t<.82+seed*.15;
   }
  }
  if(kind==='collapse'){
   const wreck=BuildingMaster.build(key,true);wreck.scale.y=smooth(t/.7);root.add(wreck);
  }else if(t<.92){
   // A real structural skeleton in the foundation, removed as panels settle.
   const height=Math.max(.02,smooth(t/.28)*(1-smooth((t-.78)/.14))*1.12);
   const mat=new T.MeshStandardMaterial({color:0x646d64,roughness:.8,metalness:.25});
   const beam=(x,y,z,w,h,d)=>{const o=new T.Mesh(new T.BoxGeometry(w,h,d),mat);o.position.set(x,y,z);o.castShadow=o.receiveShadow=true;root.add(o);};
   for(const x of [-1,1])for(const z of [-1,1])beam(x*(spec.w/2-.16),height/2+.06,z*(spec.d/2-.16),.035,height,.035);
   for(const z of [-1,1])beam(0,height+.06,z*(spec.d/2-.16),spec.w-.29,.035,.035);
   for(const x of [-1,1])beam(x*(spec.w/2-.16),height+.06,0,.035,.035,spec.d-.29);
  }
  return root;
 }
 return {pose};
})();
