/* One crank revolution drives both rigid connecting rods, the beam and the wellhead. */
window.OilwellIdle = (() => {
    const T = THREE;
    const rigs = new WeakMap();
    const axis = new T.Vector3(1,0,0);
    const poseBuilding = BuildingMasterV08.pose;
    let capturePhase = null;

    function rig(root) {
        if (rigs.has(root)) return rigs.get(root);
        const beam = root.getObjectByName("walking-beam-with-horsehead");
        const crank = root.getObjectByName("rotating-crankshaft");
        const slider = root.getObjectByName("sliding-polished-rod");
        const bridle = root.getObjectByName("horsehead-bridle");
        const rods = [-1,1].map(side => {
            const node = root.getObjectByName("crank-connecting-rod-" + side);
            const lower = node.position.clone();
            const upper = new T.Vector3(...node.userData.upperJoint);
            return { node, lower, upper, direction: upper.clone().sub(lower).normalize() };
        });
        const { lower, upper } = rods[0];
        const beamArm = new T.Vector2(upper.y-beam.position.y,upper.z-beam.position.z);
        const rodLength = Math.hypot(upper.y-lower.y,upper.z-lower.z);
        const branch = Math.sign((lower.y-beam.position.y)*beamArm.y-(lower.z-beam.position.z)*beamArm.x);
        const guide = new T.Vector3(beam.position.x,bridle.userData.top,.842).sub(beam.position);
        const result = { beam,crank,slider,bridle,rods,beamArm,rodLength,guide,
            branch,strokeBase:slider.position.y };
        rigs.set(root,result);
        return result;
    }

    function pose(root, phase) {
        const r = rig(root);
        const crankAngle = phase*Math.PI*2;
        const origin = r.beam.position;
        const drive = r.crank.position;
        r.crank.rotation.x = crankAngle;
        const crankJoint = r.rods[0].lower.clone().sub(drive).applyAxisAngle(axis,crankAngle).add(drive);

        // The rocker pin lies on both the beam circle and the connecting rod circle.
        const delta = new T.Vector2(crankJoint.y-origin.y,crankJoint.z-origin.z);
        const distance = delta.length();
        const radius = r.beamArm.length();
        const along = (radius*radius+distance*distance-r.rodLength*r.rodLength)/(2*distance);
        const heightSquared = radius*radius-along*along;
        if (heightSquared < -1e-9) throw Error("Crank cannot reach the beam");
        const across = r.branch*Math.sqrt(Math.max(0,heightSquared));
        const alongDirection = delta.divideScalar(distance);
        const joint = alongDirection.clone().multiplyScalar(along)
            .add(new T.Vector2(-alongDirection.y,alongDirection.x).multiplyScalar(across));
        const beamAngle = Math.atan2(joint.y,joint.x)-Math.atan2(r.beamArm.y,r.beamArm.x);
        r.beam.rotation.x = beamAngle;

        for (const link of r.rods) {
            const a = link.lower.clone().sub(drive).applyAxisAngle(axis,crankAngle).add(drive);
            const b = link.upper.clone().sub(origin).applyAxisAngle(axis,beamAngle).add(origin);
            link.node.position.copy(a);
            link.node.quaternion.setFromUnitVectors(link.direction,b.clone().sub(a).normalize());
        }

        // The bridle unwinds from the curved head along the fixed well axis.
        const guideHeight = r.guide.clone().applyAxisAngle(axis,beamAngle).y+origin.y;
        const carrier = r.strokeBase+guideHeight-r.bridle.userData.top;
        r.slider.position.y = carrier;
        r.bridle.position.y = carrier;
        root.updateMatrixWorld(true);
        return { beamAngle, guideHeight, carrier,
            crankJoint: crankJoint.toArray(), beamJoint: [origin.y+joint.x,origin.z+joint.y] };
    }

    BuildingMasterV08.pose = function(root,kind,phase) {
        const model = poseBuilding(root,kind,phase);
        if (kind === "idle" && root.userData.key === "oilwell")
            pose(root,capturePhase ?? phase);
        return model;
    };

    function audit(root) {
        let minHeight=Infinity,maxHeight=-Infinity,maxJointGap=0,maxAngle=0;
        const snapshots=[];
        for (let sample=0;sample<=144;sample++) {
            const state=pose(root,sample/144);
            const r=rig(root);
            minHeight=Math.min(minHeight,state.carrier);
            maxHeight=Math.max(maxHeight,state.carrier);
            maxAngle=Math.max(maxAngle,Math.abs(state.beamAngle));
            for (const rod of r.rods) {
                const builtEnd=rod.upper.clone().sub(rod.lower).applyQuaternion(rod.node.quaternion).add(rod.node.position);
                const pin=rod.upper.clone().sub(r.beam.position).applyAxisAngle(axis,state.beamAngle).add(r.beam.position);
                maxJointGap=Math.max(maxJointGap,builtEnd.distanceTo(pin));
            }
            if (sample===0 || sample===144) snapshots.push([...state.crankJoint,...state.beamJoint,state.carrier]);
        }
        const loopError=Math.max(...snapshots[0].map((value,index)=>Math.abs(value-snapshots[1][index])));
        pose(root,0);
        if (maxJointGap>.000001 || loopError>.000001 || minHeight-.02<=.328 ||
            maxHeight-.45>=.318 || maxHeight-minHeight<.15)
            throw Error("Oilwell rig failed joint, seal or stroke validation " + JSON.stringify({maxJointGap,loopError,minHeight,maxHeight}));
        return { maxJointGap,loopError,carrierTravel:maxHeight-minHeight,lowestCarrier:minHeight,highestCarrier:maxHeight,maxAngleDegrees:maxAngle*180/Math.PI };
    }

    return {
        pose,
        audit,
        capture(phase) { capturePhase=phase; }
    };
})();
