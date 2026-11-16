/* Greyhaven architecture kit, in ground cells. Y is up; the ground anchor is at zero. */
(() => {
    const original = BuildingMasterV08.build;
    const directions = [0, Math.PI / 2, Math.PI, Math.PI * 1.5];
    const wallKeys = Array.from({ length: 16 }, (_, mask) => "wall-" + mask.toString(16));
    const oilKeys = ["oilwell", "oilwell-90", "oilwell-180", "oilwell-270"];
    const wallSpec = { w: 1, d: 1 };
    const oilSpec = { w: 2, d: 2 };
    for (const key of wallKeys) BuildingMasterV08.specs[key] = wallSpec;
    for (const key of oilKeys) BuildingMasterV08.specs[key] = oilSpec;

    BuildingMasterV08.build = function (key) {
        if (oilKeys.includes(key)) {
            const oilwell = buildOilwell();
            oilwell.rotation.y = oilKeys.indexOf(key) * Math.PI / 2;
            oilwell.updateMatrixWorld(true);
            return oilwell;
        }
        if (wallKeys.includes(key)) return buildWall(key, Number.parseInt(key.at(-1), 16));
        return original(key);
    };

    function buildWall(key, connections) {
        const kit = ArchitectureKit.create(key, wallSpec);
        const { root, part, bevel, box, panel } = kit;
        root.name = "Precast reinforced wall " + connections.toString(16);
        root.userData.connections = connections;
        part("anchored-concrete-pier", "foundation", [0, 0, 0], () => {
            bevel(0, .045, 0, .38, .09, .38, "concrete", .024);
            bevel(0, .122, 0, .32, .09, .32, "steel", .017);
            bevel(0, .355, 0, .27, .54, .27, "wall", .024);
            bevel(0, .621, 0, .315, .088, .315, "edge", .017);
            bevel(0, .675, 0, .275, .027, .275, "concrete", .008);
        });

        // Matching half-panels end exactly at the cell seam, with a separate lapped coping.
        directions.forEach((angle, bit) => {
            if (connections & (1 << bit)) {
                part("butted-panel-" + bit, "shell", [0, 0, 0], group => {
                    bevel(0, .039, .318, .30, .078, .364, "concrete", .014);
                    bevel(0, .114, .305, .258, .075, .383, "steel", .013);
                    bevel(0, .342, .306, .193, .47, .388, "wall", .016);
                    bevel(0, .590, .308, .231, .057, .384, "edge", .012);
                    for (const side of [-1, 1]) {
                        box(side * .098, .218, .317, .013, .029, .335, "steel");
                        bevel(side * .106, .420, .321, .023, .152, .32, "concrete", .008);
                        box(side * .12, .455, .306, .013, .076, .304, "paint");
                        for (const z of [.2, .427]) bevel(side * .124, .455, z, .015, .025, .033, "steel", .004);
                    }
                    group.rotation.y = angle;
                });
            } else {
                part("closed-pier-face-" + bit, "cladding", [0, 0, 0], group => {
                    panel(0, .445, .139, .19, .15);
                    box(0, .267, .144, .112, .022, .018, "dark");
                    group.rotation.y = angle;
                });
            }
        });
        return kit.finish();
    }

    function buildOilwell() {
        const kit = ArchitectureKit.create("oilwell", oilSpec);
        const { root, part, materials, bevel, box, beam, cylinder, pipe, panel, sidePanel, vent, ladder, rail, hazard } = kit;
        root.name = "Greyhaven pumpjack and wellhead";
        function finish(name, color, roughness, metalness) {
            const material = materials.steel.clone();
            material.name = name;
            material.color.setHex(color);
            material.userData.originalColor = color;
            material.roughness = roughness;
            material.metalness = metalness;
            materials[name] = material;
        }
        finish("galvanized", 0x96998b, .62, .48);
        finish("castHousing", 0x727771, .80, .28);
        finish("oxide", 0x785746, .91, .10);
        finish("valveBrass", 0x998053, .67, .30);
        const cylinderAxis = (x, y, z, radius, length, material, axis, sides = 16) => {
            const cylinderMesh = cylinder(x, y, z, radius, length, material, sides);
            if (axis === "x") cylinderMesh.rotation.z = Math.PI / 2;
            if (axis === "z") cylinderMesh.rotation.x = Math.PI / 2;
            return cylinderMesh;
        };

        part("concrete-spill-containment", "foundation", [0, 0, 0], () => {
            bevel(0, .042, 0, 1.94, .084, 1.94, "concrete", .039);
            bevel(-.22, .088, -.035, 1.32, .041, 1.79, "wall", .017);
            bevel(-.28, .115, .124, .67, .05, 1.22, "steel", .022);
            for (const x of [-.925, .925]) box(x, .103, 0, .027, .057, 1.84, "edge");
            for (const z of [-.925, .925]) box(0, .103, z, 1.84, .057, .027, "edge");
            box(.81, .086, -.15, .11, .016, .62, "dark");
            for (let i = 0; i < 11; i++) box(.81, .096, -.42 + .054 * i, .11, .009, .019, "steel");
            hazard(-.19, .093, .858, .54, .085);
            for (const z of [-.59,-.22,.17,.58])
                box(-.802,.109,z,.012,.004,.192,"steel");
            box(-.78,.109,.69,.19,.008,.15,"dark");
            for (let i=0;i<6;i++) box(-.846+i*.026,.115,.69,.013,.006,.133,"castHousing");
        });

        part("drips-in-containment-tray", "detail", [0,0,0], group => {
            const outline = [[-.94,-.1],[-.70,-.67],[-.31,-.55],[.02,-.93],[.65,-.68],
                [.97,-.19],[.69,.42],[.22,.67],[-.18,.49],[-.68,.77]];
            for (const [x,y,z,w,d] of [[-.33,.111,.735,.246,.127],[-.212,.142,-.46,.22,.14],[.496,.087,.354,.174,.092]]) {
                for (const [size, opacity, height] of [[1,.12,0],[.72,.14,.001],[.44,.16,.002]]) {
                    const shape = new THREE.Shape(outline.map(([u,v]) => new THREE.Vector2(u*w*size,-v*d*size)));
                    const geometry = new THREE.ShapeGeometry(shape);
                    geometry.rotateX(-Math.PI/2);
                    const stain = new THREE.Mesh(geometry, new THREE.MeshBasicMaterial({
                        color:0x442f22,transparent:true,opacity,depthWrite:false
                    }));
                    stain.name = "oil-discoloration";
                    stain.position.set(x,y+height,z);
                    stain.receiveShadow = true;
                    group.add(stain);
                }
            }
        });

        const center = -.31;
        part("bolted-pump-support", "shell", [center, .142, .035], () => {
            for (const sign of [-1, 1]) {
                const x = center + sign * .229;
                bevel(x, .156, .015, .15, .052, .96, "dark", .014);
                for (const z of [-.39,.425]) {
                    beam([x, .18, z], [x, 1.16, .02], .081, .081, "castHousing");
                    beam([x+sign*.044,.246,z*.91],[x+sign*.044,1.097,.017],.025,.055,"paint");
                }
                beam([x, .44, -.27], [x, .44, .31], .071, .056, "castHousing");
                beam([x+sign*.043, .472, -.252], [x+sign*.043, .879, .142], .027, .032, "galvanized");
                for (const z of [-.36, .40]) {
                    bevel(x, .203, z, .138, .06, .161, "wall", .018);
                    bevel(x, .235, z, .09, .026, .125, "oxide", .009);
                    for (const offset of [-.053, .053])
                        cylinder(x + offset, .244, z, .017, .039, "galvanized", 6);
                }
            }
            for (const [y, z] of [[.44, -.23], [.44, .27], [.91, .02], [1.14, .02]]) {
                beam([center - .249, y, z], [center + .249, y, z], .062, .066, "steel");
            }
            bevel(center, 1.165, .023, .55, .155, .245, "castHousing", .031);
            cylinderAxis(center, 1.221, .024, .112, .56, "steel", "x");
            for (const sign of [-1, 1]) {
                const x = center+sign*.286;
                cylinderAxis(x,1.221,.024,.110,.058,"galvanized","x");
                cylinderAxis(x+sign*.026,1.221,.024,.078,.031,"dark","x",12);
                cylinderAxis(x+sign*.045,1.221,.024,.058,.028,"castHousing","x",8);
                for (const angle of [Math.PI/4,Math.PI*3/4,Math.PI*5/4,Math.PI*7/4]) {
                    cylinderAxis(x+sign*.036,1.221+Math.cos(angle)*.091,.024+Math.sin(angle)*.091,
                        .015,.018,"oxide","x",6);
                }
            }
            ladder(center - .31, .12, -.40, .64);
            for (const sign of [-1, 1]) {
                const x = center + sign * .229;
                beam([x,.77,-.16],[x,.77,.192],.07,.06,"steel");
                sidePanel(center + sign * .275, .772, .025, .304, .204);
            }
        });

        const pivot = [center, 1.221, .024];
        part("walking-beam-with-horsehead", "systems", pivot, group => {
            bevel(center, 1.31, -.035, .235, .117, 1.45, "dark", .019);
            bevel(center, 1.39, -.13, .267, .039, 1.20, "paint", .010);
            for (const sign of [-1,1])
                box(center+sign*.126,1.408,-.134,.013,.008,1.17,"galvanized");
            bevel(center, 1.248, -.13, .254, .034, 1.14, "castHousing", .009);
            for (const sign of [-1, 1]) {
                sidePanel(center + sign * .130, 1.318, -.17, .79, .079);
                for (const z of [-.59, .16, .45]) {
                    cylinderAxis(center + sign * .151, 1.30, z, .019, .018, "galvanized", "x", 8);
                    if (z === .16) cylinderAxis(center+sign*.155, 1.344, z, .013, .016, "oxide", "x", 6);
                }
            }

            // A faceted curved horsehead with inset livery cheeks on both sides.
            const outline = [[.363,1.379],[.559,1.475],[.733,1.44],[.828,1.337],
                [.879,1.13],[.831,.942],[.709,.965],[.651,1.112],[.489,1.225]];
            const inset = [[.468,1.358],[.574,1.421],[.702,1.392],[.768,1.311],
                [.814,1.146],[.790,1.017],[.748,1.03],[.701,1.143],[.555,1.241]];
            function profile(points, width, material) {
                const shape = new THREE.Shape(points.map(([z, y]) => new THREE.Vector2(z - pivot[2], y - pivot[1])));
                const cut = new THREE.ExtrudeGeometry(shape, { depth: width, bevelEnabled: true, bevelSize: .012,
                    bevelThickness: .009, bevelSegments: 1, curveSegments: 1, steps: 1 });
                cut.translate(0, 0, -width / 2);
                cut.rotateY(-Math.PI / 2);
                const mesh = new THREE.Mesh(cut, materials[material]);
                mesh.castShadow = mesh.receiveShadow = true;
                group.add(mesh);
            }
            profile(outline, .28, "paint");
            profile(inset, .298, "paint");
            profile([[.741,1.409],[.829,1.336],[.879,1.13],[.840,.956],
                [.817,1.033],[.853,1.135],[.803,1.321],[.731,1.380]], .242, "castHousing");
            for (const side of [-1,1]) {
                for (const [y,z] of [[1.365,.589],[1.26,.735],[1.10,.781]])
                    cylinderAxis(center+side*.166,y,z,.023,.023,"galvanized","x",8);
            }
            bevel(center, 1.079, -.713, .37, .40, .26, "paint", .047);
            bevel(center, .898, -.711, .348, .064, .231, "castHousing", .017);
            bevel(center, 1.104, -.841, .294, .25, .07, "paint", .019);
            for (const y of [.991,1.047,1.103,1.159,1.214]) {
                box(center,y,-.879,.212,.013,.011,"dark");
            }
            for (const sign of [-1,1])
                cylinderAxis(center+sign*.160,1.252,-.617,.027,.031,"castHousing","x",8);
        });

        part("fixed-drive-motor", "systems", [center, .34, -.54], () => {
            bevel(center, .253, -.553, .429, .195, .445, "dark", .035);
            bevel(center, .377, -.547, .356, .141, .364, "castHousing", .027);
            for (const x of [center-.150,center+.150])
                for (let fin=0;fin<6;fin++)
                    box(x,.446,-.688+fin*.055,.053,.024,.026,"galvanized");
            cylinderAxis(center, .373, -.56, .102, .432, "edge", "x");
        });
        part("rotating-crankshaft", "systems", [center, .393, -.56], () => {
            for (const sign of [-1, 1]) {
                const x = center + sign * .265;
                cylinderAxis(x,.393,-.56,.161,.063,"dark","x",12);
                cylinderAxis(x+sign*.024,.393,-.56,.147,.034,"galvanized","x",12);
                cylinderAxis(x+sign*.043,.393,-.56,.110,.017,"steel","x",12);
                cylinderAxis(x+sign*.056,.393,-.56,.067,.043,"castHousing","x",10);
                cylinderAxis(x+sign*.075,.478,-.606,.041,.046,"oxide","x",8);
            }
        });
        for (const sign of [-1, 1]) {
            const lower = [center+sign*.339,.478,-.606];
            const upper = [center+sign*.141,1.253,-.617];
            part("crank-connecting-rod-" + sign, "systems", lower, group => {
                group.userData.upperJoint = upper;
                beam(lower,upper,.052,.043,"galvanized");
                beam([center+sign*.322,.49,-.604],[center+sign*.133,1.205,-.616],.044,.048,"steel");
            });
        }

        const strokeBase = .495, cableLine = .815, cableTop = 1.14;
        part("sliding-polished-rod", "systems", [center,strokeBase,.784], () => {
            bevel(center,strokeBase,.786,.246,.039,.123,"steel",.012);
            cylinder(center,strokeBase-.225,.784,.024,.45,"galvanized",10);
        });
        part("horsehead-bridle", "systems", [center,strokeBase,cableLine], group => {
            group.userData.top = cableTop;
            for (const x of [center-.066,center+.066])
                beam([x,strokeBase,cableLine],[x,cableTop,cableLine],.017,.017,"edge");
        });
        part("polished-rod-and-wellhead", "systems", [center, .09, .778], () => {
            cylinder(center, .193, .784, .097, .163, "castHousing", 12);
            cylinder(center, .224, .784, .121, .046, "steel", 12);
            cylinder(center, .292, .784, .115, .046, "galvanized", 12);
            cylinder(center, .318, .784, .062, .025, "oxide", 12);
            for (const angle of [0,Math.PI/2,Math.PI,Math.PI*1.5])
                cylinder(center+Math.sin(angle)*.089,.323,.784+Math.cos(angle)*.089,.016,.021,"dark",6);
            pipe([[center + .10, .223, .78],[.22,.223,.78],[.31,.223,.70],[.31,.19,.52],
                [.38,.19,.33],[.56,.222,.27]], .039, "galvanized");
            for (const x of [center+.137,.138])
                cylinderAxis(x,.223,.78,.065,.039,"steel","x",10);
            bevel(.009,.231,.783,.136,.109,.119,"castHousing",.027);
            cylinder(.008,.302,.783,.033,.068,"oxide",8);
            beam([-.056,.340,.783],[.076,.340,.783],.023,.025,"valveBrass");
            cylinder(.202,.297,.782,.022,.102,"steel",8);
            cylinderAxis(.202,.354,.790,.043,.028,"dark","z",10);
            cylinderAxis(.202,.354,.807,.032,.006,"galvanized","z",10);
            beam([.189,.343,.811],[.216,.370,.811],.007,.007,"dark");
        });

        part("oil-buffer-vessel", "systems", [.53, .1, -.054], () => {
            for (const z of [-.335, .165]) {
                bevel(.53, .178, z, .47, .176, .143, "castHousing", .018);
                bevel(.53,.107,z,.554,.039,.181,"steel",.012);
                for (const x of [.29,.77]) cylinder(x,.134,z,.018,.023,"galvanized",6);
            }
            cylinderAxis(.53, .417, -.087, .258, .83, "paint", "z");
            for (const sign of [-1, 1]) {
                const z = -.083 + sign * .408;
                cylinderAxis(.53, .417, z, .241, .092, "edge", "z");
                cylinderAxis(.53, .417, z + sign * .050, .183, .023, "paint", "z");
                for (let bolt=0;bolt<8;bolt++) {
                    const angle=bolt*Math.PI/4;
                    cylinderAxis(.53+Math.cos(angle)*.197,.417+Math.sin(angle)*.197,z+sign*.050,
                        .017,.024,"steel","z",6);
                }
            }
            for (const z of [-.356, .188]) {
                cylinderAxis(.53, .417, z, .268, .078, "castHousing", "z");
                cylinderAxis(.53, .417, z-.036, .271, .016, "galvanized", "z");
                cylinderAxis(.53, .417, z+.036, .271, .016, "oxide", "z");
            }
            cylinder(.53, .721, -.213, .098, .124, "dark", 12);
            cylinder(.53, .797, -.213, .102, .033, "galvanized", 12);
            for (const [x,z] of [[.456,-.213],[.604,-.213],[.53,-.139],[.53,-.287]])
                cylinder(x,.823,z,.014,.028,"steel",6);
            pipe([[.53,.665,.106],[.53,.767,.106],[.555,.812,.106],[.628,.814,.106],[.650,.780,.106]],.024,"castHousing");
            pipe([[.74,.466,.34],[.81,.466,.39],[.81,.366,.506],[.81,.175,.579]], .033, "galvanized");
            pipe([[.766,.551,-.168],[.819,.551,-.168],[.842,.525,-.168],
                [.842,.341,-.168],[.818,.313,-.168],[.771,.313,-.168]], .018, "steel");
            box(.844,.433,-.169,.024,.154,.022,"valveBrass");
            box(.858,.446,-.169,.012,.112,.012,"dark");
        });

        part("service-cabinet-and-guardrail", "detail", [.62, .08, .735], () => {
            bevel(.61, .268, .735, .442, .361, .289, "paint", .035);
            bevel(.61, .468, .735, .458, .058, .300, "paint", .018);
            box(.61,.503,.734,.37,.012,.24,"castHousing");
            for (const z of [.678,.732,.786]) box(.61,.514,z,.298,.010,.013,"steel");
            panel(.61, .293, .885, .326, .228);
            sidePanel(.836, .303, .735, .231, .221);
            vent(.61, .229, .910, .185, .059, false, 4);
            bevel(.705, .345, .909, .033, .031, .013, "yellow", .006);
            bevel(.605,.367,.911,.103,.037,.015,"dark",.007);
            bevel(.605,.369,.921,.078,.021,.011,"galvanized",.004);
            pipe([[.443,.207,.615],[.406,.197,.588],[.404,.122,.528],[.404,.12,.443]],.020,"dark");
            rail([.894,.142,-.54],[.894,.142,.44],.167,"steel");
            for (const z of [-.761, -.625]) bevel(.744, .158, z, .15, .128, .08, "concrete", .02);
        });
        return kit.finish();
    }
})();
