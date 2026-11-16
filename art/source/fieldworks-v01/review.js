/* Offline acceptance boards at the game's default projection and a larger inspection scale. */
window.FieldworksReview = (() => {
    const frameSize = 320;
    const colors = { neutral: [222,225,221], blue: [62,133,190], red: [184,66,51], green: [74,146,82] };
    const images = new Map();
    const path = (value) => new URL(value, document.baseURI).href;
    const image = (src) => {
        if (!images.has(src)) {
            const bitmap = new Image();
            bitmap.src = src;
            images.set(src, bitmap.decode().then(() => bitmap));
        }
        return images.get(src);
    };
    function canvas(width, height) {
        const bitmap = document.createElement("canvas");
        bitmap.width = width;
        bitmap.height = height;
        return bitmap;
    }
    function rgb(values) {
        return "rgb(" + values.join(",") + ")";
    }
    async function layered(base, paint, tint, sourceX = 0, sourceY = 0, sourceSize = frameSize) {
        const result = canvas(frameSize, frameSize);
        const ctx = result.getContext("2d", { willReadFrequently: true });
        const [body, mask] = await Promise.all([image(base), image(paint)]);
        ctx.drawImage(mask, sourceX, sourceY, sourceSize, sourceSize, 0, 0, frameSize, frameSize);
        const contribution = ctx.getImageData(0, 0, frameSize, frameSize).data;
        ctx.clearRect(0, 0, frameSize, frameSize);
        ctx.drawImage(body, sourceX, sourceY, sourceSize, sourceSize, 0, 0, frameSize, frameSize);
        const pixels = ctx.getImageData(0, 0, frameSize, frameSize);
        for (let i = 0; i < pixels.data.length; i += 4) {
            const opacity = pixels.data[i + 3] / 255;
            if (!opacity) continue;
            const weight = contribution[i + 3] / 255;
            for (let channel = 0; channel < 3; channel++)
                pixels.data[i + channel] -= contribution[i + channel] * weight * (1 - tint[channel] / 255) / opacity;
        }
        ctx.putImageData(pixels, 0, 0);
        return result;
    }
    function title(ctx, text, line) {
        ctx.fillStyle = "#1c282a";
        ctx.fillRect(0, 0, ctx.canvas.width, 111);
        ctx.fillStyle = "#d5ae66";
        ctx.fillRect(40, 33, 4, 49);
        ctx.fillStyle = "#f2ece0";
        ctx.font = "600 32px 'Microsoft YaHei', sans-serif";
        ctx.fillText(text, 60, 55);
        ctx.font = "17px 'Microsoft YaHei', sans-serif";
        ctx.fillStyle = "#a8bab6";
        ctx.fillText(line, 61, 85);
    }
    function text(ctx, value, x, y, color = "#e9e6dc", size = 17) {
        ctx.fillStyle = color;
        ctx.font = size + "px 'Microsoft YaHei', sans-serif";
        ctx.fillText(value, x, y);
    }
    function project(x, z, origin, scale) {
        return [origin[0] + (x - z) * scale, origin[1] + (x + z) * scale / 2];
    }
    function ground(ctx, x0, z0, x1, z1, origin, scale) {
        ctx.beginPath();
        for (const [x, z] of [[x0,z0],[x1,z0],[x1,z1],[x0,z1]]) ctx.lineTo(...project(x, z, origin, scale));
        ctx.closePath();
    }
    function sprite(ctx, bitmap, x, y, footprint, scale, anchor = 220 / 320) {
        const size = footprint * scale * frameSize / 224;
        ctx.drawImage(bitmap, x - size / 2, y - size * anchor, size, size);
    }
    const maskFor = (set, x, z) => [[0,1],[1,0],[0,-1],[-1,0]]
        .reduce((mask,[dx,dz],index) => mask | (set.has((x + dx) + ":" + (z + dz)) ? 1 << index : 0), 0);
    async function renderScene(frames, showOilwell = true) {
        const board = canvas(1344, 774);
        const ctx = board.getContext("2d");
        ctx.imageSmoothingQuality = "high";
        const origin = [682, 397], scale = 24 * .87 * 1.55;
        const atlas = await image(path("../isometric/ground.png"));
        const makePattern = (index) => {
            const width = atlas.width / 2, height = atlas.height / 2;
            const patch = canvas(width, height);
            patch.getContext("2d").drawImage(atlas, index % 2 * width + 2, Math.floor(index / 2) * height + 2,
                width - 4, height - 4, 0, 0, width, height);
            const repeat = ctx.createPattern(patch, "repeat");
            repeat.setTransform(new DOMMatrix().scale(.32 * 1.55, .22 * 1.55));
            return repeat;
        };
        ctx.fillStyle = makePattern(0);
        ctx.fillRect(0, 0, board.width, board.height);
        ctx.fillStyle = "#58633a2a";
        ctx.fillRect(0, 0, board.width, board.height);
        const paving = makePattern(1), asphalt = makePattern(3);
        function paved(x0, z0, x1, z1, surface, glaze) {
            ground(ctx, x0, z0, x1, z1, origin, scale);
            ctx.fillStyle = surface;
            ctx.fill();
            ctx.fillStyle = glaze;
            ctx.fill();
        }
        paved(-8,-6,8,4,paving,"#827c6d2d");
        paved(-31,5,31,8,asphalt,"#2f383846");
        paved(5,-6,8,5,paving,"#837e7029");
        ctx.strokeStyle = "#cec4a58a";
        ctx.lineWidth = 1.7;
        ctx.setLineDash([12, 18]);
        ctx.beginPath();
        ctx.moveTo(...project(-30,6.5,origin,scale));
        ctx.lineTo(...project(30,6.5,origin,scale));
        ctx.stroke();
        ctx.setLineDash([]);

        const entries = [];
        function shadow(x,z,w,d) {
            ground(ctx,x-w/2+.08,z-d/2+.11,x+w/2+.14,z+d/2+.21,origin,scale);
            ctx.fillStyle = "#171f1b35";
            ctx.fill();
        }
        function add(x, z, bitmap, footprint, w, d, anchor) {
            shadow(x, z, w, d);
            entries.push({ x, z, bitmap, footprint, anchor });
        }
        const wallPoints = [
            ...Array.from({length:11},(_, i) => [i - 7,4]).filter(([x]) => x !== -1 && x !== 0),
            ...Array.from({length:6},(_, i) => [-7,i - 2]),
            [3,3],[3,2],[3,1],[4,2],[5,2],[3,0]
        ];
        const wallSet = new Set(wallPoints.map(point => point.join(":")));
        const walls = new Map();
        for (const [x,z] of wallPoints) {
            const bits = maskFor(wallSet,x,z);
            if (!walls.has(bits)) {
                const entry = frames["wall-" + bits.toString(16)];
                walls.set(bits, layered(entry.base, entry.paint, colors.blue));
            }
            add(x+.5,z+.5,await walls.get(bits),2,.29,.29);
        }
        for (const [key, x, z] of [["oilwell",-4.0,-.1],["refinery",.95,-1.1],["post",4.5,-3.5]]) {
            const width = key === "post" ? 1 : 2;
            if (key === "oilwell" && !showOilwell) {
                shadow(x,z,width*.93,width*.93);
                continue;
            }
            const data = frames[key];
            add(x,z,await layered(data.base,data.paint,colors.blue),2*width,width*.93,width*.93);
        }
        const tank = await layered(path("../tank-prototype/packed/tank-base.png"),path("../tank-prototype/packed/tank-paint.png"),colors.blue,0,512,512);
        add(-.1,6.6,tank,Math.SQRT2 * 9.25 / 4.845 * 224 / frameSize,1.06,1.06,188/320);
        add(5.2,6.4,tank,Math.SQRT2 * 9.25 / 4.845 * 224 / frameSize,1.06,1.06,188/320);
        entries.sort((a,b) => a.x+a.z-b.x-b.z);
        for (const item of entries) sprite(ctx,item.bitmap,...project(item.x,item.z,origin,scale),item.footprint,scale,item.anchor);
        title(ctx,"围墙与采油井","默认战场比例 1.55× · 同屏参照为现有精炼厂、哨戒炮和主战坦克");
        return board.toDataURL();
    }

    async function renderDetails(frames) {
        const board = canvas(1344, 855);
        const ctx = board.getContext("2d");
        ctx.imageSmoothingQuality = "high";
        ctx.fillStyle = "#202a2b";
        ctx.fillRect(0, 0, board.width, board.height);
        title(ctx,"模型细节与染色","采油井占 2 × 2 格 · 每段围墙占 1 格");
        const samples = [
            ["oilwell","neutral","中立"],
            ["oilwell-90","blue","蓝方 · 侧面"],
            ["oilwell-180","red","红方 · 背面"],
            ["oilwell-270","green","绿方 · 另一侧"]
        ];
        for (let i=0;i<samples.length;i++) {
            const [key,color,name] = samples[i];
            const at = [173+i*332, 333];
            ground(ctx,-1,-1,1,1,at,58);
            ctx.fillStyle = "#263334";ctx.fill();ctx.strokeStyle = "#71817958";ctx.stroke();
            for (const endpoints of [[-1,0,1,0],[0,-1,0,1]]) {
                ctx.beginPath();
                ctx.moveTo(...project(endpoints[0],endpoints[1],at,58));
                ctx.lineTo(...project(endpoints[2],endpoints[3],at,58));
                ctx.stroke();
            }
            const entry = frames[key];
            sprite(ctx,await layered(entry.base,entry.paint,colors[color]),...at,4,58);
            ctx.fillStyle = rgb(colors[color]);
            ctx.fillRect(at[0]-58,408,9,9);
            text(ctx,name,at[0]-39,418);
        }
        ctx.fillStyle = "#4c605c";
        ctx.fillRect(39, 463, 1266, 1);
        const layouts = [
            {name:"直段与端头", points:[[0,0],[1,0],[2,0]], center:[148,651]},
            {name:"转角", points:[[0,0],[1,0],[1,1]], center:[395,651]},
            {name:"三通", points:[[0,0],[1,0],[2,0],[1,1]], center:[638,651]},
            {name:"十字", points:[[0,0],[1,0],[2,0],[1,-1],[1,1]], center:[878,651]},
            {name:"缺口", points:[[0,0],[1,0],[3,0],[4,0]], center:[1153,651]}
        ];
        for (const layout of layouts) {
            const walls = new Set(layout.points.map(point => point.join(":")));
            const length = Math.max(...layout.points.map(([x,z]) => x-z));
            const minimum = Math.min(...layout.points.map(([x,z]) => x-z));
            const scale = 55;
            const origin = [layout.center[0]-(length+minimum)*scale/2, layout.center[1]-53];
            for (const [x,z] of layout.points) {
                ground(ctx,x-.5,z-.5,x+.5,z+.5,origin,scale);
                ctx.fillStyle="#293738";ctx.fill();ctx.strokeStyle="#71817980";ctx.stroke();
            }
            for (const [x,z] of [...layout.points].sort((a,b) => a[0]+a[1]-b[0]-b[1])) {
                const key = "wall-" + maskFor(walls,x,z).toString(16);
                const entry = frames[key];
                sprite(ctx,await layered(entry.base,entry.paint,colors.blue),...project(x,z,origin,scale),2,scale);
            }
            text(ctx,layout.name,layout.center[0]-47,791);
        }
        return board.toDataURL();
    }
    async function atlas(frames, keys, layer, columns) {
        const bitmap = canvas(columns*frameSize,Math.ceil(keys.length/columns)*frameSize);
        const ctx = bitmap.getContext("2d");
        for (const [index,key] of keys.entries())
            ctx.drawImage(await image(frames[key][layer]),index%columns*frameSize,Math.floor(index/columns)*frameSize);
        return bitmap.toDataURL();
    }
    async function readAtlas(name, keys, columns) {
        const frames = Object.fromEntries(keys.map(key => [key,{}]));
        for (const layer of ["base","paint"]) {
            const sheet = await image(path("../fieldworks-v01/sprites/" + name + "-" + layer + ".png"));
            const bitmap = canvas(frameSize,frameSize);
            const ctx = bitmap.getContext("2d");
            for (const [index,key] of keys.entries()) {
                ctx.clearRect(0,0,frameSize,frameSize);
                ctx.drawImage(sheet,index%columns*frameSize,Math.floor(index/columns)*frameSize,
                    frameSize,frameSize,0,0,frameSize,frameSize);
                frames[key][layer] = bitmap.toDataURL();
            }
        }
        return frames;
    }
    async function renderOilComparison(before, after, paintReview = false) {
        const board = canvas(1272,876);
        const ctx = board.getContext("2d");
        ctx.imageSmoothingQuality = "high";
        ctx.fillStyle = "#202a2b";
        ctx.fillRect(0,0,board.width,board.height);
        title(ctx,paintReview ? "采油井势力涂装" : "采油井模型细化","同视角、同尺寸、同一势力配色");
        text(ctx,"上一版",270,165,"#b3c0ba",22);
        text(ctx,paintReview ? "扩大涂装后" : "细化版",paintReview ? 880 : 904,165,"#e2ba76",22);
        ctx.fillStyle = "#455753";
        ctx.fillRect(635,147,1,682);
        ctx.fillRect(44,505,1183,1);
        for (const [index,key] of ["oilwell","oilwell-180"].entries()) {
            for (const [column,frame] of [before[key],after[key]].entries()) {
                const scale = 83;
                const at = [318 + 636*column,397 + 350*index];
                ground(ctx,-1,-1,1,1,at,scale);
                ctx.fillStyle = "#273536";ctx.fill();
                ctx.strokeStyle = "#71817958";ctx.stroke();
                sprite(ctx,await layered(frame.base,frame.paint,colors.blue),...at,4,scale);
                text(ctx,index === 0 ? "井口与储油罐" : "曲柄、连杆和支架",at[0]-62,at[1]+94);
            }
        }
        return board.toDataURL();
    }
    async function idleBackdrop(frames) {
        const board = canvas(1024,740);
        const ctx = board.getContext("2d");
        ctx.fillStyle="#202a2b";ctx.fillRect(0,0,1024,740);
        title(ctx,"采油井待机","6 秒一轮 · 正面与背面使用同一套传动机构");
        for (const x of [268,756]) {
            ground(ctx,-1,-1,1,1,[x,315],66);
            ctx.fillStyle="#273536";ctx.fill();
            ctx.strokeStyle="#71817958";ctx.stroke();
        }
        text(ctx,"井口与摆梁",221,399);
        text(ctx,"驱动曲柄与连杆",693,399);
        ctx.fillStyle="#455753";ctx.fillRect(39,419,946,1);
        const terrain=await image(await renderScene(frames,false));
        ctx.drawImage(terrain,231,198,858,270,83,451,858,270);
        text(ctx,"战场默认缩放 1.55×",83,444,"#a8bab6",15);
        return board;
    }
    async function idlePreview(backdrop, front, rear) {
        const board=canvas(backdrop.width,backdrop.height),ctx=board.getContext("2d");
        ctx.imageSmoothingQuality="high";
        ctx.drawImage(backdrop,0,0);
        const [frontBlue,rearBlue] = await Promise.all([
            layered(front.base,front.paint,colors.blue),
            layered(rear.base,rear.paint,colors.blue)
        ]);
        sprite(ctx,frontBlue,268,315,4,66);
        sprite(ctx,rearBlue,756,315,4,66);
        const scale=24*.87*1.55;
        const [x,y]=project(-4,-.1,[682,397],scale);
        sprite(ctx,frontBlue,x-231+83,y-198+451,4,scale);
        images.delete(front.base);images.delete(front.paint);
        images.delete(rear.base);images.delete(rear.paint);
        return board.toDataURL();
    }
    return { renderScene, renderDetails, atlas, readAtlas, renderOilComparison, idleBackdrop, idlePreview };
})();
