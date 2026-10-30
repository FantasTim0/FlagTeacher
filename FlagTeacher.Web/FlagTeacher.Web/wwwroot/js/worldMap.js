const tileUrl = "https://{s}.basemaps.cartocdn.com/light_nolabels/{z}/{x}/{y}{r}.png";
const attribution = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors &copy; <a href="https://carto.com/attributions">CARTO</a>';

const styles = {
    base: { weight: 0.8, color: "#6b7280", fillColor: "#000", fillOpacity: 0.01 },
    hover: { fillColor: "#6b7280", fillOpacity: 0.25 },
    selected: { weight: 2, color: "#0d47a1", fillColor: "#1976d2", fillOpacity: 0.5 },
    correct: { weight: 2, color: "#1b5e20", fillColor: "#2e7d32", fillOpacity: 0.55 },
    wrong: { weight: 2, color: "#b71c1c", fillColor: "#c62828", fillOpacity: 0.5 },
};

let map;
let dotNet;
let layers = new Map();
let marks = new Map();
let selected = null;
let locked = false;

function restyle(code) {
    const layer = layers.get(code);
    const mark = marks.get(code) ?? (code === selected ? "selected" : null);
    layer.setStyle({ ...styles.base, ...(mark ? styles[mark] : {}) });
    if (mark) {
        layer.bringToFront();
    }
}

export async function init(element, reference, shapesUrl) {
    dotNet = reference;
    map = L.map(element, {
        preferCanvas: true,
        minZoom: 1,
        maxZoom: 8,
        maxBounds: [[-85, -180], [85, 180]],
        maxBoundsViscosity: 1,
    }).setView([20, 0], 2);
    L.tileLayer(tileUrl, { attribution, subdomains: "abcd", noWrap: true, maxZoom: 8 }).addTo(map);

    const data = await (await fetch(shapesUrl)).json();
    L.geoJSON(data, {
        style: () => styles.base,
        onEachFeature: (feature, layer) => {
            const code = feature.properties.code;
            layers.set(code, layer);
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
                    layer.setStyle(styles.hover);
                }
            });
            layer.on("mouseout", () => {
                if (!locked) {
                    restyle(code);
                }
            });
        },
    }).addTo(map);

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
    map.fitBounds(layers.get(correct).getBounds(), { maxZoom: 5, padding: [20, 20] });
}

export function destroy() {
    map?.remove();
    map = null;
    layers = new Map();
    marks = new Map();
    selected = null;
    locked = false;
}
