const API = 'http://localhost:5000/api/infrastructure';

const map = L.map('map').setView([32.7767, -96.7970], 10);

L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '© OpenStreetMap contributors'
}).addTo(map);

let linesLayer = null;
let hazardCircle = null;
let threatenedLayer = null;

document.addEventListener('DOMContentLoaded', async () => {
    const res = await fetch(`${API}/lines`);
    const data = await res.json();

    linesLayer = L.geoJSON(data, {
        style: { color: '#2196F3', weight: 3 },
        onEachFeature: (feature, layer) => {
            layer.bindPopup(
                `<b>${feature.properties.name}</b><br>${feature.properties.voltage_kv} kV`
            );
        }
    }).addTo(map);

    map.fitBounds(linesLayer.getBounds());
});

document.getElementById('analyzeBtn').addEventListener('click', async () => {
    const btn = document.getElementById('analyzeBtn');
    const lat = parseFloat(document.getElementById('lat').value);
    const lng = parseFloat(document.getElementById('lng').value);
    const radius = parseFloat(document.getElementById('radius').value);
    const results = document.getElementById('results');

    btn.disabled = true;
    btn.textContent = 'Analyzing...';

    if (hazardCircle) { map.removeLayer(hazardCircle); }
    if (threatenedLayer) { map.removeLayer(threatenedLayer); }

    hazardCircle = L.circle([lat, lng], {
        radius, color: '#FFC107', fillColor: '#FFC107', fillOpacity: 0.2
    }).addTo(map);

    const res = await fetch(`${API}/risk-assessment?lat=${lat}&lng=${lng}&radiusMeters=${radius}`);
    const data = await res.json();

    if (data.totalThreatenedLines > 0) {
        const geoJson = JSON.parse(data.threatenedLinesGeoJson);
        threatenedLayer = L.geoJSON(geoJson, {
            style: { color: '#e94560', weight: 5 }
        }).addTo(map);

        const names = data.threatenedLines.map(l => `• ${l.name} (${l.voltageKv} kV)`).join('<br>');
        results.innerHTML = `<span class="at-risk">
    ⚠ ${data.totalThreatenedLines} of ${data.totalLinesChecked} lines at risk
</span><br>${names}`;
    } else {
        results.innerHTML = `<span class="safe">
            ✓ No lines threatened within ${radius}m
        </span>`;
    }

    btn.disabled = false;
    btn.textContent = 'Analyze Risk';
});
