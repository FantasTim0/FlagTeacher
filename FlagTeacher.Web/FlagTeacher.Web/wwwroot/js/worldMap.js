const copyOffsets = [-360, 0, 360];
const plainCharPixels = 6.5;
const dotRadius = 7;
const dotMaxPixels = 14;

const styles = {
    base: { weight: 0.8, color: "#6b7280", fillColor: "#f3efe6", fillOpacity: 1 },
    hover: { fillColor: "#d9d4c4", fillOpacity: 1 },
    selected: { weight: 2, color: "#0d47a1", fillColor: "#1976d2", fillOpacity: 0.5 },
    correct: { weight: 2, color: "#1b5e20", fillColor: "#2e7d32", fillOpacity: 0.55 },
    wrong: { weight: 2, color: "#b71c1c", fillColor: "#c62828", fillOpacity: 0.5 },
};

let map;
let dotNet;
let layers = new Map();
let dots = [];
let labeled = [];
let anchors = new Map();
let plainLabels = [];
let marks = new Map();
let selected = null;
let locked = false;

function restyle(code) {
    const mark = marks.get(code) ?? (code === selected ? "selected" : null);
    for (const layer of layers.get(code)) {
        layer.setStyle({ ...styles.base, ...(mark ? styles[mark] : {}) });
        if (mark) {
            layer.bringToFront();
        }
    }
}

// The first three layers of a country are its outlines in the three world copies.
function label(code, name, className) {
    const { lat, lng } = anchors.get(code);
    layers.get(code).slice(0, 3).forEach((layer, i) => {
        layer.bindTooltip(name, { permanent: true, direction: "center", className: `map-label ${className}` });
        layer.openTooltip([lat, lng + copyOffsets[i]]);
        labeled.push(layer);
    });
}

function clearLabels() {
    labeled.forEach((layer) => layer.unbindTooltip());
    labeled = [];
    plainLabels = [];
}

function plainLabel(code, name) {
    const { lat, lng, degrees } = anchors.get(code);
    label(code, name, "plain");
    layers.get(code).slice(0, 3).forEach((layer, i) => {
        plainLabels.push({ layer, latlng: [lat, lng + copyOffsets[i]], degrees, width: name.length * plainCharPixels });
    });
}

// A name is only shown while its country is wide enough on screen to hold it; zooming in reveals the rest.
function updatePlainLabels() {
    const pixelsPerDegree = (256 * 2 ** map.getZoom()) / 360;
    for (const { layer, latlng, degrees, width } of plainLabels) {
        if (degrees * pixelsPerDegree >= width) {
            layer.openTooltip(latlng);
        } else {
            layer.closeTooltip();
        }
    }
}

// Shows each dot only while its country is too small on screen to click comfortably.
function updateDots() {
    const pixelsPerDegree = (256 * 2 ** map.getZoom()) / 360;
    for (const dot of dots) {
        const small = dot.degrees * pixelsPerDegree < dotMaxPixels;
        if (small && !map.hasLayer(dot)) {
            dot.addTo(map);
        } else if (!small && map.hasLayer(dot)) {
            dot.remove();
        }
    }
}

// Labels, dots and zoom-to-country use the largest landmass, so a far-away part (Alaska, French Guiana,
// the Canary Islands) does not pull them off the country.
function mainLandmass(geometry) {
    const polygons = geometry.type === "Polygon" ? [geometry.coordinates] : geometry.coordinates;
    let best = null;
    for (const polygon of polygons) {
        const lngs = polygon[0].map((c) => c[0]);
        const lats = polygon[0].map((c) => c[1]);
        const west = Math.min(...lngs), east = Math.max(...lngs);
        const south = Math.min(...lats), north = Math.max(...lats);
        const size = (east - west) * (north - south);
        if (!best || size > best.size) {
            best = { size, west, east, south, north };
        }
    }
    return {
        lat: (best.south + best.north) / 2,
        lng: (best.west + best.east) / 2,
        degrees: Math.max(best.east - best.west, best.north - best.south),
        bounds: [[best.south, best.west], [best.north, best.east]],
    };
}

// Vector layers are not repeated across world copies, so each copy gets its own shifted layer.
function shifted(data, offset) {
    const shift = (c) => (typeof c[0] === "number" ? [c[0] + offset, c[1]] : c.map(shift));
    return {
        ...data,
        features: data.features.map((f) => ({
            ...f,
            geometry: { ...f.geometry, coordinates: shift(f.geometry.coordinates) },
        })),
    };
}

export async function init(element, reference, shapesUrl) {
    dotNet = reference;
    map = L.map(element, {
        preferCanvas: true,
        minZoom: 1,
        maxZoom: 8,
        maxBounds: [[-85, -540], [85, 540]],
        maxBoundsViscosity: 1,
    }).setView([20, 0], 2);
    map.attributionControl.addAttribution('Outlines: <a href="https://www.naturalearthdata.com">Natural Earth</a>');

    const data = await (await fetch(shapesUrl)).json();
    for (const feature of data.features) {
        anchors.set(feature.properties.code, mainLandmass(feature.geometry));
    }
    const attach = (layer, code) => {
        layer.on("click", () => {
            if (locked) {
                return;
            }
            const previous = selected;
            selected = code === selected ? null : code;
            if (previous) {
                restyle(previous);
            }
            if (selected) {
                restyle(selected);
            }
            dotNet.invokeMethodAsync("OnCountryClicked", selected);
        });
        layer.on("mouseover", () => {
            if (!locked && code !== selected) {
                layers.get(code).forEach((l) => l.setStyle(styles.hover));
            }
        });
        layer.on("mouseout", () => {
            if (!locked) {
                restyle(code);
            }
        });
    };

    for (const offset of copyOffsets) {
        L.geoJSON(shifted(data, offset), {
            style: () => styles.base,
            onEachFeature: (feature, layer) => {
                const code = feature.properties.code;
                layers.set(code, [...(layers.get(code) ?? []), layer]);
                attach(layer, code);
            },
        }).addTo(map);
    }

    // Countries that are only a few pixels wide get a dot on top, which is easy to hit.
    for (const [code, { lat, lng, degrees }] of anchors) {
        for (const offset of copyOffsets) {
            const dot = L.circleMarker([lat, lng + offset], { ...styles.base, radius: dotRadius });
            dot.degrees = degrees;
            layers.get(code).push(dot);
            attach(dot, code);
            dots.push(dot);
        }
    }
    map.on("zoomend", updateDots);
    map.on("zoomend", updatePlainLabels);
    updateDots();

    return [...layers.keys()];
}

export function reset() {
    locked = false;
    selected = null;
    marks.clear();
    clearLabels();
    layers.forEach((_, code) => restyle(code));
    map.setView([20, 0], 2);
}

export function reveal(correct, picked, correctName, pickedName, names) {
    locked = true;
    marks.clear();
    if (picked && picked !== correct) {
        marks.set(picked, "wrong");
    }
    marks.set(correct, "correct");
    layers.forEach((_, code) => restyle(code));
    clearLabels();
    for (const [code, name] of Object.entries(names ?? {})) {
        if (code !== correct && code !== picked && layers.has(code)) {
            plainLabel(code, name);
        }
    }
    label(correct, correctName, "correct");
    if (picked && picked !== correct) {
        label(picked, pickedName, "wrong");
    }
    updatePlainLabels();
    map.fitBounds(anchors.get(correct).bounds, { maxZoom: 5, padding: [20, 20] });
}

export function destroy() {
    map?.remove();
    map = null;
    layers = new Map();
    dots = [];
    labeled = [];
    anchors = new Map();
    plainLabels = [];
    marks = new Map();
    selected = null;
    locked = false;
}
