/* Offline only. Keep the camera projection identical to CommandWindow.ScreenOf. */
window.HarborBake = (() => {
    const T = THREE;
    const width = 784, height = 568, anchorX = 392, anchorY = 407, density = 2.3;
    const axis = .87, rise = .435;
    const elevation = Math.asin(rise / axis);
    const pixelsPerCell = 24 * axis * Math.SQRT2 * density;
    const verticalScale = 1 / (axis * Math.SQRT2 * Math.cos(elevation));
    const renderer = new T.WebGLRenderer({alpha: true, antialias: true, preserveDrawingBuffer: true});
    renderer.setSize(width, height);
    renderer.setClearColor(0, 0);
    renderer.toneMapping = T.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.14;
    renderer.outputColorSpace = T.SRGBColorSpace;
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = T.PCFSoftShadowMap;
    const studio = document.createElement('canvas');
    studio.width = 256;
    studio.height = 128;
    const ctx = studio.getContext('2d');
    const sky = ctx.createLinearGradient(0,0,0,128);
    sky.addColorStop(0,'#c8d2d7');sky.addColorStop(.48,'#a2aaa8');
    sky.addColorStop(.55,'#535e5b');sky.addColorStop(1,'#272d29');
    ctx.fillStyle=sky;ctx.fillRect(0,0,256,128);
    ctx.fillStyle='#f6ecd4';ctx.fillRect(24,17,42,15);
    const studioTexture = new T.CanvasTexture(studio);
    studioTexture.colorSpace = T.SRGBColorSpace;
    studioTexture.mapping = T.EquirectangularReflectionMapping;
    const environment = new T.PMREMGenerator(renderer).fromEquirectangular(studioTexture).texture;
    const camera = new T.OrthographicCamera(
        -anchorX/pixelsPerCell, (width-anchorX)/pixelsPerCell,
        anchorY/pixelsPerCell, (anchorY-height)/pixelsPerCell, .1, 90);
    const cameraSide = 18;
    camera.position.set(cameraSide, Math.SQRT2 * cameraSide * Math.tan(elevation), cameraSide);
    camera.lookAt(0,0,0);
    camera.updateMatrixWorld();
    const maskMaterials = new Map(), coveredLines = new Map();
    const covered = new T.MeshBasicMaterial({color: 0, opacity: 0, blending: T.NoBlending, depthWrite: true});
    const hiddenLine = new T.LineBasicMaterial({visible:false});
    let active;

    async function open(key) {
        await HarborMaster.ready;
        if (active) close();
        const model=HarborMaster.build(key), scene=new T.Scene(), scale=new T.Group();
        scale.scale.y=verticalScale;
        scale.add(model.root);
        scene.add(scale);
        scene.environment=environment;
        scene.add(new T.HemisphereLight(0xf1eee4,0x505e63,.79));
        const sun=new T.DirectionalLight(0xfff2de,3.0);
        sun.position.set(-8,13,8);
        sun.castShadow=true;
        sun.shadow.mapSize.set(1024,1024);
        Object.assign(sun.shadow.camera,{left:-7,right:7,top:7,bottom:-7,near:.1,far:36});
        sun.shadow.bias=-.00020;
        sun.shadow.normalBias=.007;
        scene.add(sun);
        const fill=new T.DirectionalLight(0xbdd4e4,.63);
        fill.position.set(6,5,-9);
        scene.add(fill);
        const pieces=[];
        model.root.traverse(object=>{
            if(!object.isMesh&&!object.isLineSegments)return;
            if(object.isMesh){object.castShadow=true;object.receiveShadow=true;}
            let role;
            if(key==='cover')
                for(let part=object;part&&!role;part=part.parent)role=part.userData.landingCover;
            pieces.push([object,object.material,role]);
        });
        active={model,scene,sun,pieces,cover:key==='cover'};
        return {density,anchorX,anchorY,width,height,parts:pieces.length,frames:HarborMaster.specs[key].frames};
    }

    function substitute(material) {
        if(material.userData.factionPaint){
            if(!maskMaterials.has(material))maskMaterials.set(material,ArchitectureKit.maskMaterial(material));
            return maskMaterials.get(material);
        }
        if(!material.isLineBasicMaterial)return covered;
        if(!coveredLines.has(material.opacity)){
            coveredLines.set(material.opacity,new T.LineBasicMaterial({color:0,opacity:material.opacity,
                transparent:true,depthWrite:false}));
        }
        return coveredLines.get(material.opacity);
    }

    function frame(index,count) {
        const phase = count===1?0:index/(count-1);
        active.model.pose(phase);
        renderer.shadowMap.autoUpdate=true;
        renderer.render(active.scene,camera);
        renderer.shadowMap.autoUpdate=false;
        function visible(role) {
            return !active.cover || role==='side' || (role==='ramp' && phase<.75);
        }
        function foregroundOnly(object) {
            return object.isLineSegments ? hiddenLine : covered;
        }
        try{
            if(active.cover){
                // Retain depth and the full hull's shadow while extracting the parts in front of the deck.
                for(const [object,material,role] of active.pieces)
                    object.material=visible(role)?material:foregroundOnly(object);
                renderer.render(active.scene,camera);
            }
            const base=renderer.domElement.toDataURL('image/png');
            for(const [object,material,role] of active.pieces)
                object.material=visible(role)?substitute(material):foregroundOnly(object);
            renderer.render(active.scene,camera);
            return {base,paint:renderer.domElement.toDataURL('image/png')};
        } finally {
            for(const [object,material] of active.pieces)object.material=material;
        }
    }

    function close(){
        const geos=new Set(), mats=new Set();
        active.model.root.traverse(object=>{
            if(object.geometry)geos.add(object.geometry);
            if(object.material)mats.add(object.material);
        });
        for(const g of geos)g.dispose();
        for(const m of mats)m.dispose();
        for(const m of maskMaterials.values())m.dispose();
        maskMaterials.clear();
        active.sun.shadow.map?.dispose();
        renderer.renderLists.dispose();
        active=null;
    }
    return {open,frame,close};
})();
