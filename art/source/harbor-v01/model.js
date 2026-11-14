/* Editable harbor models. Coordinates and pivots are in Greyhaven ground cells. */
window.HarborMaster = (() => {
    const T = THREE, Kit = ArchitectureKit, halfTurn = Math.PI / 2;
    const specs = {
        landing: {width: 4.8, depth: 2.04, frames: 29},
        cover: {width: 4.8, depth: 2.04, frames: 29},
        supply: {width: 9.18, depth: 2.92, frames: 37},
        office: {width: 2, depth: 2, frames: 2},
        crane: {width: 1.12, depth: 1.12, frames: 101},
        gate: {width: .86, depth: 6.76, frames: 29},
        cargo: {width: .82, depth: .46, frames: 1},
        bollard: {width: .42, depth: .36, frames: 1}
    };
    const clamp = t => Math.min(1, Math.max(0, t));
    const ease = t => { t = clamp(t); return t * t * (3 - 2 * t); };
    const between = (a, b, t) => a + (b - a) * ease(t);

    function kit(key) {
        const {width, depth} = specs[key];
        return Kit.create(key, {w: width, d: depth});
    }

    // Waterline, rubbing strake and topsides use a common tapered hull mesh.
    function hull(k, group, outline, deck) {
        const layers = [
            {h: -.09, scale: .84}, {h: .025, scale: .91},
            {h: .115, scale: .96}, {h: deck - .095, scale: 1},
            {h: deck, scale: .985}
        ];
        const materials = ['rubber', 'brick', 'paint', 'steel'];
        function mesh(points, material) {
            const geo = new T.BufferGeometry();
            geo.setAttribute('position', new T.Float32BufferAttribute(points, 3));
            const uv = [];
            for (let i = 0; i < points.length; i += 3)
                uv.push((points[i] + .57 * points[i + 2]) / .83,
                    (points[i + 1] + .43 * points[i + 2]) / .64);
            geo.setAttribute('uv', new T.Float32BufferAttribute(uv, 2));
            geo.computeVertexNormals();
            const face = new T.Mesh(geo, k.materials[material]);
            face.castShadow = face.receiveShadow = true;
            group.add(face);
        }
        const p = (i, j) => [outline[i][0] * layers[j].scale, layers[j].h, outline[i][1] * layers[j].scale];
        for (let j = 0; j < materials.length; j++) {
            const faces = [];
            for (let i = 0; i < outline.length; i++) {
                const n = (i + 1) % outline.length;
                const a = p(i, j), b = p(n, j), c = p(i, j + 1), d = p(n, j + 1);
                faces.push(...a, ...c, ...b, ...b, ...c, ...d);
            }
            mesh(faces, materials[j]);
        }
        const floor = [];
        for (let i = 0; i < outline.length; i++)
            floor.push(0, deck, 0, ...p((i + 1) % outline.length, 4), ...p(i, 4));
        mesh(floor, 'steel');
    }

    function tieDown(k, x, y, z) {
        k.bevel(x, y, z, .10, .028, .08, 'edge', .007);
        k.box(x, y + .016, z, .055, .006, .038, 'dark');
    }

    function cargoBox(k, x, y, z, material = 'brick') {
        k.bevel(x, y + .23, z, .82, .46, .46, material, .012);
        k.box(x, y + .465, z, .78, .021, .44, 'edge');
        for (const end of [-1, 1]) {
            k.box(x + end * .398, y + .23, z, .031, .44, .465, 'steel');
            for (const corner of [-1, 1])
                k.box(x + end * .395, y + .065, z + corner * .21, .057, .063, .048, 'edge');
        }
        for (let i = -3; i <= 3; i++)
            k.box(x + .096 * i, y + .23, z + .237, .023, .36, .018, 'edge');
        k.box(x + .42, y + .24, z, .018, .37, .018, 'dark');
        for (const p of [-.11, .11])
            k.box(x + .43, y + .24, z + p, .012, .36, .014, 'edge');
    }

    function landing() {
        const k = kit('landing'), deck = .36;
        const {part, moving, bevel, box, beam, cylinder, rail, windowBand, sideWindows, roofVent, antenna, hazard} = k;
        part('shallow steel hull', 'shell', [0, 0, 0], group => {
            hull(k, group, [[-2.38,-.68],[-2.08,-1.02],[1.72,-1.02],[2.25,-.76],
                [2.25,.76],[1.72,1.02],[-2.08,1.02],[-2.38,.68]], deck);
        });
        part('open vehicle well', 'cladding', [0, deck, 0], () => {
            bevel(.37, deck + .018, 0, 3.26, .044, 1.54, 'dark', .007);
            for (const z of [-.83, .83]) {
                box(.30, .37, z * .89, 3.15, .027, .09, 'steel');
                for (const x of [-1.37,-.74,-.11,.52,1.15,1.69])
                    tieDown(k, x, deck + .05, z * .66);
                for (const x of [-1.35, -.73, -.11, .51, 1.13, 1.75])
                    box(x, deck + .046, z * .42, .024, .013, .41, 'steel');
            }
            for (let i = -3; i <= 3; i++)
                box(.35 + i * .42, deck + .047, 0, .16, .012, .033, 'edge');
            bevel(1.97,.39,0,.23,.10,1.48,'edge',.008);
            hazard(1.78,.414,.004,.18,1.31);
        });
        for (const z of [-.83, .83])
            part(z > 0 ? 'near bulwark' : 'far bulwark', 'cladding', [0, deck, z], () => {
                bevel(.14, .54, z, 3.60, .29, .20, 'paint', .018);
                box(.13, .69, z, 3.62, .048, .225, 'edge');
                rail([-1.49,.68,z],[1.59,.68,z],.19,'steel');
                for (const x of [-1.37,-.74,-.11,.52,1.15,1.69])
                    bevel(x, .45, z * 1.11, .11, .105, .055, 'rubber', .013);
            }, {landingCover: z > 0 ? 'side' : null});
        part('aft wheelhouse and exhaust', 'systems', [-1.76, .4, 0], () => {
            bevel(-1.72,.70,.10,.91,.64,1.36,'wall',.045);
            bevel(-1.73,1.035,.10,1.03,.10,1.47,'paint',.022);
            sideWindows(-1.25,.85,.10,1.16,.22,3);
            windowBand(-1.72,.84,.79,.66,.23,2);
            box(-1.66,.50,.797,.56,.105,.036,'paint');
            bevel(-2.04,1.105,-.40,.24,.083,.24,'steel',.009);
            cylinder(-2.03,1.17,-.4,.05,.18,'dark',9);
            roofVent(-1.50,1.09,.43,.23,.25);
            antenna(-1.75,1.09,-.38,.54);
            beam([-2.1,.51,.82],[-2.10,.94,.82],.035,.035,'edge');
        });
        const ramp = moving('bow ramp hinge', [2.14,.385,0], 'ramp', () => {
            bevel(2.65,.385,0,1.04,.094,1.49,'steel',.02);
            for (const z of [-.69,.69]) {
                bevel(2.64,.447,z,.98,.053,.085,'paint',.009);
                beam([2.17,.47,z],[3.10,.47,z],.026,.026,'edge');
            }
            for (let i = 0; i < 8; i++)
                box(2.24 + i * .118,.444,0,.022,.019,1.28,'edge');
            box(3.155,.385,0,.07,.064,1.43,'yellow');
            for (const z of [-.53,.53]) {
                const hinge = cylinder(2.16,.387,z,.070,.15,'edge',9);
                hinge.rotation.x = halfTurn;
            }
        }, {landingCover: 'ramp'});
        return {root: k.finish(), pose(t) {
            ramp.rotation.z = between(1.49, -.40, t);
        }};
    }

    function supply() {
        const k = kit('supply'), deck = .63;
        const {part, bevel, box, beam, cylinder, rail, vent, sideWindows, windowBand, roofVent, antenna, ladder} = k;
        part('raked supply hull', 'shell', [0, 0, 0], group => {
            hull(k, group, [[-4.59,-.82],[-4.19,-1.37],[-3.52,-1.46],[2.85,-1.46],
                [3.76,-1.02],[4.57,-.26],[4.57,.26],[3.76,1.02],[2.85,1.46],
                [-3.52,1.46],[-4.19,1.37],[-4.59,.82]], deck);
            for (const z of [-1.445,1.445]) for (const x of [-3.22,-1.52,.17,1.85]) {
                box(x,.29,z,.97,.085,.042,'edge');
                box(x,.393,z,.95,.144,.035,'paint');
            }
        });
        part('cargo deck, lanes and winches', 'cladding', [0, deck, 0], () => {
            bevel(.03,deck+.025,0,5.31,.054,2.25,'dark',.012);
            for (const z of [-1.29,1.29]) {
                rail([-4.11,deck,z],[2.77,deck,z],.22,'edge');
                rail([2.77,deck,z],[4.39,deck,z*.18],.22,'edge');
                box(.02,deck+.055,z*.9,5.1,.018,.055,'yellow');
                for (const x of [-2.12,-1.34,-.56,.22,1,1.78,2.56])
                    tieDown(k,x,deck+.06,z*.78);
            }
            for (const x of [-2.25,-1.45,-.65,.15,.95,1.75,2.55])
                box(x,deck+.055,0,.025,.012,2.15,'steel');
            for (const x of [2.95,3.42]) for (const z of [-.43,.43]) {
                cylinder(x,deck+.115,z,.088,.23,'dark',10);
                cylinder(x,deck+.24,z,.13,.07,'edge',10);
            }
            bevel(3.46,deck+.15,0,.56,.29,.45,'steel',.035);
            for (const [x,z,m] of [[-1.57,-.59,'brick'],[-.66,-.59,'steel'],[.25,-.59,'wall'],
                [1.32,-.59,'brick'],[2.2,-.59,'steel'],[.7,.62,'brick'],[1.63,.62,'steel']])
                cargoBox(k,x,deck+.062,z,m);
            cargoBox(k,.7,deck+.548,.62,'wall');
        });
        part('twin deckhouse, glazing and funnel', 'systems', [-3.23,deck,0], () => {
            bevel(-3.32,1.04,0,1.65,.82,2.35,'wall',.08);
            bevel(-3.34,1.46,0,1.82,.107,2.47,'edge',.019);
            bevel(-3.29,1.73,0,1.32,.51,2.14,'paint',.058);
            bevel(-3.25,2.01,0,1.49,.101,2.24,'wall',.02);
            sideWindows(-2.622,1.81,0,1.82,.22,6);
            windowBand(-3.26,1.81,1.08,1.16,.22,4);
            for (const x of [-3.87,-3.32,-2.78])
                vent(x,1.08,1.19,.30,.23,false,5);
            bevel(-3.73,2.28,-.26,.38,.48,.43,'steel',.045);
            box(-3.74,2.43,-.26,.386,.11,.435,'paint');
            bevel(-3.74,2.54,-.26,.42,.075,.47,'dark',.013);
            roofVent(-2.93,2.077,.65,.43,.30);
            antenna(-3.06,2.07,-.52,.79);
            beam([-3.06,2.69,-.91],[-3.06,2.69,-.16],.029,.029,'edge');
            rail([-3.99,1.50,1.14],[-2.56,1.50,1.14],.18,'edge');
            ladder(-3.96,.65,1.17,.89);
        });
        return {root: k.finish(), pose(t) { k.root.rotation.y = t * halfTurn; }};
    }

    function office() {
        const k = kit('office');
        const {part, box, bevel, door, windowBand, sideWindows, sidePanel, vent, stairs, roofVent, hvac, antenna, pipe} = k;
        part('two-cell footing', 'foundation', [0,0,0], () => {
            bevel(0,.044,0,1.99,.088,1.99,'concrete',.012);
            box(0,.09,0,1.94,.012,.022,'edge');
        });
        part('quay dispatch office', 'shell', [0,.08,0], () => {
            bevel(-.035,.56,-.075,1.72,.93,1.53,'wall',.035);
            box(-.035,.18,.705,1.71,.10,.038,'steel');
            bevel(-.035,1.04,-.075,1.86,.155,1.67,'paint',.023);
            bevel(-.04,1.13,-.075,1.73,.045,1.54,'edge',.009);
            for (const x of [-.866,.807]) for (const z of [-.79,.668])
                box(x,.54,z,.062,.88,.069,'steel');
            door(-.45,.11,.701,.38,.58);
            stairs(-.45,.77,.53,3);
            windowBand(.34,.70,.698,.72,.21,3);
            box(.34,.707,.754,.68,.16,.008,'window');
            sideWindows(.834,.73,-.20,1.10,.23,4,true);
            sidePanel(.838,.38,.36,.32,.31,'paint');
            vent(.858,.43,-.52,.25,.23,true,5);
            bevel(.32,.925,.72,.82,.072,.21,'paint',.012);
            box(.36,.916,.831,.51,.033,.017,'lamp');
        });
        part('roof services and signal mast', 'systems', [0,1.15,0], () => {
            hvac(.41,1.16,-.32,.52,.36);
            roofVent(-.41,1.15,-.27,.35,.37);
            pipe([[.74,.18,-.64],[.75,.98,-.64],[.69,1.21,-.64],[.42,1.23,-.64]],.027);
            antenna(-.62,1.16,.34,.59);
            bevel(-.62,1.59,.33,.12,.08,.12,'dark',.014);
            box(-.62,1.642,.33,.085,.047,.085,'lamp');
        });
        return {root:k.finish(), pose(t) {
            k.materials.lamp.color.setHex(t ? 0xe8d199 : 0x4f5550);
            k.materials.lamp.emissiveIntensity = t ? .84 : 0;
            k.materials.window.color.setHex(t ? 0x696b56 : 0x293a3d);
            k.materials.window.emissiveIntensity = t ? .28 : 0;
        }};
    }

    function crane() {
        const k = kit('crane');
        const {part, moving, box, bevel, beam, rail, cylinder, sideWindows, ladder} = k;
        const turnY = 2.45, reach = 5.22, tipY = 2.89, quayYaw = -2.22, vesselYaw = .157;
        part('lattice tower and concrete anchor', 'foundation', [0,0,0], () => {
            bevel(0,.055,0,1.12,.11,1.12,'concrete',.018);
            bevel(0,.15,0,.84,.16,.84,'steel',.022);
            for (const x of [-.34,.34]) for (const z of [-.34,.34]) {
                box(x,.222,z,.18,.063,.18,'edge');
                beam([x,.22,z],[x*.69,2.35,z*.69],.073,.073,'yellow');
            }
            for (let i = 0; i < 5; i++) {
                const y=.30+i*.39, w=.33-i*.021, n=w-.021;
                for (const s of [-1,1]) {
                    beam([-w,y,s*w],[n,y+.38,s*n],.04,.04,'steel');
                    beam([-w,y,s*w],[w,y,s*w],.049,.049,'yellow');
                    beam([s*w,y,w],[s*n,y+.38,-n],.043,.043,'steel');
                }
            }
            ladder(.319,.21,.32,2.08);
            cylinder(0,2.39,0,.31,.18,'steel',16);
        });
        const boom = moving('turntable, jib and operator cab',[0,turnY,0],'slew',() => {
            bevel(0,turnY+.11,0,1.02,.23,1.0,'paint',.027);
            box(0,turnY+.231,0,.96,.031,.94,'steel');
            bevel(-.66,turnY+.27,0,.56,.43,.76,'wall',.045);
            bevel(-.59,turnY+.52,0,.54,.076,.77,'paint',.015);
            sideWindows(-.37,turnY+.36,0,.58,.21,2);
            rail([-.5,turnY+.22,.52],[.64,turnY+.22,.52],.18);
            for (const z of [-.17,.17]) {
                beam([.05,turnY+.02,z],[reach,tipY,z*.43],.074,.071,'yellow');
                beam([.12,turnY+.68,z],[reach,tipY+.10,z*.43],.066,.067,'yellow');
                for(let i=0;i<9;i++) {
                    const a=i/9,b=(i+1)/9;
                    beam([.15+a*(reach-.15),turnY+.09+a*(tipY-turnY-.09),z*(1-.57*a)],
                         [.15+b*(reach-.15),turnY+.69+b*(tipY-turnY-.59),z*(1-.57*b)],.040,.04,'steel');
                }
            }
            cylinder(reach,tipY+.06,0,.095,.20,'dark',12).rotation.x=halfTurn;
            bevel(-1.08,turnY+.14,0,.44,.38,.64,'paint',.034);
            box(-1.08,turnY+.34,0,.40,.036,.60,'steel');
            beam([-.93,turnY+.50,0],[2.9,turnY+.55,0],.033,.033,'steel');
        });
        const line = moving('independent hoist line',[reach,1.8,0],'hoist',() => {
            for (const z of [-.105,.105]) cylinder(reach,1.8,z,.014,1,'dark',6);
        });
        const load = moving('sling, spreader and lifted cargo',[reach,0,0],'cargo',() => {
            cargoBox(k,reach,0,0,'brick');
        });
        const spreader = moving('spreader frame',[reach,0,0],'spreader',() => {
            bevel(reach,.07,0,.65,.075,.32,'yellow',.01);
            for (const z of [-.15,.15]) beam([reach-.30,.10,z],[reach+.30,.10,z],.044,.044,'dark');
        });
        return {root:k.finish(), pose(t) {
            const swing = ease((t-.36)/.26), restow = ease((t-.88)/.12);
            const heading = vesselYaw + (quayYaw-vesselYaw)*swing*(1-restow);
            boom.rotation.y = heading;
            const x = reach*Math.cos(heading), z = -reach*Math.sin(heading);
            let y = t<.18 ? between(1.81,.68,t/.18) :
                t<.38 ? between(.68,1.47,(t-.18)/.20) :
                t<.63 ? 1.47 : t<.84 ? between(1.47,.04,(t-.63)/.21) :
                between(.04,1.81,(t-.88)/.12);
            const attached = t>=.18&&t<.85;
            const rig = attached ? y+.47 : t<.18 ? y+.47 : Math.max(.51,y+.47);
            load.position.set(x,y,z);load.rotation.y=heading;load.visible=attached;
            spreader.position.set(x,rig,z);spreader.rotation.y=heading;
            const length=Math.max(.07,tipY-rig);
            line.position.set(x,tipY-length/2,z);line.rotation.y=heading;line.scale.y=length;
        }};
    }

    function gate() {
        const k = kit('gate'), {part, moving, box, bevel, cylinder, sidePanel, beam} = k, leaves=[];
        for (const side of [-1,1]) {
            const z = side * 3.04;
            part('freight gate cabinet '+side,'foundation',[0,0,z],() => {
                bevel(0,.057,z,.81,.114,.64,'concrete',.016);
                bevel(-.05,.39,z,.53,.64,.43,'steel',.026);
                bevel(-.05,.743,z,.61,.12,.51,'paint',.015);
                sidePanel(.228,.402,z,.27,.36,'paint');
                box(.275,.63,z,.042,.067,.20,'dark');
                cylinder(.228,.888,z,.058,.132,'dark',10);
                for(const dx of [-.31,.31]){
                    cylinder(dx,.18,z+side*.23,.054,.35,'yellow',8);
                    cylinder(dx,.255,z+side*.23,.056,.065,'dark',8);
                }
            });
            leaves.push(moving('hinged road leaf '+side,[0,.815,z],'barrier',() => {
                bevel(0,.815,z-side*1.505,.112,.135,3.01,'yellow',.016);
                for(let i=0;i<7;i++)
                    box(.059,.817,z-side*(.24+i*.425),.026,.106,.205,'dark');
                for(let i=0;i<6;i++)
                    box(0,.886,z-side*(.36+i*.44),.108,.019,.10,'edge');
                beam([0,.786,z-side*.19],[0,.61,z-side*.81],.046,.047,'steel');
                const pin=cylinder(0,.815,z,.099,.238,'edge',12);pin.rotation.z=halfTurn;
            }));
        }
        return {root:k.finish(), pose(t) {
            leaves[0].rotation.x=-t*halfTurn;
            leaves[1].rotation.x=t*halfTurn;
        }};
    }

    function cargo() {
        const k=kit('cargo');
        k.part('quay freight container','foundation',[0,0,0],()=>cargoBox(k,0,.013,0));
        return {root:k.finish(),pose(){}};
    }
    function bollard() {
        const k=kit('bollard');
        k.part('moorings','foundation',[0,0,0],()=>{
            k.bevel(0,.025,0,.42,.05,.36,'concrete',.007);
            k.bevel(0,.054,0,.30,.04,.25,'steel',.007);
            k.cylinder(0,.152,0,.083,.17,'rubber',10);
            k.bevel(0,.234,0,.28,.083,.18,'steel',.025);
            for(const x of [-.111,.111])k.cylinder(x,.08,0,.022,.019,'edge',6);
        });
        return {root:k.finish(),pose(){}};
    }
    const models = {landing,cover:landing,supply,office,crane,gate,cargo,bollard};
    return {ready:Kit.ready,specs,build:key=>models[key]()};
})();
