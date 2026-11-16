const fs = require("node:fs/promises");
const http = require("node:http");
const path = require("node:path");
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || "playwright");

const root = path.resolve(__dirname, "..");
const output = path.join(root, "art/source/fieldworks-v01");
const walls = Array.from({ length: 16 }, (_, mask) => "wall-" + mask.toString(16));
const angles = ["oilwell", "oilwell-90", "oilwell-180", "oilwell-270"];
const comparison = ["refinery", "post"];
const oilOnly = process.argv.includes("--oilwell");
const oilIdle = process.argv.includes("--oilwell-idle");
const paintReview = process.argv.includes("--paint-review");
const mime = { ".js": "text/javascript", ".html": "text/html", ".png": "image/png" };

const server = http.createServer(async (request, response) => {
    try {
        const pathname = decodeURIComponent(new URL(request.url, "http://localhost").pathname);
        const file = path.resolve(root, "." + pathname);
        if (!file.startsWith(root + path.sep)) {
            response.writeHead(403).end();
            return;
        }
        const contents = await fs.readFile(file);
        response.setHeader("Content-Type", mime[path.extname(file)] || "application/octet-stream");
        response.end(contents);
    } catch {
        response.writeHead(404).end();
    }
});

async function png(relative, data) {
    const filename = path.join(output, relative);
    await fs.mkdir(path.dirname(filename), { recursive: true });
    await fs.writeFile(filename, Buffer.from(data.split(",")[1], "base64"));
}

async function bake(page, key, keepModel) {
    return page.evaluate(async ({ key, keepModel }) => {
        const bake = BuildingBakeV08;
        const stats = await bake.open(key);
        const frame = bake.endpoint();
        if (frame.audit.paintInk < 35) throw Error("Paint contribution is missing for " + key);
        if (!keepModel) return { ...frame, stats };

        const master = BuildingMasterV08.build(key);
        const spec = BuildingMasterV08.specs[key];
        master.updateMatrixWorld(true);
        const bounds = new THREE.Box3().setFromObject(master);
        if (bounds.min.x < -spec.w / 2 - .014 || bounds.max.x > spec.w / 2 + .014 ||
            bounds.min.z < -spec.d / 2 - .014 || bounds.max.z > spec.d / 2 + .014 ||
            Math.abs(bounds.min.y) > .013)
            throw Error("Master exceeds its ground cells: " + key + " " + JSON.stringify(bounds));

        const model = master.toJSON();
        const geometries = new Set();
        master.traverse(node => { if (node.geometry) geometries.add(node.geometry); });
        model.geometries = Array.from(geometries, geometry => {
            const copy = new THREE.BufferGeometry().copy(geometry);
            copy.uuid = geometry.uuid;
            const json = copy.toJSON();
            copy.dispose();
            return json;
        });
        delete model.shapes;
        const materialSources = new Map();
        const textures = new Map(model.textures.map(texture => [texture.uuid, texture.source || texture.image]));
        for (const material of model.materials) {
            if (!material.map) continue;
            const name = ["wall", "concrete"].includes(material.name) ? "cast-concrete.png" : "coated-armor.png";
            materialSources.set(textures.get(material.map), "../../buildings-v09/materials/" + name);
        }
        for (const source of model.images) {
            if (!materialSources.has(source.uuid)) throw Error("Unmapped model texture for " + key);
            source.url = materialSources.get(source.uuid);
        }
        if (key === "oilwell" || key === "wall-f") {
            const loader = new THREE.ObjectLoader().setResourcePath(new URL("../fieldworks-v01/models/", document.baseURI).href);
            const saved = await loader.parseAsync(model);
            const loadedBounds = new THREE.Box3().setFromObject(saved);
            if (loadedBounds.min.distanceTo(bounds.min) > .0001 || loadedBounds.max.distanceTo(bounds.max) > .0001)
                throw Error("Reloaded model bounds differ for " + key);
        }
        for (const geometry of geometries) geometry.dispose();
        return { ...frame, stats, model, bounds: [bounds.min.toArray(),bounds.max.toArray()] };
    }, { key, keepModel });
}

async function exportOilIdle(page) {
    const framesPerSecond = 24;
    const count = framesPerSecond * 6;
    const columns = 12;
    const previewPath = process.env.GREYHAVEN_IDLE_REVIEW_FRAMES;
    const views = [["front","oilwell"],["rear","oilwell-180"]];
    const details = {};
    const scene = {};
    if (previewPath) {
        Object.assign(scene, await page.evaluate(walls => FieldworksReview.readAtlas("wall",walls,4),walls));
        for (const key of comparison) {
            const {base,paint} = await bake(page,key,false);
            scene[key] = { base,paint };
        }
        await page.evaluate(async frames => {
            window.oilIdleBackdrop=await FieldworksReview.idleBackdrop(frames);
        },scene);
        await fs.mkdir(previewPath,{recursive:true});
    }
    await page.evaluate(() => { window.oilIdleFrames={}; });
    for (const [view,key] of views) {
        const master = await bake(page,key,view==="front");
        if (master.model) {
            await fs.writeFile(path.join(output,"models","oilwell-idle.json"),JSON.stringify(master.model));
        }
        const audit = await page.evaluate(({key,view}) => {
            const sample=BuildingMasterV08.build(key);
            const result=OilwellIdle.audit(sample);
            const geometries=new Set();
            sample.traverse(node=>{if(node.geometry)geometries.add(node.geometry);});
            for (const geometry of geometries) geometry.dispose();
            window.oilIdleFrames[view]=[];
            return result;
        },{key,view});
        console.log(view + " travel " + audit.carrierTravel.toFixed(3) + " cells");
        const bounds=[320,320,0,0];
        let minPaintFraction=1,maxPaintFraction=0;
        for (let index=0;index<count;index++) {
            const frame = await page.evaluate(({view,phase}) => {
                OilwellIdle.capture(phase);
                try {
                    const {base,paint,audit}=BuildingBakeV08.endpoint();
                    if (audit.paintInk<35) throw Error("Oilwell idle lost faction paint");
                    window.oilIdleFrames[view].push({base,paint});
                    return audit;
                } finally {
                    OilwellIdle.capture(null);
                }
            },{view,phase:index/count});
            for (let coordinate=0;coordinate<4;coordinate++)
                bounds[coordinate]=coordinate<2?Math.min(bounds[coordinate],frame.bounds[coordinate]):Math.max(bounds[coordinate],frame.bounds[coordinate]);
            minPaintFraction=Math.min(minPaintFraction,frame.paintFraction);
            maxPaintFraction=Math.max(maxPaintFraction,frame.paintFraction);
            if ((index+1)%36===0) console.log(view + " " + (index+1) + "/" + count);
        }
        for (const layer of ["base","paint"]) {
            const sheet=await page.evaluate(({view,layer,columns})=>{
                const frames=window.oilIdleFrames[view];
                return FieldworksReview.atlas(frames,frames.map((_,i)=>i),layer,columns);
            },{view,layer,columns});
            await png(`idle/oilwell-${view}-${layer}.png`,sheet);
        }
        details[view]={ angle:view==="front"?0:180,bounds,minPaintFraction,maxPaintFraction,...audit };
    }
    if (previewPath) {
        for (let index=0;index<count;index++) {
            const frame=await page.evaluate(index=>FieldworksReview.idlePreview(window.oilIdleBackdrop,
                window.oilIdleFrames.front[index],window.oilIdleFrames.rear[index]),index);
            await fs.writeFile(path.join(previewPath,String(index).padStart(3,"0")+".png"),
                Buffer.from(frame.split(",")[1],"base64"));
        }
    }
    await fs.writeFile(path.join(output,"idle","manifest.json"),JSON.stringify({
        framesPerSecond,frames:count,seconds:count/framesPerSecond,columns,cell:320,anchor:[160,220],views:details
    },null,2)+"\n");
}

async function run() {
    await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
    const browser = await chromium.launch({ channel: "msedge", headless: true });
    try {
        const page = await browser.newPage();
        const errors = [];
        page.on("pageerror", error => errors.push(error.message));
        await page.goto(`http://127.0.0.1:${server.address().port}/art/source/fieldworks-v01/index.html`);
        await fs.mkdir(path.join(output, "models"), { recursive: true });
        if (oilIdle) {
            await exportOilIdle(page);
            if (errors.length) throw Error(errors.join("\n"));
            return;
        }
        const frames = oilOnly ? await page.evaluate(walls => FieldworksReview.readAtlas("wall",walls,4),walls) : {};
        const before = oilOnly ? await page.evaluate(angles => FieldworksReview.readAtlas("oilwell",angles,4),angles) : null;
        const metadata = oilOnly ? JSON.parse(await fs.readFile(path.join(output,"audit.json"),"utf8")).models : {};
        const requested = oilOnly ? angles : [...walls,...angles];
        for (const key of [...requested, ...comparison]) {
            const keepModel = walls.includes(key) || key === "oilwell";
            const entry = await bake(page, key, keepModel);
            frames[key] = { base: entry.base, paint: entry.paint };
            if (entry.model) {
                await fs.writeFile(path.join(output, "models", key + ".json"), JSON.stringify(entry.model));
                metadata[key] = { ...entry.stats, bounds: entry.bounds, frame: entry.audit };
            } else if (angles.includes(key)) {
                metadata[key] = { ...entry.stats, frame: entry.audit };
            }
            console.log(key, entry.audit.ink, entry.audit.paintInk);
        }
        for (const [key, keys, columns] of [["wall", walls, 4], ["oilwell", angles, 4]]) {
            if (oilOnly && key === "wall") continue;
            for (const layer of ["base", "paint"]) {
                const sheet = await page.evaluate(({ frames, keys, layer, columns }) =>
                    FieldworksReview.atlas(frames, keys, layer, columns), { frames, keys, layer, columns });
                await png(`sprites/${key}-${layer}.png`, sheet);
            }
        }
        const scene = await page.evaluate(frames => FieldworksReview.renderScene(frames), frames);
        await png("review/战场比例.png", scene);
        const detail = await page.evaluate(frames => FieldworksReview.renderDetails(frames), frames);
        await png("review/建模细节.png", detail);
        if (before) {
            const revisions = await page.evaluate(({before, frames,paintReview}) =>
                FieldworksReview.renderOilComparison(before,frames,paintReview),{before,frames,paintReview});
            await png(paintReview ? "review/采油井染色前后对照.png" : "review/采油井前后对照.png",revisions);
        }
        if (errors.length) throw Error(errors.join("\n"));
        await fs.writeFile(path.join(output, "audit.json"), JSON.stringify({
            frameSize: 320, anchor: [160,220], footprintSpan: 224,
            wallBits: { positiveZ: 1, positiveX: 2, negativeZ: 4, negativeX: 8 },
            wallOrder: walls, oilwellOrder: angles, models: metadata
        }, null, 2) + "\n");
    } finally {
        await browser.close();
    }
}

run().catch(error => { console.error(error); process.exitCode = 1; }).finally(() => server.close());
