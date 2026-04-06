const express = require('express');
const https = require('https');

const app = express();
const PORT = process.env.PORT || 3000;

// Configurar Express para confiar en el proxy de Render y obtener la IP real del usuario
app.set('trust proxy', true);

// Registro de IPs autorizadas temporalmente (IP_ID -> Timestamp)
const authorizedKnocks = new Map();
const KNOCK_TIMEOUT = 60 * 1000; // El permiso dura 60 segundos

// URL apuntando a tu catálogo de GitHub
const catalogUrl = 'https://raw.githubusercontent.com/Dioscarmesi/Cine.github.io/refs/heads/main/Cine/micinema_catalog.json';

let catalog = null;
let isFetching = false;

// Función para descargar el catálogo desde GitHub
function loadCatalog() {
    if (isFetching) return;
    isFetching = true;
    https.get(catalogUrl, (res) => {
        let rawData = '';
        res.on('data', (chunk) => { rawData += chunk; });
        res.on('end', () => {
            try {
                catalog = JSON.parse(rawData);
                console.log('✅ Catálogo JSON actualizado.');
            } catch (e) {
                console.error('❌ Error JSON:', e.message);
            }
            isFetching = false;
        });
    }).on('error', (e) => {
        console.error('❌ Error de red:', e.message);
        isFetching = false;
    });
}
loadCatalog();

const TROLL_VIDEO = 'https://www.youtube.com/watch?v=YcCBzPG5q3I';

// === ENDPOINT DE "TOQUE" (Knock) ===
// Este endpoint lo llama Unity antes de poner el video
app.get('/knock/:id', (req, res) => {
    const id = req.params.id;
    const ip = req.ip; // Gracias a 'trust proxy', esto es la IP del jugador

    const knockKey = `${ip}_${id}`;
    authorizedKnocks.set(knockKey, Date.now());

    console.log(`🔑 IP AUTORIZADA: [${ip}] para ID [${id}]`);
    res.status(200).send("OK");
});

// Ruta dinámica para redireccionar (Ejemplo: https://tu-api.com/movie_001)
app.get('/:id', (req, res) => {
    const requestedId = req.params.id;
    const ip = req.ip;
    const isAdmin = req.query.admin === '1';

    // Limpieza de knocks antiguos (opcional, para no llenar la memoria)
    const now = Date.now();
    
    // VERIFICACIÓN DE "TOQUE A LA PUERTA"
    const knockKey = `${ip}_${requestedId}`;
    const knockTime = authorizedKnocks.get(knockKey);
    const isAuthorized = knockTime && (now - knockTime < KNOCK_TIMEOUT);

    if (!isAuthorized && !isAdmin) {
        console.log(`🚫 ACCESO DENEGADO: La IP [${ip}] no ha tocado a la puerta para [${requestedId}]`);
        return res.redirect(302, TROLL_VIDEO);
    }

    // Si llegamos aquí, la IP tiene permiso
    console.log(`✅ DISFRUTANDO: IP [${ip}] cargando [${requestedId}]`);
    
    // Una vez usado, el permiso se podría borrar, pero lo dejamos 60s por si el reproductor re-conecta
    loadCatalog();

    if (!catalog) {
        return res.status(500).send("Catálogo no disponible. Intenta de nuevo.");
    }

    let movie = null;
    if (catalog.movies) movie = catalog.movies.find(m => m.id === requestedId);
    if (!movie && catalog.series) movie = catalog.series.find(s => s.id === requestedId);

    if (movie) {
        const urlToPlay = movie.videoUrl || (movie.links && movie.links.default);
        if (!urlToPlay) return res.status(404).send("Sin link de video.");

        return res.redirect(302, urlToPlay);
    } else {
        return res.status(404).send(`ID '${requestedId}' no encontrado.`);
    }
});

// Iniciar servidor
app.listen(PORT, () => {
    console.log(`🚀 Sistema de Seguridad IP Knocking activo en puerto ${PORT}`);
});

