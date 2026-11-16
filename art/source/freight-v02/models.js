/* +Z forward, Y up and floor origin zero, matching UnitMaster and TankMaster.
   The shared tank kit supplies lighting-compatible coating and faction paint. */
window.UnitMaster = (() => {
    const T = THREE;
    const specs = Object.fromEntries(["engine", "empty", "crates", "tarp"]
        .map(name => [name, { width: 10.3, cell: 256 }]));

    function build(name) {
        if (!specs[name]) throw Error("Unknown railway model: " + name);
        const { root, p, mesh, box, cyl, beam, bevel, hull } = TankMaster.kit(false);
        root.name = "Greyhaven freight " + name;

        function material(key, color, roughness, metalness = 0) {
            p[key] = new T.MeshStandardMaterial({ color, roughness, metalness });
            p[key].name = key;
        }
        material("warning", 0xb3974e, .72);
        material("paintOxide", 0x604843, .67, .12);
        material("deckWood", 0x6b5544, .91);
        material("boxWood", 0x989077, .89);
        material("tarp", 0x596555, .93);
        material("webbing", 0x807a5f, .90);

        function cable(a, b, radius, finish = "steel") {
            return beam(a, b, radius, finish);
        }
        function inset(w, h, x, y, z, m) {
            return bevel(w, h, .031, .008, x, y, z, m);
        }

        // Two independent bogies, axleboxes, brake reservoirs and a sprung drawbar.
        for (const z of [-2.63, 2.63]) {
            bevel(2.7, .38, 1.65, .052, 0, .60, z, "dark");
            cyl(.23, 1.56, 0, .77, z, "steel", "x");
            for (const axle of [z - .48, z + .48]) {
                cyl(.12, 2.51, 0, .37, axle, "steel", "x");
                for (const side of [-1, 1]) {
                    const x = side * 1.14;
                    cyl(.365, .184, x, .37, axle, "dark", "x");
                    cyl(.310, .215, x, .37, axle, "steel", "x");
                    cyl(.192, .241, x, .37, axle, "armor", "x");
                    cyl(.077, .268, x, .37, axle, "bolt", "x");
                    bevel(.19, .24, .32, .025, side * 1.32, .60, axle, "steel");
                }
            }
            for (const side of [-1, 1]) {
                for (const offset of [-.25, .25])
                    cyl(.084, .26, side * 1.2, .62, z + offset, "steel");
                bevel(.18, .21, 1.52, .035, side * 1.36, .74, z, "armor");
            }
        }
        bevel(2.84, .33, 8.2, .085, 0, .89, 0, "armor");
        bevel(2.46, .18, 7.86, .045, 0, .67, 0, "dark");
        for (const sign of [-1, 1]) {
            bevel(2.66, .26, .19, .037, 0, .80, sign * 4.04, "steel");
            bevel(.29, .23, .37, .033, 0, .67, sign * 4.20, "dark");
            bevel(.44, .17, .17, .025, 0, .67, sign * 4.35, "steel");
            for (const side of [-1, 1]) {
                bevel(.34, .225, .095, .035, side * 1.08, .86, sign * 4.17, "dark");
                cable([side * .52, .57, sign * 4.02], [side * .47, .46, sign * 4.35], .024, "dark");
            }
        }

        if (name === "engine") {
            bevel(2.76, .18, 7.78, .045, 0, 1.09, 0, "paintOxide");
            bevel(2.17, .98, 4.75, .115, 0, 1.74, -.96, "armor");
            bevel(1.85, .16, 4.39, .042, 0, 2.28, -.99, "edge");
            hull(2.48, 1.42, 1.78, 0, 1.19, 2.30, "armor", .88);
            bevel(2.51, .194, 1.81, .066, 0, 2.65, 2.29, "edge");
            bevel(2.13, .74, 1.05, .12, 0, 1.59, 3.48, "armor");
            inset(1.35, .42, 0, 1.49, 4.021, "paint");
            inset(1.10, .45, 0, 1.74, -3.352, "paint");

            for (const side of [-1, 1]) {
                const x = side * 1.096;
                bevel(.073, .671, 3.07, .025, x, 1.71, -.57, "paint");
                for (const z of [-1.55, -.58, .42])
                    box(.018, .63, .021, x + .037 * side, 1.715, z, "steel");
                for (const z of [-2.02, -1.05, -.08, .89])
                    cyl(.026, .044, x + .039 * side, 1.98, z, "bolt", "x");
                bevel(.08, .57, .84, .018, side * 1.108, 1.75, -2.73, "dark");
                for (let slat = 0; slat < 7; slat++)
                    box(.079, .031, .69, side * 1.151, 1.53 + slat * .065, -2.73, "steel");
                bevel(.063, .46, .70, .019, side * 1.135, 2.19, 2.36, "dark");
                bevel(.071, .36, .59, .016, side * 1.151, 2.19, 2.36, "glass");
                bevel(.061, .34, .76, .020, side * 1.166, 1.54, 2.31, "paint");
                for (const z of [-3.67, -2.39, -.44, 1.08, 2.91, 3.67])
                    cable([side * 1.31, 1.14, z], [side * 1.31, 1.58, z], .034);
                cable([side * 1.31, 1.58, -3.67], [side * 1.31, 1.58, 3.67], .033);
                bevel(.35, .12, .25, .032, side * .55, 1.70, 3.99, "lamp");
            }
            for (const sign of [-1, 1]) {
                for (const x of [-.49, .49]) {
                    inset(.85, .45, x, 2.19, sign > 0 ? 3.12 : 1.48, "dark");
                    inset(.72, .35, x, 2.19, sign > 0 ? 3.148 : 1.453, "glass");
                }
                bevel(2.14, .24, .03, .009, 0, .89, sign * 4.151, "dark");
                for (const x of [-.80, -.48, -.16, .16, .48, .80]) {
                    const band = box(.095, .23, .024, x, .89, sign * 4.174, "warning");
                    band.rotation.z = -.51;
                }
            }
            cyl(.40, .058, 0, 2.37, -2.45, "steel");
            cyl(.32, .064, 0, 2.405, -2.45, "dark");
            for (let i = -3; i <= 3; i++) box(.46, .015, .019, 0, 2.44, -2.45 + i * .066, "steel");
            bevel(.47, .25, .48, .04, .20, 2.43, -.68, "steel");
            bevel(.35, .045, .33, .014, .20, 2.57, -.68, "dark");
            return root;
        }

        for (let i = -6; i <= 6; i++) bevel(2.53, .095, .58, .015, 0, 1.09, i * .593, "deckWood");
        for (const side of [-1, 1]) {
            bevel(.145, .29, 7.90, .038, side * 1.37, 1.10, 0, "armor");
            for (const z of [-3.66, -2.23, -.77, .77, 2.23, 3.66]) {
                bevel(.173, .21, .18, .017, side * 1.42, 1.16, z, "steel");
                bevel(.14, .12, .14, .021, side * 1.42, 1.30, z, "edge");
            }
        }
        for (const sign of [-1, 1]) bevel(2.72, .24, .17, .037, 0, 1.12, sign * 3.94, "steel");

        if (name === "crates") {
            for (const z of [-1.32, 1.25]) {
                bevel(1.94, .14, 1.94, .025, 0, 1.22, z, "deckWood");
                bevel(1.78, .72, 1.69, .105, 0, 1.63, z, "boxWood");
                for (const offset of [-.59, .59]) {
                    box(1.79, .033, .12, 0, 1.995, z + offset, "webbing");
                    for (const side of [-1, 1])
                        box(.03, .73, .12, side * .894, 1.63, z + offset, "webbing");
                }
            }
        } else if (name === "tarp") {
            hull(2.06, .82, 5.48, 0, 1.16, -.09, "tarp", .75);
            for (const z of [-1.94, -.73, .48, 1.69]) {
                const strap = [[-1.059, 1.26], [-.75, 1.99], [.75, 1.99], [1.059, 1.26]];
                for (let i = 1; i < strap.length; i++)
                    cable([...strap[i - 1], z], [...strap[i], z], .044, "webbing");
            }
        }
        return root;
    }

    return { specs, build };
})();
