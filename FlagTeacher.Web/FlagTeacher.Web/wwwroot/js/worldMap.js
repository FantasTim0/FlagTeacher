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
let home = new Map();
let dots = [];
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

    for (const offset of [-360, 0, 360]) {
        L.geoJSON(shifted(data, offset), {
            style: () => styles.base,
            onEachFeature: (feature, layer) => {
                const code = feature.properties.code;
                layers.set(code, [...(layers.get(code) ?? []), layer]);
                if (offset === 0) {
                    home.set(code, layer);
                }
                attach(layer, code);
            },
        }).addTo(map);
    }

    // Countries that are only a few pixels wide get a dot on top, which is easy to hit.
    for (const [code, layer] of home) {
        const bounds = layer.getBounds();
        const center = bounds.getCenter();
        const degrees = Math.max(bounds.getEast() - bounds.getWest(), bounds.getNorth() - bounds.getSouth());
        for (const offset of [-360, 0, 360]) {
            const dot = L.circleMarker([center.lat, center.lng + offset], { ...styles.base, radius: dotRadius });
            dot.degrees = degrees;
            layers.get(code).push(dot);
            attach(dot, code);
            dots.push(dot);
        }
    }
    map.on("zoomend", updateDots);
    updateDots();

    return [...layers.keys()];
}

export function reset() {
    locked = false;
    selected = null;
    marks.clear();
    layers.forEach((_, code) => restyle(code));
    map.setView([20, 0], 2);
}

export function reveal(correct, picked) {
    locked = true;
    marks.clear();
    if (picked && picked !== correct) {
        marks.set(picked, "wrong");
    }
    marks.set(correct, "correct");
    layers.forEach((_, code) => restyle(code));
    map.fitBounds(home.get(correct).getBounds(), { maxZoom: 5, padding: [20, 20] });
}

export function destroy() {
    map?.remove();
    map = null;
    layers = new Map();
    home = new Map();
    dots = [];
    marks = new Map();
    selected = null;
    locked = false;
}
